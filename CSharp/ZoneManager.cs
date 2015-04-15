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
    [Info("ZoneManager", "Reneb", "2.0.9", ResourceId = 739)]
    class ZoneManager : RustPlugin
    {
        ////////////////////////////////////////////
        /// FIELDS
        ////////////////////////////////////////////
        StoredData storedData;

        static Hash<string, ZoneDefinition> zonedefinitions = new Hash<string, ZoneDefinition>();
        public Hash<BasePlayer, string> LastZone = new Hash<BasePlayer, string>();
        public static Hash<BasePlayer, List<Zone>> playerZones = new Hash<BasePlayer, List<Zone>>();

        public static int triggerLayer;
        public static int playersMask;

        public FieldInfo[] allZoneFields;
        public FieldInfo cachedField;
        public static FieldInfo fieldInfo;
         
        /////////////////////////////////////////
        /// Cached Fields, used to make the plugin faster
        /////////////////////////////////////////
        public static Vector3 cachedDirection;
        public Collider[] cachedColliders;
        public DamageTypeList emptyDamageType;
        public List<DamageTypeEntry> emptyDamageList;
        public BasePlayer cachedPlayer;

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

                r = radius.ToString();

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
                radiation.radiationSize = GetComponent<UnityEngine.SphereCollider>().radius;
                radiation.interestLayers = playersMask;
            }
            void OnDestroy()
            {
                GameObject.Destroy(radiation);
            }

        }
        /////////////////////////////////////////
        // Zone
        // is a Monobehavior
        // used to detect the colliders with players
        // and created everything on it's own (radiations, locations, etc)
        /////////////////////////////////////////
        public class Zone : MonoBehaviour
        {
            public ZoneDefinition info;
            public List<BasePlayer> inTrigger = new List<BasePlayer>();
            public List<BasePlayer> whiteList = new List<BasePlayer>();
            public List<BasePlayer> keepInList = new List<BasePlayer>();
            RadiationZone radiationzone;
            float radiationamount;

            void Awake()
            {
                gameObject.layer = triggerLayer;
                gameObject.name = "Zone Manager";
                gameObject.AddComponent<UnityEngine.SphereCollider>();
                gameObject.SetActive(true);
            }
            public void SetInfo(ZoneDefinition info)
            {
                this.info = info;
                GetComponent<UnityEngine.Transform>().position = info.Location.GetPosition();
                GetComponent<UnityEngine.SphereCollider>().radius = info.Location.GetRadius();
                radiationamount = 0f;
                if (float.TryParse(info.radiation, out radiationamount))
                    radiationzone = gameObject.AddComponent<RadiationZone>();
            }
            void OnDestroy()
            {
                if (radiationzone != null)
                    GameObject.Destroy(radiationzone);
            }
            void OnTriggerEnter(Collider col)
            {
                if (col.GetComponentInParent<BasePlayer>())
                {
                    inTrigger.Add(col.GetComponentInParent<BasePlayer>());
                    OnEnterZone(this, col.GetComponentInParent<BasePlayer>());
                }
            }
            void OnTriggerExit(Collider col)
            {
                if (col.GetComponentInParent<BasePlayer>())
                {
                    inTrigger.Remove(col.GetComponentInParent<BasePlayer>());
                    OnExitZone(this, col.GetComponentInParent<BasePlayer>());
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
            public string eject;
            public string pvpgod;
            public string pvegod;
            public string sleepgod;
            public string undestr;
            public string nobuild;
            public string notp;
            public string nochat;
            public string nodeploy;
            public string nokits;
            public string nosuicide;
            public string killsleepers;
            public string radiation;
            public string enter_message;
            public string leave_message;

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
            public HashSet<ZoneDefinition> ZoneDefinitions = new HashSet<ZoneDefinition>();
            public StoredData()
            {
            }
        }
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("ZoneManager", storedData);
        }
        void LoadData()
        {
            zonedefinitions.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("ZoneManager");
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
            triggerLayer = UnityEngine.LayerMask.NameToLayer("Trigger");
            playersMask = LayerMask.GetMask(new string[] { "Player (Server)" });
            LoadData();
        }
        /////////////////////////////////////////
        // Unload()
        // Called when the plugin is unloaded
        /////////////////////////////////////////
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(Zone));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
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
            allZoneFields = typeof(ZoneDefinition).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            emptyDamageType = new DamageTypeList();
            emptyDamageList = new List<DamageTypeEntry>();
            foreach (KeyValuePair<string, ZoneDefinition> pair in zonedefinitions)
            {
                NewZone(pair.Value);
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
                    gameobject.GetComponentInParent<BuildingBlock>().Kill(BaseNetworkable.DestroyMode.Gib);
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

        /////////////////////////////////////////
        // OnPlayerChat(ConsoleSystem.Arg arg)
        // Called when a user writes something in the chat, doesn't take in count the commands
        /////////////////////////////////////////
        object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            if (arg.connection == null) return null;
            if (arg.connection.player == null) return null;
            if (hasTag((BasePlayer)arg.connection.player, "nochat"))
            {
                SendMessage((BasePlayer)arg.connection.player, "You are not allowed to chat here");
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
            if (arg == null) return null;
            if (arg.connection == null) return null;
            if (arg.connection.player == null) return null;
            if (arg.cmd == null) return null;
            if (arg.cmd.name == null) return null;
            if ((string)arg.cmd.name == "kill" && hasTag((BasePlayer)arg.connection.player, "nosuicide"))
            {
                SendMessage((BasePlayer)arg.connection.player, "You are not allowed to suicide here");
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

        /////////////////////////////////////////
        // OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        // Called when any entity is attacked
        /////////////////////////////////////////
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity is BasePlayer)
            {
                cachedPlayer = entity as BasePlayer;
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
                    }
                    else if (hasTag(cachedPlayer, "pvegod"))
                        CancelDamage(hitinfo);
                }
            }
            else if ((entity is BuildingBlock) || (entity is WorldItem))
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
            if(entity is BasePlayer)
            {
                cachedPlayer = entity as BasePlayer;
                if(playerZones[cachedPlayer] != null)
                    playerZones[cachedPlayer].Clear(); 
            }
        }

        /////////////////////////////////////////
        // OnEntitySpawned(BaseNetworkable basenet)
        // Called when any entity is spawned
        /////////////////////////////////////////
        void OnEntitySpawned(BaseNetworkable basenet)
        {
            if (basenet is TimedExplosive)
            {
                timer.Once(4f, () => CheckExplosivePosition(basenet as TimedExplosive));
            }
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
            if (zonedefinitions[ZoneID] == null) zonedef = new ZoneDefinition();
            else zonedef = zonedefinitions[ZoneID];
            zonedef.ID = ZoneID;

            string editvalue;
            for (int i = 0; i < args.Length; i = i + 2)
            {

                cachedField = GetZoneField(args[i]);
                if (cachedField == null) continue;

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
                        editvalue = (string)args[i + 1];
                        break;
                }
                cachedField.SetValue(zonedef, editvalue);
                if (args[i].ToLower() == "radius") { if (zonedef.Location != null) zonedef.Location = new ZoneLocation(zonedef.Location.GetPosition(), editvalue); }
            }

            if (position != default(Vector3)) { zonedef.Location = new ZoneLocation((Vector3)position, (zonedef.radius != null) ? zonedef.radius : "20"); }

            if (zonedefinitions[ZoneID] != null) storedData.ZoneDefinitions.Remove(zonedefinitions[ZoneID]);
            zonedefinitions[ZoneID] = zonedef;
            storedData.ZoneDefinitions.Add(zonedefinitions[ZoneID]);
            SaveData();
            if (zonedef.Location == null) return false;
            RefreshZone(ZoneID);
            return true;
        }
        bool EraseZone(string ZoneID)
        {
            if (zonedefinitions[ZoneID] == null) return false;
            zonedefinitions[ZoneID] = null;
            storedData.ZoneDefinitions.Remove(zonedefinitions[ZoneID]);
            SaveData();
            RefreshZone(ZoneID);
            return true;
        }
        List<BasePlayer> GetPlayersInZone(string ZoneID)
        {
            List<BasePlayer> baseplayers = new List<BasePlayer>();
            foreach (KeyValuePair<BasePlayer, List<Zone>> pair in playerZones)
            {
                foreach (Zone zone in pair.Value)
                {
                    if (zone.info.ID == ZoneID)
                    {
                        baseplayers.Add(pair.Key);
                    }
                }
            }
            return baseplayers;
        }
        bool isPlayerInZone(string ZoneID, BasePlayer player)
        {
            if (playerZones[player] == null) return false;
            foreach (Zone zone in playerZones[player])
            {
                if (zone.info.ID == ZoneID)
                {
                    return true;
                }
            }
            return false;
        }
        bool AddPlayerToZoneWhitelist(string ZoneID, BasePlayer player)
        {
            Zone targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return false;
            AddToWhitelist(targetZone, player);
            return true;
        }
        bool AddPlayerToZoneKeepinlist(string ZoneID, BasePlayer player)
        {
            Zone targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return false;
            AddToKeepinlist(targetZone, player);
            return true;
        }
        bool RemovePlayerFromZoneWhitelist(string ZoneID, BasePlayer player)
        {
            Zone targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return false;
            RemoveFromWhitelist(targetZone, player);
            return true;
        }
        bool RemovePlayerFromZoneKeepinlist(string ZoneID, BasePlayer player)
        {
            Zone targetZone = GetZoneByID(ZoneID);
            if (targetZone == null) return false;
            RemoveFromKeepinlist(targetZone, player);
            return true; 
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
            var objects = GameObject.FindObjectsOfType(typeof(Zone));
            if (objects != null)
                foreach (Zone gameObj in objects)
                {
                    if (gameObj.info.ID == ZoneID) return gameObj;
                }

            return null;
        }

        void NewZone(ZoneDefinition zonedef)
        {
            if (zonedef == null) return;
            var newgameObject = new UnityEngine.GameObject();
            var newZone = newgameObject.AddComponent<Zone>();
            newZone.SetInfo(zonedef);
        }
        void RefreshZone(string zoneID)
        {
            var objects = GameObject.FindObjectsOfType(typeof(Zone));
            if (objects != null)
                foreach (Zone gameObj in objects)
                {
                    if (gameObj.info.ID == zoneID)
                    {
                        foreach (KeyValuePair<BasePlayer, List<Zone>> pair in playerZones)
                        {
                            if (pair.Value.Contains(gameObj)) playerZones[pair.Key].Remove(gameObj);
                        }
                        GameObject.Destroy(gameObj);
                        if (zonedefinitions[zoneID] != null)
                        {
                            NewZone(zonedefinitions[zoneID]);
                        }
                        break;
                    }
                }
        }

        int GetRandom(int min, int max) { return UnityEngine.Random.Range(min, max); }

        FieldInfo GetZoneField(string name)
        {
            name = name.ToLower();
            foreach (FieldInfo fieldinfo in allZoneFields) { if (fieldinfo.Name == name) return fieldinfo; }
            return null;
        }
        static bool hasTag(BasePlayer player, string tagname)
        {
            if (playerZones[player] == null) { return false; }
            if (playerZones[player].Count == 0) { return false; }
            fieldInfo = typeof(ZoneDefinition).GetField(tagname, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (Zone zone in playerZones[player])
            {
                if (fieldInfo.GetValue(zone.info) != null)
                    return true;
            }
            return false;
        }



        BasePlayer FindPlayerByRadius(Vector3 position, float rad)
        {
            cachedColliders = Physics.OverlapSphere(position, rad, playersMask);
            foreach (Collider collider in cachedColliders)
            {
                if (collider.GetComponentInParent<BasePlayer>())
                    return collider.GetComponentInParent<BasePlayer>();
            }
            return null;
        }
        void CheckExplosivePosition(TimedExplosive explosive)
        {
            if (explosive == null) return;
            var objects = GameObject.FindObjectsOfType(typeof(Zone));
            if (objects != null)
                foreach (Zone zone in objects)
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
            if (playerZones[player] == null) playerZones[player] = new List<Zone>();
            if (!playerZones[player].Contains(zone)) playerZones[player].Add(zone);
            if (zone.info.enter_message != null) SendMessage(player, zone.info.enter_message);
            if (zone.info.eject != null && !isAdmin(player) && !zone.whiteList.Contains(player) && !zone.keepInList.Contains(player)) EjectPlayer(zone, player);
            Interface.CallHook("OnEnterZone", zone.info.ID, player);
        }
        static void OnExitZone(Zone zone, BasePlayer player)
        {
            if (playerZones[player].Contains(zone)) playerZones[player].Remove(zone);
            if (zone.info.leave_message != null) SendMessage(player, zone.info.leave_message);
            if (zone.keepInList.Contains(player)) AttractPlayer(zone, player);
            Interface.CallHook("OnExitZone", zone.info.ID, player);
        }

        static void EjectPlayer(Zone zone, BasePlayer player)
        {
            cachedDirection = player.transform.position - zone.transform.position;
            player.transform.position = zone.transform.position + (cachedDirection / cachedDirection.magnitude * (zone.GetComponent<UnityEngine.SphereCollider>().radius + 1f));
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { player.transform.position });
            player.TransformChanged();
        }
        static void AttractPlayer(Zone zone, BasePlayer player)
        {
            cachedDirection = player.transform.position - zone.transform.position;
            player.transform.position = zone.transform.position + (cachedDirection / cachedDirection.magnitude * (zone.GetComponent<UnityEngine.SphereCollider>().radius - 1f));
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { player.transform.position });
            player.TransformChanged();
        }
        static bool isAdmin(BasePlayer player)
        {
            if (player.net.connection.authLevel > 0)
                return true;
            return false;
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
            var newzoneinfo = new ZoneDefinition(player.transform.position);
            newzoneinfo.ID = GetRandom(1, 99999999).ToString();
            NewZone(newzoneinfo);
            if (zonedefinitions[newzoneinfo.ID] != null) storedData.ZoneDefinitions.Remove(zonedefinitions[newzoneinfo.ID]);
            zonedefinitions[newzoneinfo.ID] = newzoneinfo;
            LastZone[player] = newzoneinfo.ID;
            storedData.ZoneDefinitions.Add(zonedefinitions[newzoneinfo.ID]);
            SaveData();
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
        }
        [ChatCommand("zone_list")]
        void cmdChatZoneList(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "zone")) { SendMessage(player, "You don't have access to this command"); return; }
            SendMessage(player, "========== Zone list ==========");
            if (zonedefinitions.Count == 0) { SendMessage(player, "empty"); return; }
            foreach (KeyValuePair<string, ZoneDefinition> pair in zonedefinitions)
            {
                SendMessage(player, string.Format("{0} => {1} - {2}", pair.Key, pair.Value.name, pair.Value.Location.String()));
            }
        }
        [ChatCommand("zone")]
        void cmdChatZone(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, "zone")) { SendMessage(player, "You don't have access to this command"); return; }
            if (LastZone[player] == null) { SendMessage(player, "You must first say: /zone_edit XXXXXID"); return; }
            object value;

            if (args.Length < 2)
            {
                SendMessage(player, "/zone option value/reset");
                foreach (FieldInfo fieldinfo in allZoneFields)
                {
                    value = fieldinfo.GetValue(zonedefinitions[LastZone[player]]);
                    switch (fieldinfo.Name)
                    {
                        case "Location":
                            value = ((ZoneLocation)value).String();
                            break;
                        default:
                            if (value == null) value = "false";
                            break;
                    }
                    SendMessage(player, string.Format("{0} => {1}", fieldinfo.Name, value.ToString()));
                }
                return;
            }
            string editvalue;
            for (int i = 0; i < args.Length; i = i + 2)
            {

                cachedField = GetZoneField(args[i]);
                if (cachedField == null) continue;

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
                cachedField.SetValue(zonedefinitions[LastZone[player]], editvalue);
                if (args[i].ToLower() == "radius") { zonedefinitions[LastZone[player]].Location = new ZoneLocation(zonedefinitions[LastZone[player]].Location.GetPosition(), editvalue); }
                SendMessage(player, string.Format("{0} set to {1}", cachedField.Name, editvalue));
            }
            RefreshZone(LastZone[player]);
            SaveData();
        }
        static void SendMessage(BasePlayer player, string message) { player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", "ZoneManager", message), 1.0 }); }
    }
}
