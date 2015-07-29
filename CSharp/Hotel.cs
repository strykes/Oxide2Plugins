namespace Oxide.Plugins
{
    [Info("Hotel", "Reneb", "1.0.0")]
    class Hotel : RustPlugin
    {

        ////////////////////////////////////////////////////////////
        // Plugin References
        ////////////////////////////////////////////////////////////
		
		
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

        public class Room
        {
            public string roomid;
            public string x;
            public string y;
            public string z;
            public List<object> defaultDeployables;
			public string renter;
			public string checkingTime;
			public string checkoutTime;
			
            Vector3 pos;

            public Room()
            {
            }

            public Room(string userid, string logType, Vector3 frompos, Vector3 topos)
            {
                this.userid = userid;
                this.fx = frompos.x.ToString();
                this.fy = frompos.y.ToString();
                this.fz = frompos.z.ToString();
                this.tx = topos.x.ToString();
                this.ty = topos.y.ToString();
                this.tz = topos.z.ToString();
                this.td = logType;
                this.lg = LogTime().ToString();
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
			
			public Dictionary<string, Room> rooms;
			
            Vector3 pos;

            public HotelData()
            {
            }

            public HotelData(string hotelname)
            {
                this.hotelname = hotelname;
            }

            public Vector3 Pos()
            {
                if (pos == default(Vector3))
                    pos = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return pos;
            }
            
            public void RefreshRooms()
            {
				
            }
            
            public void AddRoom(Room newroom)
            {
            	if(rooms.ContainsKey[newroom.roomid])
            		rooms.Remove(newroom.roomid);
            	
            	rooms.Add(newroom.roomid, newroom);
            }
            
            
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
