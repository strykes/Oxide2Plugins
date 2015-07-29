namespace Oxide.Plugins
{
    [Info("Hotel", "Reneb", "1.0.0")]
    class Hotel : RustPlugin
    {

        ////////////////////////////////////////////////////////////
        // Plugin References
        ////////////////////////////////////////////////////////////
		
		
		// FIELDS
		static int deployableColl = UnityEngine.LayerMask.GetMask(new string[] { "Deployed" });
		static int constructionColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction" });
		 ////////////////////////////////////////////////////////////
        // cached Fields
        ////////////////////////////////////////////////////////////
		public static Dictionary<string, HotelData> EditHotel = new Dictionary<string, HotelData>();
		
		////////////////////////////////////////////////////////////
        // Config Management
        ////////////////////////////////////////////////////////////
		
		static int authlevel = 2;
		
		////////////////////////////////////////////////////////////
        // Data Management
        ////////////////////////////////////////////////////////////

        static StoredData storedData;

        class StoredData
        {
            public HashSet<HotelData> Hotels = new HashSet<HotelData>();

            public StoredData()
            {
            }
        }

        void OnServerSave()
        {
            SaveData();
        }

        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("Hotel", storedData);
        }

        void LoadData()
        {
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Hotel");
            }
            catch
            {
                storedData = new StoredData();
            }
            
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
			
			public DeployableItem()
            {
            }

            public DeployableItem(Deployable deployable)
            {
            	prefabname = StringPool.Get(deployable.prefabID).ToString();
            	
                this.x = Math.Ceil(deployable.transform.position.x).ToString();
                this.y = Math.Ceil(deployable.transform.position.y).ToString();
                this.z = Math.Ceil(deployable.transform.position.z).ToString();
                
                this.rx = deployable.transform.rotation.x.ToString();
                this.ry = deployable.transform.rotation.y.ToString();
                this.rz = deployable.transform.rotation.z.ToString();
                this.rw = deployable.transform.rotation.w.ToString();
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
                this.x = Math.Ceil(position.x).ToString();
                this.y = Math.Ceil(position.y).ToString();
                this.z = Math.Ceil(position.z).ToString();
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
            bool enabled;

            public HotelData()
            {
            	this.enabled = false;
            }

            public HotelData(string hotelname)
            {
                this.hotelname = hotelname;
                this.x = "0";
                this.y = "0";
                this.z = "0";
                this.r = "60";
                this.rr = "20"
                this.enabled = false;
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
            	if(Pos() == default(Vector3))
            		return;
				Dictionary<string, Room> detectedRooms = FindAllRooms( Pos(), Convert.ToSingle(this.r), Convert.ToSingle(this.rr) );
            	
            	List<string> toAdd = new List<string>();
            	List<string> toDelete = new List<string>();
            	
            	foreach( KeyValuePair<string, Room> pair in rooms )
            	{
            		if( pair.Value.renter != null )
					{
						detectedRooms.Remove( pair.Key );
						Debug.Log( string.Format( "{0} is occupied and can't be edited", pair.Key ) );
						continue;
					}
            		if( !detectedRooms.ContainsKey( pair.Key ) )
            		{
            			toDelete.Add( pair.Key );
            		}
            	}
            	foreach( KeyValuePair<string, Room> pair in detectedRooms )
            	{
            		if( !rooms.ContainsKey( pair.Key ) )
            		{
            			toAdd.Add( pair.Key );
            		}
            	}
            	foreach( string roomid in toDelete )
            	{
            		rooms.Remove( roomid );
            		Debug.Log( string.Format( "{0} doesnt exist anymore, removing this room", roomid ) );
            	}
            	foreach( string roomid in toAdd )
            	{
            		Debug.Log( string.Format( "{0} is a new room, adding it", roomid ) );
            		rooms.Add( roomid, detectedRooms[roomid] );
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
            	if(rooms.ContainsKey[newroom.roomid])
            		rooms.Remove(newroom.roomid);
            	
            	rooms.Add(newroom.roomid, newroom);
            }
            
            
        }
		
		static Dictionary<string, Room> FindAllRooms( Vector3 position, float radius, float roomradius )
		{
			List<BaseLock> listLocks = new List<BaseLock>();
			foreach(Collider col in UnityEngine.Physics.OverlapSphere(position, radius, deployableColl))
			{
				if( col.GetComponentInParent<BaseLock>() != null )
				{
					listLocks.Add( col.GetComponentInParent<BaseLock>() );
					
				}
			}
			
			Dictionary<Deployable, string> deployables = new Dictionary<Deployable, string>();
			Dictionary<string, Room> tempRooms = new Dictionary<string, Room>();
			
			foreach( BaseLock lock in listLocks )
			{
				Room newRoom = new Room(lock.transform.position);
				newRoom.defaultDeployables = new List<DeployableItem>();
				var founditems = new List<Deployable>();
				foreach(Collider col in UnityEngine.Physics.OverlapSphere(position, radius, deployableColl))
				{
					Deployable deploy = col.GetComponentInParent<Deployable>();
					if( deploy != null )
					{
						if( !founditems.Contains( deploy ) )
						{
							founditems.Add( deploy );
							bool canReach = true;
							foreach( RaycastHit rayhit in UnityEngine.Physics.RaycastAll( deploy.transform.position, (lock.transform.position - deploy.transform.position).normalized, Vector3.Distance(deploy.transform.position, lock.transform.position ) - 0.5f, constructionColl ))
							{
								canReach = false;
								break;
							}
							if(!canReach) continue;
							
							if( deployables.ContainsKey( deploy ) )
							{
								deployables[deploy] = "0";
							}
							else
							{
								deployables.Add( deploy, newRoom.roomid );
							}
						}
					}
				}
				tempRooms.Add(newRoom.roomid, newRoom);
			}
			foreach (KeyValuePair<Deployable, string> pair in deployables)
			{
				if( pair.Value != "0" )
				{
					DeployableItem newDeployItem = new DeployableItem( pair.Key );
					tempRooms[ pair.Value ].defaultDeployables.Add( newDeployItem );
				}
			}
			
			return tempRooms;
		}
		
        static void AddLog(string userid, string logType, Vector3 frompos, Vector3 topos)
        {
            if (anticheatlogs[userid] == null)
                anticheatlogs[userid] = new List<AntiCheatLog>();
            AntiCheatLog newlog = new AntiCheatLog(userid, logType, frompos, topos);
            (anticheatlogs[userid]).Add(newlog);
            storedData.AntiCheatLogs.Add(newlog);
        }

        static double LogTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }
        
        
        
        void RefreshAdminHotelGUI(BasePlayer player)
        {
        	
        }
        
        void RemoveAdminHotelGUI(BasePlayer player)
        {
        
        }
        
        void ShowHotelGrid(BasePlayer player)
        {
        
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
            
            HotelData removeHotel;
            foreach( HotelData hoteldata in storedData.Hotels)
            {
            	if(hoteldata.hotelname.ToLower() == editedhotel.hotelname.ToLower())
            	{
            		removeHotel = hoteldata;
            		break;
            	}
            }
            if(removeHotel != null)
            {
            	storedData.Hotels.Remove( removeHotel );
            }
            
            storedData.Hotels.Add( editedhotel );
            
            SaveData();
            
            EditHotel.Remove( player.userID.ToString() );
            
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
            
            EditHotel.Remove( player.userID.ToString() );
            
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

        	if (args.Count == 0)
        	{
        		SendReply(player, "==== Available options ====");
        		SendReply(player, "/hotel location => sets the center hotel location where you stand");
        		SendReply(player, "/hotel radius XX => sets the radius of the hotel (the entire structure of the hotel needs to be covered by the zone");
        		SendReply(player, "/hotel rooms => refreshs the rooms (detects new rooms, deletes rooms if they don't exist anymore, if rooms are in use they won't get taken in count)");
        	}
        	else
        	{
        		switch( args[0].ToLower() )
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
        				if(args.Count == 1)
        				{
        					SendReply(player, "/hotel radius XX");
        					return;
        				}
        				int rad = 20;
        				int.TryParse( args[1], out rad );
        				if(rad < 1) rad = 20;
        				
        				string[] zoneargs = new string[] { "name", editedhotel.hotelname, "radius", rad.ToString() };
                    	ZoneManager.Call("CreateOrUpdateZone", editedhotel.hotelname, zoneargs);
                    	
                    	(EditHotel[player.userID.ToString()]).r = rad.ToString();
                    	
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
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            
            if(EditHotel.ContainsKey(player.userID.ToString()))
            {
            	SendReply(player, "You are already editing a hotel. You must close or save it first.");
				return;
            }
            
            if(args.Count == 0)
            {
            	SendReply(player, "You must select the name of the hotel you want to edit: /hotel_edit HOTELNAME");
				return;
            }
            
            string hname = args[0];
            foreach(HotelData hotel in storedData.Hotels)
            {
            	if(hotel.hotelname.ToLower() == hname.ToLower())
            	{
            		EditHotel.Add(player.userID.ToString(), hotel);
            		hotel.Deactivate();
            		break;
            	}
            }
			if( !EditHotel.ContainsKey(player.userID.ToString()) )
			{
				SendReply(player, "The hotel you are trying to edit doesn't exist");
				return;
			}
            SendReply(player, string.Format("You are editing the hotel named: {0}. Now say /hotel to continue configuring your hotel. Note that no one can register/leave the hotel while you are editing it.",EditHotel[player.userID.ToString()].hotelname));
            RefreshAdminHotelGUI( player );
        }
        
        [ChatCommand("hotel_new")]
        void cmdChatHotelNew(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            
            if(EditHotel.ContainsKey(player.userID.ToString()))
            {
            	SendReply(player, "You are already editing a hotel. You must close or save it first.");
				return;
            }
            
            if(args.Count == 0)
            {
            	SendReply(player, "You must select a name for the new hotel: /hotel_new HOTELNAME");
				return;
            }
            
            string hname = args[0];
            foreach(HotelData hotel in storedData.Hotels)
            {
            	if(hotel.hotelname.ToLower() == hname.ToLower())
            	{
            		SendReply(player, string.Format("{0} is already the name of a hotel",hname));
            		return;
            	}
            }
            
            HotelData newhotel = new HotelData(args[0]);
            EditHotel.Add(player.userID.ToString(), newhotel);
            SendReply(player, string.Format("You've created a new Hotel named: {0}. Now say /hotel to continue configuring your hotel.",hname));
            RefreshAdminHotelGUI( player );
        }
        
        
	}
}
