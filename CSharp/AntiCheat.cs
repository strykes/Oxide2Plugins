using System.Collections.Generic;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
 
namespace Oxide.Plugins
{
    [Info("AntiCheat", "Reneb", "2.1.14", ResourceId = 730)]
    class AntiCheat : RustPlugin
    {
    	////////////////////////////////////////////////////////////
    	// Cached Fields
    	////////////////////////////////////////////////////////////
    	
    	static RaycastHit cachedRaycasthit;
    	
    	////////////////////////////////////////////////////////////
    	// Fields
    	////////////////////////////////////////////////////////////
    	
    	[PluginReference]
        Plugin EnhancedBanSystem;
    	
    	[PluginReference]
        Plugin DeadPlayersList;
    	
        static Vector3 VectorDown = new Vector3(0f, -1f, 0f);
        static int constructionColl;
        
        float lastTime;
        bool serverInitialized = false;
        
        Oxide.Plugins.Timer activateTimer;
        
        GameObject originalWallhack;
        
        List<GameObject> ListGameObjects = new List<GameObject>();
		static List<BasePlayer> adminList = new List<BasePlayer>();
        Hash<TriggerBase, Hash<BaseEntity, Vector3>> TriggerData = new Hash<TriggerBase, Hash<BaseEntity, Vector3>>();
        Hash<TriggerBase, BuildingBlock> TriggerToBlock = new Hash<TriggerBase, BuildingBlock>();
        Hash<BaseEntity, float> lastDetections = new Hash<BaseEntity, float>();
        Dictionary<uint, float> DoorCheck = new Dictionary<uint, float>();


		////////////////////////////////////////////////////////////
    	// Config Fields
    	////////////////////////////////////////////////////////////
    	
        static int authIgnore = 1;
        static int fpsIgnore = 30;

        static bool speedhack = true;
        static bool speedhackPunish = true;
        static int speedhackDetections = 3;
        static float minSpeedPerSecond = 10f;
		static bool speedhackLog = true;

        static bool flyhack = true;
        static bool flyhackPunish = true;
        static int flyhackDetections = 3; 
		static bool flyhackLog = true;
		
        static bool wallhack = true;
		static bool wallhackLog = true;
		
        static bool fpsCheckCalled = false;
        static ConsoleSystem.Arg fpsCaller;
        static List<PlayerHack> fpsCalled = new List<PlayerHack>();
        static float fpsTime;
        
        static string multipleNames = "Multiple players found with this name";
        static string noPlayerFound = "No player found with this name";
		
		////////////////////////////////////////////////////////////
    	// Config Management
    	////////////////////////////////////////////////////////////
		
        void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T) 
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        private void CheckCfgFloat(string Key, ref float var)
        {
        
            if (Config[Key] != null) 
                var = Convert.ToSingle(Config[Key]);
            else
                Config[Key] = var;
        } 
           
        void Init()
        {
            CheckCfg<int>("Settings: Ignore Hacks for authLevel", ref authIgnore);
            CheckCfg<int>("Settings: FPS Ignore", ref fpsIgnore);
            CheckCfg<bool>("SpeedHack: activated", ref speedhack);
            CheckCfg<bool>("SpeedHack: Punish", ref speedhackPunish);
            CheckCfg<int>("SpeedHack: Punish Detections", ref speedhackDetections);
            CheckCfgFloat("SpeedHack: Speed Detection", ref minSpeedPerSecond);
            CheckCfg<bool>("Flyhack: activated", ref flyhack);
            CheckCfg<bool>("Flyhack: Punish", ref flyhackPunish);
            CheckCfg<int>("Flyhack: Punish Detections", ref flyhackDetections);
            CheckCfg<bool>("Wallhack: activated", ref wallhack);
            SaveConfig();            
        } 
		////////////////////////////////////////////////////////////
    	// Log Management
    	////////////////////////////////////////////////////////////
    	
    	static StoredData storedData;
        static Hash<string, List<AntiCheatLog>> anticheatlogs = new Hash<string, List<AntiCheatLog>>();
        
        class StoredData
        {
            public HashSet<AntiCheatLog> AntiCheatLogs = new HashSet<AntiCheatLog>();

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
            Interface.GetMod().DataFileSystem.WriteObject("AntiCheatLogs", storedData);
        }
        
