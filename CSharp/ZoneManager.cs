// Reference: RustBuild

using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;

using UnityEngine;

using Oxide.Core;

using Rust;

namespace Oxide.Plugins
{
    [Info("ZoneManager", "Reneb", "2.1.0", ResourceId = 739)]
    class ZoneManager : RustPlugin
    {
        ////////////////////////////////////////////
        /// Configs
        ////////////////////////////////////////////
        private bool Changed;
        private static float AutolightOnTime;
        private static float AutolightOffTime;
        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        private void LoadVariables()
        {
            AutolightOnTime = Convert.ToSingle(GetConfig("AutoLights", "Lights On Time", "18.0"));
            AutolightOffTime = Convert.ToSingle(GetConfig("AutoLights", "Lights Off Time", "8.0"));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }
        void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }


        ////////////////////////////////////////////
        /// FIELDS
        ////////////////////////////////////////////
        StoredData storedData;

        static readonly Hash<string, ZoneDefinition> zonedefinitions = new Hash<string, ZoneDefinition>();
        public readonly Hash<BasePlayer, string> LastZone = new Hash<BasePlayer, string>();
        public static readonly Hash<BasePlayer, List<Zone>> playerZones = new Hash<BasePlayer, List<Zone>>();
        public static readonly Hash<BaseCombatEntity, List<Zone>> buildingZones = new Hash<BaseCombatEntity, List<Zone>>();
        public static readonly Hash<BaseNPC, List<Zone>> npcZones = new Hash<BaseNPC, List<Zone>>();
        public static readonly Hash<ResourceDispenser, List<Zone>> resourceZones = new Hash<ResourceDispenser, List<Zone>>();

        public static int triggerLayer;
        public static int playersMask;
        public static int buildingMask;
        public static int AIMask;

