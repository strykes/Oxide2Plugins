using System.Collections.Generic;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("AntiCheat", "Reneb & 4Seti", "2.2.16", ResourceId = 730)]
    class AntiCheat : RustPlugin
    {
        ////////////////////////////////////////////////////////////
        // Cached Fields
        ////////////////////////////////////////////////////////////

        static RaycastHit cachedRaycasthit;
        static DamageTypeList emptyDamage = new DamageTypeList();

        ////////////////////////////////////////////////////////////
        // Fields
        ////////////////////////////////////////////////////////////

        [PluginReference]
        Plugin EnhancedBanSystem;

        [PluginReference]
        Plugin DeadPlayersList;
         
        static Vector3 VectorDown = new Vector3(0f, -1f, 0f);
        static int constructionColl;
        static int bulletmask;
		static int flyColl;
		
        float lastTime;
        bool serverInitialized = false;

        Oxide.Plugins.Timer activateTimer;

        GameObject originalWallhack;
        
        List<GameObject> ListGameObjects = new List<GameObject>();
        static List<BasePlayer> adminList = new List<BasePlayer>();
        Hash<TriggerBase, Hash<BaseEntity, Vector3>> TriggerData = new Hash<TriggerBase, Hash<BaseEntity, Vector3>>();
        Hash<TriggerBase, BuildingBlock> TriggerToBlock = new Hash<TriggerBase, BuildingBlock>();
        Hash<BaseEntity, float> lastDetections = new Hash<BaseEntity, float>();
		Hash<ulong, int> whDetectCount = new Hash<ulong, int>();

		Hash<ulong, float> whDetectReset = new Hash<ulong, float>();

		Dictionary<uint, float> DoorCheck = new Dictionary<uint, float>();

        Hash<BasePlayer, float> lastWallhack = new Hash<BasePlayer, float>();

        ////////////////////////////////////////////////////////////
        // Config Fields
        ////////////////////////////////////////////////////////////

        static int authIgnore = 1;
        static int fpsIgnore = 30;
        static bool banFamilyShare = true;
        static bool shouldBan = true;
		static float resetTime = 60f;

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
        static bool wallhackDoors = true;
        static bool wallhackFloors = true;
        static bool wallhackWalls = true;
        static bool wallhackPunish = true;
        static bool wallhackLog = true;
        static int wallhackDetections = 2;


        static bool wallhackkills = true;
        static bool wallhackkillsLog = true;

        static bool meleeoverrangehack = true;

        static bool meleespeedhack = true;

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
            CheckCfg<bool>("Settings: Punish - true = Ban, false = Kick", ref shouldBan);
            CheckCfg<int>("Settings: FPS Ignore", ref fpsIgnore);
            CheckCfg<bool>("Settings: Ban Also Family Owner", ref banFamilyShare);
            CheckCfg<bool>("MeleeSpeed Hack: activated", ref meleespeedhack);
            CheckCfg<bool>("MeleeOverRange Hack: activated", ref meleeoverrangehack);
            CheckCfg<bool>("SpeedHack: activated", ref speedhack);
            CheckCfg<bool>("SpeedHack: Punish", ref speedhackPunish);
            CheckCfg<bool>("SpeedHack: Log", ref speedhackLog);
            CheckCfg<int>("SpeedHack: Punish Detections", ref speedhackDetections);
            CheckCfgFloat("SpeedHack: Speed Detection", ref minSpeedPerSecond);
            CheckCfg<bool>("Flyhack: activated", ref flyhack);
            CheckCfg<bool>("Flyhack: Punish", ref flyhackPunish);
            CheckCfg<bool>("Flyhack: Log", ref flyhackLog);
            CheckCfg<int>("Flyhack: Punish Detections", ref flyhackDetections);
            CheckCfg<bool>("Wallhack: activated", ref wallhack);
            CheckCfg<bool>("Wallhack: Protect - Walls", ref wallhackWalls);
            CheckCfg<bool>("Wallhack: Protect - Doors", ref wallhackDoors);
            CheckCfg<bool>("Wallhack: Protect - Floors", ref wallhackFloors);
            CheckCfg<bool>("Wallhack: Punish", ref wallhackPunish);
            CheckCfg<bool>("Wallhack: Log", ref wallhackLog);
            CheckCfg<int>("Wallhack: Punish Detections", ref wallhackDetections);

            CheckCfg<bool>("Wallhack Kills: activated", ref wallhackkills);
            CheckCfg<bool>("Wallhack Kills: Log", ref wallhackkillsLog);
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
                if (anticheatlogs[thelog.userid] == null)
                    anticheatlogs[thelog.userid] = new List<AntiCheatLog>();
                (anticheatlogs[thelog.userid]).Add(thelog);
            }
        }

        public class AntiCheatLog
        {
            public string userid;
            public string fx;
            public string fy;
            public string fz;
            public string tx;
            public string ty;
            public string tz;
            public string td;
            public string lg;

            Vector3 frompos;
            Vector3 topos;
			
			public AntiCheatLog()
            {
            }
			
            public AntiCheatLog(string userid, string logType, Vector3 frompos, Vector3 topos)
            {
                this.userid = userid;
                this.fx = frompos.x.ToString();
                this.fy = frompos.y.ToString();
                this.fz = frompos.z.ToString();
                this.tx = topos.x.ToString();
                this.ty = topos.y.ToString();
                this.tz = topos.z.ToString();
                this.td = logType;
                // GET TIME HERE
            }

            public Vector3 FromPos()
            {
                if (frompos == default(Vector3))
                    frompos = new Vector3(float.Parse(fx), float.Parse(fy), float.Parse(fz));
                return frompos;
            }

            public Vector3 ToPos()
            {
                if (topos == default(Vector3))
                    topos = new Vector3(float.Parse(tx), float.Parse(ty), float.Parse(tz));
                return topos;
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

        ////////////////////////////////////////////////////////////
        // Plugin Initialization
        ////////////////////////////////////////////////////////////

        void Loaded()
        {
            constructionColl = LayerMask.GetMask(new string[] { "Construction" });
            flyColl = LayerMask.GetMask(new string[] { "Construction", "Deployed", "Tree", "Terrain", "Resource", "World" });
            if (!permission.PermissionExists("cananticheat")) permission.RegisterPermission("cananticheat", this);
        }

        void OnServerInitialized()
        {
            serverInitialized = true;
            bulletmask = CollisionSettings.BulletAttack().value;
            originalWallhack = new UnityEngine.GameObject("Anti Wallhack");
            originalWallhack.AddComponent<MeshCollider>();
            originalWallhack.AddComponent<TriggerBase>();
            originalWallhack.gameObject.layer = UnityEngine.LayerMask.NameToLayer("Trigger");
            UnityEngine.LayerMask newlayermask = new UnityEngine.LayerMask();
            newlayermask.value = 133120;
            originalWallhack.GetComponent<TriggerBase>().interestLayers = newlayermask;
            
            timer.Once(0.5f, () => RefreshAllWalls());
            LoadData();
            timer.Once(1f, () => RefreshPlayers());
        }
        void RefreshPlayers()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.GetComponent<PlayerHack>() != null) GameObject.Destroy(player.GetComponent<PlayerHack>());
                if (player.net.connection.authLevel > 0 || permission.UserHasPermission(player.userID.ToString(), "cananticheat"))
                {
                    if (!adminList.Contains(player))
                        adminList.Add(player);
                    continue;
                }
                if (!speedhack && !flyhack) continue;
                player.gameObject.AddComponent<PlayerHack>();
            }
        }
        void Unload()
        {
            foreach (GameObject gameObj in ListGameObjects)
            {
                GameObject.Destroy(gameObj);
            }
            var objects = GameObject.FindObjectsOfType(typeof(PlayerHack));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            objects = GameObject.FindObjectsOfType(typeof(PlayerLog));
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

                if (!player.IsWounded() && !player.IsDead() && !player.IsSleeping() && Performance.frameRate > fpsIgnore)
                    CheckForHacks(this);

                lastPosition = player.transform.position;

                if (fpsCheckCalled)
                    if (!fpsCalled.Contains(this))
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

        void OnPlayerRespawned(BasePlayer player)
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
            var currenttime = Time.realtimeSinceStartup;
            foreach (BuildingBlock block in UnityEngine.Resources.FindObjectsOfTypeAll<BuildingBlock>())
            {
                if (block.blockDefinition != null)
                {
                    if ( wallhackWalls && block.blockDefinition.hierachyName == "wall")
                    { 
                        CreateNewProtectionFromBlock(block, true);
                    }
                    else if ( wallhackDoors && block.blockDefinition.hierachyName == "door.hinged")
                    {
                        CreateNewProtectionFromBlock(block, true);
                        if(block.net != null && block.net.ID is uint)
                            if(!DoorCheck.ContainsKey(block.net.ID))
                                DoorCheck.Add(block.net.ID, Time.realtimeSinceStartup);
                    }
                    else if (wallhackFloors && (block.blockDefinition.hierachyName == "floor" || block.blockDefinition.hierachyName == "floor.triangle"))
                    {
                        CreateNewProtectionFromBlock(block, true);
                    }
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
                    if (buildingblock.blockDefinition.hierachyName.Contains("floor"))
                    {
                    // SHOULD REDO THIS PART AS IT4S HORRIBLE BUT IT WILL FIX THE FALSE DETECTIONS
                        if ( buildingblock.transform.position.y - initialPos.y < 2f)
                        {
                            return false;
                        }
                    }
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
                TriggerData[triggerbase] = new Hash<BaseEntity, Vector3>();
            (TriggerData[triggerbase])[entity] = entity.transform.position;
        }

        void OnEntityLeave(TriggerBase triggerbase, BaseEntity entity)
        {
            if (entity == null || triggerbase == null || triggerbase.gameObject.name != "Anti Wallhack(Clone)") return;
            if (entity.GetComponent<BasePlayer>() == null) return;
            if (TriggerToBlock[triggerbase] == null)
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
                                if (wallhackLog)
                                    AddLog(player.userID.ToString(), "wall", (TriggerData[triggerbase])[entity], entity.transform.position);
                                SendMsgAdmin(string.Format("{0} was detected wallhacking from {1} to {2}", player.displayName, (TriggerData[triggerbase])[entity].ToString(), entity.transform.position.ToString()));
                                PrintWarning(string.Format("{0}[{3}] was detected wallhacking from {1} to {2}", player.displayName, (TriggerData[triggerbase])[entity].ToString(), entity.transform.position.ToString(), player.userID));

								if (wallhackPunish)
									if (whDetectCount[player.userID] >= wallhackDetections)
									{
										Punish(player, string.Format("rWallhack ({0})", TriggerToBlock[triggerbase].blockDefinition.hierachyName.ToString()));
										return;
									}
									else
									{
										if (Time.realtimeSinceStartup < whDetectReset[player.userID])
										{
											whDetectCount[player.userID]++;
											whDetectReset[player.userID] = Time.realtimeSinceStartup + resetTime;
										}
										else
										{
											whDetectCount[player.userID] = 1;
											whDetectReset[player.userID] = Time.realtimeSinceStartup + resetTime;
										}
                                    }
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

        ////////////////////////////////////////////////////////////
        // Anti Wallhack kills related
        ////////////////////////////////////////////////////////////

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
            if (hack.lastTickSpeed == hack.lastTick)
            {
                hack.speedHackDetections++;
                if (speedhackLog)
                    AddLog(hack.player.userID.ToString(), "speed", hack.lastPosition, hack.player.transform.position);
                SendDetection(string.Format("{0} - {1} is being detected with: Speedhack ({2}m/s)", hack.player.userID.ToString(), hack.player.displayName, hack.Distance3D.ToString()));
                if (hack.speedHackDetections >= speedhackDetections)
                {
                    if (speedhackPunish)
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
            foreach(Collider col in UnityEngine.Physics.OverlapSphere(hack.player.transform.position, 3f, flyColl))
            {
            	return;
            }
            if (hack.lastTickFly == hack.lastTick)
            {
                hack.flyHackDetections++;
                if (flyhackLog)
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

        void WallhackKillCheck(BasePlayer player, BasePlayer attacker, HitInfo hitInfo)
        {
            if (Physics.Linecast(attacker.eyes.position, hitInfo.HitPositionWorld, out cachedRaycasthit, bulletmask))
            {
                if (cachedRaycasthit.collider.GetComponentInParent<BuildingBlock>() != null)
                {
                    CancelDamage(hitInfo);
                    if (Time.realtimeSinceStartup - lastWallhack[attacker] > 0.5f)
                    {
                        lastWallhack[attacker] = Time.realtimeSinceStartup;
                        SendDetection(string.Format("{0} - {1} is being detected killing {2} through a wall", attacker.userID.ToString(), attacker.displayName, player.displayName));

                        if (wallhackkillsLog)
                            AddLog(attacker.userID.ToString(), "wallkill", attacker.eyes.position, hitInfo.HitPositionWorld);
                    }
                }
            }
        }


        void OnBasePlayerAttacked(BasePlayer player, HitInfo hitInfo)
        {
            if (!wallhackkills) return;
            if (player.IsDead()) return;
            if (hitInfo.Initiator == null) return;
            if (player.health - hitInfo.damageTypes.Total() > 0f) return;
            BasePlayer attacker = hitInfo.Initiator.ToPlayer();
            if (attacker == null) return;
            if (attacker == player) return;
            WallhackKillCheck(player, attacker, hitInfo);
        }
        void CancelDamage(HitInfo hitinfo)
        {
            hitinfo.damageTypes = emptyDamage;
            hitinfo.HitEntity = null;
        }

        ////////////////////////////////////////////////////////////
        // Anti OverKill related
        ////////////////////////////////////////////////////////////

        public Hash<BasePlayer, float> lastAttack = new Hash<BasePlayer, float>();
        public Hash<BasePlayer, int> attackSpeedDetections = new Hash<BasePlayer, int>();

        void OnMeleeAttack(BaseMelee melee, HitInfo info)
        {
            var thetime = Time.realtimeSinceStartup;
            BasePlayer attacker = melee.GetParentEntity() as BasePlayer;
            if (attacker == null) return;

            if (meleespeedhack)
            {
                if ((Time.realtimeSinceStartup - lastAttack[attacker]) < melee.repeatDelay - 0.2f)
                {
                    if (Performance.frameRate > fpsIgnore)
                    {
                        CancelDamage(info);
                        attackSpeedDetections[attacker]++;
                        if(attackSpeedDetections[attacker] > 1)
                            SendDetection(string.Format("{0} - {1} was detected hiting too fast - @ {2}", attacker.userID.ToString(), attacker.displayName.ToString(), attacker.transform.position.ToString()));
                    }
                }
                else
                    attackSpeedDetections[attacker] = 0;

                lastAttack[attacker] = Time.realtimeSinceStartup;
            }
            if (meleeoverrangehack)
            {
                if (info.HitEntity == null) return;
                BasePlayer victim = info.HitEntity as BasePlayer;
                if (victim == null) return;
                if (victim == attacker) return;
                if (Vector3.Distance(attacker.eyes.position, info.HitPositionWorld) > melee.maxDistance + 2f)
                {
                    if (Vector3.Distance(attacker.transform.position, victim.transform.position) > melee.maxDistance)
                    {
                        CancelDamage(info);
                        if (Performance.frameRate > fpsIgnore)
                            SendDetection(string.Format("{0} - {1} was detected attacking {2} with {6} from {3}m - {4} to {5}", attacker.userID.ToString(), attacker.displayName.ToString(), victim.displayName, Vector3.Distance(attacker.transform.position, victim.transform.position).ToString(), attacker.transform.position.ToString(), victim.transform.position.ToString(), info.Weapon.LookupShortPrefabName().ToString()));
                    }
                }
            }
        }

        ////////////////////////////////////////////////////////////
        // Admin Chat related
        ////////////////////////////////////////////////////////////

        static void SendDetection(string msg)
        {
            foreach (BasePlayer player in adminList)
            {
                if (player != null && player.net != null)
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
            if (player.net.connection.authLevel > 0 || permission.UserHasPermission(player.userID.ToString(), "cananticheat"))
            {
                if (!adminList.Contains(player))
                    adminList.Add(player);
            }
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (adminList.Contains(player))
                adminList.Remove(player);
        }

        ////////////////////////////////////////////////////////////
        // Punish a player
        ////////////////////////////////////////////////////////////

        void Ban(object source, BasePlayer target, string msg, bool theboolean)
        {
            if (EnhancedBanSystem != null) return;
            ServerUsers.Set(target.userID, ServerUsers.UserGroup.Banned, target.displayName, msg);
            ServerUsers.Save();

			//ConsoleSystem.Broadcast("chat.add", new object[] { 0, "<color=orange>AntiCheat:</color> " + msg });

			PrintWarning(string.Format("{0}[{1}] banned by AntiCheat", target.displayName, target.userID));
            Network.Net.sv.Kick(target.net.connection, "Banned for hacking");
        } 

        static void Punish(BasePlayer player, string msg)
        {
            if (player.net.connection.authLevel < authIgnore)
            {
                if (shouldBan)
                {
                    if (banFamilyShare)
                        if (player.net.connection.ownerid != player.userID)
                        {
                            ServerUsers.Set(player.net.connection.ownerid, ServerUsers.UserGroup.Banned, player.displayName, msg);
                            ServerUsers.Save();
                        }
                    Interface.GetMod().CallHook("Ban", null, player, msg, false);
                }
                else
                {
                    player.Kick(msg);
                }
            }
            else
            {
                GameObject.Destroy(player.GetComponent<PlayerHack>());
            }
        }

        bool hasAccess(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission(player.userID.ToString(), "cananticheat");
        }

        bool FindPlayerByName(string name, out string targetid, out string targetname)
        {
            ulong userid;
            targetid = string.Empty;
            targetname = string.Empty;
            if (name.Length == 17 && ulong.TryParse(name, out userid))
            {
                targetid = name;
                return true;
            }

            foreach (BasePlayer player in UnityEngine.Object.FindObjectsOfTypeAll(typeof(BasePlayer)))
            {
                if (player.displayName == name)
                {
                    targetid = player.userID.ToString();
                    targetname = player.displayName;
                    return true;
                }
                if (player.displayName.Contains(name))
                {
                    if (targetid == string.Empty)
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
            if (targetid == multipleNames)
                return false;
            if (targetid != string.Empty)
                return true;
            targetid = noPlayerFound;
            if (DeadPlayersList == null)
                return false;
            Dictionary<string, string> deadPlayers = DeadPlayersList.Call("GetPlayerList", null) as Dictionary<string, string>;
            if (deadPlayers == null)
                return false;

            foreach (KeyValuePair<string, string> pair in deadPlayers)
            {
                if (pair.Value == name)
                {
                    targetid = pair.Key;
                    targetname = pair.Value;
                    return true;
                }
                if (pair.Value.Contains(name))
                {
                    if (targetid == noPlayerFound)
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
            if (targetid == multipleNames)
                return false;
            if (targetid != noPlayerFound)
                return true;
            return false;
        }

        ////////////////////////////////////////////////////////////
        // Log Class
        ////////////////////////////////////////////////////////////
        public class AcLog
        {
            public Vector3 frompos;
            public string message;
            public Vector3 topos;

            public AcLog(Vector3 frompos, Vector3 topos, string message )
            {
                this.frompos = frompos;
                this.topos = topos;
                this.message = message;
            }
        }


        public class PlayerLog : MonoBehaviour
        {
            public BasePlayer player;
            public Vector3 lastPosition;
            public List<AcLog> logs = new List<AcLog>();
            

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckLogs", 1f, 2f);
            }
            void CheckLogs()
            {
                if (!player.IsConnected()) { GameObject.Destroy(this); return; }
                foreach(AcLog log in logs)
                {
                    player.SendConsoleCommand("ddraw.arrow", 2f, UnityEngine.Color.red, log.frompos, log.topos, 0.5f);
                    if (log.message != string.Empty)
                    {
                        
                        player.SendConsoleCommand("ddraw.text", 2f, UnityEngine.Color.white, log.frompos, log.message);
                    }

                }
            }
            public void Clear()
            {
                logs.Clear();
            }
            public void AddLog(AntiCheatLog aclog, string targetid)
            {
                string detectionText = string.Empty;
                switch (aclog.td)
                {
                    case "speed":
                        detectionText = string.Format("{0} - speed - {1}m/s", targetid, Vector3.Distance(aclog.ToPos(), aclog.FromPos()).ToString());
                        break;
                    case "fly":
                        detectionText = string.Format("{0} - fly - {1}m/s", targetid, Vector3.Distance(aclog.ToPos(), aclog.FromPos()).ToString());
                        break;
                    case "wall":
                        detectionText = string.Format("{0} - wall", targetid);
                        break;
                    case "wallkill":
                        detectionText = string.Format("{0} - wallkill", targetid);
                    break;
                    default:

                        break;
                }
                logs.Add(new AcLog(aclog.FromPos(), aclog.ToPos(), detectionText));
            }
        }
		
		void TeleportAdmin(BasePlayer player, Vector3 destination)
        {
            player.StartSleeping();
            player.transform.position = destination;
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();

            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.UpdatePlayerCollider(true, false);
            player.SendNetworkUpdateImmediate(false);
            player.ClientRPCPlayer(null, player, "StartLoading");
            player.SendFullSnapshot();
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, false);
            player.ClientRPCPlayer(null, player, "FinishLoading");

        }
		
        ////////////////////////////////////////////////////////////
        // Chat Commands
        ////////////////////////////////////////////////////////////
		
        [ChatCommand("ac")]
        void cmdChatAC(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            if (args == null || args.Length < 2)
            {
                if (player.GetComponent<PlayerLog>())
                {
                    SendReply(player, "Deactivated AntiCheat Log Viewer.");
                    GameObject.Destroy(player.GetComponent<PlayerLog>());
                    return;
                }
                SendReply(player, "/ac player PLAYERNAME/STEAMID => to show all the hack detections made by this player");
                SendReply(player, "/ac radius RADIUS => to show all hack detections in this radius.");
                return;
            }

            if (args[0].ToLower() == "player")
            {
                PlayerLog playerlog = player.GetComponent<PlayerLog>();
                if (playerlog == null)
                    playerlog = player.gameObject.AddComponent<PlayerLog>();
                string targetid = string.Empty;
                string targetname = string.Empty;
                if (!FindPlayerByName(args[1], out targetid, out targetname))
                {
                    SendReply(player, targetid);
                    return;
                }
                if (anticheatlogs[targetid] == null || (anticheatlogs[targetid]).Count == 0)
                {
                    SendReply(player, string.Format("{0} {1} - has no hack detections", targetid, targetname));
                    return;
                }
                SendReply(player, string.Format("{0} {1} - has {2} hack detections", targetid, targetname, (anticheatlogs[targetid]).Count.ToString()));
                string detectionText = string.Empty;
                foreach (AntiCheatLog aclog in anticheatlogs[targetid])
                {
                    playerlog.AddLog(aclog, targetid);
                }
                SendReply(player, string.Format("You may say: /ac_tp NUMBER, to teleport to the specific detection (0-{0})", (playerlog.logs.Count - 1).ToString()));
            } 
            else if (args[0].ToLower() == "radius")
            {
                PlayerLog playerlog = player.GetComponent<PlayerLog>();
                if (playerlog == null)
                    playerlog = player.gameObject.AddComponent<PlayerLog>();
                float radius = 20f;
                if(!float.TryParse(args[1],out radius))
                {
                    SendReply(player, "/ac radius XXX");
                    return;
                }
                string detectionText = string.Empty;
                playerlog.Clear();
                foreach ( KeyValuePair<string, List<AntiCheatLog>> pair in anticheatlogs)
                {
                    foreach(AntiCheatLog aclog in pair.Value)
                    {
                        if(Vector3.Distance(player.transform.position, aclog.FromPos()) < radius )
                        {
                            playerlog.AddLog(aclog, pair.Key);
                        }
                    }
                }
                SendReply(player, string.Format("{0} detections were made in a {1}m radius around you",playerlog.logs.Count.ToString(),radius.ToString()));
                SendReply(player, string.Format("You may say: /ac_tp NUMBER, to teleport to the specific detection (0-{0})",(playerlog.logs.Count-1).ToString()));
            } 
            else
            {
                SendReply(player, string.Format("This argument: \"{0}\" doesn't exist", args[0]));
            }
        }
        
		[ChatCommand("ac_tp")]
        void cmdChatACTP(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            PlayerLog playerlog = player.GetComponent<PlayerLog>();
            if (playerlog == null)
        	{
            	SendReply(player, "You must use /ac player NAME/STEAMID or /ac radius XXX before using this command"); 
                return;
            }
            if(playerlog.logs == null  || playerlog.logs.Count == 0)
            {
            	SendReply(player, "Couldn't find any logs in your current log list, use /ac first");
            	return;
            }
            if(args.Length == 0)
            {
            	SendReply(player, string.Format("You must select the number of the detection you want to teleport to (0-{0})",(playerlog.logs.Count-1).ToString()));
            	return;
            }
            int lognumber = 0;
            if(!int.TryParse(args[0], out lognumber))
            {
            	SendReply(player, string.Format("You must select the number of the detection you want to teleport to (0-{0})",(playerlog.logs.Count-1).ToString()));
            	return;
            }
            if(lognumber < 0 || lognumber >= playerlog.logs.Count)
            {
            	SendReply(player, string.Format("You must select a number of the detection between 0 and {0}",(playerlog.logs.Count-1).ToString()));
            	return;
            }
            if(Vector3.Distance(player.transform.position, playerlog.logs[lognumber].frompos) < 100f)
            	ForcePlayerPosition(player, playerlog.logs[lognumber].frompos);
            else
            	TeleportAdmin(player, playerlog.logs[lognumber].frompos);
            SendReply(player, string.Format("{0} - {1} - {2}", playerlog.logs[lognumber].message.ToString(), playerlog.logs[lognumber].frompos.ToString(), playerlog.logs[lognumber].topos.ToString()));
        }
		
        [ChatCommand("ac_list")]
        void cmdChatACList(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            foreach (KeyValuePair<string, List<AntiCheatLog>> pair in anticheatlogs)
            {
                SendReply(player, string.Format("{0} - {1} detections", pair.Key, pair.Value.Count.ToString()));
            }

            SaveData();
        }

        [ChatCommand("ac_remove")]
        void cmdChatACRemove(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            string targetid = string.Empty;
            string targetname = string.Empty;
            if (!FindPlayerByName(args[0], out targetid, out targetname))
            {
                SendReply(player, targetid);
                return;
            }
            if (anticheatlogs[targetid] == null || (anticheatlogs[targetid]).Count == 0)
            {
                SendReply(player, string.Format("{0} {1} - has no hack detections", targetid, targetname));
                return;
            }
           
            foreach (AntiCheatLog aclog in anticheatlogs[targetid])
            {
                storedData.AntiCheatLogs.Remove(aclog);
            }
            anticheatlogs.Remove(targetid);
            SendReply(player, string.Format("Removed: {0} {1} anticheat logs", targetid, targetname));
            SaveData();
        }

        [ChatCommand("ac_reset")]
        void cmdChatACReset(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
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
        private BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            var player = BasePlayer.Find(nameOrIdOrIp);
            if (player == null)
            {
                ulong id;
                if (ulong.TryParse(nameOrIdOrIp, out id))
                    player = BasePlayer.FindSleeping(id);
            }
            return player;
        }
        [ConsoleCommand("ac.check")]
        void cmdConsoleAcCheck(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, "You dont have access to this command");
                    return;
                }
            }
            if(arg.Args.Length == 0)
            {
            	SendReply(arg, "ac.check PLAYER/STEAMID");
                return;
            }
            var targetplayer = FindPlayer(arg.Args[0]);
            if(targetplayer == null)
            {
            	SendReply(arg, "No players found");
                return;
            }
            PlayerHack playerhack = targetplayer.GetComponent<PlayerHack>();
            if(playerhack == null)
            {
            	targetplayer.gameObject.AddComponent<PlayerHack>();
            	SendReply(arg, string.Format("{0} is now being checked",targetplayer.displayName));
            }
            else
            {
            	SendReply(arg, string.Format("{0} is already being checked",targetplayer.displayName));
            }
        }
        
        void SendFPSCount()
        {
            if (fpsCaller is ConsoleSystem.Arg)
            {
                SendReply((ConsoleSystem.Arg)fpsCaller, string.Format("Checking all players on your server took {0}s", fpsTime.ToString()));
            }
        }
    }
}
