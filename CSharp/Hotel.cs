using System;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Hotel", "Reneb", "1.0.0")]
    class Hotel : RustPlugin
    {

        ////////////////////////////////////////////////////////////
        // Plugin References
        ////////////////////////////////////////////////////////////

        [PluginReference]
        Plugin ZoneManager;

        ////////////////////////////////////////////////////////////
        // Fields
        ////////////////////////////////////////////////////////////

        static int deployableColl = UnityEngine.LayerMask.GetMask(new string[] { "Deployed" });
        static int constructionColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Construction Trigger" });

        ////////////////////////////////////////////////////////////
        // cached Fields
        ////////////////////////////////////////////////////////////

        public static Dictionary<string, HotelData> EditHotel = new Dictionary<string, HotelData>();
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        public static Vector3 Vector3UP = new Vector3(0f, 0.1f, 0f);
        public FieldInfo fieldWhiteList;

        ////////////////////////////////////////////////////////////
        // Config Management
        ////////////////////////////////////////////////////////////

        static int authlevel = 2;
        static string MessageAlreadyEditing = "You are already editing a hotel. You must close or save it first.";
        static string MessageHotelNewHelp = "You must select a name for the new hotel: /hotel_new HOTELNAME";
        static string MessageHotelEditHelp = "You must select the name of the hotel you want to edit: /hotel_edit HOTELNAME";
        static string MessageHotelEditEditing = "You are editing the hotel named: {0}. Now say /hotel to continue configuring your hotel. Note that no one can register/leave the hotel while you are editing it.";
        static string MessageErrorAlreadyExist = "{0} is already the name of a hotel";
        static string MessageErrorNotAllowed = "You are not allowed to use this command";
        static string MessageErrorEditDoesntExist = "The hotel \"{0}\" doesn't exist";

        static string MessageHotelNewCreated = "You've created a new Hotel named: {0}. Now say /hotel to continue configuring your hotel.";

        static string GUIBoardAdmin = "Hotel Name: {name} \nHotel Location: {loc} \nHotel Radius: {hrad} \nRooms Radius: {rrad} \nRooms: {rnum} \nOccupied: {onum}";
        static string xmin = "0.7";
        static string xmax = "1.0";
        static string ymin = "0.2";
        static string ymax = "0.8";

        public static string adminguijson = @"[  
			{ 
				""name"": ""HotelAdmin"",
				""parent"": ""Overlay"",
				""components"":
				[
					{
						 ""type"":""UnityEngine.UI.Image"",
						 ""color"":""0.1 0.1 0.1 0.7"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""{xmin} {ymin}"",
						""anchormax"": ""{xmax} {ymax}""
						
					}
				]
			},
			{
				""parent"": ""HotelAdmin"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{msg}"",
						""fontSize"":15,
						""align"": ""MiddleCenter"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0 0.1"",
						""anchormax"": ""1 0.8""
					}
				]
			}
		]
		";

        ////////////////////////////////////////////////////////////
        // Data Management
        ////////////////////////////////////////////////////////////

        static StoredData storedData;

        class StoredData
        {
            public HashSet<HotelData> Hotels = new HashSet<HotelData>();

            public StoredData() { }
        }

        void OnServerSave() { SaveData(); }

        void SaveData() { Interface.GetMod().DataFileSystem.WriteObject("Hotel", storedData); }

        void LoadData()
        {
            try { storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Hotel"); }
            catch { storedData = new StoredData(); }
        }

        public class DeployableItem
        {
            public string x;
            public string y;
            public string z;
            public string rx;
            public string ry;
            public string rz;
            public string rw;
            public string prefabname;

            Vector3 pos;
            Quaternion rot;

            public DeployableItem()
            {
            }

            public DeployableItem(Deployable deployable)
            {
                prefabname = StringPool.Get(deployable.prefabID).ToString();

                this.x = deployable.transform.position.x.ToString();
                this.y = deployable.transform.position.y.ToString();
                this.z = deployable.transform.position.z.ToString();

                this.rx = deployable.transform.rotation.x.ToString();
                this.ry = deployable.transform.rotation.y.ToString();
                this.rz = deployable.transform.rotation.z.ToString();
                this.rw = deployable.transform.rotation.w.ToString();
            }
            public Vector3 Pos()
            {
                if (pos == default(Vector3))
                    pos = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return pos;
            }
            public Quaternion Rot()
            {
                if (rot == default(Quaternion))
                    rot = new Quaternion(float.Parse(rx), float.Parse(ry), float.Parse(rz), float.Parse(rw));
                return rot;
            }
        }
        public class Room
        {
            public string roomid;
            public string x;
            public string y;
            public string z;

            public List<DeployableItem> defaultDeployables;

            public string renter;
            public string checkingTime;
            public string checkoutTime;

            Vector3 pos;

            public Room()
            {
            }

            public Room(Vector3 position)
            {
                this.x = Math.Ceiling(position.x).ToString();
                this.y = Math.Ceiling(position.y).ToString();
                this.z = Math.Ceiling(position.z).ToString();
                this.roomid = string.Format("{0}:{1}:{2}", this.x, this.y, this.z);
            }

            public Vector3 Pos()
            {
                if (pos == default(Vector3))
                    pos = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return pos;
            }
        }

        public class HotelData
        {
            public string hotelname;
            public string x;
            public string y;
            public string z;
            public string r;
            public string rr;

            public Dictionary<string, Room> rooms;

            Vector3 pos;
            public bool enabled;

            public HotelData()
            {
                enabled = false;
                if (rooms == null) rooms = new Dictionary<string, Room>();
            }

            public HotelData(string hotelname)
            {
                this.hotelname = hotelname;
                this.x = "0";
                this.y = "0";
                this.z = "0";
                this.r = "60";
                this.rr = "20";
                this.rooms = new Dictionary<string, Room>();
                enabled = false;
            }

            public Vector3 Pos()
            {
                if (this.x == "0" && this.y == "0" && this.z == "0")
                    return default(Vector3);
                if (pos == default(Vector3))
                    pos = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return pos;
            }

            public void RefreshRooms()
            {
                if (Pos() == default(Vector3))
                    return;
                Dictionary<string, Room> detectedRooms = FindAllRooms(Pos(), Convert.ToSingle(this.r), Convert.ToSingle(this.rr));

                List<string> toAdd = new List<string>();
                List<string> toDelete = new List<string>();
                if (rooms == null) rooms = new Dictionary<string, Room>();
                if (rooms.Count > 0)
                {
                    foreach (KeyValuePair<string, Room> pair in rooms)
                    {
                        if (pair.Value.renter != null)
                        {
                            detectedRooms.Remove(pair.Key);
                            Debug.Log(string.Format("{0} is occupied and can't be edited", pair.Key));
                            continue;
                        }
                        if (!detectedRooms.ContainsKey(pair.Key))
                        {
                            toDelete.Add(pair.Key);
                        }
                    }
                }
                foreach (KeyValuePair<string, Room> pair in detectedRooms)
                {
                    if (!rooms.ContainsKey(pair.Key))
                    {
                        toAdd.Add(pair.Key);
                    }
                    else
                    {
                        rooms[pair.Key] = pair.Value;
                    }

                }
                foreach (string roomid in toDelete)
                {
                    rooms.Remove(roomid);
                    Debug.Log(string.Format("{0} doesnt exist anymore, removing this room", roomid));
                }
                foreach (string roomid in toAdd)
                {
                    Debug.Log(string.Format("{0} is a new room, adding it", roomid));
                    rooms.Add(roomid, detectedRooms[roomid]);
                }
            }

            public void Deactivate()
            {
                enabled = false;
            }
            public void Activate()
            {
                enabled = true;
            }

            public void AddRoom(Room newroom)
            {
                if (rooms.ContainsKey(newroom.roomid))
                    rooms.Remove(newroom.roomid);

                rooms.Add(newroom.roomid, newroom);
            }
        }

        static Dictionary<string, Room> FindAllRooms(Vector3 position, float radius, float roomradius)
        {
            List<Vector3> listLocks = new List<Vector3>();
            foreach (Collider col in UnityEngine.Physics.OverlapSphere(position, radius, constructionColl))
            {
                Door door = col.GetComponentInParent<Door>();
                if (door != null)
                {
                    if (door.HasSlot(BaseEntity.Slot.Lock))
                    {
                        door.SetFlag(BaseEntity.Flags.Open, false);
                        door.SendNetworkUpdateImmediate(true);
                        listLocks.Add(door.transform.position);
                    }
                }
            }
           
            Dictionary<Deployable, string> deployables = new Dictionary<Deployable, string>();
            Dictionary<string, Room> tempRooms = new Dictionary<string, Room>();

            foreach (Vector3 block in listLocks )
			{
                Room newRoom = new Room(block);
                newRoom.defaultDeployables = new List<DeployableItem>();
                var founditems = new List<Deployable>();
                foreach (Collider col in UnityEngine.Physics.OverlapSphere(block, roomradius, deployableColl))
                {
                    
                    Deployable deploy = col.GetComponentInParent<Deployable>();
                    if (deploy != null)
                    {
                        if (!founditems.Contains(deploy))
                        {
                            founditems.Add(deploy);
                            bool canReach = true;
                            foreach (RaycastHit rayhit in UnityEngine.Physics.RaycastAll(deploy.transform.position + Vector3UP, (block + Vector3UP - deploy.transform.position).normalized, Vector3.Distance(deploy.transform.position, block) - 0.2f, constructionColl ))
							{
                                canReach = false;
                                break;
                            }
                            if (!canReach) continue;

                            if (deployables.ContainsKey(deploy))
                            {
                                deployables[deploy] = "0";
                            }
                            else
                            {
                                deployables.Add(deploy, newRoom.roomid);
                            }
                        }
                    }
                }
                tempRooms.Add(newRoom.roomid, newRoom);
            }
            foreach (KeyValuePair<Deployable, string> pair in deployables)
            {
                if (pair.Value != "0")
                {
                    DeployableItem newDeployItem = new DeployableItem(pair.Key);
                    tempRooms[pair.Value].defaultDeployables.Add(newDeployItem);
                }
            }

            return tempRooms;
        }

        static double LogTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        void Loaded()
        {
            adminguijson = adminguijson.Replace("{xmin}", xmin).Replace("{xmax}", xmax).Replace("{ymin}", ymin).Replace("{ymax}", ymax);
            fieldWhiteList = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            LoadData();
        }

        object CanUseDoor( BasePlayer player, CodeLock codelock )
        {
            BaseEntity parententity = codelock.GetParentEntity();
            if (parententity == null) return null;
            if (parententity.HasFlag(BaseEntity.Flags.Open)) return null;
            string zonename = string.Empty;
            HotelData targethotel = null;
            foreach ( HotelData hotel in storedData.Hotels )
            {
                object isplayerinzone = ZoneManager.Call("isPlayerInZone", hotel.hotelname, player);
                if (isplayerinzone is bool && (bool)isplayerinzone)
                {
                    targethotel = hotel;
                }
            }
            if (targethotel == null) return null;
            if( !targethotel.enabled )
            {
                SendReply(player, "This Hotel is under maintenance by the admin, you may not open this door at the moment");
                return false;
            }
            List<ulong> whitelitedPlayers = fieldWhiteList.GetValue(codelock) as List<ulong>;
            if (codelock.IsLocked())
            {
                
                if (whitelitedPlayers.Contains(player.userID)) return true;
                else return false;
            }
            else
            {
                if (!whitelitedPlayers.Contains(player.userID))
                {
                    ResetRoom(codelock);
                }
            }
            return null;
        }
        bool FindHotelAndRoomByPos(Vector3 position, out HotelData hoteldata, out Room roomdata)
        {
            hoteldata = null;
            roomdata = null;
            position.x = Mathf.Ceil(position.x);
            position.y = Mathf.Ceil(position.y);
            position.z = Mathf.Ceil(position.z);
            foreach (HotelData hotel in storedData.Hotels)
            {
                foreach(KeyValuePair<string,Room> pair in hotel.rooms)
                {
                    if(pair.Value.Pos() == position)
                    {
                        hoteldata = hotel;
                        roomdata = pair.Value;
                        return true;
                    }
                }
            }
            return false;

        }
        void ResetRoom( CodeLock codelock )
        {
            BaseEntity door = codelock.GetParentEntity();
            Vector3 block = door.transform.position;
            HotelData hotel = null;
            Room room = null;
            if(!FindHotelAndRoomByPos(block, out hotel, out room))
            {
                Debug.LogWarning(block.ToString() + " was not found as a room from a hotel, WTF?");
                return;
            }
            var founditems = new List<Deployable>();
            foreach (Collider col in UnityEngine.Physics.OverlapSphere(block, Convert.ToSingle(hotel.rr), deployableColl))
            {
                Deployable deploy = col.GetComponentInParent<Deployable>();
                if (deploy != null)
                {
                   
                    if (!founditems.Contains(deploy))
                    {
                        bool canReach = true;
                        foreach (RaycastHit rayhit in UnityEngine.Physics.RaycastAll(deploy.transform.position + Vector3UP, (block + Vector3UP - deploy.transform.position).normalized, Vector3.Distance(deploy.transform.position, block) - 0.2f, constructionColl))
                        {
                            canReach = false;
                            break;
                        }
                        if (!canReach) continue;
                        foreach (Collider col2 in UnityEngine.Physics.OverlapSphere(block, Convert.ToSingle(hotel.rr), constructionColl))
                        {
                            if (col2.GetComponentInParent<Door>() != null)
                            {
                                if (col2.transform.position != block)
                                {
                                    bool canreach2 = true;
                                    foreach (RaycastHit rayhit in UnityEngine.Physics.RaycastAll(deploy.transform.position + Vector3UP, (col2.transform.position + Vector3UP - deploy.transform.position).normalized, Vector3.Distance(deploy.transform.position, col2.transform.position) - 0.2f, constructionColl))
                                    {
                                        canreach2 = false;
                                    }
                                    if (canreach2)
                                    {
                                        canReach = false;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (!canReach) continue;
                       
                        founditems.Add(deploy);
                    }
                }
            }
            foreach(Deployable deploy in founditems)
            {
                deploy.GetComponent<BaseEntity>().KillMessage();
            }
            foreach(DeployableItem deploy in room.defaultDeployables)
            {
                UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab(deploy.prefabname);
                if (newPrefab == null)
                {
                    return;
                }
                BaseEntity entity = GameManager.server.CreateEntity(newPrefab, deploy.Pos(), deploy.Rot());
                if (entity == null) return;
                entity.Spawn(true);
                
            }
            room.renter = null;
            room.checkingTime = null;
            room.checkoutTime = null;
        }
        void RefreshAdminHotelGUI(BasePlayer player)
        {
            RemoveAdminHotelGUI(player);

            if (!EditHotel.ContainsKey(player.userID.ToString())) return;
            string Msg = CreateAdminGUIMsg(player);
            if (Msg == string.Empty) return;
            string send = adminguijson.Replace("{msg}", Msg);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", send);
        }

        string CreateAdminGUIMsg(BasePlayer player)
        {
            string newguimsg = string.Empty;

            HotelData hoteldata = EditHotel[player.userID.ToString()];

            //GUIBoardAdmin = "Hotel Name: {name} \nHotel Location: {loc} \nHotel Radius: {hrad} \nRooms Radius: {rrad} \nRooms: {rnum} \nOccupied: {onum}"; 
            var loc = hoteldata.x == null ? "None" : string.Format("{0} {1} {2}", hoteldata.x, hoteldata.y, hoteldata.z);
            var hrad = hoteldata.r == null ? "None" : hoteldata.r;
            var rrad = hoteldata.rr == null ? "None" : hoteldata.rr;
            var rnum = hoteldata.rooms == null ? "0" : hoteldata.rooms.Count.ToString();
            var onumint = 0;
            if (hoteldata.rooms != null)
            {
                foreach (KeyValuePair<string, Room> pair in hoteldata.rooms)
                {
                    if (pair.Value.renter != null)
                    {
                        onumint++;
                    }
                }
            }
            var onum = onumint.ToString();
            newguimsg = GUIBoardAdmin.Replace("{name}", hoteldata.hotelname).Replace("{loc}", loc).Replace("{hrad}", hrad).Replace("{rrad}", rrad).Replace("{rnum}", rnum).Replace("{onum}", onum);
            return newguimsg;
        }

        void RemoveAdminHotelGUI(BasePlayer player)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", "HotelAdmin");
        }

        void ShowHotelGrid(BasePlayer player)
        {
            HotelData hoteldata = EditHotel[player.userID.ToString()];
            if (hoteldata.x != null && hoteldata.r != null)
            {
                Vector3 hpos = hoteldata.Pos();
                float hrad = Convert.ToSingle(hoteldata.r);
                player.SendConsoleCommand("ddraw.sphere", 5f, UnityEngine.Color.blue, hpos, hrad);
            }
            if (hoteldata.rooms != null)
            {
                foreach (KeyValuePair<string, Room> pair in hoteldata.rooms)
                {
                    List<DeployableItem> deployables = pair.Value.defaultDeployables;
                    foreach (DeployableItem deployable in deployables)
                    {
                        player.SendConsoleCommand("ddraw.arrow", 10f, UnityEngine.Color.green, pair.Value.Pos(), deployable.Pos(), 0.5f);
                    }
                }
            }
        }

        bool hasAccess(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel >= authlevel) return true;
            return permission.UserHasPermission(player.userID.ToString(), "canhotel");
        }

        [ChatCommand("hotel_save")]
        void cmdChatHotelSave(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            if (!EditHotel.ContainsKey(player.userID.ToString()))
            {
                SendReply(player, "You are not editing a hotel.");
                return;
            }
            HotelData editedhotel = EditHotel[player.userID.ToString()];

            HotelData removeHotel = null;
            foreach (HotelData hoteldata in storedData.Hotels)
            {
                if (hoteldata.hotelname.ToLower() == editedhotel.hotelname.ToLower())
                {
                    removeHotel = hoteldata;
                    
                    break;
                }
            }
            if (removeHotel != null)
            {
                storedData.Hotels.Remove(removeHotel);
                removeHotel.Activate();
            }
            editedhotel.Activate();

            storedData.Hotels.Add(editedhotel);

            SaveData();

            EditHotel.Remove(player.userID.ToString());

            SendReply(player, "Hotel Saved and Closed.");

            RemoveAdminHotelGUI(player);
        }

        [ChatCommand("hotel_close")]
        void cmdChatHotelClose(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            if (!EditHotel.ContainsKey(player.userID.ToString()))
            {
                SendReply(player, "You are not editing a hotel.");
                return;
            }
            HotelData editedhotel = EditHotel[player.userID.ToString()];
            foreach (HotelData hoteldata in storedData.Hotels)
            {
                if (hoteldata.hotelname.ToLower() == editedhotel.hotelname.ToLower())
                {
                    hoteldata.Activate();

                    break;
                }
            }
            
            EditHotel.Remove(player.userID.ToString());

            SendReply(player, "Hotel Closed without saving.");

            RemoveAdminHotelGUI(player);
        }

        [ChatCommand("hotel")]
        void cmdChatHotel(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            if (!EditHotel.ContainsKey(player.userID.ToString()))
            {
                SendReply(player, "You are not editing a hotel. Create a new one with /hotel_new, or edit an existing one with /hotel_edit");
                return;
            }

            HotelData editedhotel = EditHotel[player.userID.ToString()];

            if (args.Length == 0)
            {
                SendReply(player, "==== Available options ====");
                SendReply(player, "/hotel location => sets the center hotel location where you stand");
                SendReply(player, "/hotel radius XX => sets the radius of the hotel (the entire structure of the hotel needs to be covered by the zone");
                SendReply(player, "/hotel rooms => refreshs the rooms (detects new rooms, deletes rooms if they don't exist anymore, if rooms are in use they won't get taken in count)");
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case "location":
                        string rad = editedhotel.r == null ? "20" : editedhotel.r;
                        string[] zoneargs = new string[] { "name", editedhotel.hotelname, "radius", rad };
                        ZoneManager.Call("CreateOrUpdateZone", editedhotel.hotelname, zoneargs, player.transform.position);

                        (EditHotel[player.userID.ToString()]).x = player.transform.position.x.ToString();
                        (EditHotel[player.userID.ToString()]).y = player.transform.position.y.ToString();
                        (EditHotel[player.userID.ToString()]).z = player.transform.position.z.ToString();

                        SendReply(player, string.Format("Location set to {0}", player.transform.position.ToString()));
                        break;

                    case "rooms":
                        SendReply(player, "Rooms Refreshing ...");
                        (EditHotel[player.userID.ToString()]).RefreshRooms();
                        SendReply(player, "Rooms Refreshed");
                        break;

                    case "radius":
                        if (args.Length == 1)
                        {
                            SendReply(player, "/hotel radius XX");
                            return;
                        }
                        int rad2 = 20;
                        int.TryParse(args[1], out rad2);
                        if (rad2 < 1) rad2 = 20;

                        string[] zoneargs2 = new string[] { "name", editedhotel.hotelname, "radius", rad2.ToString() };
                        ZoneManager.Call("CreateOrUpdateZone", editedhotel.hotelname, zoneargs2);

                        (EditHotel[player.userID.ToString()]).r = rad2.ToString();

                        SendReply(player, string.Format("Radius set to {0}", args[1]));
                        break;

                    default:
                        SendReply(player, string.Format("Wrong argument {0}", args[0]));
                        break;
                }
            }

            ShowHotelGrid(player);
            RefreshAdminHotelGUI(player);
        }


        [ChatCommand("hotel_edit")]
        void cmdChatHotelEdit(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, MessageErrorNotAllowed); return; }
            if (EditHotel.ContainsKey(player.userID.ToString())) { SendReply(player, MessageAlreadyEditing); return; }
            if (args.Length == 0) { SendReply(player, MessageHotelEditHelp); return; }

            string hname = args[0];
            foreach (HotelData hotel in storedData.Hotels)
            {
                if (hotel.hotelname.ToLower() == hname.ToLower())
                {
                    hotel.Deactivate();
                    EditHotel.Add(player.userID.ToString(), hotel);
                    break;
                }
            }
            if (!EditHotel.ContainsKey(player.userID.ToString())) { SendReply(player, string.Format(MessageErrorEditDoesntExist, args[0])); return; }

            SendReply(player, string.Format(MessageHotelEditEditing, EditHotel[player.userID.ToString()].hotelname));

            RefreshAdminHotelGUI(player);
        }

        [ChatCommand("hotel_new")]
        void cmdChatHotelNew(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, MessageErrorNotAllowed); return; }
            if (EditHotel.ContainsKey(player.userID.ToString())) { SendReply(player, MessageAlreadyEditing); return; }
            if (args.Length == 0) { SendReply(player, MessageHotelNewHelp); return; }

            string hname = args[0];
            Debug.Log(storedData.Hotels.Count.ToString());
            if (storedData.Hotels.Count > 0)
            {
                foreach (HotelData hotel in storedData.Hotels)
                {
                    if (hotel.hotelname.ToLower() == hname.ToLower())
                    {
                        SendReply(player, string.Format(MessageErrorAlreadyExist, hname));
                        return;
                    }
                }
            }
            HotelData newhotel = new HotelData(hname);
            newhotel.Deactivate();
            EditHotel.Add(player.userID.ToString(), newhotel);

            SendReply(player, string.Format(MessageHotelNewCreated, hname));
            RefreshAdminHotelGUI(player);
        }
    }
}