        public static readonly FieldInfo[] allZoneFields = typeof(ZoneDefinition).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        public FieldInfo cachedField;
        public static readonly FieldInfo npcNextTick = typeof(NPCAI).GetField("nextTick", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

        public Vector3 Vector3Down = new Vector3(0f, -1f, 0f);
        /////////////////////////////////////////
        /// Cached Fields, used to make the plugin faster
        /////////////////////////////////////////
        public static Vector3 cachedDirection;
        public Collider[] cachedColliders;
        public DamageTypeList emptyDamageType;
        public List<DamageTypeEntry> emptyDamageList;
        public BasePlayer cachedPlayer;
        public RaycastHit cachedRaycasthit;
        public static Dictionary<BasePlayer, List<string>> playerTags = new Dictionary<BasePlayer, List<string>>();

        /////////////////////////////////////////
        // ZoneLocation
        // Stored information for the zone location and radius
        /////////////////////////////////////////
        public class ZoneLocation
        {
            public string x;
            public string y;
            public string z;
            public string r;
            Vector3 position;
            float radius;

            public ZoneLocation() { }

            public ZoneLocation(Vector3 position, string radius)
            {
                x = position.x.ToString();
                y = position.y.ToString();
                z = position.z.ToString();

                r = radius;

                this.position = position;
                this.radius = float.Parse(radius);
            }

            public Vector3 GetPosition()
            {
                if (position == Vector3.zero)
                    position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return position;
            }
            public float GetRadius()
            {
                if (radius == 0f)
                    radius = float.Parse(r);
                return radius;
            }
            public string String()
            {
                return string.Format("Pos({0},{1},{2}) - Rad({3})", x, y, z, r);
            }
        }
        /////////////////////////////////////////
        // RadiationZone
        // is a MonoBehaviour
        // This is needed for zones that use radiations only
        /////////////////////////////////////////
        public class RadiationZone : MonoBehaviour
        {
            public TriggerRadiation radiation;
            Zone zone;

            void Awake()
            {
                radiation = gameObject.AddComponent<TriggerRadiation>();
                zone = GetComponent<Zone>();
                radiation.RadiationAmount = float.Parse(zone.info.radiation);
                radiation.radiationSize = GetComponent<SphereCollider>().radius;
                radiation.interestLayers = playersMask;
            }
            void OnDestroy()
            {
                Destroy(radiation);
            }

        }
        /////////////////////////////////////////
        // Zone
        // is a Monobehavior
        // used to detect the colliders with players
        // and created everything on it's own (radiations, locations, etc)
        /////////////////////////////////////////

        static float GetSkyHour()
        {
            return TOD_Sky.Instance.Cycle.Hour;
        }
        public class Zone : MonoBehaviour
        {
            public ZoneDefinition info;
            public List<BasePlayer> inTrigger = new List<BasePlayer>();
            public List<BasePlayer> whiteList = new List<BasePlayer>();
            public List<BasePlayer> keepInList = new List<BasePlayer>();
            RadiationZone radiationzone;
            float radiationamount;

            int zoneMask;
            HashSet<Collider> buildingblocks = new HashSet<Collider>();

            bool lightsOn = false;

            void Awake()
            {
                gameObject.layer = triggerLayer;
                gameObject.name = "Zone Manager";

                gameObject.AddComponent<SphereCollider>();

                gameObject.SetActive(true);
                enabled = false;
            }

            public void SetInfo(ZoneDefinition info)
            {
                this.info = info;
                GetComponent<Transform>().position = info.Location.GetPosition();
                GetComponent<SphereCollider>().radius = info.Location.GetRadius();
                radiationamount = 0f;
                zoneMask = 0;
                if (info.undestr != null || info.nodecay != null)
                {
                    zoneMask |= 1 << LayerMask.NameToLayer("Construction");
                    zoneMask |= 1 << LayerMask.NameToLayer("Deployed");
                }
                if (info.nogather != null)
                {
                    zoneMask |= 1 << LayerMask.NameToLayer("Resource");
                    zoneMask |= 1 << LayerMask.NameToLayer("Tree");
                }
                if (info.nopve != null || info.npcfreeze != null)
                {
                    zoneMask |= 1 << LayerMask.NameToLayer("AI");
                }
                if (zoneMask != 0) InvokeRepeating("CheckCollisions", 0f, 10f);

                if (info.autolights != null)
                {
                    float currentTime = GetSkyHour();

                    if (currentTime > AutolightOffTime && currentTime < AutolightOnTime)
                    {
                        lightsOn = true;
                    }
                    else
                    {
                        lightsOn = false;
                    }
                    InvokeRepeating("CheckLights", 0f, 10f);
                }

                if (float.TryParse(info.radiation, out radiationamount))
                    radiationzone = gameObject.AddComponent<RadiationZone>();
                CheckSleepers(this);
            }
            void OnDestroy()
            {
                if (radiationzone != null)
                    Destroy(radiationzone);

                foreach (var zones in buildingZones.Values)
                    zones.Remove(this);
                foreach (var zones in npcZones.Values)
                    zones.Remove(this);
                foreach (var zones in resourceZones.Values)
                    zones.Remove(this);
                UpdateAllPlayers();
            }
            void CheckLights()
            {
                float currentTime = GetSkyHour();
                if (currentTime > AutolightOffTime && currentTime < AutolightOnTime)
                {
                    if (lightsOn)
                    {
                        foreach (var col in Physics.OverlapSphere(GetComponent<Transform>().position, GetComponent<UnityEngine.SphereCollider>().radius, buildingMask))
                        {
                            var oven = col.GetComponentInParent<BaseOven>();
                            if (oven != null)
                            {
                                if (!oven.IsInvoking("Cook"))
                                    oven.SetFlag(BaseEntity.Flags.On, false);
                            }
                        }
                        lightsOn = false;
                    }
                }
                else
                {
                    if (!lightsOn)
                    {
                        foreach (var col in Physics.OverlapSphere(GetComponent<UnityEngine.Transform>().position, GetComponent<UnityEngine.SphereCollider>().radius, buildingMask))
                        {
                            var oven = col.GetComponentInParent<BaseOven>();
                            oven?.SetFlag(BaseEntity.Flags.On, true);
                        }
                        lightsOn = true;
                    }
                }
            }
            void CheckCollisions()
            {
                foreach (var col in Physics.OverlapSphere(info.Location.GetPosition(), info.Location.GetRadius(), zoneMask))
                {
                    if (buildingblocks.Contains(col)) continue;
                    buildingblocks.Add(col);

                    var basecombat = col.GetComponentInParent<BaseCombatEntity>();
                    var npcai = col.GetComponentInParent<BaseNPC>();
                    var baseresource = col.GetComponentInParent<ResourceDispenser>();
                    if (baseresource != null)
                    {
                        List<Zone> zones;
                        if (!resourceZones.TryGetValue(baseresource, out zones))
                            resourceZones[baseresource] = zones = new List<Zone>();
                        if (!zones.Contains(this))
                            zones.Add(this);
                    }
                    else if (npcai != null)
                    {
                        if(info.nopve != null)
                        {
                            List<Zone> zones;
                            if (!npcZones.TryGetValue(npcai, out zones))
                                npcZones[npcai] = zones = new List<Zone>();
                            if (!zones.Contains(this))
                                zones.Add(this);
                        }
                    }
                    else if (basecombat != null)
                    {
                        List<Zone> zones;
                        if (!buildingZones.TryGetValue(basecombat, out zones))
                            buildingZones[basecombat] = zones = new List<Zone>();
                        if (!zones.Contains(this))
                            zones.Add(this);
                    }
                    if (info.nodecay != null)
                    {
                        var decay = col.GetComponentInParent<Decay>();
                        if (decay != null)
                        {
                            Destroy(decay);
                        }
                    }
                    if (info.npcfreeze != null)
                        if (npcai != null)
                            npcNextTick.SetValue(npcai, 999999999999f);
                }
            }
            void OnTriggerEnter(Collider col)
            {
                var player = col.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    inTrigger.Add(player);
                    OnEnterZone(this, player);
                }
            }
            void OnTriggerExit(Collider col)
            {
                var player = col.GetComponentInParent<BasePlayer>();
                if (player != null)
                {
                    inTrigger.Remove(player);
                    OnExitZone(this, player);
                }
            }
        }

