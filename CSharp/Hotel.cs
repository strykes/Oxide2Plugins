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
        static string MessageMaintenance = "This Hotel is under maintenance by the admin, you may not open this door at the moment";
        static string MessageErrorUnavaibleRoom = "This room is unavaible, seems like it wasn't set correctly";
        static string MessageHotelNewCreated = "You've created a new Hotel named: {0}. Now say /hotel to continue configuring your hotel.";
        static string MessageErrorNotAllowedToEnter = "You are not allowed to enter this room, it's already been used my someone else";

        static string GUIBoardAdmin = "                             <color=green>HOTEL MANAGER</color> \n\nHotel Name:      {name} \n\nHotel Location: {loc} \nHotel Radius:     {hrad} \n\nRooms Radius:   {rrad} \nRooms:                {rnum} \n<color=red>Occupied:            {onum}</color>";
        static string xmin = "0.7";
        static string xmax = "1.0";
        static string ymin = "0.6";
        static string ymax = "0.9";

        static string RentRoomAllowedMax = "2";
        static string RentRoomAllowedMaxPerHotel = "1";
        static string RentRoomDuration = "172800";

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
						""align"": ""MiddleLeft"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0.1 0.1"",
						""anchormax"": ""1 1""
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
                this.rr = "5";
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

        ////////////////////////////////////////////////////////////
        // Random Methods
        ////////////////////////////////////////////////////////////

        static double LogTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        static void CloseDoor(Door door)
        {
            door.SetFlag(BaseEntity.Flags.Open, false);
            door.SendNetworkUpdateImmediate(true);
        }
        static void OpenDoor(Door door)
        {
            door.SetFlag(BaseEntity.Flags.Open, true);
            door.SendNetworkUpdateImmediate(true);
        }
        static void LockLock(CodeLock codelock)
        {
            codelock.SetFlag(BaseEntity.Flags.Locked, true);
            codelock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }
        static void UnlockLock(CodeLock codelock)
        {
            codelock.SetFlag(BaseEntity.Flags.Locked, false);
            codelock.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        ////////////////////////////////////////////////////////////
        // Oxide Hooks
        ////////////////////////////////////////////////////////////

        void Unload()
        {
            SaveData();
        }

        void Loaded()
        {
            adminguijson = adminguijson.Replace("{xmin}", xmin).Replace("{xmax}", xmax).Replace("{ymin}", ymin).Replace("{ymax}", ymax);
            fieldWhiteList = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            LoadData();
        }

        object CanUseDoor(BasePlayer player, CodeLock codelock)
        {
            BaseEntity parententity = codelock.GetParentEntity();
            if (parententity == null) return null;
            if (parententity.HasFlag(BaseEntity.Flags.Open)) return null;

            string zonename = string.Empty;
            HotelData targethotel = null;
            foreach (HotelData hotel in storedData.Hotels)
            {
                //Check if the player is inside a hotel
                // Is this the best way to do it?
                // Might need to actually make a list of all codelocks that are used inside a hotel instead of this ...
                object isplayerinzone = ZoneManager.Call("isPlayerInZone", hotel.hotelname, player);
                if (isplayerinzone is bool && (bool)isplayerinzone) targethotel = hotel;
            }
            if (targethotel == null) return null;

            if (!targethotel.enabled)
            {
                SendReply(player, MessageMaintenance);
                return false;
            }

            Room room = FindRoomByDoorAndHotel(targethotel, parententity);
            if(room == null)
            {
                SendReply(player, MessageErrorUnavaibleRoom);
                return false;
            }

            if(room.renter == null)
            {
                if (!CanRentRoom(player, targethotel)) return false;
                ResetRoom(codelock, targethotel, room);
                NewRoomOwner(codelock, player, targethotel, room);
            }

            LockLock(codelock);

            if (room.renter != player.userID.ToString())
            {
                SendReply(player, MessageErrorNotAllowedToEnter);
                return false;
            }
            
            return true;
        }

       

        ////////////////////////////////////////////////////////////
        // Room Management Functions
        ////////////////////////////////////////////////////////////

        static List<Vector3> FindRoomsFromPosition( Vector3 position, float radius )
        {
            List<Vector3> listLocks = new List<Vector3>();
            foreach (Collider col in UnityEngine.Physics.OverlapSphere(position, radius, constructionColl))
            {
                Door door = col.GetComponentInParent<Door>();
                if (door == null) continue;
                if (!door.HasSlot(BaseEntity.Slot.Lock)) continue;

                CloseDoor(door);
                listLocks.Add(door.transform.position);
            }
            return listLocks;
        } 

        static Dictionary<string, Room> FindAllRooms(Vector3 position, float radius, float roomradius)
        {
            List<Vector3> listLocks = FindRoomsFromPosition(position, radius);

            Hash<Deployable, string> deployables = new Hash<Deployable, string>();
            Dictionary<string, Room> tempRooms = new Dictionary<string, Room>();

            foreach (Vector3 pos in listLocks )
			{
                Room newRoom = new Room(pos);
                newRoom.defaultDeployables = new List<DeployableItem>();
                List<Deployable> founditems = new List<Deployable>();

                foreach (Collider col in UnityEngine.Physics.OverlapSphere(pos, roomradius, deployableColl))
                {
                    Deployable deploy = col.GetComponentInParent<Deployable>();
                    if (deploy == null) continue;
                    if (founditems.Contains(deploy)) continue;
                    founditems.Add(deploy);

                    bool canReach = true;
                    foreach (RaycastHit rayhit in UnityEngine.Physics.RaycastAll(deploy.transform.position + Vector3UP, (pos + Vector3UP - deploy.transform.position).normalized, Vector3.Distance(deploy.transform.position, pos) - 0.2f, constructionColl)) { canReach = false; break; }
                    if (!canReach) continue;

                    if (deployables[deploy] != null) deployables[deploy] = "0";
                    else deployables[deploy] = newRoom.roomid;
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

        static Room FindRoomByDoorAndHotel(HotelData hotel, BaseEntity door)
        {
            string roomid = string.Format("{0}:{1}:{2}", Math.Ceiling(door.transform.position.x).ToString(), Math.Ceiling(door.transform.position.y).ToString(), Math.Ceiling(door.transform.position.z).ToString());
            if (!hotel.rooms.ContainsKey(roomid)) return null;

            return hotel.rooms[roomid];
        }


        bool CanRentRoom(BasePlayer player, HotelData hotel)
        {
            foreach(KeyValuePair<string, Room> pair in hotel.rooms)
            {
                if(pair.Value.renter == player.userID.ToString())
                {
                    SendReply(player, "You already have a room in this hotel!");
                    return false;
                }
            }
            return true;
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
        CodeLock FindCodeLockByRoomID( string roomid )
        {
            string[] rpos = roomid.Split(':');
            if (rpos.Length != 3) return null;

            return FindCodeLockByPos(new Vector3(Convert.ToSingle(rpos[0]), Convert.ToSingle(rpos[1]), Convert.ToSingle(rpos[2])));
        }
        CodeLock FindCodeLockByPos( Vector3 pos )
        {
            CodeLock findcode = null;
            foreach (Collider col in UnityEngine.Physics.OverlapSphere(pos, 2f, constructionColl))
            {
                if (col.GetComponentInParent<Door>() == null) continue;
                if (!col.GetComponentInParent<Door>().HasSlot(BaseEntity.Slot.Lock)) continue;

                BaseEntity slotentity = col.GetComponentInParent<Door>().GetSlot(BaseEntity.Slot.Lock);
                if (slotentity == null) continue;
                if (slotentity.GetComponent<CodeLock>() == null) continue;

                if (findcode != null)
                    if (Vector3.Distance(pos, findcode.GetParentEntity().transform.position) < Vector3.Distance(pos, col.transform.position))
                        continue;
                findcode = slotentity.GetComponent<CodeLock>();
            }
            return findcode;
        }
        void SpawnDeployable(string prefabname, Vector3 pos, Quaternion rot, BasePlayer player = null)
        {
            UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab(prefabname);
            if (newPrefab == null) return;

            BaseEntity entity = GameManager.server.CreateEntity(newPrefab, pos, rot);
            if (entity == null) return;

            if(player != null)
                entity.SendMessage("SetDeployedBy", player, UnityEngine.SendMessageOptions.DontRequireReceiver);

            entity.Spawn(true);
        }

        void NewRoomOwner( CodeLock codelock, BasePlayer player, HotelData hotel, Room room )
        {
            BaseEntity door = codelock.GetParentEntity();
            Vector3 block = door.transform.position;

            EmptyDeployablesRoom(block, Convert.ToSingle(hotel.rr));

            foreach (DeployableItem deploy in room.defaultDeployables) { SpawnDeployable(deploy.prefabname, deploy.Pos(), deploy.Rot(), player); }

            List<ulong> whitelist = new List<ulong>();
            whitelist.Add(player.userID);
            fieldWhiteList.SetValue(codelock, whitelist);

            room.renter = player.userID.ToString();
            room.checkingTime = LogTime().ToString();

            // NEED TO SET A CHECKOUT TIME!!
            //room.checkoutTime = null;

            LockLock(codelock);
        }
        void EmptyDeployablesRoom( Vector3 doorpos, float radius )
        {
            var founditems = new List<Deployable>();
            foreach (Collider col in UnityEngine.Physics.OverlapSphere(doorpos, radius, deployableColl))
            {
                Deployable deploy = col.GetComponentInParent<Deployable>();
                if (deploy == null) continue;
                if (founditems.Contains(deploy)) continue;

                bool canReach = true;
                foreach (RaycastHit rayhit in UnityEngine.Physics.RaycastAll(deploy.transform.position + Vector3UP, (doorpos + Vector3UP - deploy.transform.position).normalized, Vector3.Distance(deploy.transform.position, doorpos) - 0.2f, constructionColl)) { canReach = false; break; }
                if (!canReach) continue;

                foreach (Collider col2 in UnityEngine.Physics.OverlapSphere(doorpos, radius, constructionColl))
                {
                    if (col2.GetComponentInParent<Door>() == null) continue;
                    if (col2.transform.position == doorpos) continue;

                    bool canreach2 = true;
                    foreach (RaycastHit rayhit in UnityEngine.Physics.RaycastAll(deploy.transform.position + Vector3UP, (col2.transform.position + Vector3UP - deploy.transform.position).normalized, Vector3.Distance(deploy.transform.position, col2.transform.position) - 0.2f, constructionColl)) { canreach2 = false; }
                    if (canreach2) { canReach = false; break; }
                }
                if (!canReach) continue;

                founditems.Add(deploy);
            }
            foreach (Deployable deploy in founditems)
            {
                if (!(deploy.GetComponentInParent<BaseEntity>().isDestroyed))
                    deploy.GetComponent<BaseEntity>().KillMessage();
            }
        }
        void ResetRoom( CodeLock codelock, HotelData hotel, Room room )
        {
            BaseEntity door = codelock.GetParentEntity();
            Vector3 block = door.transform.position;

            EmptyDeployablesRoom(block, Convert.ToSingle(hotel.rr));
            foreach (DeployableItem deploy in room.defaultDeployables) { SpawnDeployable(deploy.prefabname, deploy.Pos(), deploy.Rot(), null); }

            fieldWhiteList.SetValue(codelock, new List<ulong>());

            UnlockLock(codelock);

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

            string loc = hoteldata.x == null ? "None" : string.Format("{0} {1} {2}", hoteldata.x, hoteldata.y, hoteldata.z);
            string hrad = hoteldata.r == null ? "None" : hoteldata.r;
            string rrad = hoteldata.rr == null ? "None" : hoteldata.rr;
            string rnum = hoteldata.rooms == null ? "0" : hoteldata.rooms.Count.ToString();

            int onumint = 0;
            if (hoteldata.rooms != null)
            {
                foreach (KeyValuePair<string, Room> pair in hoteldata.rooms)
                {
                    if (pair.Value.renter != null) onumint++;
                }
            }
            string onum = onumint.ToString();

            newguimsg = GUIBoardAdmin.Replace("{name}", hoteldata.hotelname).Replace("{loc}", loc).Replace("{hrad}", hrad).Replace("{rrad}", rrad).Replace("{rnum}", rnum).Replace("{onum}", onum);

            return newguimsg;
        }

        void RemoveAdminHotelGUI(BasePlayer player) { CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", "HotelAdmin"); }

        void ShowHotelGrid(BasePlayer player)
        {
            HotelData hoteldata = EditHotel[player.userID.ToString()];
            if (hoteldata.x != null && hoteldata.r != null)
            {
                Vector3 hpos = hoteldata.Pos();
                float hrad = Convert.ToSingle(hoteldata.r);
                player.SendConsoleCommand("ddraw.sphere", 5f, UnityEngine.Color.blue, hpos, hrad);
            }
            if (hoteldata.rooms == null) return;
            foreach (KeyValuePair<string, Room> pair in hoteldata.rooms)
            {
                List<DeployableItem> deployables = pair.Value.defaultDeployables;
                foreach (DeployableItem deployable in deployables)
                {
                    player.SendConsoleCommand("ddraw.arrow", 10f, UnityEngine.Color.green, pair.Value.Pos(), deployable.Pos(), 0.5f);
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
                SendReply(player, "/hotel roomradius XX => sets the radius of the rooms");
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
                    case "roomradius":
                        if (args.Length == 1)
                        {
                            SendReply(player, "/hotel roomradius XX");
                            return;
                        }
                        int rad3 = 5;
                        int.TryParse(args[1], out rad3);
                        if (rad3 < 1) rad3 = 5;

                        (EditHotel[player.userID.ToString()]).rr = rad3.ToString();

                        SendReply(player, string.Format("RoomRadius set to {0}", args[1]));
                        break;
                    case "rooms":
                        SendReply(player, "Rooms Refreshing ...");
                        (EditHotel[player.userID.ToString()]).RefreshRooms();
                        SendReply(player, "Rooms Refreshed");
                        break;
                    case "reset":
                        foreach( KeyValuePair<string,Room> pair in (EditHotel[player.userID.ToString()]).rooms )
                        {
                            CodeLock codelock = FindCodeLockByRoomID(pair.Key);
                            if (codelock == null) continue;
                            Debug.Log(pair.Value.roomid + " " + codelock.GetParentEntity().transform.position.ToString());
                            ResetRoom(codelock, (EditHotel[player.userID.ToString()]), pair.Value);
                        }
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
                    if (hotel.x != null && hotel.r != null)
                    {
                        foreach (Collider col in UnityEngine.Physics.OverlapSphere(hotel.Pos(), Convert.ToSingle(hotel.r), constructionColl))
                        {
                            Door door = col.GetComponentInParent<Door>();
                            if (door != null)
                            {
                                if (door.HasSlot(BaseEntity.Slot.Lock))
                                {
                                    door.SetFlag(BaseEntity.Flags.Open, false);
                                    door.SendNetworkUpdateImmediate(true);
                                }
                            }
                        }
                    }
                    EditHotel.Add(player.userID.ToString(), hotel);
                    break;
                }
            }

            if (!EditHotel.ContainsKey(player.userID.ToString())) { SendReply(player, string.Format(MessageErrorEditDoesntExist, args[0])); return; }

            SendReply(player, string.Format(MessageHotelEditEditing, EditHotel[player.userID.ToString()].hotelname));

            RefreshAdminHotelGUI(player);
        }

        [ChatCommand("hotel_remove")]
        void cmdChatHotelRemove(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, MessageErrorNotAllowed); return; }
            if (EditHotel.ContainsKey(player.userID.ToString())) { SendReply(player, MessageAlreadyEditing); return; }
            if (args.Length == 0) { SendReply(player, MessageHotelEditHelp); return; }

            string hname = args[0];
            HotelData targethotel = null;
            foreach (HotelData hotel in storedData.Hotels)
            {
                if (hotel.hotelname.ToLower() == hname.ToLower())
                {
                    hotel.Deactivate();
                    targethotel = hotel;
                    break;
                }
            }
            if(targethotel == null) { SendReply(player, string.Format(MessageErrorEditDoesntExist, args[0])); return; }

            storedData.Hotels.Remove(targethotel);
            SaveData();
            SendReply(player, string.Format("Hotel Named: {0] was successfully removed", hname));

        }

        [ChatCommand("hotel_reset")]
        void cmdChatHotelReset(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, MessageErrorNotAllowed); return; }
            if (EditHotel.ContainsKey(player.userID.ToString())) { SendReply(player, MessageAlreadyEditing); return; }

            storedData.Hotels = new HashSet<HotelData>();
            SaveData();
            SendReply(player, "Hotels were all deleted");

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
