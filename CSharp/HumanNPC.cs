// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("HumanNPC", "Reneb", "0.0.2")]
    class HumanNPC : RustPlugin
    {

        ////////////////////////////////////////////////////// 
        ///  Fields
        //////////////////////////////////////////////////////
        private static FieldInfo serverinput;
        private static FieldInfo viewangles;
        private static int playerLayer;
        private DamageTypeList emptyDamage;
        private List<Oxide.Plugins.Timer> TimersList;

        StoredData storedData;
        static Hash<string, Waypoint> waypoints = new Hash<string, Waypoint>();
        Hash<string, HumanNPCInfo> humannpcs = new Hash<string, HumanNPCInfo>();
        
        [PluginReference] Plugin Kits;

        ////////////////////////////////////////////////////// 
        ///  Cached Fields
        //////////////////////////////////////////////////////
        private Quaternion currentRot;
        
        private Vector3 closestHitpoint;
        private Vector3 eyesPosition;

        private RaycastHit hitinfo;

        private object closestEnt;

        ////////////////////////////////////////////////////// 
        ///  class WaypointInfo
        ///  Waypoint information, position & speed
        ///  public => will be saved in the data file
        ///  non public => won't be saved in the data file
        //////////////////////////////////////////////////////
        class WaypointInfo
		{
			public string x;
			public string y;
			public string z;
			public string s;
			Vector3 position;
			float speed;

			public WaypointInfo(Vector3 position, float speed)
			{
				x = position.x.ToString();
				y = position.y.ToString();
				z = position.z.ToString();
				s = speed.ToString();
			
				this.position = position;
				this.speed = speed;
			}

			public Vector3 GetPosition()
			{
				if (position == Vector3.zero)
					position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
				return position;
			}
			public float GetSpeed()
			{
 			    speed = Convert.ToSingle(s);
				return speed;
			}
		}

        ////////////////////////////////////////////////////// 
        ///  class SpawnInfo
        ///  Spawn information, position & rotation
        ///  public => will be saved in the data file
        ///  non public => won't be saved in the data file
        //////////////////////////////////////////////////////
        class SpawnInfo
		{
			public string x;
			public string y;
			public string z;
			public string rx;
            public string ry;
            public string rz;
            public string rw;
			Vector3 position;
            Quaternion rotation;

			public SpawnInfo(Vector3 position, Quaternion rotation)
			{
				x = position.x.ToString();
				y = position.y.ToString();
				z = position.z.ToString();
				
                rx = rotation.x.ToString();
				ry = rotation.y.ToString();
				rz = rotation.z.ToString();
			    rw = rotation.w.ToString();

				this.position = position;
				this.rotation = rotation;
			}

			public Vector3 GetPosition()
			{
				if (position == Vector3.zero)
					position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
				return position;
			}
			public Quaternion GetRotation()
			{
                if(rotation.x == 0f)
				    rotation = new Quaternion(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz), Convert.ToSingle(rw));
				return rotation;
			}
            public string String()
            {
                return string.Format("Pos({0},{1},{2}) - Rot({3},{4},{5},{6})",x,y,z,rx,ry,rz,rw);
            }
		}

        ////////////////////////////////////////////////////// 
        ///  class Waypoint
        ///  Waypoint List information
        //////////////////////////////////////////////////////
    	class Waypoint
        {
        	public string Name;
        	public List<WaypointInfo> Waypoints;
        	
        	public Waypoint()
        	{
        		Waypoints = new List<WaypointInfo>();
        	}
        	public void AddWaypoint(Vector3 position, float speed)
        	{
        		Waypoints.Add(new WaypointInfo( position, speed ));
        	}
        }

        ////////////////////////////////////////////////////// 
        ///  class HumanPlayer : MonoBehaviour
        ///  MonoBehaviour: managed by UnityEngine
        ///  makes it able to create it's own system so it can loop via Update()
        //////////////////////////////////////////////////////
        class HumanPlayer : MonoBehaviour
        {
            public HumanNPCInfo info;
            public BasePlayer player;

            public bool invulnerability;
            public float collisionRadius;

            // Cached Values for Waypoints
            private float secondsToTake;
            private float secondsTaken;
            private Vector3 EndPos;
            private Vector3 StartPos;
            private string lastWaypoint;
            private float waypointDone;
            private List<WaypointInfo> cachedWaypoints;
            private int currentWaypoint;

            // Cached Values for entity collisions
            Collider[] colliderArray;
            float lastTick;
            List<BasePlayer> deletePlayers;
            List<BasePlayer> collidePlayers;
            List<BasePlayer> addPlayers;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                deletePlayers = new List<BasePlayer>();
                collidePlayers = new List<BasePlayer>();
                addPlayers = new List<BasePlayer>();
            }
            public void SetInfo(HumanNPCInfo info)
            {
                this.info = info;
                InitPlayer();
            } 
            void InitPlayer()
            { 
                if(info == null) return;
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.userID = ulong.Parse( info.userid );
                player.displayName = info.displayName;
                invulnerability = bool.Parse( info.invulnerability );
                collisionRadius = float.Parse( info.collisionRadius );
                player.health = float.Parse( info.health );
                player.syncPosition = true;
                player.transform.position = info.spawnInfo.GetPosition();
                player.TransformChanged();
                SetViewAngle(player, info.spawnInfo.GetRotation());
                player.EndSleeping();
                player.UpdateNetworkGroup();
                lastTick = Time.realtimeSinceStartup;
                Interface.CallHook("OnNPCRespawn", player);
                enabled = true;
            }
            void FindNextWaypoint()
            {
                StartPos = player.transform.position;
                Interface.CallHook("OnNPCPosition", player, StartPos);
                cachedWaypoints = GetWayPoints(info.waypoint);
                if(lastWaypoint != info.waypoint || currentWaypoint >= (cachedWaypoints.Count-1))
                    currentWaypoint = -1;
                currentWaypoint++;
                EndPos = cachedWaypoints[currentWaypoint].GetPosition(); 
                secondsToTake = Vector3.Distance(EndPos, StartPos) / cachedWaypoints[currentWaypoint].GetSpeed();
                SetViewAngle(player, Quaternion.LookRotation( EndPos - StartPos ));
                secondsTaken = 0f;
                waypointDone = 0f; 
                lastWaypoint = info.waypoint;
            }
            void Move()
            {
                if(info.waypoint == "" || info.waypoint == null) return;
                if (secondsTaken == 0f) FindNextWaypoint();
                secondsTaken += Time.deltaTime;
                waypointDone = Mathf.InverseLerp(0f, secondsToTake, secondsTaken);
                player.transform.position = Vector3.Lerp(StartPos,EndPos, waypointDone);
                player.SendNetworkUpdate(BasePlayer.NetworkQueue.Positional);
                if (waypointDone >= 1f)
                    secondsTaken = 0f;
            }
            void LookUp()
            {
                if (Time.realtimeSinceStartup > (lastTick + 2))
                {
                    colliderArray = Physics.OverlapSphere(player.transform.position, collisionRadius, playerLayer);
                    foreach (BasePlayer targetplayer in collidePlayers)
                        deletePlayers.Add(targetplayer);
                    foreach (Collider collider in colliderArray)
                    {
                        if (collider.GetComponentInParent<BasePlayer>())
                        {
                            if (collidePlayers.Contains(collider.GetComponentInParent<BasePlayer>()))
                                deletePlayers.Remove(collider.GetComponentInParent<BasePlayer>());
                            else if (player != collider.GetComponentInParent<BasePlayer>())
                                OnEnterCollision(collider.GetComponentInParent<BasePlayer>());
                        }
                    }
                    foreach (BasePlayer targetplayer in deletePlayers)
                        OnLeaveCollision(targetplayer);
                    foreach (BasePlayer targetplayer in addPlayers)
                        collidePlayers.Add(targetplayer);
                    addPlayers.Clear();
                    deletePlayers.Clear();
                    lastTick = Time.realtimeSinceStartup;
                }
            }
            void OnEnterCollision(BasePlayer targetplayer)
            {
                addPlayers.Add(targetplayer);
                if(targetplayer != null)
                    Interface.CallHook("OnEnterNPC", player, targetplayer);
            }
            void OnLeaveCollision(BasePlayer targetplayer)
            {
                collidePlayers.Remove(targetplayer);
                if(targetplayer != null)
                    Interface.CallHook("OnLeaveNPC", player, targetplayer);
            } 
             void Update()
            {
                if(info == null) enabled = false;
                 LookUp();
                 Move();
            } 
        }

        ////////////////////////////////////////////////////// 
        ///  class HumanNPCInfo
        ///  NPC information that will be saved inside the datafile
        ///  public => will be saved in the data file
        ///  non public => won't be saved in the data file
        //////////////////////////////////////////////////////
        class HumanNPCInfo
        {
            public string userid;
            public string displayName;
            public string invulnerability;
            public string health;
            public string respawn;
            public string respawnSeconds;
            public SpawnInfo spawnInfo;
            public string waypoint;
            public string collisionRadius;
            public string spawnkit;
            public List<string> message_hello;
            public List<string> message_bye;
            public List<string> message_use;
            public List<string> message_hurt;
            public List<string> message_kill;

            public HumanNPCInfo(ulong userid, Vector3 position, Quaternion rotation)
			{
				this.userid = userid.ToString();
                displayName = "NPC";
                invulnerability = "true";
                health = "50";
                respawn = "true";
                respawnSeconds = "60";
                spawnInfo = new SpawnInfo(position, rotation);
                collisionRadius = "10";
			}
        }
    	
    	class WaypointEditor : MonoBehaviour
        {
            public Waypoint targetWaypoint;

            void Awake()
            {
            }
        }
    	class NPCEditor : MonoBehaviour
        {
            public BasePlayer player;
            public HumanPlayer targetNPC;
            void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
        }

    	class StoredData
        {
            public HashSet<Waypoint> WayPoints = new HashSet<Waypoint>();
            public HashSet<HumanNPCInfo> HumanNPCs = new HashSet<HumanNPCInfo>();

            public StoredData()
            {
            }
        }
    	
    	
    	
    	void Loaded()
        {
            LoadData();
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            viewangles = typeof(BasePlayer).GetField("viewAngles", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            TimersList = new List<Oxide.Plugins.Timer>();
            eyesPosition = new Vector3(0f,0.5f,0f);

        }
        
        void Unloaded()
        {
            SaveData();
        }
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(WaypointEditor));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            objects = GameObject.FindObjectsOfType(typeof(HumanPlayer));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            objects = GameObject.FindObjectsOfType(typeof(NPCEditor));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            foreach (Oxide.Plugins.Timer timers in TimersList)
                timers.Destroy();
            TimersList.Clear(); 
        } 
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("HumanNPC", storedData);
        }
		void LoadData()
        {
            waypoints.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("HumanNPC");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var thewaypoint in storedData.WayPoints)
                waypoints[thewaypoint.Name] = thewaypoint;
            foreach (var thenpc in storedData.HumanNPCs)
                humannpcs[thenpc.userid] = thenpc;
        }
        ////////////////////////////////////////////////////// 
        ///  Oxide Hooks
        //////////////////////////////////////////////////////

        ////////////////////////////////////////////////////// 
        ///  OnServerInitialized()
        ///  called when the server is done being initialized
        //////////////////////////////////////////////////////
        void OnServerInitialized()
        {
            playerLayer = -LayerMask.NameToLayer("Player (Server)");
            RefreshAllNPC();
            emptyDamage = new DamageTypeList();
        }

        ////////////////////////////////////////////////////// 
        ///  OnServerSave() 
        ///  called when a server performs a save
        //////////////////////////////////////////////////////
    	void OnServerSave()
        {
            SaveData();
        }

        ////////////////////////////////////////////////////// 
        /// OnPlayerInput(BasePlayer player, InputState input)
        /// Called when a plugin presses a button
        //////////////////////////////////////////////////////
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.USE))
            {
                TryGetPlayerView(player, out currentRot);
                if(Physics.Raycast( new Ray(player.transform.position + eyesPosition, currentRot*Vector3.forward) , out hitinfo, 5f, playerLayer))
                    if (hitinfo.collider.GetComponentInParent<HumanPlayer>() != null)
                        Interface.CallHook("OnUseNPC", hitinfo.collider.GetComponentInParent<BasePlayer>(), player);
            }
        }

        ////////////////////////////////////////////////////// 
        /// OnEntityAttacked(BaseCombatEntity entity, HitInfo hitinfo)
        /// Called when an entity gets attacked (can be anything, building, animal, player ..)
        //////////////////////////////////////////////////////
        void OnEntityAttacked(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity.GetComponent<HumanPlayer>() != null)
            {
                Interface.CallHook("OnHitNPC", entity.GetComponent<BasePlayer>(), hitinfo);
                if (entity.GetComponent<HumanPlayer>().invulnerability)
                {
                    hitinfo.damageTypes = emptyDamage;
                    hitinfo.HitMaterial = 0;
                }
            }
        }

        ////////////////////////////////////////////////////// 
        /// OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        /// Called when an entity gets killed (can be anything, building, animal, player ..)
        //////////////////////////////////////////////////////
        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (entity.GetComponent<HumanPlayer>() != null)
            {
                Interface.CallHook("OnKillNPC", entity.GetComponent<BasePlayer>(), hitinfo);
                if (entity.GetComponent<HumanPlayer>().info.respawn == "true")
                {
                    var userid = entity.GetComponent<HumanPlayer>().info.userid;
                    TimersList.Add( timer.Once(float.Parse(entity.GetComponent<HumanPlayer>().info.respawnSeconds), () => SpawnNPC(userid)) );
                }
            }
        }
        ////////////////////////////////////////////////////// 
        /// End of Oxide Hooks
        //////////////////////////////////////////////////////


        BasePlayer FindPlayerByID(ulong userid)
        {
            var allBasePlayer = UnityEngine.Resources.FindObjectsOfTypeAll<BasePlayer>();
            foreach (BasePlayer player in allBasePlayer)
            {
                if(player.userID == userid) return player;
            }
            return null;
        }
        static void SetViewAngle(BasePlayer player, Quaternion ViewAngles)
        {
            viewangles.SetValue(player, ViewAngles);
            player.SendNetworkUpdate(BasePlayer.NetworkQueue.Positional);
        }
        void RefreshAllNPC()
        {
            List<string> npcspawned = new List<string>();
            foreach (KeyValuePair<string, HumanNPCInfo> pair in humannpcs)
            {
                BasePlayer findplayer = FindPlayerByID(Convert.ToUInt64(pair.Key));
                npcspawned.Add(pair.Key);
                if(findplayer == null)
                    SpawnNPC(pair.Key);
                else
                    RefreshNPC(findplayer);
            }
            foreach(BasePlayer player in UnityEngine.Resources.FindObjectsOfTypeAll<BasePlayer>())
            {
                if (player.userID < 76560000000000000L && player.userID > 0L)
                {
                    if(!npcspawned.Contains(player.userID.ToString()))
                    {
                        player.KillMessage();
                        Puts(string.Format("Detected a HumanNPC with no data, deleting him: {0} {1}", player.userID.ToString(), player.displayName));
                    }
                }
            }

        }
        void SpawnNPC(string userid)
        {
            if(humannpcs[userid] == null) return;
            var newplayer = GameManager.server.CreateEntity("player/player", humannpcs[userid].spawnInfo.GetPosition(), humannpcs[userid].spawnInfo.GetRotation()).ToPlayer();
            newplayer.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
            newplayer.Spawn(true); 
            newplayer.userID = Convert.ToUInt64(userid);
            var humanplayer = newplayer.gameObject.AddComponent<HumanPlayer>();
            humanplayer.SetInfo( humannpcs[userid] );
            Puts("Spawned NPC: "+userid);
        }
        void RefreshNPC(BasePlayer player)
        {
            if(player.GetComponent<HumanPlayer>() != null) GameObject.Destroy(player.GetComponent<HumanPlayer>());
            var humanplayer = player.gameObject.AddComponent<HumanPlayer>();
            humanplayer.SetInfo( humannpcs[player.userID.ToString()] );
            Puts("Refreshed NPC: "+player.userID.ToString());
        }
    	bool hasAccess(BasePlayer player)
        {
			if(player.net.connection.authLevel < 1)
			{
				SendReply(player, "You don't have access to this command");
				return false;
			}
			return true;
        }
        bool isEditingWP(BasePlayer player, int ttype)
        {
        	if(player.GetComponent<WaypointEditor>() != null)
        	{
        		if(ttype == 0)
        			SendReply(player, string.Format("You are already editing {0}",player.GetComponent<WaypointEditor>().targetWaypoint.Name.ToString()));

        		return true;
        	}
        	else
        	{
        		if(ttype == 1)
        			SendReply(player, string.Format("You are not editing any waypoints, say /waypoints_new or /waypoints_edit NAME"));
        			
        		return false;
        	}
        }
        bool hasNoArguments(BasePlayer player, string[] args, int Number)
        {
        	if(args.Length < Number)
        	{
        		SendReply(player, "Not enough Arguments, say /waypoints_help for more informations");
        		return true;
        	}
        	return false;
        }
        bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            var input = serverinput.GetValue(player) as InputState;
            if (input == null)
                return false;
            if (input.current == null)
                return false;

            viewAngle = Quaternion.Euler(input.current.aimAngles);
            return true;
        }
        bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            UnityEngine.Ray ray = new UnityEngine.Ray(sourceEye, sourceDir * Vector3.forward);

            var hits = UnityEngine.Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<TriggerBase>() == null)
                {
                    if (hit.distance < closestdist)
                    {
                        closestdist = hit.distance;
                        closestEnt = hit.collider;
                        closestHitpoint = hit.point;
                    }
                }
            }
            if (closestEnt is bool)
                return false;
            return true;
        }
        string GetRandomMessage(List<string> messagelist)
        {
            return messagelist[GetRandom(0,messagelist.Count)];
        }
        int GetRandom(int min,int max)
        {
            return UnityEngine.Random.Range(min,max);
        }
        [ChatCommand("npc_add")]
        void cmdChatNPCAdd(BasePlayer player, string command, string[] args)
        {
            if (player.GetComponent<NPCEditor>() != null)
            {
                SendReply(player, "NPC Editor: Already editing an NPC, say /npc_end first");
                return;
            }
            if (!TryGetPlayerView(player, out currentRot))
            {
                return;
            }

            var newplayer = GameManager.server.CreateEntity("player/player", player.transform.position, currentRot).ToPlayer();
            newplayer.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
            newplayer.displayName = "NPC";
            newplayer.Spawn(true);
            var humannpcinfo = new HumanNPCInfo(newplayer.userID, player.transform.position, currentRot);
            var humanplayer = newplayer.gameObject.AddComponent<HumanPlayer>();
            humanplayer.SetInfo(humannpcinfo);

            var npceditor = player.gameObject.AddComponent<NPCEditor>();
            npceditor.targetNPC = humanplayer;
            
            if(humannpcs[newplayer.userID.ToString()] != null) storedData.HumanNPCs.Remove(humannpcs[newplayer.userID.ToString()]);
            humannpcs[newplayer.userID.ToString()] = humannpcinfo;
            storedData.HumanNPCs.Add(humannpcs[newplayer.userID.ToString()]);
            SaveData();
        }
         [ChatCommand("npc_edit")]
        void cmdChatNPCEdit(BasePlayer player, string command, string[] args)
        {
            if (player.GetComponent<NPCEditor>() != null)
            {
                SendReply(player, "NPC Editor: Already editing an NPC, say /npc_end first");
                return;
            }
            if (!TryGetPlayerView(player, out currentRot))
            {
                return;
            }
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint))
            {
                return;
            }
            
            if (((Collider)closestEnt).GetComponentInParent<HumanPlayer>() == null)
            {
                SendReply(player, "This is not an NPC");
                return;
            }
            var npceditor = player.gameObject.AddComponent<NPCEditor>();
            npceditor.targetNPC = ((Collider)closestEnt).GetComponentInParent<HumanPlayer>();
            SendReply(player, string.Format("NPC Editor: Start Editing {0} - {1}",npceditor.targetNPC.player.displayName,npceditor.targetNPC.player.userID.ToString()));
        }
         [ChatCommand("npc_reset")]
        void cmdChatNPCReset(BasePlayer player, string command, string[] args)
        {
            if (player.GetComponent<NPCEditor>() != null)
            {
                GameObject.Destroy(player.GetComponent<NPCEditor>());
            }
            humannpcs.Clear();
             storedData.HumanNPCs.Clear();
            SaveData();
            OnServerInitialized();
        }
         
        static List<WaypointInfo> GetWayPoints(string name) => waypoints[name]?.Waypoints;
        string GetNPCName(string userid) => humannpcs[userid]?.displayName;

        List<string> ListFromArgs(string[] args, int from)
        {
            var newlist = new List<string>();
            for(var i = from; i < args.Length ; i++)
            {
                newlist.Add(args[i]);
            }
            return newlist;
        }
        [ChatCommand("npc")]
        void cmdChatNPC(BasePlayer player, string command, string[] args)
        {
            if (player.GetComponent<NPCEditor>() == null)
            {
                SendReply(player, "NPC Editor: You need to be editing an NPC, say /npc_add or /npc_edit");
                return;
            }
            var npceditor = player.GetComponent<NPCEditor>();
            if (args.Length == 0)
            {
                SendReply(player, "/npc bye reset/\"TEXT\" \"TEXT2\" \"TEXT3\" => Dont forgot the \", this is what NPC with say when a player gets away, multiple texts are possible");
                SendReply(player, "/npc radius XXX => Radius of which the NPC will detect the player");
                SendReply(player, "/npc name \"THE NAME\" => To set a name to the NPC");
                SendReply(player, "/npc health XXX => To set the Health of the NPC");
                SendReply(player, "/npc hello reset/\"TEXT\" \"TEXT2\" \"TEXT3\" => Dont forgot the \", this what will be said when the player gets close to the NPC");
                SendReply(player, "/npc hurt reset/\"TEXT\" \"TEXT2\" \"TEXT3\" => Dont forgot the \", set a message to tell the player when he hurts the NPC");
                SendReply(player, "/npc invulnerable true/false => To set the NPC invulnerable or not");
                SendReply(player, "/npc kill reset/\"TEXT\" \"TEXT2\" \"TEXT3\" => Dont forgot the \", set a message to tell the player when he kills the NPC");
                SendReply(player, "/npc kit reset/\"KitName\" => To set the kit of this NPC, requires the Kit plugin");
                SendReply(player, "/npc respawn true/false XX => To set it to respawn on death after XX seconds, default is instant respawn");
                SendReply(player, "/npc spawn \"new\" => To set the new spawn location");
                SendReply(player, "/npc use reset/\"TEXT\" \"TEXT2\" \"TEXT3\" => Dont forgot the \", this what will be said when the player presses USE on the NPC");
                SendReply(player, "/npc waypoints reset/\"Waypoint list Name\" => To set waypoints of an NPC, /npc_help for more informations");
                return;
            }
            if (args[0] == "name")
            {
                if (args.Length == 1)
                {
                    SendReply(player,string.Format("This NPC name is: {0}",npceditor.targetNPC.info.displayName));
                    return;
                }
                npceditor.targetNPC.info.displayName = args[1];
            }
            else if (args[0] == "invulnerable")
            {
                if (args.Length == 1)
                {
                    SendReply(player, string.Format("This NPC invulnerability is set to: {0}", npceditor.targetNPC.info.invulnerability));
                    return;
                }
                if(args[1] == "true" || args[1] == "1")
                    npceditor.targetNPC.info.invulnerability = "true";
                else
                    npceditor.targetNPC.info.invulnerability = "false";
            } 
            else if (args[0] == "health")
            {
                if (args.Length == 1)
                {
                    SendReply(player, string.Format("This NPC Initial health is set to: {0}",  npceditor.targetNPC.info.health));
                    return;
                }
                npceditor.targetNPC.info.health = args[1];
            }
                else if (args[0] == "radius")
            {
                if (args.Length == 1)
                {
                    SendReply(player, string.Format("This NPC Collision radius is set to: {0}",  npceditor.targetNPC.info.collisionRadius));
                    return;
                }
                npceditor.targetNPC.info.collisionRadius = args[1];
            }
            else if (args[0] == "respawn")
            {
                if (args.Length < 2)
                {
                    SendReply(player, string.Format("This NPC Respawn is set to: {0} after {1} seconds", npceditor.targetNPC.info.respawn, npceditor.targetNPC.info.respawnSeconds));
                    return;
                }
                if(args[1] == "true" || args[1] == "1")
                    npceditor.targetNPC.info.respawn = "true";
                else
                    npceditor.targetNPC.info.respawn = "false";

                npceditor.targetNPC.info.respawnSeconds = "60";
                if (args.Length > 2)
                    npceditor.targetNPC.info.respawnSeconds = args[2];
            }
            else if (args[0] == "spawn") 
            {
                if (args.Length < 2)
                {
                    SendReply(player, string.Format("This NPC Spawn was set to: {0}", npceditor.targetNPC.info.spawnInfo.String()));
                    return;
                }
                TryGetPlayerView(player, out currentRot);
                var newSpawn = new SpawnInfo(player.transform.position, currentRot); 
                npceditor.targetNPC.info.spawnInfo = newSpawn;

                SendReply(player, string.Format("This NPC Spawn now is set to: {0}", newSpawn.String()));
            }
            else if (args[0] == "waypoints") 
            {
                if (args.Length < 2)
                {
                    if( npceditor.targetNPC.info.waypoint == null || npceditor.targetNPC.info.waypoint == "" )
                        SendReply(player, "No waypoints set for this NPC yet");
                    else
                        SendReply(player, string.Format("This NPC waypoints are: {0}",npceditor.targetNPC.info.waypoint));
                    return;
                }
                if(args[1] == "reset")
                {
                    npceditor.targetNPC.info.waypoint = "";
                }
                else if(waypoints[args[1]] == null)
                {
                    SendReply(player, "This waypoint doesn't exist");
                    return;
                }
                else
                {
                    npceditor.targetNPC.info.waypoint = args[1];
                }
            }
            else if (args[0] == "kit") 
            {
                if (args.Length < 2)
                {
                    if( npceditor.targetNPC.info.spawnkit == null || npceditor.targetNPC.info.spawnkit == "" )
                        SendReply(player, "No spawn kits set for this NPC yet");
                    else
                        SendReply(player, string.Format("This NPC spawn kit is: {0}",npceditor.targetNPC.info.spawnkit));
                    return;
                }
                npceditor.targetNPC.info.spawnkit = args[1];
            }
            else if (args[0] == "hello") 
            {
                if (args.Length < 2)
                {
                    if( npceditor.targetNPC.info.message_hello == null || (npceditor.targetNPC.info.message_hello.Count == 0) )
                        SendReply(player, "No hello message set yet");
                    //else
                        //SendReply(player, string.Format("This NPC will say hi: {0}",npceditor.targetNPC.info.message_hello));
                    return;
                }
                if(args[1] == "reset")
                {
                    npceditor.targetNPC.info.message_hello = new List<string>();
                }
                else
                {
                    npceditor.targetNPC.info.message_hello = ListFromArgs(args,1);
                }
            }
            else if (args[0] == "bye") 
            {
                if (args.Length < 2)
                {
                    if( npceditor.targetNPC.info.message_bye == null || npceditor.targetNPC.info.message_bye.Count == 0  )
                        SendReply(player, "No bye message set yet");
                    //else
                       // SendReply(player, string.Format("This NPC will say bye: {0}",npceditor.targetNPC.info.message_bye));
                    return;
                } 
                if(args[1] == "reset")
                {
                    npceditor.targetNPC.info.message_bye = new List<string>();
                }
                else
                {
                    npceditor.targetNPC.info.message_bye = ListFromArgs(args,1);
                }
            }
            else if (args[0] == "use") 
            {
                if (args.Length < 2)
                {
                    if( npceditor.targetNPC.info.message_use == null || npceditor.targetNPC.info.message_use.Count == 0 )
                        SendReply(player, "No bye message set yet");
                    //else
                       // SendReply(player, string.Format("This NPC will say bye: {0}",npceditor.targetNPC.info.message_use));
                    return;
                }
                if(args[1] == "reset")
                {
                    npceditor.targetNPC.info.message_use = new List<string>();
                }
                else
                {
                    npceditor.targetNPC.info.message_use = ListFromArgs(args,1);
                }
            }
            else if (args[0] == "hurt") 
            {
                if (args.Length < 2)
                {
                    if( npceditor.targetNPC.info.message_hurt == null || npceditor.targetNPC.info.message_hurt.Count == 0 )
                        SendReply(player, "No hurt message set yet");
                    //else
                        //SendReply(player, string.Format("This NPC will say ouch: {0}",npceditor.targetNPC.info.message_hurt));
                    return;
                }
                if(args[1] == "reset")
                {
                    npceditor.targetNPC.info.message_hurt = new List<string>();
                }
                else
                {
                    npceditor.targetNPC.info.message_hurt = ListFromArgs(args,1);
                }
            }
            else if (args[0] == "kill") 
            {
                if (args.Length < 2)
                {
                    if( npceditor.targetNPC.info.message_kill == null || npceditor.targetNPC.info.message_kill.Count == 0 )
                        SendReply(player, "No kill message set yet");
                    //else
                        //SendReply(player, string.Format("This NPC will say a death message: {0}",npceditor.targetNPC.info.message_kill));
                    return;
                }
                if(args[1] == "reset")
                {
                    npceditor.targetNPC.info.message_kill = new List<string>();
                }
                else
                {
                    npceditor.targetNPC.info.message_kill = ListFromArgs(args,1);
                }
            }
            else 
            {
                SendReply(player, "Wrong Argument, /npc for more informations");
                return;
            }
            SaveData();
            RefreshNPC(npceditor.targetNPC.player);
            if(args.Length > 1)
                SendReply(player, string.Format("NPC Editor: Set {0} to {1}", args[0], args[1]));
        }

        [ChatCommand("npc_end")]
        void cmdChatNPCEnd(BasePlayer player, string command, string[] args)
        { 
            if (player.GetComponent<NPCEditor>() == null)
            {
                SendReply(player, "NPC Editor: You are not editing any NPC");
                return;
            }
            GameObject.Destroy(player.GetComponent<NPCEditor>());
            SendReply(player, "NPC Editor: Ended");
        }
        [ChatCommand("npc_remove")]
        void cmdChatNPCRemove(BasePlayer player, string command, string[] args)
        {
            if (!TryGetPlayerView(player, out currentRot)) return;
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint)) return;
            if (((Collider)closestEnt).GetComponentInParent<HumanPlayer>() == null)
            {
                SendReply(player, "This is not an NPC");
                return;
            }
            var userid = ((Collider)closestEnt).GetComponentInParent<BasePlayer>().userID.ToString();
            storedData.HumanNPCs.Remove(humannpcs[userid]);
            humannpcs[userid] = null;
            RefreshAllNPC();
            SendReply(player, string.Format("NPC {0} Removed",userid));
        }


        ////////////////////////////////////////////////////// 
        // Waypoints manager
        ////////////////////////////////////////////////////// 
        
    	[ChatCommand("waypoints_new")]
        void cmdWaypointsNew(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(isEditingWP(player,0)) return;
            
            var newWaypoint = new Waypoint();
            if(newWaypoint == null)
            {
            	SendReply(player, "Waypoints: Something went wrong while making a new waypoint");
            	return;
            }
            var newWaypointEditor = player.gameObject.AddComponent<WaypointEditor>();
            newWaypointEditor.targetWaypoint = newWaypoint;
            SendReply(player, "Waypoints: New WaypointList created, you may now add waypoints.");
        }
        [ChatCommand("waypoints_add")]
        void cmdWaypointsAdd(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(!isEditingWP(player,1)) return;
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if(WaypointEditor.targetWaypoint == null)
            {
            	SendReply(player, "Waypoints: Something went wrong while getting your WaypointList");
            	return;
            }
            float speed = 3f;
            if(args.Length > 0) float.TryParse(args[0], out speed);
            WaypointEditor.targetWaypoint.AddWaypoint(player.transform.position,speed);
            
            SendReply(player, string.Format("Waypoint Added: {0} {1} {2} - Speed: {3}",player.transform.position.x.ToString(),player.transform.position.y.ToString(),player.transform.position.z.ToString(),speed.ToString()));
        }
        [ChatCommand("waypoints_list")]
        void cmdWaypointsList(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(waypoints.Count == 0)
            {
                SendReply(player, "No waypoints created yet");
                return;
            }
            SendReply(player, "==== Waypoints ====");
            foreach (KeyValuePair<string, Waypoint> pair in waypoints)
            {
                SendReply(player, pair.Key);
            }

        }
        [ChatCommand("waypoints_save")]
        void cmdWaypointsSave(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(!isEditingWP(player,1)) return;
            if(args.Length == 0)
            {
                SendReply(player, "Waypoints: /waypoints_save NAMEOFWAYPOINT");
                return;
            }
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if(WaypointEditor.targetWaypoint == null)
            {
            	SendReply(player, "Waypoints: Something went wrong while getting your WaypointList");
            	return;
            }
            
            WaypointEditor.targetWaypoint.Name = args[0];
            
            if(waypoints[args[0]] != null) storedData.WayPoints.Remove(waypoints[args[0]]);
            waypoints[args[0]] = WaypointEditor.targetWaypoint;
            storedData.WayPoints.Add(waypoints[args[0]]);
            SendReply(player, string.Format("Waypoints: New waypoint saved with: {0} with {1} waypoints stored",WaypointEditor.targetWaypoint.Name, WaypointEditor.targetWaypoint.Waypoints.Count.ToString()));
        	GameObject.Destroy(player.GetComponent<WaypointEditor>());
            SaveData();
        }
        [ChatCommand("waypoints_close")]
        void cmdWaypointsClose(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(!isEditingWP(player,1)) return;
            SendReply(player, "Waypoints: Closed without saving");
        	GameObject.Destroy(player.GetComponent<WaypointEditor>());
        }


        ////////////////////////////////////////////////////// 
        // NPC HOOKS:
        // will call ALL plugins
        ////////////////////////////////////////////////////// 

        ////////////////////////////////////////////////////// 
        /// OnHitNPC(BasePlayer npc, HitInfo hinfo)
        /// called when an NPC gets hit
        //////////////////////////////////////////////////////
        void OnHitNPC(BasePlayer npc, HitInfo hinfo)
        {
            if(npc.GetComponent<HumanPlayer>().info.message_hurt != null && npc.GetComponent<HumanPlayer>().info.message_hurt.Count != 0)
                if(hinfo.Initiator != null)
                    if(hinfo.Initiator.ToPlayer() != null)
                        hinfo.Initiator.ToPlayer().SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_hurt)), 1.0 });
        }


        ////////////////////////////////////////////////////// 
        ///  OnUseNPC(BasePlayer npc, BasePlayer player)
        ///  called when a player press USE while looking at the NPC (5m max)
        //////////////////////////////////////////////////////
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if(npc.GetComponent<HumanPlayer>().info.message_use != null && npc.GetComponent<HumanPlayer>().info.message_use.Count != 0)
                player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_use)), 1.0 });
        }

        ////////////////////////////////////////////////////// 
        ///  OnEnterNPC(BasePlayer npc, BasePlayer player)
        ///  called when a player gets close to an NPC (default is in 10m radius)
        //////////////////////////////////////////////////////
        void OnEnterNPC(BasePlayer npc, BasePlayer player)
        {
            if(npc.GetComponent<HumanPlayer>().info.message_hello != null && npc.GetComponent<HumanPlayer>().info.message_hello.Count != 0)
                player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_hello)), 1.0 });
        }

        ////////////////////////////////////////////////////// 
        ///  OnLeaveNPC(BasePlayer npc, BasePlayer player)
        ///  called when a player gets away from an NPC
        //////////////////////////////////////////////////////
        void OnLeaveNPC(BasePlayer npc, BasePlayer player)
        {
            if(npc.GetComponent<HumanPlayer>().info.message_bye != null && npc.GetComponent<HumanPlayer>().info.message_bye.Count != 0)
                player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_bye)), 1.0 });
        }

        ////////////////////////////////////////////////////// 
        ///  OnKillNPC(BasePlayer npc, HitInfo hinfo)
        ///  called when an NPC gets killed
        //////////////////////////////////////////////////////
        void OnKillNPC(BasePlayer npc, HitInfo hinfo)
        {
            if(npc.GetComponent<HumanPlayer>().info.message_kill != null && npc.GetComponent<HumanPlayer>().info.message_kill.Count != 0)
                if(hinfo.Initiator != null)
                    if(hinfo.Initiator.ToPlayer() != null)
                        hinfo.Initiator.ToPlayer().SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_kill)), 1.0 });
        }

        ////////////////////////////////////////////////////// 
        ///  OnNPCPosition(BasePlayer npc, Vector3 pos)
        ///  Called when an npc reachs a position
        //////////////////////////////////////////////////////
        void OnNPCPosition(BasePlayer npc, Vector3 pos)
        { 
            return;
        }

        ////////////////////////////////////////////////////// 
        ///  OnNPCRespawn(BasePlayer npc)
        ///  Called when an NPC respawns
        ///  here it will give an NPC a kit and set the first tool in the belt as the active weapon
        //////////////////////////////////////////////////////
        void OnNPCRespawn(BasePlayer npc)
        { 
            if(npc.GetComponent<HumanPlayer>().info.spawnkit != null && npc.GetComponent<HumanPlayer>().info.spawnkit != "")
            {
                npc.inventory.Strip();
                Kits.Call("GiveKit", npc, npc.GetComponent<HumanPlayer>().info.spawnkit);
                if(npc.inventory.containerBelt.GetSlot(0) != null)
                {
                    npc.svActiveItem = npc.inventory.containerBelt.GetSlot(0);
                    HeldEntity entity2 = npc.svActiveItem.GetHeldEntity() as HeldEntity;
                    entity2.SetHeld(true);
                }
                npc.SV_ClothingChanged();
                npc.inventory.ServerUpdate(0f);
            }
        }
    }
}