        /////////////////////////////////////////
        // ZoneDefinition
        // Stored informations on the zones
        /////////////////////////////////////////
        public class ZoneDefinition
        {

            public string name;
            public string radius;
            public ZoneLocation Location;
            public string ID;
            public string autolights;
            public string eject;
            public string pvpgod;
            public string pvegod;
            public string sleepgod;
            public string undestr;
            public string nobuild;
            public string notp;
            public string nochat;
            public string nogather;
            public string nopve;
            public string nowounded;
            public string nodecay;
            public string nodeploy;
            public string nokits;
            public string noboxloot;
            public string noplayerloot;
            public string nocorpse;
            public string nosuicide;
            public string noremove;
            public string nobleed;
            public string killsleepers;
            public string radiation;
            public string enter_message;
            public string leave_message;
            public string npcfreeze;

            public ZoneDefinition()
            {

            }

            public ZoneDefinition(Vector3 position)
            {
                this.radius = "20";
                Location = new ZoneLocation(position, this.radius);
            }

        }
        /////////////////////////////////////////
        // Data Management
        /////////////////////////////////////////
        class StoredData
        {
            public readonly HashSet<ZoneDefinition> ZoneDefinitions = new HashSet<ZoneDefinition>();
            public StoredData()
            {
            }
        }
        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("ZoneManager", storedData);
        }
        void LoadData()
        {
            zonedefinitions.Clear();
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("ZoneManager");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var zonedef in storedData.ZoneDefinitions)
                zonedefinitions[zonedef.ID] = zonedef;
        }
        /////////////////////////////////////////
        // OXIDE HOOKS
        /////////////////////////////////////////

        /////////////////////////////////////////
        // Loaded()
        // Called when the plugin is loaded
        /////////////////////////////////////////
        void Loaded()
        {
            permission.RegisterPermission("zone", this);
            permission.RegisterPermission("candeploy", this);
            permission.RegisterPermission("canbuild", this);
            triggerLayer = LayerMask.NameToLayer("Trigger");
            playersMask = LayerMask.GetMask("Player (Server)");
            buildingMask = LayerMask.GetMask("Deployed");
            AIMask = LayerMask.GetMask("AI");
            /* for(int i = 0; i < 25; i ++)
             {
                 Debug.Log(UnityEngine.LayerMask.LayerToName(i));
             }*/
            LoadData();
            LoadVariables();
        }
        /////////////////////////////////////////
        // Unload()
        // Called when the plugin is unloaded
        /////////////////////////////////////////
        void Unload()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<Zone>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }
        void Unloaded()
        {
            SaveData();
        }
        /////////////////////////////////////////
        // OnServerInitialized()
        // Called when the server is initialized
        /////////////////////////////////////////
        void OnServerInitialized()
        {
            emptyDamageType = new DamageTypeList();
            emptyDamageList = new List<DamageTypeEntry>();
            foreach (var pair in zonedefinitions)
            {
                NewZone(pair.Value);
            }
        }

        static void CheckSleepers(Zone zone)
        {
            foreach(var player in BasePlayer.sleepingPlayerList)
            {
                if(Vector3.Distance(player.transform.position, zone.info.Location.GetPosition()) <= zone.info.Location.GetRadius())
                {
                    OnEnterZone(zone, player);
                }
            }
        }

        /////////////////////////////////////////
        // OnEntityBuilt(Planner planner, GameObject gameobject)
        // Called when a buildingblock was created
        /////////////////////////////////////////
        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner.ownerPlayer == null) return;
            if (hasTag(planner.ownerPlayer, "nobuild"))
            {
                if (!hasPermission(planner.ownerPlayer, "canbuild"))
                {
                    gameobject.GetComponentInParent<BaseCombatEntity>().Kill(BaseNetworkable.DestroyMode.Gib);
                    SendMessage(planner.ownerPlayer, "You are not allowed to build here");
                }
            }
        }
        /////////////////////////////////////////
        // OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        // Called when an item was deployed
        /////////////////////////////////////////
        void OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        {
            if (deployer.ownerPlayer == null) return;
            if (hasTag(deployer.ownerPlayer, "nodeploy"))
            {
                if (!hasPermission(deployer.ownerPlayer, "candeploy"))
                {
                    deployedEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                    SendMessage(deployer.ownerPlayer, "You are not allowed to deploy here");
                }
            }
        }

        void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity ownerEntity, float delta)
        {
            if (metabolism.bleeding.value < 0.01f) return;
            var player = ownerEntity.GetComponent<BasePlayer>();
            if (player == null) return;
            if (hasTag(player, "nobleed"))
            {
                metabolism.bleeding.value = 0f;
            }
        }

        /////////////////////////////////////////
        // OnPlayerChat(ConsoleSystem.Arg arg)
        // Called when a user writes something in the chat, doesn't take in count the commands
        /////////////////////////////////////////
        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return null;
            if (hasTag(arg.Player(), "nochat"))
            {
                SendMessage(arg.Player(), "You are not allowed to chat here");
                return false;
            }
            return null;
        }

        /////////////////////////////////////////
        // OnRunCommand(ConsoleSystem.Arg arg)
        // Called when a user executes a command
        /////////////////////////////////////////
        object OnRunCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return null;
            if (arg.cmd?.name == null) return null;
            if (arg.cmd.name == "kill" && hasTag(arg.Player(), "nosuicide"))
            {
                SendMessage(arg.Player(), "You are not allowed to suicide here");
                return false;
            }
            return null;
        }

        /////////////////////////////////////////
        // OnPlayerDisconnected(BasePlayer player)
        // Called when a user disconnects
        /////////////////////////////////////////
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (hasTag(player, "killsleepers")) player.Die();
        }

        void OnMeleeAttack(BaseMelee melee, HitInfo hitinfo)
        {
            var disp = hitinfo.HitEntity?.GetComponent<ResourceDispenser>();
            if (disp != null)
            {
                List<Zone> resourceZone;
                if (!resourceZones.TryGetValue(disp, out resourceZone)) return;
                foreach (var zone in resourceZone)
                {
                    if (zone.info.nogather != null)
                    {
                        hitinfo.HitEntity = null;
                    }
                }
            }
        }

        /////////////////////////////////////////
        // OnEntityAttacked(BaseCombatEntity entity, HitInfo hitinfo)
        // Called when any entity is attacked
        /////////////////////////////////////////
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity is BasePlayer)
            {
                cachedPlayer = (BasePlayer) entity;
                if (cachedPlayer.IsSleeping())
                {
                    if (hasTag(cachedPlayer, "sleepgod"))
                        CancelDamage(hitinfo);
                }
                else if (hitinfo.Initiator != null)
                {
                    if (hitinfo.Initiator is BasePlayer)
                    {
                        if (hasTag(cachedPlayer, "pvpgod"))
                            CancelDamage(hitinfo);
                        else if (hasTag((BasePlayer) hitinfo.Initiator, "pvpgod"))
                            CancelDamage(hitinfo);
                    }
                    else if (hasTag(cachedPlayer, "pvegod"))
                        CancelDamage(hitinfo);
                }
            }
            else if (entity is BaseNPC)
            {
                var npcai = (BaseNPC) entity;
                List<Zone> zones;
                if (!npcZones.TryGetValue(npcai, out zones)) return;
                foreach (var zone in zones)
                {
                    if (zone.info.nopve != null)
                    {
                        CancelDamage(hitinfo);
                    }
                }
            }
            else if (entity is BaseCombatEntity)
            {
                List<Zone> zones;
                if (!buildingZones.TryGetValue(entity, out zones)) return;
                foreach (var zone in zones)
                {
                    if (zone.info.undestr != null)
                    {
                        CancelDamage(hitinfo);
                    }
                }
            }
            else if (entity is WorldItem)
            {
                if (hitinfo != null && hitinfo.Initiator != null)
                {
                    if (hitinfo.Initiator is BasePlayer)
                    {
                        if (hasTag(hitinfo.Initiator as BasePlayer, "undestr"))
                            CancelDamage(hitinfo);
                    }
                }
            }
        }

        /////////////////////////////////////////
        // OnEntityDeath(BaseNetworkable basenet)
        // Called when any entity is spawned
        /////////////////////////////////////////
        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity is BasePlayer)
            {
                cachedPlayer = (BasePlayer) entity;
                if (hasTag(cachedPlayer, "nocorpse"))
                {
                    timer.Once(0.1f, () => EraseCorpse(entity.transform.position));
                }
                if (playerZones[cachedPlayer] != null)
                {
                    playerZones[cachedPlayer].Clear();
                    UpdateTags(cachedPlayer);
                }
            }
        }
        void EraseCorpse(Vector3 position)
        {
            if (Physics.Raycast(position, Vector3Down, out cachedRaycasthit))
            {
                position = cachedRaycasthit.point;
            }
            foreach (var collider in Physics.OverlapSphere(position, 2f))
            {
                var corpse = collider.GetComponentInParent<BaseCorpse>();
                corpse?.Kill(BaseNetworkable.DestroyMode.None);
            }
        }


        /////////////////////////////////////////
        // OnEntitySpawned(BaseNetworkable entity)
        // Called when any kind of entity is spawned in the world
        /////////////////////////////////////////
        void OnEntitySpawned(BaseNetworkable entity)
        {
            var npc = entity.GetComponent<NPCAI>();
            if (npc != null)
            {
                npcNextTick.SetValue(npc, Time.time + 10f);
            }
        }

        /////////////////////////////////////////
        // OnPlayerLoot(PlayerLoot lootInventory,  BasePlayer targetPlayer)
        // Called when a player tries to loot another player
        /////////////////////////////////////////
        void OnPlayerLoot(PlayerLoot lootInventory, object target)
        {
            if (target is BasePlayer || target is BaseCorpse)
                OnLootPlayer(lootInventory.GetComponent<BasePlayer>());
            else
                OnLootBox(lootInventory.GetComponent<BasePlayer>(), target);
        }
        void OnLootPlayer(BasePlayer looter)
        {
            if (hasTag(looter, "noplayerloot"))
            {
                timer.Once(0.01f, looter.EndLooting);
            }
        }
        void OnLootBox(BasePlayer looter, object target)
        {
            if (hasTag(looter, "noboxloot"))
            {
                timer.Once(0.01f, looter.EndLooting);
            }
        }

        /////////////////////////////////////////
        // CanBeWounded(BasePlayer player)
        // Called from the Kits plugin (Reneb) when trying to redeem a kit
        /////////////////////////////////////////
        object CanBeWounded(BasePlayer player, HitInfo hitinfo)
        {
            if (hasTag(player, "nowounded")) { return false; }
            return null;
        }

        /////////////////////////////////////////
        // Outside Plugin Hooks
        /////////////////////////////////////////

        /////////////////////////////////////////
        // canRedeemKit(BasePlayer player)
        // Called from the Kits plugin (Reneb) when trying to redeem a kit
        /////////////////////////////////////////
        object canRedeemKit(BasePlayer player)
        {
            if (hasTag(player, "nokits")) { return "You may not redeem a kit inside this area"; }
            return null;
        }

        /////////////////////////////////////////
        // canTeleport(BasePlayer player)
        // Called from Teleportation System (Mughisi) when a player tries to teleport
        /////////////////////////////////////////
        object canTeleport(BasePlayer player)
        {
            if (hasTag(player, "notp")) { return "You may not teleport in this area"; }
            return null;
        }

        /////////////////////////////////////////
        // canRemove(BasePlayer player)
        // Called from Teleportation System (Mughisi) when a player tries to teleport
        /////////////////////////////////////////
        object canRemove(BasePlayer player)
        {
            if (hasTag(player, "noremove")) { return "You may not use the remover tool in this area"; }
            return null;
        }

        /////////////////////////////////////////
        // External calls to this plugin
        /////////////////////////////////////////

        /////////////////////////////////////////
        // CreateOrUpdateZone(string ZoneID, object[] args)
        // Create or Update a zone from an external plugin
        // ZoneID should be a name, like Arena (for an arena plugin) (even if it's called an ID :p)
        // args are the same a the /zone command
        // args[0] = "radius" args[1] = "50" args[2] = "eject" args[3] = "true", etc
        // Third parameter is obviously need if you create a NEW zone (or want to update the position)
        /////////////////////////////////////////
        bool CreateOrUpdateZone(string ZoneID, string[] args, Vector3 position = default(Vector3))
        {
            ZoneDefinition zonedef;
            if (!zonedefinitions.TryGetValue(ZoneID, out zonedef))
                zonedef = new ZoneDefinition { ID = ZoneID };
            else
                storedData.ZoneDefinitions.Remove(zonedef);
            for (var i = 0; i < args.Length; i = i + 2)
            {
                cachedField = GetZoneField(args[i]);
                if (cachedField == null) continue;

                string editvalue;
                switch (args[i + 1].ToLower())
                {
                    case "true":
                    case "1":
                        editvalue = "true";
                        break;
                    case "null":
                    case "0":
                    case "false":
                    case "reset":
                        editvalue = null;
                        break;
                    default:
                        editvalue = args[i + 1];
                        break;
                }
                cachedField.SetValue(zonedef, editvalue);
                if (args[i].ToLower() == "radius")
                {
                    if (zonedef.Location != null)
                        zonedef.Location = new ZoneLocation(zonedef.Location.GetPosition(), editvalue);
                }
            }

            if (position != default(Vector3)) { zonedef.Location = new ZoneLocation(position, zonedef.radius ?? "20"); }

            zonedefinitions[ZoneID] = zonedef;
            storedData.ZoneDefinitions.Add(zonedef);
            SaveData();

            if (zonedef.Location == null) return false;
            RefreshZone(ZoneID);
            return true;
        }
        bool EraseZone(string ZoneID)
        {
            ZoneDefinition zone;
            if (!zonedefinitions.TryGetValue(ZoneID, out zone)) return false;

            storedData.ZoneDefinitions.Remove(zone);
            zonedefinitions.Remove(ZoneID);
            SaveData();
            RefreshZone(ZoneID);
            return true;
        }
        List<string> ZoneFieldListRaw()
        {
            return allZoneFields.Select(fieldinfo => fieldinfo.Name).ToList();
        }

        Dictionary<string, string> ZoneFieldList(string ZoneID)
        {
            ZoneDefinition zone;
            if (!zonedefinitions.TryGetValue(ZoneID, out zone)) return null;
            var fieldlistzone = new Dictionary<string, string>();

            foreach (var fieldinfo in allZoneFields)
            {
                var value = fieldinfo.GetValue(zone);
                switch (fieldinfo.Name)
                {
                    case "Location":
                        value = ((ZoneLocation)value).String();
                        break;
                    default:
                        if (value == null) value = "false";
                        break;
                }
                fieldlistzone.Add(fieldinfo.Name, value.ToString());
            }
            return fieldlistzone;
        }
        List<BasePlayer> GetPlayersInZone(string ZoneID)
        {
            var baseplayers = new List<BasePlayer>();
            foreach (var pair in playerZones)
            {
                baseplayers.AddRange(pair.Value.Where(zone => zone.info.ID == ZoneID).Select(zone => pair.Key));
            }
            return baseplayers;
        }
        bool isPlayerInZone(string ZoneID, BasePlayer player)
        {
            List<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones)) return false;
            return zones.Any(zone => zone.info.ID == ZoneID);
        }
        bool AddPlayerToZoneWhitelist(string ZoneID, BasePlayer player)
        {
            var targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return false;
            AddToWhitelist(targetZone, player);
            return true;
        }
        bool AddPlayerToZoneKeepinlist(string ZoneID, BasePlayer player)
        {
            var targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return false;
            AddToKeepinlist(targetZone, player);
            return true;
        }
        bool RemovePlayerFromZoneWhitelist(string ZoneID, BasePlayer player)
        {
            var targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return false;
            RemoveFromWhitelist(targetZone, player);
            return true;
        }
        bool RemovePlayerFromZoneKeepinlist(string ZoneID, BasePlayer player)
        {
            var targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return false;
            RemoveFromKeepinlist(targetZone, player);
            return true;
        }

        void ShowZone(BasePlayer player, string ZoneID)
        {
            var targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return;
            player.SendConsoleCommand("ddraw.sphere", 5f, UnityEngine.Color.blue, targetZone.info.Location.GetPosition(), targetZone.info.Location.GetRadius());
        }

        /////////////////////////////////////////
        // Random Commands
        /////////////////////////////////////////
        void AddToWhitelist(Zone zone, BasePlayer player) { if (!zone.whiteList.Contains(player)) zone.whiteList.Add(player); }
        void RemoveFromWhitelist(Zone zone, BasePlayer player) { if (zone.whiteList.Contains(player)) zone.whiteList.Remove(player); }
        void AddToKeepinlist(Zone zone, BasePlayer player) { if (!zone.keepInList.Contains(player)) zone.keepInList.Add(player); }
        void RemoveFromKeepinlist(Zone zone, BasePlayer player) { if (zone.keepInList.Contains(player)) zone.keepInList.Remove(player); }

        Zone GetZoneByID(string ZoneID)
        {
            var objects = UnityEngine.Object.FindObjectsOfType<Zone>();
            return objects?.FirstOrDefault(gameObj => gameObj.info.ID == ZoneID);
        }

        void NewZone(ZoneDefinition zonedef)
        {

            if (zonedef == null) return;
            var newgameObject = new GameObject();
            var newZone = newgameObject.AddComponent<Zone>();

            newZone.SetInfo(zonedef);
        }
        void RefreshZone(string zoneID)
        {
            var zone = GetZoneByID(zoneID);
            if (zone != null)
            {
                foreach (var pair in playerZones)
                {
                    if (pair.Value.Contains(zone)) playerZones[pair.Key].Remove(zone);
                }
                UnityEngine.Object.Destroy(zone);
            }
            if (zonedefinitions[zoneID] != null)
            {
                NewZone(zonedefinitions[zoneID]);
            }
        }

        int GetRandom(int min, int max) { return UnityEngine.Random.Range(min, max); }

        FieldInfo GetZoneField(string name)
        {
            name = name.ToLower();
            return allZoneFields.FirstOrDefault(fieldinfo => fieldinfo.Name == name);
        }
        static void UpdateAllPlayers()
        {
            var players = playerTags.Select(pair => pair.Key).ToList();
            foreach (var player in players)
            {
                UpdateTags(player);
            }
        }
        static void UpdateTags(BasePlayer player)
        {
            List<string> tags;
            if (!playerTags.TryGetValue(player, out tags))
                playerTags[player] = tags = new List<string>();
            tags.Clear();
            List<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones) || zones.Count == 0) return;
            foreach (var zone in zones)
            {
                tags.AddRange(allZoneFields.Where(fieldinfo => fieldinfo.GetValue(zone.info) != null).Select(fieldinfo => fieldinfo.Name));
            }
        }
        static bool hasTag(BasePlayer player, string tagname)
        {
            List<string> tags;
            if (!playerTags.TryGetValue(player, out tags) || tags.Count == 0) return false;
            return tags.Contains(tagname);
        }

        BasePlayer FindPlayerByRadius(Vector3 position, float rad)
        {
            cachedColliders = Physics.OverlapSphere(position, rad, playersMask);
            return cachedColliders.Select(collider => collider.GetComponentInParent<BasePlayer>()).FirstOrDefault(player => player != null);
        }
        void CheckExplosivePosition(TimedExplosive explosive)
        {
            if (explosive == null) return;
            var objects = UnityEngine.Object.FindObjectsOfType<Zone>();
            if (objects != null)
                foreach (var zone in objects)
                {
                    if (zone.info.undestr != null)
                    {
                        if (Vector3.Distance(explosive.GetEstimatedWorldPosition(), zone.transform.position) <= (zone.info.Location.GetRadius()))
                            explosive.KillMessage();
                    }
                }
        }

        void CancelDamage(HitInfo hitinfo)
        {
            hitinfo.damageTypes = emptyDamageType;
            hitinfo.DoHitEffects = false;
            hitinfo.HitMaterial = 0;
        }
        static void OnEnterZone(Zone zone, BasePlayer player)
        {
            List<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones))
                playerZones[player] = zones = new List<Zone>();
            if (!zones.Contains(zone)) zones.Add(zone);
            UpdateTags(player);
            if (zone.info.enter_message != null) SendMessage(player, zone.info.enter_message);
            if (zone.info.eject != null && !isAdmin(player) && !zone.whiteList.Contains(player) && !zone.keepInList.Contains(player)) EjectPlayer(zone, player);
            Interface.CallHook("OnEnterZone", zone.info.ID, player);
        }
        static void OnExitZone(Zone zone, BasePlayer player)
        {
            playerZones[player].Remove(zone);
            UpdateTags(player);
            if (zone.info.leave_message != null) SendMessage(player, zone.info.leave_message);
            if (zone.keepInList.Contains(player)) AttractPlayer(zone, player);
            Interface.CallHook("OnExitZone", zone.info.ID, player);
        }

        static void EjectPlayer(Zone zone, BasePlayer player)
        {
            cachedDirection = player.transform.position - zone.transform.position;
            player.MovePosition(zone.transform.position + (cachedDirection / cachedDirection.magnitude * (zone.GetComponent<SphereCollider>().radius + 1f)));
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { player.transform.position });
            player.TransformChanged();
        }
        static void AttractPlayer(Zone zone, BasePlayer player)
        {
            cachedDirection = player.transform.position - zone.transform.position;
            player.MovePosition(zone.transform.position + (cachedDirection / cachedDirection.magnitude * (zone.GetComponent<UnityEngine.SphereCollider>().radius - 1f)));
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { player.transform.position });
            player.TransformChanged();
        }
        static bool isAdmin(BasePlayer player)
        {
            if (player.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }
        bool hasPermission(BasePlayer player, string permname)
        {
            if (player.net.connection.authLevel > 1)
                return true;
            return permission.UserHasPermission(player.userID.ToString(), permname);
        }
        //////////////////////////////////////////////////////////////////////////////
        /// Chat Commands
        //////////////////////////////////////////////////////////////////////////////
        [ChatCommand("zone_add")]
        void cmdChatZoneAdd(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "zone")) { SendMessage(player, "You don't have access to this command"); return; }
            var newzoneinfo = new ZoneDefinition(player.transform.position) {ID = GetRandom(1, 99999999).ToString()};
            NewZone(newzoneinfo);
            if (zonedefinitions[newzoneinfo.ID] != null) storedData.ZoneDefinitions.Remove(zonedefinitions[newzoneinfo.ID]);
            zonedefinitions[newzoneinfo.ID] = newzoneinfo;
            LastZone[player] = newzoneinfo.ID;
            storedData.ZoneDefinitions.Add(newzoneinfo);
            SaveData();
            ShowZone(player, newzoneinfo.ID);
            SendMessage(player, "New Zone created, you may now edit it: " + newzoneinfo.Location.String());
        }
        [ChatCommand("zone_reset")]
        void cmdChatZoneReset(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "zone")) { SendMessage(player, "You don't have access to this command"); return; }

            zonedefinitions.Clear();
            storedData.ZoneDefinitions.Clear();
            SaveData();
            Unload();
            SendMessage(player, "All Zones were removed");
        }
        [ChatCommand("zone_remove")]
        void cmdChatZoneRemove(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "zone")) { SendMessage(player, "You don't have access to this command"); return; }
            if (args.Length == 0) { SendMessage(player, "/zone_remove XXXXXID"); return; }
            if (zonedefinitions[args[0]] == null) { SendMessage(player, "This zone doesn't exist"); return; }
            storedData.ZoneDefinitions.Remove(zonedefinitions[args[0]]);
            zonedefinitions[args[0]] = null;
            SaveData();
            RefreshZone(args[0]);
            SendMessage(player, "Zone " + args[0] + " was removed");
        }
        [ChatCommand("zone_edit")]
        void cmdChatZoneEdit(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "zone")) { SendMessage(player, "You don't have access to this command"); return; }
            if (args.Length == 0) { SendMessage(player, "/zone_edit XXXXXID"); return; }
            if (zonedefinitions[args[0]] == null) { SendMessage(player, "This zone doesn't exist"); return; }
            LastZone[player] = args[0];
            SendMessage(player, "Editing zone ID: " + args[0]);
            ShowZone(player, LastZone[player]);
        }
        [ChatCommand("zone_list")]
        void cmdChatZoneList(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "zone")) { SendMessage(player, "You don't have access to this command"); return; }
            SendMessage(player, "========== Zone list ==========");
            if (zonedefinitions.Count == 0) { SendMessage(player, "empty"); return; }
            foreach (var pair in zonedefinitions)
            {
                SendMessage(player, $"{pair.Key} => {pair.Value.name} - {pair.Value.Location.String()}");
            }
        }
        [ChatCommand("zone")]
        void cmdChatZone(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "zone")) { SendMessage(player, "You don't have access to this command"); return; }
            if (LastZone[player] == null) { SendMessage(player, "You must first say: /zone_edit XXXXXID"); return; }

            var zoneId = LastZone[player];
            var zoneDefinition = zonedefinitions[zoneId];
            if (args.Length < 2)
            {
                SendMessage(player, "/zone option value/reset");
                foreach (FieldInfo fieldinfo in allZoneFields)
                {
                    var value = fieldinfo.GetValue(zoneDefinition);
                    switch (fieldinfo.Name)
                    {
                        case "Location":
                            value = ((ZoneLocation)value).String();
                            break;
                        default:
                            if (value == null) value = "false";
                            break;
                    }
                    SendMessage(player, $"{fieldinfo.Name} => {value}");
                }
                return;
            }
            for (var i = 0; i < args.Length; i = i + 2)
            {

                cachedField = GetZoneField(args[i]);
                if (cachedField == null) continue;

                string editvalue;
                switch (args[i + 1])
                {
                    case "true":
                    case "1":
                        editvalue = "true";
                        break;
                    case "null":
                    case "0":
                    case "false":
                    case "reset":
                        editvalue = null;
                        break;
                    default:
                        editvalue = args[i + 1];
                        break;
                }
                cachedField.SetValue(zoneDefinition, editvalue);
                if (args[i].ToLower() == "radius") { zoneDefinition.Location = new ZoneLocation(zoneDefinition.Location.GetPosition(), editvalue); }
                SendMessage(player, $"{cachedField.Name} set to {editvalue}");
            }
            RefreshZone(zoneId);
            SaveData();
            ShowZone(player, zoneId);
        }
        static void SendMessage(BasePlayer player, string message) { if(player.net?.connection != null) player.SendConsoleCommand("chat.add", "0", string.Format("<color=#FA58AC>{0}:</color> {1}", "ZoneManager", message), 1.0); }
    }
}
