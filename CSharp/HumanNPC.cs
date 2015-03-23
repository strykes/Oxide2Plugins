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
    [Info("HumanNPC", "Reneb", "1.0")]
    class HumanNPC : RustPlugin
    {

        private static FieldInfo serverinput;
        private static FieldInfo viewangles;
        private DamageTypeList emptyDamage;
        private List<Oxide.Plugins.Timer> TimersList;
        StoredData storedData;
        static Hash<string, Waypoint> waypoints = new Hash<string, Waypoint>();
        Hash<string, HumanNPCInfo> humannpcs = new Hash<string, HumanNPCInfo>();
        [PluginReference] Plugin Kits;
         
        // Cached
        private Quaternion currentRot;
        private object closestEnt;
        private Vector3 closestHitpoint;
        private RaycastHit hitinfo;

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
                enabled = false;
                player = GetComponent<BasePlayer>();
                deletePlayers = new List<BasePlayer>();
                collidePlayers = new List<BasePlayer>();
                addPlayers = new List<BasePlayer>();
            }
            public void SetInfo(HumanNPCInfo info)
            {
                this.info = info;
                enabled = true;
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
                    
                    colliderArray = Physics.OverlapSphere(player.transform.position, collisionRadius);
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
                
                Move();
                LookUp();
            } 
        }
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
        }
        void OnServerInitialized()
        {
            RefreshAllNPC();
            emptyDamage = new DamageTypeList();
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
    	void OnServerSave()
        {
            SaveData();
        }
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.USE))
                if(Physics.Raycast(player.eyes.Ray(), out hitinfo, 5f))
                    if (hitinfo.collider.GetComponentInParent<HumanPlayer>() != null)
                        Interface.CallHook("OnUseNPC", hitinfo.collider.GetComponentInParent<BasePlayer>(), player);
        }
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
            player.gameObject.AddComponent<HumanPlayer>();
            player.GetComponent<HumanPlayer>().SetInfo( humannpcs[player.userID.ToString()] );
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
                SendReply(player, "/npc name \"THE NAME\" => To set a name to the NPC");
                SendReply(player, "/npc health XXX => To set the Health of the NPC");
                SendReply(player, "/npc hello \"TEXT\" => Dont forgot the \", this what will be said to the players when they interract with the NPC");
                SendReply(player, "/npc invulnerable true/false => To set the NPC invulnerable or not");
                SendReply(player, "/npc respawn true/false XX => To set it to respawn on death after XX seconds, default is instant respawn");
                SendReply(player, "/npc spawn \"new\" => To set the new spawn location");
                SendReply(player, "/npc waypoint set/reset \"Waypoint list Name\" => To set waypoints of an NPC, /npc_help for more informations");

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
                if(waypoints[args[1]] == null)
                {
                    SendReply(player, "This waypoint doesn't exist");
                    return;
                }
                npceditor.targetNPC.info.waypoint = args[1];
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










        // NPC HOOKS:
        void OnHitNPC(BasePlayer npc, HitInfo hinfo)
        {
            if(hinfo.Initiator != null)
                if(hinfo.Initiator.ToPlayer() != null)
                    hinfo.Initiator.ToPlayer().SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, "Ouch that hurt!"), 1.0 });
        }
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, "How can i help you?"), 1.0 });
        }
        void OnEnterNPC(BasePlayer npc, BasePlayer player)
        {
            player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, "Hi"), 1.0 });
        }
        void OnLeaveNPC(BasePlayer npc, BasePlayer player)
        {
            player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, "Bye"), 1.0 });
        }
        void OnKillNPC(BasePlayer npc, HitInfo hinfo)
        {
            if(hinfo.Initiator != null)
                if(hinfo.Initiator.ToPlayer() != null)
                    hinfo.Initiator.ToPlayer().SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.displayName, "You murderer!"), 1.0 });
        }
        void OnNPCPosition(BasePlayer npc, Vector3 pos)
        { 
            return;
        }
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