        void LoadData()
        {
            anticheatlogs.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("AntiCheatLogs");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var thelog in storedData.AntiCheatLogs)
            {
            	if(anticheatlogs[thelog.userid] == null)
            		anticheatlogs[thelog.userid] = new List<AntiCheatLog>();
                (anticheatlogs[thelog.userid]).Add(thelog);
            }
        }
        
    	class AntiCheatLog
        {
            public string userid;
            public string fx;
            public string fy;
            public string fz;
            public string tx;
            public string ty;
            public string tz;
            public string sd;
            public string lg;
            
            Vector3 frompos;
            Vector3 topos;
               
            public AntiCheatLog(string userid, string logType, Vector3 frompos, Vector3 topos)
            {
                this.userid = userid;
                this.fx = frompos.x.ToString();
                this.fy = frompos.y.ToString();
                this.fz = frompos.z.ToString();
                this.tx = topos.x.ToString();
                this.ty = topos.y.ToString();
                this.tz = topos.z.ToString();
                // GET TIME HERE
            }
			
			public Vector3 FromPos()
			{
				if(frompos == default(Vector3))
					frompos = new Vector3( float.Parse(fx), float.Parse(fy), float.Parse(fz) );
				return frompos;
			}
			
			public Vector3 ToPos()
			{
				if(topos == default(Vector3))
					topos = new Vector3( float.Parse(tx), float.Parse(ty), float.Parse(tz) );
				return topos;
			}
        }
        
        static void AddLog(string userid, string logType, Vector3 frompos, Vector3 topos)
        {
        	if(anticheatlogs[userid] == null)
        		anticheatlogs[userid] = new List<AntiCheatLog>();
        	AntiCheatLog newlog = new AntiCheatLog(userid, logType, frompos, topos);
        	(anticheatlogs[userid]).Add(newlog);
        	storedData.AntiCheatLogs.Add(newlog);
        }
    	
		////////////////////////////////////////////////////////////
    	// Plugin Initialization
    	////////////////////////////////////////////////////////////
        
        void Loaded()
        {  
            constructionColl = LayerMask.GetMask( new string[] { "Construction" });
            if (!permission.PermissionExists("cananticheat")) permission.RegisterPermission("cananticheat", this);
        }
        
        void OnServerInitialized()
        { 
            serverInitialized = true;
            originalWallhack = new UnityEngine.GameObject("Anti Wallhack");
            originalWallhack.AddComponent<MeshCollider>();
            originalWallhack.AddComponent<TriggerBase>();
            originalWallhack.gameObject.layer = UnityEngine.LayerMask.NameToLayer("Trigger");
            var newlayermask = new UnityEngine.LayerMask();
            newlayermask.value = 133120;
            originalWallhack.GetComponent<TriggerBase>().interestLayers = newlayermask;
            RefreshAllWalls(); 
            RefreshPlayers();
        }
        void RefreshPlayers()
        {
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.GetComponent<PlayerHack>() != null) GameObject.Destroy(player.GetComponent<PlayerHack>());
                if(player.net.connection.authLevel > 0 || permission.UserHasPermission(player.userID.ToString(), "cananticheat"))
                {
                	if(!adminList.Contains(player))
        			adminList.Add(player);
        			continue;
                }
                if (!speedhack && !flyhack) continue;
                player.gameObject.AddComponent<PlayerHack>();
            }
        }
        void Unload()
        {
            foreach(GameObject gameObj in ListGameObjects)
            { 
                GameObject.Destroy(gameObj);
            } 
            var objects = GameObject.FindObjectsOfType(typeof(PlayerHack));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            if (activateTimer != null)
                activateTimer.Destroy();
        } 
		
		
		
		////////////////////////////////////////////////////////////
    	// PlayerHack class
    	////////////////////////////////////////////////////////////
    	
        public class PlayerHack : MonoBehaviour
        {
            public BasePlayer player;
            public Vector3 lastPosition;
            public float Distance3D;
            public float VerticalDistance;
            public bool isonGround;
            
            public float currentTick;
            public float lastTick;

            public float speedHackDetections = 0f;
            public float lastTickSpeed;

            public float flyHackDetections = 0f;
            public float lastTickFly;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckPlayer", 1f, 1f);
                lastPosition = player.transform.position;
            }
            void CheckPlayer()
            {           
                if (!player.IsConnected()) GameObject.Destroy(this);
                currentTick = Time.realtimeSinceStartup;
                Distance3D = Vector3.Distance(player.transform.position, lastPosition);
                VerticalDistance = player.transform.position.y - lastPosition.y;
                isonGround = player.IsOnGround();

                if(!player.IsWounded() && !player.IsDead() && !player.IsSleeping() && Performance.frameRate > fpsIgnore)
                    CheckForHacks(this);

                lastPosition = player.transform.position;

                if(fpsCheckCalled)
                    if(!fpsCalled.Contains(this))
                    {
                        fpsCalled.Add(this);
                        fpsTime += (Time.realtimeSinceStartup - currentTick);
                    }

                lastTick = currentTick;
            }
        }
        static void CheckForHacks(PlayerHack hack)
        {
            if (speedhack)
                CheckForSpeedHack(hack);
            if (flyhack)
                CheckForFlyhack(hack);
        }
        
        void OnPlayerRespawned(BasePlayer  player)
        {
            if (player.net.connection.authLevel >= authIgnore) return;
            if (player.GetComponent<PlayerHack>() == null)
            {
                player.gameObject.AddComponent<PlayerHack>();
            } 
        }
        
        ////////////////////////////////////////////////////////////
    	// Wallhack related
    	////////////////////////////////////////////////////////////
        
        void RefreshAllWalls()
        {
            if (!wallhack) return;
            var allbuildings = UnityEngine.Resources.FindObjectsOfTypeAll<BuildingBlock>();
            var currenttime = Time.realtimeSinceStartup;
            foreach (BuildingBlock block in allbuildings)
            {
                if (block.blockDefinition != null && (block.blockDefinition.hierachyName == "wall" || block.blockDefinition.hierachyName == "door.hinged"))
                {
                    CreateNewProtectionFromBlock(block, true);
                    if (block.blockDefinition.hierachyName == "door.hinged")                    
                            DoorCheck.Add(block.net.ID, Time.realtimeSinceStartup);                                        
                }
            } 
            Debug.Log(string.Format("AntiCheat: Took {0} seconds to protect all walls & doors", (Time.realtimeSinceStartup - currenttime).ToString()));
        }
        
        void OnDoorOpened(BuildingBlock door)
        {
            if (DoorCheck.ContainsKey(door.net.ID))
                DoorCheck[door.net.ID] = Time.realtimeSinceStartup;            
			else
                DoorCheck.Add(door.net.ID, Time.realtimeSinceStartup);
        }
        
        void OnDoorClosed(BuildingBlock door)
        {
            if (DoorCheck.ContainsKey(door.net.ID))
                DoorCheck[door.net.ID] = Time.realtimeSinceStartup;
            else
                DoorCheck.Add(door.net.ID, Time.realtimeSinceStartup);
        }
        
        bool isOpen(BuildingBlock block)
        {
            if (DoorCheck.ContainsKey(block.net.ID))
            {
                if (Time.realtimeSinceStartup - DoorCheck[block.net.ID] < 6f)
                    return true;
            }
            return block.IsOpen();
        }
        
        bool CheckWallhack(BuildingBlock buildingblock, BaseEntity col, Vector3 initialPos)
        {
            Vector3 cachedDiff = col.transform.position - initialPos;
            if (initialPos.y - buildingblock.transform.position.y > 2) return false;

            if (UnityEngine.Physics.Linecast(initialPos, col.transform.position, out cachedRaycasthit, constructionColl))
            {
                if (cachedRaycasthit.collider.GetComponentInParent<BuildingBlock>() == buildingblock)
                {
                    return true;
                }
            }
            return false;
        } 
        
        void ForcePlayerBack(BasePlayer player, Vector3 entryposition, Vector3 exitposition)
        {
            var distance = Vector3.Distance(exitposition, entryposition) + 0.5f;
            var direction = (entryposition - exitposition).normalized;
            ForcePlayerPosition(player, exitposition + (direction * distance));
        }
        
         void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.transform.position = destination;
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();
        }
        
        void OnEntityEnter(TriggerBase triggerbase, BaseEntity entity)
        {
            if (triggerbase.gameObject.name != "Anti Wallhack(Clone)") return;
            if (entity.GetComponent<BasePlayer>() == null) return;
            if (TriggerData[triggerbase] == null)
                TriggerData[triggerbase] = new Hash<BaseEntity,Vector3>();
            (TriggerData[triggerbase])[entity] = entity.transform.position;
        }
        
        void OnEntityLeave(TriggerBase triggerbase, BaseEntity entity)
        {
            if (entity == null || triggerbase == null || triggerbase.gameObject.name != "Anti Wallhack(Clone)") return;
            if (entity.GetComponent<BasePlayer>() == null) return;
            if(TriggerToBlock[triggerbase] == null)
            {
                GameObject.Destroy(triggerbase.gameObject);
                return;
            }

            if (!isOpen(TriggerToBlock[triggerbase]))
            {
            	BasePlayer player = entity.GetComponent<BasePlayer>();
                if (player.net.connection != null)
                {
                    if (player.net.connection.authLevel < authIgnore)
                        if (CheckWallhack(TriggerToBlock[triggerbase], entity, (TriggerData[triggerbase])[entity]))
                        {
                            if (Performance.frameRate > fpsIgnore && (Time.realtimeSinceStartup - lastDetections[entity]) < 2f)
                            {
                            	if(wallhackLog)
                					AddLog(player.userID.ToString(), "wall", (TriggerData[triggerbase])[entity], entity.transform.position);
                                SendMsgAdmin(string.Format("{0} was detected wallhacking from {1} to {2}", player.displayName, (TriggerData[triggerbase])[entity].ToString(), entity.transform.position.ToString()));
                                PrintWarning(string.Format("{0}[{3}] was detected wallhacking from {1} to {2}", player.displayName, (TriggerData[triggerbase])[entity].ToString(), entity.transform.position.ToString(), player.userID));
                            }
                            lastDetections[entity] = Time.realtimeSinceStartup;
                            ForcePlayerBack(player, (TriggerData[triggerbase])[entity], entity.transform.position);
                        }
                }
            } 
            (TriggerData[triggerbase]).Remove(entity);
        }
        
        void CreateNewProtectionFromBlock(BuildingBlock buildingblock, bool Immediate)
        {
            var newgameobject = UnityEngine.Object.Instantiate(originalWallhack);
            ListGameObjects.Add(newgameobject);
            var mesh = newgameobject.GetComponent<MeshCollider>();
            newgameobject.transform.position = buildingblock.transform.position;
            newgameobject.transform.rotation = buildingblock.transform.rotation;
            mesh.convex = false;
            mesh.sharedMesh = buildingblock.GetComponentInChildren<UnityEngine.MeshCollider>().sharedMesh;
            mesh.enabled = true;
            if (Immediate) newgameobject.SetActive(true);
            else
            {
                newgameobject.SetActive(false);
                timer.Once(20f, () => ActivateGameObject(newgameobject));
            }
            TriggerToBlock[newgameobject.GetComponent<TriggerBase>()] = buildingblock;
        }
        
        void ActivateGameObject(UnityEngine.GameObject gameObj)
        {
            if (gameObj == null) return;
            gameObj.SetActive(true);
        }
        
        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (!serverInitialized) return;
            if (!wallhack) return;
            var buildingblock = gameobject.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null) return;
            if (buildingblock.blockDefinition.hierachyName == "wall" || buildingblock.blockDefinition.hierachyName == "door.hinged")
            {
                CreateNewProtectionFromBlock(buildingblock, false);
                if (buildingblock.blockDefinition.hierachyName == "door.hinged")
                    DoorCheck.Add(buildingblock.net.ID, Time.realtimeSinceStartup);   
            }
        }
        
        ////////////////////////////////////////////////////////////
    	// Speedhack related
    	////////////////////////////////////////////////////////////
        
        static void CheckForSpeedHack(PlayerHack hack)
        {
            if (hack.Distance3D < minSpeedPerSecond) return;
            if (hack.VerticalDistance < -10f) return;
            if(hack.lastTickSpeed == hack.lastTick)
            {
                hack.speedHackDetections++;
                if(speedhackLog)
                	AddLog(hack.player.userID.ToString(), "speed", hack.lastPosition, hack.player.transform.position);
                SendDetection(string.Format("{0} - {1} is being detected with: Speedhack ({2}m/s)", hack.player.userID.ToString(), hack.player.displayName, hack.Distance3D.ToString()));
                if(hack.speedHackDetections >= speedhackDetections)
                {
                    if(speedhackPunish)
                        Punish(hack.player, string.Format("rSpeedhack ({0}m/s)", hack.Distance3D.ToString()));
                }
                
            }
            else
            {
                hack.speedHackDetections = 0f;
            }
            hack.lastTickSpeed = hack.currentTick;
        }
        
        ////////////////////////////////////////////////////////////
    	// Flyhack related
    	////////////////////////////////////////////////////////////
    	
        static void CheckForFlyhack(PlayerHack hack)
        { 
            if (hack.isonGround) return;
            if (hack.player.transform.position.y < 5f) return;
            if (hack.VerticalDistance < -10f) return; 
            if (UnityEngine.Physics.Raycast(hack.player.transform.position, VectorDown, 5f)) return;
            if (hack.lastTickFly == hack.lastTick) 
            {
                hack.flyHackDetections++;
                if(flyhackLog)
                	AddLog(hack.player.userID.ToString(), "fly", hack.lastPosition, hack.player.transform.position);
                SendDetection(string.Format("{0} - {1} is being detected with: Flyhack ({2}m/s)", hack.player.userID.ToString(), hack.player.displayName, hack.Distance3D.ToString()));
                if (hack.flyHackDetections >= flyhackDetections)
                {
                    if (flyhackPunish)
                        Punish(hack.player, string.Format("rFlyhack ({0}m/s)", hack.Distance3D.ToString()));
                }
            } 
            else
            {
                hack.flyHackDetections = 0f;
            }
            hack.lastTickFly = hack.currentTick;
        }
        
        
        ////////////////////////////////////////////////////////////
    	// Admin Chat related
    	////////////////////////////////////////////////////////////
        
        static void SendDetection(string msg)
        {
            foreach (BasePlayer player in adminList)
            {
                if(player != null && player.net != null)
                {
                    player.SendConsoleCommand("chat.add", new object[] { 0, msg.QuoteSafe() });
                }
            }
            Interface.GetMod().LogWarning(msg);
        }
        static void SendMsgAdmin(string msg)
        {
        	foreach (BasePlayer player in adminList)
            {
                if (player != null && player.net != null)
                {
                        player.SendConsoleCommand("chat.add", new object[] { 0, msg.QuoteSafe() });
                }
            }
        }
        
        void OnPlayerInit(BasePlayer player)
        {
        	if(player.net.connection.authLevel > 0 || permission.UserHasPermission(player.userID.ToString(), "cananticheat"))
        	{
        		if(!adminList.Contains(player))
        			adminList.Add(player);
        	}
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
        	if(adminList.Contains(player))
        		adminList.Remove(player);
        }
        
        ////////////////////////////////////////////////////////////
    	// Punish a player
    	////////////////////////////////////////////////////////////
        
        static void Punish(BasePlayer player, string msg)
        {
            if (player.net.connection.authLevel < authIgnore)
            {
            	if(EnhancedBanSystem != null)
            	{
                	Interface.GetMod().CallHook("Ban", null, player, msg, false);
                }
                else
                {
                	// ADD BANNNN!!
                
                }
            }
            else
            {
                GameObject.Destroy(player.GetComponent<PlayerHack>());
            }
        }
        
        bool hasAccess( BasePlayer player )
        {
        	if(player == null) return false;
        	if(player.net.connection.authLevel > 0) return true;
        	return permission.UserHasPermission(player.userID.ToString(), "cananticheat");
        }
        
        bool FindPlayerByName( string name , out string targetid, out string targetname )
        {
        	ulong userid;
        	targetid = string.Empty;
        	targetname = string.Empty;
        	if( name.Length == 17 && ulong.TryParse( name, out userid ) )
        	{
        		targetid = name;
        		return true;
        	}
        	
        	foreach( BasePlayer player in UnityEngine.Object.FindObjectsOfTypeAll<BasePlayer>() )
        	{
        		if( player.displayName == name )
        		{
        			targetid = player.userID.ToString();
        			targetname = player.displayName;
        			return true;
        		}
        		if( player.displayName.Contains( name ) )
        		{
        			if(targetid == string.Empty)
        			{
        				targetid = player.userID.ToString();
        				targetname = player.displayName;
        			}
        			else
        			{
        				targetid = multipleNames;
        			}
        		}
        	}
        	if( targetid == multipleNames )
        		return false;
        	if( targetid != string.Empty )
        		return true;
        	targetid = noPlayerFound;
        	if(DeadPlayersList == null)
            	return false;
            Dictionary<string, string> deadPlayers = DeadPlayersList.Call("GetPlayerList", null) as Dictionary<string, string>;
            if(deadPlayers == null)
            	return false;
            
            foreach( KeyValuePair<string, string> pair in deadPlayers)
        	{
        		if( pair.Value == name )
        		{
        			targetid = pair.Key;
        			targetname = pair.Value;
        			return true;
        		}
        		if( pair.Value.Contains( name ) )
        		{
        			if(targetid == noPlayerFound)
        			{
        				targetid = pair.Key;
        				targetname = pair.Value;
        			}
        			else
        			{
        				targetid = multipleNames;
        			}
        		}
        	}
        	if( targetid == multipleNames )
        		return false;
            if(targetid != noPlayerFound)
            	return true;
            return false;
        }
        
        ////////////////////////////////////////////////////////////
    	// Chat Commands
    	////////////////////////////////////////////////////////////
        
        [ChatCommand("ac_logs")]
        void cmdChatACLogs(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            if(args == null || args.Length < 2)
            {
            	SendReply(player, "/ac_logs player PLAYERNAME/STEAMID => to show all the hack detections made by this player");
            	SendReply(player, "/ac_logs radius RADIUS => to show all hack detections in this radius.");
            	return;
            }
            if(args[0].ToLower() == "player")
            {
				string targetid = string.Empty;
				string targetname = string.Empty;
				if( !FindPlayerByName( args[1] , out targetid, out targetname ) )
				{
					SendReply(player, targetid);
					return;
				}
				if(anticheatlogs[targetid] == null || (anticheatlogs[targetid]).Count == 0)
				{
					SendReply(player, string.Format("{0} {1} - has no hack detections", targetid, targetname));
					return;
				}
				SendReply(player, string.Format("{0} {1} - has {2} hack detections", targetid, targetname, (anticheatlogs[targetid]).Count.ToString()));
            	// TO BE MADE
            }
            else if(args[0].ToLower() == "radius")
            {
            	// TO BE MADE
            
            }
            else
            {
            	SendReply(player,string.Format("This argument: \"{0}\" doesn't exist",args[0]));
            }
        }
        
        [ChatCommand("ac_reset")]
        void cmdChatACReset(BasePlayer player, string command, string[] args)
        {
            if(player.net.connection.authLevel < 2) { SendReply(player, "You dont have access to this command"); return; }
            anticheatlogs.Clear();
            storedData.AntiCheatLogs.Clear();
            SaveData();
            SendReply(player, "AntiCheat", "Logs were resetted");
        }
        ////////////////////////////////////////////////////////////
    	// Console Commands
    	////////////////////////////////////////////////////////////
    	
        [ConsoleCommand("ac.fps")]
        void cmdConsoleAcFPS(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, "You dont have access to this command");
                    return;
                }
            }
            SendReply(arg, "Checking the time the anticheat takes to check all your current players");
            fpsCheckCalled = true;
            fpsCaller = arg;
            fpsTime = 0f;
            fpsCalled.Clear();
            timer.Once(2f, () => SendFPSCount());
        }
        void SendFPSCount()
        {
            if(fpsCaller is ConsoleSystem.Arg)
            {
                SendReply((ConsoleSystem.Arg)fpsCaller, string.Format("Checking all players on your server took {0}s", fpsTime.ToString()));
            }
        }
    }
}
