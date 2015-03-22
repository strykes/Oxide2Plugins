// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Rust;

namespace Oxide.Plugins
{
    [Info("HumanNPC", "Reneb", "1.0")]
    class HumanNPC : RustPlugin
    {
    
    	/// VARIABLES
    	private static FieldInfo serverinput;
    	private static FieldInfo viewangles;
    	public static Core.Configuration.DynamicConfigFile NPCList;
    	private List<Oxide.Plugins.Timer> TimersList;
    	
    	/// CACHED VARIABLES
    	private object closestEnt;
        private Vector3 closestHitpoint;
        private Quaternion currentRot;
        private Quaternion viewAngle;
        private RaycastHit raycasthitinfo;
        private DamageTypeList emptyDamage;
        
		
		/// WAYPOINTS
		
		StoredData storedData;
        Hash<string, Waypoint> waypoints = new Hash<string, Waypoint>();
        bool dataChanged = false;
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
				if (Single.IsNaN(speed))
					speed = float.Parse(s);
				return speed;
			}
		}
    	class Waypoint
        {
        	public string Name;
        	public List<WaypointInfo> Waypoints;
        	
        	public Waypoint()
        	{
        	}
        	
        	public Waypoint(string name)
        	{
        		Name = name;
        		Waypoints = new List<WaypointInfo>();
        	}
        	public AddWaypoint(Vector3 position, float speed)
        	{
        		Waypoints.Add(new WaypointInfo( position, speed ));
        	}
        }
    	
    	class WaypointEditor
        {
            public Waypoint targetWaypoint;

            public WaypointEditor()
            {
            }
        }
    	
    	class StoredData
        {
            public HashSet<Waypoint> WayPoints = new HashSet<Waypoint>();

            public StoredData()
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

        class HumanPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public Vector3 spawnPosition;
            private Vector3 LastWaypoint;
            private Vector3 NextWaypoint;
            
            public Quaternion spawnRotation;
            public Quaternion tempQuaternion;
            
            public string onEnterMessage;
            public string onLeaveMessage;
            public Dictionary<string, object> npcData;
            public Vector3 tempVector3;
            
            public float initialHealth;
            public float walkSpeed;
            public float runSpeed;
            private float secondsToTake;
            private float secondsTaken;
            private float waypointDone;
            private float lastTick;
            
            public bool invulnerability;
            public bool useWaypoint;
            public bool respawn;
            public float respawnSeconds;
            
            public List<object> Waypoints;
            
            public int currentWaypoint;
            public int maxWaypoint;
            private int currentColliding;
            
            
            private List<BasePlayer> insideSpherePlayers;
            private List<BasePlayer> deletePlayers;
            private List<BasePlayer> addPlayers;
            
            

            void Awake()
            {
                player = GetComponent<BasePlayer>(); 
                if (npcData != null && npcData.ContainsKey("name"))
                    player.displayName = npcData["name"].ToString();
                else
                    player.displayName = "NPC";
                onEnterMessage = "Well hello there";
                onLeaveMessage = "See you soon";
                insideSpherePlayers = new List<BasePlayer>();
                addPlayers = new List<BasePlayer>();
                deletePlayers = new List<BasePlayer>();
            }
            public void InitPlayer()
            {

                if (npcData == null) GetNPCData(this);
                if (npcData == null)
                {
                    player.KillMessage();
                }
                if (!npcData.ContainsKey("spawnPosition")) return;
                var spawnposition = npcData["spawnPosition"] as Dictionary<string, object>;
                spawnPosition = new Vector3(Convert.ToSingle(spawnposition["x"]), Convert.ToSingle(spawnposition["y"]), Convert.ToSingle(spawnposition["z"]));
                spawnRotation = new Quaternion(0f, 0f, 0f, 0f);
                if (npcData.ContainsKey("spawnRotation"))
                {
                    var spawnrotation = npcData["spawnRotation"] as Dictionary<string, object>;
                    spawnRotation = new Quaternion(Convert.ToSingle(spawnrotation["x"]), Convert.ToSingle(spawnrotation["y"]), Convert.ToSingle(spawnrotation["z"]), Convert.ToSingle(spawnrotation["w"]));
                }
                if (npcData.ContainsKey("name"))
                {
                    player.displayName = npcData["name"].ToString();
                }
                invulnerability = false;
                if (npcData.ContainsKey("invulnerable"))
                {
                    invulnerability = Convert.ToBoolean(npcData["invulnerable"]);
                }
                initialHealth = player.StartMaxHealth();
                if (npcData.ContainsKey("health"))
                {
                    initialHealth = Convert.ToSingle(npcData["health"]);
                }
                respawn = true;
                respawnSeconds = 60f;
                if (npcData.ContainsKey("respawn"))
                {
                    respawn = Convert.ToBoolean(npcData["respawn"]);
                    if (respawn == true)
                    {
                        if (npcData.ContainsKey("respawnSeconds"))
                        {
                            respawnSeconds = Convert.ToSingle(npcData["respawnSeconds"]);
                        }
                    } 
                }
                useWaypoint = false;
                Waypoints = new List<object>();
                maxWaypoint = 0;
                if (npcData.ContainsKey("useWaypoint"))
                {
                    useWaypoint = Convert.ToBoolean(npcData["useWaypoint"]);
                    if (useWaypoint == true)
                    {
                        Waypoints = npcData["Waypoints"] as List<object>;
                        maxWaypoint = Waypoints.Count;
                    }
                }
                walkSpeed = 3f;
                if (npcData.ContainsKey("walkSpeed"))
                {
                    walkSpeed = Convert.ToSingle(npcData["walkSpeed"]);
                }
            }
            public void Spawn()
            {
                if (player == null) player = GetComponent<BasePlayer>();
                if (player.net == null) player.Spawn(true);
            }
            public void SpawnRespawn()
            {
                if (player == null) player = GetComponent<BasePlayer>();
                if (player.net == null) player.Spawn(true);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.ChangePlayerState(PlayerState.Type.Dead, false);
                player.metabolism.Reset();
                player.InitializeHealth(initialHealth, initialHealth);
                GiveDefaultNPCItems();
                player.ChangePlayerState(PlayerState.Type.Normal, false);

                player.transform.position = spawnPosition;
                SetViewAngle(player, spawnRotation);
                player.CancelInvoke("KillMessage");
                player.UpdateNetworkGroup();
                player.UpdatePlayerCollider(true, false);
                player.SendNetworkUpdate(BasePlayer.NetworkQueue.Positional);
            }
            public void GiveDefaultNPCItems()
            {
                player.inventory.Strip();
                var newitem = ItemManager.CreateByName("bow_hunting", 1);
                player.inventory.GiveItem(newitem, player.inventory.containerBelt);
                player.svActiveItem = newitem;
                HeldEntity entity2 = player.svActiveItem.GetHeldEntity() as HeldEntity;
                entity2.SetHeld(true);
                player.inventory.GiveItem(ItemManager.CreateByName("urban_boots", 1), player.inventory.containerWear);
                player.inventory.GiveItem(ItemManager.CreateByName("urban_jacket", 1), player.inventory.containerWear);
                player.inventory.GiveItem(ItemManager.CreateByName("urban_pants", 1), player.inventory.containerWear);
                player.SV_ClothingChanged();
                player.inventory.ServerUpdate(0f);
            }
            public BasePlayer GetPlayer()
            {
                return player;
            }
            bool FindNextWaypoint()
            {
                LastWaypoint = player.transform.position;
                if (currentWaypoint < 0) currentWaypoint = -1;
                if (currentWaypoint >= Waypoints.Count - 1) currentWaypoint = -1;
                maxWaypoint = Waypoints.Count;
                currentWaypoint++;
                var newwaypoint = Waypoints[currentWaypoint] as Dictionary<string,object>;
                NextWaypoint = new Vector3(Convert.ToSingle(newwaypoint["x"]), Convert.ToSingle(newwaypoint["y"]), Convert.ToSingle(newwaypoint["z"]));
                secondsToTake = Vector3.Distance(NextWaypoint, LastWaypoint)/walkSpeed;
                SetViewAngle(player, Quaternion.LookRotation( NextWaypoint - LastWaypoint ));
                secondsTaken = 0f;
                waypointDone = 0f; 
                return true;
            }
            void OnEnterCollision(BasePlayer colliderplayer)
            {
                addPlayers.Add(colliderplayer);
                colliderplayer.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", player.displayName, onEnterMessage), 1.0 });
            }
            void OnLeaveCollision(BasePlayer colliderplayer)
            {
                insideSpherePlayers.Remove(colliderplayer);
                colliderplayer.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", player.displayName, onLeaveMessage), 1.0 });
            }
            void Update()
            {
                if (useWaypoint && maxWaypoint > 1)
                {
                    if (currentWaypoint == null) currentWaypoint = -1; 
                    if (secondsTaken == 0) FindNextWaypoint();
                    secondsTaken += Time.deltaTime;
                    waypointDone = Mathf.InverseLerp(0f, secondsToTake, secondsTaken); 
                    player.transform.position = Vector3.Lerp(LastWaypoint,NextWaypoint, waypointDone);
                    player.SendNetworkUpdate(BasePlayer.NetworkQueue.Positional);
                    if (waypointDone >= 1f)
                        secondsTaken = 0;
                }
                if (Time.realtimeSinceStartup > (lastTick + 2))
                {
                    deletePlayers.Clear();
                    foreach (BasePlayer targetplayer in insideSpherePlayers)
                    {
                        deletePlayers.Add(targetplayer);
                    }
                    currentColliding = 0;
                    Collider[] colliderArray = Physics.OverlapSphere(player.transform.position, 5f);
                    foreach (Collider collider in colliderArray)
                    {
                        if (collider.GetComponentInParent<BasePlayer>())
                        {
                            if (insideSpherePlayers.Contains(collider.GetComponentInParent<BasePlayer>()))
                            {
                                Debug.Log("yes inside");
                                deletePlayers.Remove(collider.GetComponentInParent<BasePlayer>());
                            }
                            else if (player != collider.GetComponentInParent<BasePlayer>())
                            {
                                OnEnterCollision(collider.GetComponentInParent<BasePlayer>());
                            }
                        }
                    }
                    foreach (BasePlayer targetplayer in deletePlayers)
                    {
                        Debug.Log("leave");
                        OnLeaveCollision(targetplayer);
                    }
                    
                    foreach (BasePlayer targetplayer in addPlayers)
                    {
                        insideSpherePlayers.Add(targetplayer);
                        
                    }
                    addPlayers.Clear();
                    lastTick = Time.realtimeSinceStartup;
                }
            }
        }

        void Loaded()
        {
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            viewangles = typeof(BasePlayer).GetField("viewAngles", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            LoadWPData();
            LoadData();
            TimersList = new List<Oxide.Plugins.Timer>();
        }
        void OnServerSave()
        {
            SaveData();
        }
        void LoadData()
        {
            NPCList = Interface.GetMod().DataFileSystem.GetDatafile("HumanNPC"); 
        }
        void LoadWPData()
        {
            waypoints.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Waypoints");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var thewaypoint in storedData.Waypoint)
                waypoints[thewaypoint.Name] = thewaypoint;
        }
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("HumanNPC");
        }
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(HumanPlayer));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            var objects = GameObject.FindObjectsOfType(typeof(WaypointEditor));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            foreach (Oxide.Plugins.Timer timers in TimersList)
            {
                timers.Destroy();
            }
            TimersList.Clear(); 
        } 
        void OnServerInitialized()
        {
            emptyDamage = new DamageTypeList();
            var allBasePlayer = UnityEngine.Resources.FindObjectsOfTypeAll<BasePlayer>();
            HumanPlayer currentHumanPlayer;
            var SpawnHuman = new List<ulong>();
            foreach (BasePlayer player in allBasePlayer)
            {
                if(NPCList[player.userID.ToString()] != null)
                {
                    if (player.GetComponent<HumanPlayer>() == null)
                        currentHumanPlayer = player.gameObject.AddComponent<HumanPlayer>();
                    else
                        currentHumanPlayer = player.GetComponent<HumanPlayer>();

                    currentHumanPlayer.InitPlayer();
                    currentHumanPlayer.SpawnRespawn();
                    SpawnHuman.Add(player.userID);
                }
                else if (player.userID < 76560000000000000L && player.userID > 0L)
                {
                    Puts(string.Format("Detected a HumanNPC with no data, deleting him: {0} {1}", player.userID.ToString(), player.displayName));
                    player.KillMessage();
                } 
            }
            foreach (KeyValuePair<string, object> pair in NPCList)
            {
                if (!SpawnHuman.Contains(Convert.ToUInt64(pair.Key)))
                {
                    Puts("couldn't find");
                    var newplayer = GameManager.server.CreateEntity("player/player", new Vector3(0f,0f,0f), new Quaternion(0f,0f,0f,0f)).ToPlayer();
                    var humanplayer = newplayer.gameObject.AddComponent<HumanPlayer>();
                    humanplayer.Spawn();

                    newplayer.userID = Convert.ToUInt64(pair.Key);
                    humanplayer.InitPlayer();
                    humanplayer.SpawnRespawn();
                    SpawnHuman.Add(newplayer.userID);
                }
            }
        }
        static void GetNPCData(HumanPlayer humannpc)
        {
            humannpc.npcData = new Dictionary<string, object>();
            if (NPCList[humannpc.player.userID.ToString()] != null)
            {
                humannpc.npcData = NPCList[humannpc.player.userID.ToString()] as Dictionary<string, object>;
            }
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
        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (entity.GetComponent<HumanPlayer>() != null)
            {
                if (entity.GetComponent<HumanPlayer>().respawn)
                {
                    var userid = entity.GetComponent<HumanPlayer>().player.userID;
                    TimersList.Add( timer.Once(entity.GetComponent<HumanPlayer>().respawnSeconds, () => ForceRespawn(userid)) );
                }
            }
        }
        void OnEntityAttacked(BaseCombatEntity player, HitInfo hitinfo)
        {
            if (player.GetComponent<HumanPlayer>() != null)
            {
                if (player.GetComponent<HumanPlayer>().invulnerability)
                {
                    hitinfo.damageTypes = emptyDamage;
                    hitinfo.HitMaterial = 0;
                }
            }
        }
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.USE))
            {
                if(Physics.Raycast(player.eyes.Ray(), out raycasthitinfo, 5f))
                { 
                    if (hitinfo.collider.GetComponentInParent<HumanPlayer>() != null)
                    {
                        SendMessage(player, hitinfo.collider.GetComponentInParent<HumanPlayer>(), hitinfo.collider.GetComponentInParent<HumanPlayer>().player.userID.ToString());
                    }
                }
            } 
        }
        public static void SetViewAngle(BasePlayer player, Quaternion ViewAngles)
        {
            viewangles.SetValue(player, ViewAngles);
            player.SendNetworkUpdate(BasePlayer.NetworkQueue.Positional);
        }
        void SendMessage(BasePlayer player, HumanPlayer fromNPC, string message)
        {
            player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=orange>{0}:</color> {1}", fromNPC.player.displayName, message), 1.0 });
        }
        void ForceRespawn(ulong userid)
        {
            var target = BasePlayer.FindByID(userid);
            if (target == null)
            {
                target = GameManager.server.CreateEntity("player/player", new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 0f)).ToPlayer();
                target.gameObject.AddComponent<HumanPlayer>();
            }
            var humanplayer = target.GetComponent<HumanPlayer>();
            humanplayer.Spawn();
            target.userID = userid;
            humanplayer.InitPlayer();
            humanplayer.SpawnRespawn();
        }
        void RefreshNPC(HumanPlayer humanplayer)
        {
            humanplayer.Spawn();
            humanplayer.npcData = NPCList[humanplayer.player.userID.ToString()] as Dictionary<string, object>;
            humanplayer.InitPlayer();
            humanplayer.SpawnRespawn();
        }
        void EndNPCEditor(BasePlayer player)
        {
            if (player.GetComponent<NPCEditor>() == null)
                SendReply(player, "NPC Editor: already ended");
            else
            {
                GameObject.Destroy(player.GetComponent<NPCEditor>());
                SendReply(player, "NPC Editor: ended");
            }
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
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint))
            {
                return;
            }
            var newplayer = GameManager.server.CreateEntity("player/player", closestHitpoint, currentRot).ToPlayer();
            var humanplayer = newplayer.gameObject.AddComponent<HumanPlayer>();
            var npceditor = player.gameObject.AddComponent<NPCEditor>();
            npceditor.targetNPC = humanplayer;
            humanplayer.Spawn();

            NPCList[newplayer.userID.ToString()] = new Dictionary<string, object>();
            var addnpcdata = NPCList[newplayer.userID.ToString()] as Dictionary<string, object>;

            var addnpcspawnposition = new Dictionary<string,object>();
            addnpcspawnposition.Add("x",closestHitpoint.x);
            addnpcspawnposition.Add("y",closestHitpoint.y);
            addnpcspawnposition.Add("z",closestHitpoint.z);

            var addnpcspawnrotation = new Dictionary<string,object>();
            addnpcspawnrotation.Add("x",currentRot.x);
            addnpcspawnrotation.Add("y",currentRot.y);
            addnpcspawnrotation.Add("z",currentRot.z);
            addnpcspawnrotation.Add("w",currentRot.w);

            addnpcdata.Add("spawnPosition", addnpcspawnposition);
            addnpcdata.Add("spawnRotation", addnpcspawnrotation);
            addnpcdata.Add("name", "NPC");
            addnpcdata.Add("invulnerability", "false");
            addnpcdata.Add("respawn", "true");
            addnpcdata.Add("respawntime", "60");

            humanplayer.npcData = addnpcdata;
            SaveData();

            humanplayer.InitPlayer();

            humanplayer.SpawnRespawn();
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
                SendReply(player, "/npc invulnerable true/false => To set the NPC invulnerable or not");
                SendReply(player, "/npc respawn true/false XX => To set it to respawn on death after XX seconds, default is instant respawn");
                SendReply(player, "/npc health XXX => To set the Health of the NPC");
                SendReply(player, "/npc spawn => To set the new spawn location");
                SendReply(player, "/npc waypoint add/reset => To set waypoints for the NPC");
                SendReply(player, "/npc walkspeed XX => To set the speed of an NPC");
                SendReply(player, "/npc hello \"TEXT\" => Dont forgot the \", this what will be said to the players when they interract with the NPC");
                return;
            }
            var editnpcdata = NPCList[npceditor.targetNPC.player.userID.ToString()] as Dictionary<string, object>;
            if (args[0] == "name")
            {
                if (args.Length == 1)
                {
                    SendReply(player,string.Format("This NPC name is: {0}",npceditor.targetNPC.player.displayName.ToString()));
                    return;
                }
                if (!editnpcdata.ContainsKey("name"))
                    editnpcdata.Add("name", args[1]);
                else
                    editnpcdata["name"]= args[1];
                
            }
            else if (args[0] == "invulnerable")
            {
                if (args.Length == 1)
                {
                    SendReply(player, string.Format("This NPC invulnerability is set to: {0}", npceditor.targetNPC.invulnerability.ToString()));
                    return;
                }
                if (!editnpcdata.ContainsKey("invulnerable"))
                    editnpcdata.Add("invulnerable", args[1]);
                else
                    editnpcdata["invulnerable"] = args[1];
            } 
            else if (args[0] == "health")
            {
                if (args.Length == 1)
                {
                    SendReply(player, string.Format("This NPC Initial health is set to: {0}", npceditor.targetNPC.initialHealth.ToString()));
                    return;
                }
                if (!editnpcdata.ContainsKey("health"))
                    editnpcdata.Add("health", args[1]);
                else
                    editnpcdata["health"] = args[1];
                if (args[1] == "0")
                    editnpcdata.Remove("health");
            }
            else if (args[0] == "respawn")
            {
                if (args.Length < 2)
                {
                    SendReply(player, string.Format("This NPC Respawn is set to: {0} after {1} seconds", npceditor.targetNPC.respawn.ToString(), npceditor.targetNPC.respawnSeconds.ToString()));
                    return;
                }
                if (!editnpcdata.ContainsKey("respawn"))
                    editnpcdata.Add("respawn", args[1]);
                else
                    editnpcdata["respawn"] = args[1];

                if (!editnpcdata.ContainsKey("respawnSeconds"))
                    editnpcdata.Add("respawnSeconds", "60");
                else
                    editnpcdata["respawnSeconds"] = "60";
                if (args.Length > 2)
                {
                    editnpcdata["respawnSeconds"] = args[2];
                }
            }
            else if (args[0] == "walkspeed")
            {
                if (args.Length < 2)
                {
                    SendReply(player, string.Format("This NPC walkspeed is set to: {0} ", (npceditor.targetNPC.walkSpeed).ToString()));
                    return;
                }
                if (!editnpcdata.ContainsKey("walkSpeed"))
                    editnpcdata.Add("walkSpeed", args[1]);
                else
                    editnpcdata["walkSpeed"] = args[1];
            }
            else if (args[0] == "spawn") 
            {

                SendReply(player, string.Format("This NPC Spawn was set to: {0} {1} {2}", npceditor.targetNPC.spawnPosition.x.ToString(), npceditor.targetNPC.spawnPosition.y.ToString(), npceditor.targetNPC.spawnPosition.z.ToString()));
                var editnpcspawnposition = new Dictionary<string, object>();
                editnpcspawnposition.Add("x", player.transform.position.x);
                editnpcspawnposition.Add("y", player.transform.position.y);
                editnpcspawnposition.Add("z", player.transform.position.z);

                TryGetPlayerView(player, out currentRot);
                var editnpcspawnrotation = new Dictionary<string, object>();
                editnpcspawnrotation.Add("x", currentRot.x);
                editnpcspawnrotation.Add("y", currentRot.y);
                editnpcspawnrotation.Add("z", currentRot.z);
                editnpcspawnrotation.Add("w", currentRot.w);
                editnpcdata["spawnPosition"] = editnpcspawnposition;
                editnpcdata["spawnRotation"] = editnpcspawnrotation;
                SendReply(player, string.Format("This NPC Spawn now is set to: {0} {1} {2}", player.transform.position.x.ToString(), player.transform.position.y.ToString(), player.transform.position.z.ToString()));
            }
            else if (args[0] == "waypoint")
            {
                if (args.Length == 1)
                {
                    SendReply(player, string.Format("This NPC Weapons is set to: {0} and has {1} waypoints", npceditor.targetNPC.useWaypoint.ToString(), npceditor.targetNPC.Waypoints.Count.ToString()));
                    return;
                }
                if (args[1] == "reset")
                {
                    if (!editnpcdata.ContainsKey("useWaypoint"))
                        editnpcdata.Add("useWaypoint", "false");
                    else
                        editnpcdata["useWaypoint"] = "false";

                    if (!editnpcdata.ContainsKey("Waypoints"))
                        editnpcdata.Add("useWaypoint", new List<object>());
                    else
                        editnpcdata["Waypoints"] = new List<object>();
                }
                else if (args[1] == "add")
                {
                    if (!editnpcdata.ContainsKey("useWaypoint"))
                        editnpcdata.Add("useWaypoint", "true");
                    else
                        editnpcdata["useWaypoint"] = "true";
                    if (!editnpcdata.ContainsKey("Waypoints"))
                        editnpcdata.Add("Waypoints", new List<object>());

                    var newwaypointpos = new Dictionary<string, object>();
                    newwaypointpos.Add("x", player.transform.position.x);
                    newwaypointpos.Add("y", player.transform.position.y);
                    newwaypointpos.Add("z", player.transform.position.z);

                    ((List<object>)editnpcdata["Waypoints"]).Add(newwaypointpos);
                    SendReply(player, "Waypoint added");
                    return;
                }
                else
                {
                    SendReply(player, "Wrong arguments: /npc waypoint reset/add");
                    return;
                }
            }
            else
            {
                SendReply(player, "Wrong Argument, /npc for more informations");
                return;
            }
            SaveData();
            RefreshNPC(npceditor.targetNPC);
            if(args.Length > 1)
                SendReply(player, string.Format("NPC Editor: Set {0} to {1}", args[0], args[1]));
        }
        [ChatCommand("npc_reset")]
        void cmdChatNPCReset(BasePlayer player, string command, string[] args)
        { 
            NPCList.Clear();
            SaveData();
            SendReply(player, "All NPC were Removed");
            OnServerInitialized();
        }
        [ChatCommand("npc_end")]
        void cmdChatNPCEnd(BasePlayer player, string command, string[] args)
        {
            EndNPCEditor(player);
        }
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        
        bool isEditingWP(BasePlayer player, int ttype)
        {
        	if(player.GetComponent<WaypointEditor>())
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
        [ChatCommand("waypoints_new")]
        void cmdWaypointsNew(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(isEditing(player,0)) return;
            
            var newWaypoint = new Waypoint( args[0].ToString() );
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
            if(!isEditing(player,1)) return;
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
        [ChatCommand("waypoints_save")]
        void cmdWaypointsSave(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(!isEditing(player,1)) return;
            if(hasEnoughArguments(player,args,1)) return;
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if(WaypointEditor.targetWaypoint == null)
            {
            	SendReply(player, "Waypoints: Something went wrong while getting your WaypointList");
            	return;
            }
            
            WaypointEditor.targetWaypoint.Name = args[0];
            
            if(waypoints[args[0]] != null) storedData.Waypoint.Remove(waypoints[args[0]]);
            waypoints[args[0]] = WaypointEditor.targetWaypoint;
            storedData.Waypoint.Add(waypoints[args[0]]);
            SendReply(player, string.Format("Waypoints: New waypoint saved with: {0} with {1} waypoints stored",WaypointEditor.targetWaypoint.Name, WaypointEditor.targetWaypoint.Waypoints.Count.ToString()));
        	GameObject.Destroy(player.GetComponent<WaypointEditor>());
        }
    }
}
