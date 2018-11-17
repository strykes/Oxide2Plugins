using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Oxide.Game.Rust.Cui;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("ZoneManager", "Reneb / Nogrod", "2.5.09", ResourceId = 739)]
    public class ZoneManager : RustPlugin
    {
        #region Fields
        private DynamicConfigFile ZoneManagerData;
        private StoredData storedData;
        
        [PluginReference] Plugin PopupNotifications, Spawns;

        private static ZoneManager instance;

        private ZoneFlags disabledFlags = ZoneFlags.None;        

        private Hash<string, Zone> zoneObjects = new Hash<string, Zone>();
        private readonly Dictionary<string, Zone.Definition> zoneDefinitions = new Dictionary<string, Zone.Definition>();

        private readonly Dictionary<ulong, string> lastPlayerZone = new Dictionary<ulong, string>();

        private readonly Dictionary<BasePlayer, HashSet<Zone>> playerZones = new Dictionary<BasePlayer, HashSet<Zone>>();
        private readonly Dictionary<BaseCombatEntity, HashSet<Zone>> buildingZones = new Dictionary<BaseCombatEntity, HashSet<Zone>>();
        private readonly Dictionary<BaseNpc, HashSet<Zone>> npcZones = new Dictionary<BaseNpc, HashSet<Zone>>();
        private readonly Dictionary<ResourceDispenser, HashSet<Zone>> resourceZones = new Dictionary<ResourceDispenser, HashSet<Zone>>();
        private readonly Dictionary<BaseEntity, HashSet<Zone>> otherZones = new Dictionary<BaseEntity, HashSet<Zone>>();
        private readonly Dictionary<BasePlayer, ZoneFlags> playerTags = new Dictionary<BasePlayer, ZoneFlags>();
        
        private static readonly int playersMask = 131072;
        private static readonly Collider[] colBuffer = Vis.colBuffer;

        private static FieldInfo SearchLight_secondsRemaining;

        private const string permZone = "zonemanager.zone";
        private const string permIgnoreFlag = "zonemanager.ignoreflag.";
        #endregion

        #region Oxide Hooks       
        private void Loaded()
        {
            instance = this;

            lang.RegisterMessages(Messages, this);
            ZoneManagerData = Interface.Oxide.DataFileSystem.GetFile("ZoneManager");
            ZoneManagerData.Settings.Converters = new JsonConverter[] { new StringEnumConverter(), new UnityVector3Converter(), };

            permission.RegisterPermission(permZone, this);
            foreach (ZoneFlags flag in Enum.GetValues(typeof(ZoneFlags)))
                permission.RegisterPermission(permIgnoreFlag + flag.ToString().ToLower(), this);

            SearchLight_secondsRemaining = typeof(SearchLight).GetField("secondsRemaining", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            LoadVariables();
            LoadData();
        }

        private void OnServerInitialized() => InitializeZones();

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                CuiHelper.DestroyUi(player, ZMUI);

            for (int i = zoneObjects.Count - 1; i >= 0; i--)
            {
                var zone = zoneObjects.ElementAt(i);
                OnZoneDestroy(zone.Value);
                UnityEngine.Object.Destroy(zone.Value);
                zoneObjects.Remove(zone.Key);
            }

            instance = null;            
        }

        private void OnTerrainInitialized() => InitializeZones();          
        
        private void InitializeZones()
        {
            if (Initialized) return;
            foreach (Zone.Definition zoneDefinition in zoneDefinitions.Values)
                CreateNewZone(zoneDefinition);
            Initialized = true;
        }

        private void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            BasePlayer player = planner?.GetOwnerPlayer();
            if (player == null)
                return;

            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null)
                return;

            if (entity is BuildingBlock || entity is SimpleBuildingBlock)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoBuild, true))
                {
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
                    SendMessage(player, msg("noBuild", player.UserIDString));
                }
            }
            else
            {
                if (entity is BuildingPrivlidge)
                {
                    if (HasPlayerFlag(player, ZoneFlags.NoCup, false))
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        SendMessage(player, msg("noCup", player.UserIDString));
                    }
                }
                else
                {
                    if (HasPlayerFlag(player, ZoneFlags.NoDeploy, true))
                    {
                        entity.Kill(BaseNetworkable.DestroyMode.Gib);
                        SendMessage(player, msg("noDeploy", player.UserIDString));
                    }
                }
            }
        }

        private object OnStructureUpgrade(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoUpgrade, true))
            {
                SendMessage(player, msg("noUpgrade", player.UserIDString));
                return false;
            }
            return null;
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        {
            BasePlayer player = deployer.GetOwnerPlayer();
            if (player == null)
                return;

            if (HasPlayerFlag(player, ZoneFlags.NoDeploy, true))
            {
                deployedEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                SendMessage(player, msg("noDeploy", player.UserIDString));
            }            
        }

        private void OnRunPlayerMetabolism(PlayerMetabolism metabolism, BaseCombatEntity ownerEntity, float delta)
        {
            BasePlayer player = ownerEntity as BasePlayer;
            if (player == null)
                return;

            if (metabolism.bleeding.value > 0 && HasPlayerFlag(player, ZoneFlags.NoBleed, false))
                metabolism.bleeding.value = 0f;
            if (metabolism.oxygen.value < 1 && HasPlayerFlag(player, ZoneFlags.NoDrown, false))
                metabolism.oxygen.value = 1;
        }

        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
                return null;

            if (HasPlayerFlag(player, ZoneFlags.NoChat, true))
            {
                SendMessage(player, msg("noChat", player.UserIDString));
                return false;
            }
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null || arg.cmd?.Name == null)
                return null;

            if (arg.cmd.Name == "kill" && HasPlayerFlag(player, ZoneFlags.NoSuicide, false))
            {
                SendMessage(player, msg("noSuicide", player.UserIDString));
                return false;
            }
            return null;
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.KillSleepers, true))
            {
                player.Die();
                return;
            }

            if (HasPlayerFlag(player, ZoneFlags.EjectSleepers, true))
            {
                HashSet<Zone> zones;
                if (!playerZones.TryGetValue(player, out zones) || zones.Count == 0)
                    return;
                foreach (Zone zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.EjectSleepers))
                    {
                        EjectPlayer(zone, player);
                        return;
                    }
                }
            }
        }
        
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity == null || entity.GetComponent<ResourceDispenser>() != null)
                return;

            BasePlayer attacker = hitinfo.InitiatorPlayer;
            BasePlayer victim = entity as BasePlayer;

            if (victim != null)
            {
                if (victim.IsSleeping() && HasPlayerFlag(victim, ZoneFlags.SleepGod, false))                
                    CancelDamage(hitinfo);                
                else if (attacker != null)
                {
                    if (attacker.userID < 76560000000000000L) return;
                    if (HasPlayerFlag(victim, ZoneFlags.PvpGod, false))
                        CancelDamage(hitinfo);
                    else if (HasPlayerFlag(attacker, ZoneFlags.PvpGod, false))
                        CancelDamage(hitinfo);
                }
                else if (HasPlayerFlag(victim, ZoneFlags.PveGod, false))
                    CancelDamage(hitinfo);
                else if (hitinfo.Initiator is FireBall && HasPlayerFlag(victim, ZoneFlags.PvpGod, false))
                    CancelDamage(hitinfo);
                return;
            }

            BaseNpc baseNpc = entity as BaseNpc;
            if (baseNpc != null)
            {
                HashSet<Zone> zones;
                if (!npcZones.TryGetValue(baseNpc, out zones)) return;
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.NoPve))
                    {
                        if (hitinfo.InitiatorPlayer != null && CanBypass(hitinfo.InitiatorPlayer, ZoneFlags.NoPve)) continue;
                        CancelDamage(hitinfo);
                        break;
                    }
                }
                return;
            }

            if (!(entity is LootContainer) && !(entity is BaseHelicopter))
            {
                ResourceDispenser resource = entity.GetComponent<ResourceDispenser>();
                HashSet<Zone> zones;
                if (!buildingZones.TryGetValue(entity, out zones) && (resource == null || !resourceZones.TryGetValue(resource, out zones)))
                    return;
                foreach (Zone zone in zones)
                {
                    if (HasZoneFlag(zone, ZoneFlags.UnDestr))
                    {
                        if (hitinfo.InitiatorPlayer != null && CanBypass(hitinfo.InitiatorPlayer, ZoneFlags.UnDestr)) continue;
                        CancelDamage(hitinfo);
                        break;
                    }
                }
            }
        }

        private void OnEntityKill(BaseNetworkable networkable)
        {
            BaseEntity entity = networkable as BaseEntity;
            if (entity == null)
                return;

            ResourceDispenser resource = entity.GetComponent<ResourceDispenser>();
            if (resource != null)
            {
                HashSet<Zone> zones;
                if (resourceZones.TryGetValue(resource, out zones))
                    OnResourceExitZone(null, resource, true);
                return;
            }

            BasePlayer player = entity as BasePlayer;
            if (player != null)
            {
                HashSet<Zone> zones;
                if (playerZones.TryGetValue(player, out zones))
                    OnPlayerExitZone(null, player, true);
                return;
            }

            BaseNpc npc = entity as BaseNpc;
            if (npc != null)
            {
                HashSet<Zone> zones;
                if (npcZones.TryGetValue(npc, out zones))
                    OnNpcExitZone(null, npc, true);
                return;
            }

            BaseCombatEntity building = entity as BaseCombatEntity;
            if (building != null && !(entity is LootContainer) && !(entity is BaseHelicopter))
            {
                HashSet<Zone> zones;
                if (buildingZones.TryGetValue(building, out zones))
                    OnBuildingExitZone(null, building, true);
            }
            else
            {
                HashSet<Zone> zones;
                if (otherZones.TryGetValue(entity, out zones))
                    OnOtherExitZone(null, entity, true);
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BaseCorpse)
            {
                timer.Once(2f, () =>
                {
                    HashSet<Zone> zones;
                    if (entity == null || entity.IsDestroyed || !entity.GetComponent<ResourceDispenser>() || !resourceZones.TryGetValue(entity.GetComponent<ResourceDispenser>(), out zones)) return;
                    foreach (Zone zone in zones)
                    {
                        if (HasZoneFlag(zone, ZoneFlags.NoCorpse) && !CanBypass((entity as BaseCorpse).OwnerID, ZoneFlags.NoCorpse))
                        {
                            entity.Kill(BaseNetworkable.DestroyMode.None);
                            break;
                        }
                    }
                });
            }
            else if (entity is BuildingBlock && zoneObjects != null)
            {
                BuildingBlock block = (BuildingBlock)entity;
                if (EntityHasFlag(entity as BuildingBlock, ZoneFlags.NoStability) && !CanBypass((entity as BuildingBlock).OwnerID, ZoneFlags.NoStability))
                    block.grounded = true;
            }
            else if (entity is LootContainer || entity is JunkPile || entity is BaseNpc || entity is WorldItem || entity is DroppedItem || entity is DroppedItemContainer)
                timer.In(2, () => CheckSpawnedEntity(entity));
        }         
       
        private object CanBeWounded(BasePlayer player, HitInfo hitinfo) => HasPlayerFlag(player, ZoneFlags.NoWounded, false) ? (object)false : null;

        private object CanUpdateSign(BasePlayer player, Signage sign)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoSignUpdates, false))
            {
                SendMessage(player, msg("noSignUpdates", player.UserIDString));
                return false;
            }
            return null;
        }

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoOvenToggle, false))
            {
                SendMessage(player, msg("noOvenToggle", player.UserIDString));
                return false;
            }
            return null;
        }        

        private object CanUseVending(VendingMachine machine, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoVending, false))
            {
                SendMessage(player, msg("noVending", player.UserIDString));
                return false;
            }
            return null;
        }

        private object CanHideStash(StashContainer stash, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoStash, false))
            {
                SendMessage(player, msg("noStash", player.UserIDString));
                return false;
            }
            return null;
        }

        private object CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
        {
            BasePlayer player = itemCrafter.GetComponent<BasePlayer>();
            if (player != null)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoCraft, false))
                {
                    SendMessage(player, msg("noCraft", player.UserIDString));
                    return false;
                }
            }
            return null;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mount)
        {
            if (player != null)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoMount, false))
                {
                    SendMessage(player, msg("noMount", player.UserIDString));
                    return false;
                }
            }
            return null;
        }

        #region Looting Hooks
        private object CanLootPlayer(BasePlayer target, BasePlayer looter) => OnLootPlayerInternal(looter, target);

        private void OnLootPlayer(BasePlayer looter, BasePlayer target) => OnLootPlayerInternal(looter, target);

        private object OnLootPlayerInternal(BasePlayer looter, BasePlayer target)
        {
            if (HasPlayerFlag(looter, ZoneFlags.NoPlayerLoot, false) || (target != null && HasPlayerFlag(target, ZoneFlags.NoPlayerLoot, false)))
            {
                SendMessage(looter, msg("noLoot", looter.UserIDString));
                NextTick(looter.EndLooting);
                return false;
            }
            return null;
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity is DroppedItemContainer || entity is LootableCorpse)
                OnLootInternal(player, ZoneFlags.NoPlayerLoot);
            if (entity is StorageContainer)
                OnLootInternal(player, ZoneFlags.NoBoxLoot);            
        }

        private object CanLootEntity(LootableCorpse corpse, BasePlayer player) => CanLootInternal(player, ZoneFlags.NoPlayerLoot);

        private object CanLootEntity(DroppedItemContainer container, BasePlayer player) => CanLootInternal(player, ZoneFlags.NoPlayerLoot);

        private object CanLootEntity(StorageContainer container, BasePlayer player) => CanLootInternal(player, ZoneFlags.NoBoxLoot);

        private object CanLootInternal(BasePlayer player, ZoneFlags flag)
        {
            if (HasPlayerFlag(player, flag, false))
            {
                SendMessage(player, msg("noLoot", player.UserIDString));
                return false;
            }
            return null;
        }

        private void OnLootInternal(BasePlayer player, ZoneFlags flag)
        {
            if (HasPlayerFlag(player, flag, false))
            {
                SendMessage(player, msg("noLoot", player.UserIDString));
                NextTick(player.EndLooting);
            }
        }
        #endregion

        #region Pickup Hooks
        private object CanPickupEntity(BaseCombatEntity entity, BasePlayer player) => CanPickupInternal(player, ZoneFlags.NoEntityPickup);

        private object CanPickupLock(BasePlayer player, BaseLock baseLock) => CanPickupInternal(player, ZoneFlags.NoEntityPickup);

        private object OnItemPickup(Item item, BasePlayer player) => CanPickupInternal(player, ZoneFlags.NoPickup);

        private object CanPickupInternal(BasePlayer player, ZoneFlags flag)
        {
            if (HasPlayerFlag(player, flag, false))
            {
                SendMessage(player, msg("noPickup", player.UserIDString));
                return false;
            }
            return null;
        }
        #endregion

        #region Gather Hooks        
        private object CanLootEntity(ResourceContainer container, BasePlayer player) => OnGatherInternal(player);

        private object OnCollectiblePickup(Item item, BasePlayer player) => OnGatherInternal(player);

        private object OnCropGather(PlantEntity plant, Item item, BasePlayer player) => OnGatherInternal(player);

        private object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item) => OnGatherInternal(entity?.ToPlayer());

        private object OnGatherInternal(BasePlayer player)
        {
            if (player != null)
            {
                if (HasPlayerFlag(player, ZoneFlags.NoGather, false))
                {
                    SendMessage(player, msg("noGather", player.UserIDString));
                    return false;
                }
            }
            return null;
        }
        #endregion

        #region Targeting Hooks
        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity entity) => OnTargetPlayerInternal(entity?.ToPlayer(), ZoneFlags.NoTurretTargeting);

        private object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity) => OnTargetPlayerInternal(entity?.ToPlayer(), ZoneFlags.NoAPCTargeting);

        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (HasPlayerFlag(player, ZoneFlags.NoHeliTargeting, false))
            {
                heli.interestZoneOrigin = heli.GetRandomPatrolDestination();
                return false;
            }
            return null;
        }

        private object CanHelicopterStrafeTarget(PatrolHelicopterAI heli, BasePlayer player) => OnTargetPlayerInternal(player, ZoneFlags.NoHeliTargeting);

        private object OnHelicopterTarget(HelicopterTurret turret, BaseCombatEntity entity) => OnTargetPlayerInternal(entity?.ToPlayer(), ZoneFlags.NoHeliTargeting);
        
        private object OnNpcPlayerTarget(NPCPlayerApex npcPlayer, BaseEntity entity) => OnTargetPlayerInternal(entity?.ToPlayer(), ZoneFlags.NoNPCTargeting);

        private object OnTargetPlayerInternal(BasePlayer player, ZoneFlags flag)
        {
            if (player != null)
            {
                if (HasPlayerFlag(player, flag, false))                
                    return false;                
            }
            return null;
        }
        #endregion

        #region Additional KillSleeper Checks
        private void OnPlayerSleep(BasePlayer player)
        {
            if (player == null)
                return;

            player.Invoke(() => KillSleepingPlayer(player), 5f);
        }
       
        private void KillSleepingPlayer(BasePlayer player)
        {
            if (player == null || !player.IsSleeping())
                return;

            if (HasPlayerFlag(player, ZoneFlags.KillSleepers, true))
            {
                if (player.IsConnected)
                    OnPlayerSleep(player);
                else player.Die();
            }
        }
        #endregion
        #endregion

        #region Zone Management
        #region Zone Component
        public class Zone : MonoBehaviour
        {
            public Definition definition;            
            public ZoneFlags disabledFlags = ZoneFlags.None;

            public readonly HashSet<ulong> WhiteList = new HashSet<ulong>();
            public readonly HashSet<ulong> KeepInList = new HashSet<ulong>();

            private HashSet<BasePlayer> players = new HashSet<BasePlayer>();
            private HashSet<BaseCombatEntity> buildings = new HashSet<BaseCombatEntity>();

            private List<TriggerBase> triggers = new List<TriggerBase>();
            private bool lightsOn;

            #region Initialization
            private void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                gameObject.name = "Zone Manager";
                enabled = false;

                Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            private void OnDestroy()
            {
                Cleanup();

                if (instance != null)
                    instance.OnZoneDestroy(this);

                Destroy(gameObject);
            }

            private void Cleanup()
            {
                if (IsInvoking(CheckEntities))
                    CancelInvoke(CheckEntities);

                if (IsInvoking(CheckAlwaysLights))
                    CancelInvoke(CheckAlwaysLights);

                if (IsInvoking(CheckLights))
                    CancelInvoke(CheckLights);

                foreach (TriggerBase trigger in triggers)
                {
                    if (trigger.gameObject.name.StartsWith("Trigger"))
                        Destroy(trigger.gameObject);
                    else Destroy(trigger);
                }

                triggers.Clear();
            }
            
            public void SetInfo(Definition definition)
            {
                this.definition = definition;
                Cleanup();
                InitializeZone(definition.Enabled);
            }

            public void InitializeZone(bool active)
            {
                if (definition == null)
                    return;

                gameObject.name = $"Zone Manager({definition.Id})";
                transform.position = definition.Location;
                transform.rotation = Quaternion.Euler(definition.Rotation);
                UpdateCollider();

                RegisterPermission();
                InitializeAutoLights();
                InitializeRadiation();
                InitializeComfort();
                InitializeTemperature();

                SetZoneStatus(active);
            }

            private void UpdateCollider()
            {
                SphereCollider sphereCollider = gameObject.GetComponent<SphereCollider>();
                BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();
                if (definition.Size != Vector3.zero)
                {
                    if (sphereCollider != null)
                        Destroy(sphereCollider);

                    if (boxCollider == null)
                    {
                        boxCollider = gameObject.AddComponent<BoxCollider>();
                        boxCollider.isTrigger = true;                        
                    }
                    boxCollider.size = definition.Size;
                }
                else
                {
                    if (boxCollider != null)
                        Destroy(boxCollider);

                    if (sphereCollider == null)
                    {
                        sphereCollider = gameObject.AddComponent<SphereCollider>();
                        sphereCollider.isTrigger = true;
                    }
                    sphereCollider.radius = definition.Radius;
                }
            }   
            
            private void RegisterPermission()
            {
                if (!string.IsNullOrEmpty(definition.Permission) && !instance.permission.PermissionExists(definition.Permission))
                    instance.permission.RegisterPermission(definition.Permission, instance);
            }

            private void InitializeAutoLights()
            {
                if (HasZoneFlag(ZoneFlags.AlwaysLights))
                {
                    if (IsInvoking(CheckAlwaysLights))
                        CancelInvoke(CheckAlwaysLights);
                    InvokeRepeating(CheckAlwaysLights, 5f, 60f);
                }
                else if (HasZoneFlag(ZoneFlags.AutoLights))
                {
                    float currentTime = GetSkyHour();

                    if (currentTime > instance.AutolightOffTime && currentTime < instance.AutolightOnTime)
                        lightsOn = true;
                    else
                        lightsOn = false;
                    if (IsInvoking(CheckLights))
                        CancelInvoke(CheckLights);
                    InvokeRepeating(CheckLights, 5f, 30f);
                }
            }

            #region Create Effect Triggers
            private void InitializeRadiation()
            {
                if (definition.Radiation > 0)
                {
                    GameObject radiationObject = gameObject;

                    if (definition.Size != Vector3.zero)
                    {
                        radiationObject = new GameObject();
                        radiationObject.name = $"Trigger Radiation {definition.Id}";
                        radiationObject.transform.position = gameObject.transform.position;

                        SphereCollider collider = radiationObject.AddComponent<SphereCollider>();
                        collider.radius = definition.Radius;
                        collider.isTrigger = true;
                    }

                    TriggerRadiation radiation = radiationObject.GetComponent<TriggerRadiation>() ?? radiationObject.AddComponent<TriggerRadiation>();                
                    radiation.RadiationAmountOverride = definition.Radiation;
                    radiation.radiationSize = definition.Radius;
                    radiation.interestLayers = playersMask;
                    radiation.enabled = true;

                    if (!triggers.Contains(radiation))
                        triggers.Add(radiation);
                }               
            }

            private void InitializeComfort()
            {
                if (definition.Comfort > 0)
                {
                    GameObject comfortObject = gameObject;

                    if (definition.Size != Vector3.zero)
                    {
                        comfortObject = new GameObject();
                        comfortObject.name = $"Trigger Comfort {definition.Id}";
                        comfortObject.transform.position = gameObject.transform.position;

                        SphereCollider collider = comfortObject.AddComponent<SphereCollider>();
                        collider.radius = definition.Radius;
                        collider.isTrigger = true;
                    }

                    TriggerComfort comfort = comfortObject.GetComponent<TriggerComfort>() ?? comfortObject.AddComponent<TriggerComfort>();
                    comfort.baseComfort = definition.Comfort;
                    comfort.triggerSize = definition.Radius;
                    comfort.interestLayers = playersMask;
                    comfort.enabled = true;

                    if (!triggers.Contains(comfort))
                        triggers.Add(comfort);
                }                
            }

            private void InitializeTemperature()
            {
                if (definition.Temperature != 0)
                {
                    GameObject temperatureObject = gameObject;

                    if (definition.Size != Vector3.zero)
                    {
                        temperatureObject = new GameObject();
                        temperatureObject.name = $"Trigger Temperature {definition.Id}";
                        temperatureObject.transform.position = gameObject.transform.position;

                        SphereCollider collider = temperatureObject.AddComponent<SphereCollider>();
                        collider.radius = definition.Radius;
                        collider.isTrigger = true;
                    }

                    TriggerTemperature temperature = temperatureObject.GetComponent<TriggerTemperature>() ?? temperatureObject.AddComponent<TriggerTemperature>();
                    temperature.Temperature = definition.Temperature;
                    temperature.triggerSize = definition.Radius;
                    temperature.interestLayers = playersMask;
                    temperature.enabled = true;

                    if (!triggers.Contains(temperature))
                        triggers.Add(temperature);
                }                
            }
            #endregion

            private void SetZoneStatus(bool active)
            {
                if (active)
                {
                    if (!IsInvoking(CheckEntities))
                        InvokeRandomized(CheckEntities, 1f, 5f, 1f);
                    
                    gameObject.SetActive(true);                    
                }
                else
                {                    
                    if (IsInvoking(CheckEntities))
                        CancelInvoke(CheckEntities);

                    if (IsInvoking(CheckAlwaysLights))
                        CancelInvoke(CheckAlwaysLights);

                    if (IsInvoking(CheckLights))
                        CancelInvoke(CheckLights);

                    foreach (TriggerBase trigger in triggers)
                    {
                        foreach (BasePlayer player in players)
                            player.LeaveTrigger(trigger);

                        Destroy(trigger);
                    }
                    triggers.Clear();

                    gameObject.SetActive(false);                    
                }
            }
            #endregion

            #region InvokeHandler
            private bool IsInvoking(Action action) => InvokeHandler.IsInvoking(this, action);

            private void Invoke(Action action, float time) => InvokeHandler.Invoke(this, action, time);

            private void InvokeRepeating(Action action, float time, float repeat) => InvokeHandler.InvokeRepeating(this, action, time, repeat);

            private void InvokeRandomized(Action action, float time, float repeat, float random) => InvokeHandler.InvokeRandomized(this, action, time, repeat, random);

            private void CancelInvoke(Action action) => InvokeHandler.CancelInvoke(this, action);
            #endregion

            #region Flag Monitoring
            private bool HasZoneFlag(ZoneFlags flag) => ((disabledFlags & flag) == flag) ? false : (definition.Flags & ~disabledFlags & flag) == flag;
            
            private void CheckLights()
            {
                float currentTime = GetSkyHour();
                if (currentTime > instance.AutolightOffTime && currentTime < instance.AutolightOnTime)
                {
                    if (!lightsOn) return;
                    foreach (var building in buildings)
                    {
                        SearchLight searchLight = building as SearchLight;
                        if (searchLight != null)
                        {
                            SearchLight_secondsRemaining.SetValue(searchLight, 0f);
                            searchLight.SetFlag(BaseEntity.Flags.On, false, false);
                            continue;
                        }
                        BaseOven oven = building as BaseOven;
                        if (oven != null && !oven.IsInvoking(oven.Cook))
                        {
                            oven.SetFlag(BaseEntity.Flags.On, false);
                            continue;
                        }
                        Door door = building as Door;
                        if (door != null && door.PrefabName.Contains("shutter"))
                            door.SetFlag(BaseEntity.Flags.Open, true);
                        
                        
                    }
                    foreach (var player in players)
                    {
                        if (player.userID >= 76560000000000000L || player.inventory?.containerWear?.itemList == null) continue;
                        List<Item> items = player.inventory.containerWear.itemList;
                        foreach (var item in items)
                        {
                            if (!item.info.shortname.Equals("hat.miner") && !item.info.shortname.Equals("hat.candle")) continue;
                            item.SwitchOnOff(false, player);
                            player.inventory.ServerUpdate(0f);
                            break;
                        }
                    }
                    lightsOn = false;
                }
                else
                {
                    if (lightsOn) return;
                    foreach (var building in buildings)
                    {
                        SearchLight searchLight = building as SearchLight;
                        if (searchLight != null)
                        {
                            SearchLight_secondsRemaining.SetValue(searchLight, 9999999999f);
                            searchLight.SetFlag(BaseEntity.Flags.On, true, false);
                            continue;
                        }
                        BaseOven oven = building as BaseOven;
                        if (oven != null && !oven.IsInvoking(oven.Cook))
                        {
                            oven.SetFlag(BaseEntity.Flags.On, true);
                            continue;
                        }
                        Door door = building as Door;
                        if (door != null && door.PrefabName.Contains("shutter"))
                            door.SetFlag(BaseEntity.Flags.Open, false);
                    }
                    ItemDefinition fuel = ItemManager.FindItemDefinition("lowgradefuel");
                    foreach (BasePlayer player in players)
                    {
                        if (player.userID >= 76560000000000000L || player.inventory?.containerWear?.itemList == null) continue; // only npc
                        List<Item> items = player.inventory.containerWear.itemList;
                        foreach (var item in items)
                        {
                            if (!item.info.shortname.Equals("hat.miner") && !item.info.shortname.Equals("hat.candle"))
                                continue;
                            if (item.contents == null)
                                item.contents = new ItemContainer();
                            Item[] array = item.contents.itemList.ToArray();
                            for (var i = 0; i < array.Length; i++)
                                array[i].Remove(0f);
                            Item newItem = ItemManager.Create(fuel, 100);
                            newItem.MoveToContainer(item.contents);
                            item.SwitchOnOff(true, player);
                            player.inventory.ServerUpdate(0f);
                            break;
                        }
                    }
                    lightsOn = true;
                }
            }

            private void CheckAlwaysLights()
            {
                foreach (var building in buildings)
                {
                    BaseOven oven = building as BaseOven;
                    if (oven == null || oven.IsInvoking(oven.Cook))
                        continue;
                    oven.SetFlag(BaseEntity.Flags.On, true);
                }
            }

            public void OnEntityKill(BaseCombatEntity entity)
            {
                BasePlayer player = entity as BasePlayer;
                if (player != null)
                    players.Remove(player);
                else if (entity != null && !(entity is LootContainer) && !(entity is BaseHelicopter) && !(entity is BaseNpc))
                    buildings.Remove(entity);
            }
            #endregion

            #region Trigger Detection  
            // Need something better then this, it is required to detect when a player is teleported out of the zone and put to sleep since the BasePlayer.Sleep() method destroys the players RigidBody thus making the players exit from the zone undetectable by any trigger/collision messages...            
            private void CheckEntities()
            {
                HashSet<BasePlayer> oldPlayers = players;
                players = new HashSet<BasePlayer>();

                int entities = definition.Size != Vector3.zero ? Physics.OverlapBoxNonAlloc(definition.Location, definition.Size / 2, colBuffer, Quaternion.Euler(definition.Rotation), playersMask) : Physics.OverlapSphereNonAlloc(definition.Location, definition.Radius, colBuffer, playersMask);

                for (var i = 0; i < entities; i++)
                {
                    BasePlayer player = colBuffer[i].GetComponentInParent<BasePlayer>();
                    colBuffer[i] = null;
                    if (player != null)
                    {
                        if (players.Add(player) && !oldPlayers.Contains(player))
                            instance.OnPlayerEnterZone(this, player);
                    }
                }

                foreach (BasePlayer player in oldPlayers)
                {
                    if (!players.Contains(player))
                        instance.OnPlayerExitZone(this, player);
                }
            }

            private void OnTriggerEnter(Collider col)
            {                
                BasePlayer player = col.GetComponent<BasePlayer>();
                if (player != null)
                {                    
                    OnPlayerEnter(player);
                    return;
                }
                else if (!col.transform.CompareTag("MeshColliderBatch"))
                    OnColliderEnterZone(col);
                else
                {
                    MeshColliderBatch colliderBatch = col.GetComponent<MeshColliderBatch>();
                    if (colliderBatch == null)
                        return;

                    var colliders = colliderBatch.meshLookup.src.data;
                    Bounds bounds = gameObject.GetComponent<Collider>().bounds;
                    foreach (var instance in colliders)
                    {
                        if (instance.collider && instance.collider.bounds.Intersects(bounds))
                            OnColliderEnterZone(instance.collider);
                    }
                }
            }

            private void OnColliderEnterZone(Collider col)
            {
                if (HasZoneFlag(ZoneFlags.NoDecay))
                {
                    DecayEntity decayEntity = col.GetComponentInParent<DecayEntity>();
                    if (decayEntity != null)
                        decayEntity.decay = null;
                }

                ResourceDispenser resourceDispenser = col.GetComponentInParent<ResourceDispenser>();
                if (resourceDispenser != null)
                {
                    instance.OnResourceEnterZone(this, resourceDispenser);
                    return;
                }

                BaseEntity entity = col.GetComponentInParent<BaseEntity>();
                if (entity == null)
                    return;

                BaseNpc npc = entity as BaseNpc;
                if (npc != null)
                {
                    instance.OnNpcEnterZone(this, npc);
                    return;
                }

                BaseCombatEntity combatEntity = entity as BaseCombatEntity;
                
                if (combatEntity != null && !(entity is LootContainer) && !(entity is BaseHelicopter))
                {
                    buildings.Add(combatEntity);
                    instance.OnBuildingEnterZone(this, combatEntity);
                }
                else instance.OnOtherEnterZone(this, entity);
            }

            private void OnTriggerExit(Collider col)
            {
                BasePlayer player = col.GetComponent<BasePlayer>();
                if (player != null)
                {                   
                    OnPlayerExit(player);
                    return;
                }
                else if(!col.transform.CompareTag("MeshColliderBatch"))
                    OnColliderExitZone(col);
                else
                {
                    MeshColliderBatch colliderBatch = col.GetComponent<MeshColliderBatch>();
                    if (colliderBatch == null)
                        return;

                    var colliders = colliderBatch.meshLookup.src.data;
                    Bounds bounds = gameObject.GetComponent<Collider>().bounds;
                    foreach (var instance in colliders)
                    {
                        if (instance.collider && instance.collider.bounds.Intersects(bounds))
                            OnColliderExitZone(instance.collider);
                    }
                }
            }

            private void OnColliderExitZone(Collider col)
            {
                DecayEntity decayEntity = col.GetComponentInParent<DecayEntity>();
                if (decayEntity != null && decayEntity.decay == null)
                    decayEntity.decay = PrefabAttribute.server.Find<Decay>(decayEntity.prefabID);

                ResourceDispenser resourceDispenser = col.GetComponentInParent<ResourceDispenser>();
                if (resourceDispenser != null)
                {
                    instance.OnResourceExitZone(this, resourceDispenser);
                    return;
                }

                BaseEntity entity = col.GetComponentInParent<BaseEntity>();
                if (entity == null)
                    return;

                BaseNpc npc = entity as BaseNpc;
                if (npc != null)
                {
                    instance.OnNpcExitZone(this, npc);
                    return;
                }

                BaseCombatEntity combatEntity = entity as BaseCombatEntity;
                if (combatEntity != null && !(entity is LootContainer) && !(entity is BaseHelicopter))
                {
                    buildings.Remove(combatEntity);
                    instance.OnBuildingExitZone(this, combatEntity);
                    return;
                }

                instance.OnOtherExitZone(this, entity);
            }

            private void OnPlayerEnter(BasePlayer player)
            {
                if (player == null)
                    return;

                if (players.Add(player))
                {
                    instance.OnPlayerEnterZone(this, player);

                    if (definition.Size != Vector3.zero)
                    {
                        foreach (TriggerBase trigger in triggers)
                        {
                            if (trigger != null)
                            {
                                if (trigger.entityContents == null)
                                    trigger.entityContents = new HashSet<BaseEntity>();

                                if (!trigger.entityContents.Contains(player))
                                    player.EnterTrigger(trigger);
                            }
                        }
                    }
                }
            }

            private void OnPlayerExit(BasePlayer player)
            {
                if (player == null)
                    return;

                if (players.Remove(player))
                {
                    instance.OnPlayerExitZone(this, player);

                    if (definition.Size != Vector3.zero)
                    {
                        foreach (TriggerBase trigger in triggers)
                        {
                            if (trigger != null)
                            {
                                if (trigger.entityContents == null)
                                    return;

                                if (!trigger.entityContents.Contains(player))
                                    player.EnterTrigger(trigger);
                            }
                        }
                    }
                }
            }
            #endregion

            #region Zone Definition
            public class Definition
            {
                public string Name;
                public float Radius;
                public float Radiation;
                public float Comfort;
                public float Temperature;
                public Vector3 Location;
                public Vector3 Size;
                public Vector3 Rotation;
                public string Id;
                public string EnterMessage;
                public string LeaveMessage;
                public string Permission;
                public string EjectSpawns;
                public bool Enabled = true;
                public ZoneFlags Flags;

                public Definition() { }

                public Definition(Vector3 position)
                {
                    Radius = 20f;
                    Location = position;
                }
            }
            #endregion
        }
        #endregion
       
        private void OnZoneDestroy(Zone zone)
        {
            HashSet<Zone> zones;
            foreach (var key in playerZones.Keys.ToArray())
                if (playerZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnPlayerExitZone(zone, key);
            foreach (var key in buildingZones.Keys.ToArray())
                if (buildingZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnBuildingExitZone(zone, key);
            foreach (var key in npcZones.Keys.ToArray())
                if (npcZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnNpcExitZone(zone, key);
            foreach (var key in resourceZones.Keys.ToArray())
                if (resourceZones.TryGetValue(key, out zones) && zones.Contains(zone))
                    OnResourceExitZone(zone, key);
            foreach (var key in otherZones.Keys.ToArray())
            {
                if (!otherZones.TryGetValue(key, out zones))
                {
                    Puts("Zone: {0} Entity: {1} ({2}) {3}", zone.definition.Id, key.GetType(), key.net?.ID, key.IsDestroyed);
                    continue;
                }
                if (zones.Contains(zone))
                    OnOtherExitZone(zone, key);
            }
        }
        #endregion

        #region Flags
        [Flags]
        public enum ZoneFlags : ulong
        {
            None = 0L,
            AutoLights = 1UL,
            Eject = 1UL << 1,
            PvpGod = 1UL << 2,
            PveGod = 1UL << 3,
            SleepGod = 1UL << 4,
            UnDestr = 1UL << 5,
            NoBuild = 1UL << 6,
            NoTp = 1UL << 7,
            NoChat = 1UL << 8,
            NoGather = 1UL << 9,
            NoPve = 1UL << 10,
            NoWounded = 1UL << 11,
            NoDecay = 1UL << 12,
            NoDeploy = 1UL << 13,
            NoKits = 1UL << 14,
            NoBoxLoot = 1UL << 15,
            NoPlayerLoot = 1UL << 16,
            NoCorpse = 1UL << 17,
            NoSuicide = 1UL << 18,
            NoRemove = 1UL << 19,
            NoBleed = 1UL << 20,
            KillSleepers = 1UL << 21,
            NpcFreeze = 1UL << 22,
            NoDrown = 1UL << 23,
            NoStability = 1UL << 24,
            NoUpgrade = 1UL << 25,
            EjectSleepers = 1UL << 26,
            NoPickup = 1UL << 27,
            NoCollect = 1UL << 28,
            NoDrop = 1UL << 29,
			Kill = 1UL << 30,
            NoCup = 1UL << 31,
            AlwaysLights = 1UL << 32,
            NoTrade = 1UL << 33,
            NoShop = 1UL << 34,
            NoSignUpdates = 1UL << 35,
            NoOvenToggle = 1UL << 36,
            NoLootSpawns = 1UL << 37,
            NoNPCSpawns = 1UL << 38,
            NoVending = 1UL << 39,
            NoStash = 1UL << 40,
            NoCraft = 1UL << 41,
            NoHeliTargeting = 1UL << 42,
            NoTurretTargeting = 1UL << 43,
            NoAPCTargeting = 1UL << 44,
            NoNPCTargeting = 1UL << 45,
            NoEntityPickup = 1UL << 46,
            NoMount = 1UL << 47,
        }

        private bool HasZoneFlag(Zone zone, ZoneFlags flag) => ((disabledFlags & flag) == flag) ? false : (zone.definition.Flags & ~zone.disabledFlags & flag) == flag;        

        private static bool HasAnyFlag(ZoneFlags flags, ZoneFlags flag) => (flags & flag) != ZoneFlags.None;        

        private static bool HasAnyZoneFlag(Zone zone) => (zone.definition.Flags & ~zone.disabledFlags) != ZoneFlags.None;        

        private static void AddZoneFlag(Zone.Definition zone, ZoneFlags flag) => zone.Flags |= flag;        

        private static void RemoveZoneFlag(Zone.Definition zone, ZoneFlags flag) => zone.Flags &= ~flag;        
        #endregion        

        #region External Plugin Hooks        
        private object canRedeemKit(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoKits, false) ? "You may not redeem a kit inside this area" : null;        

        private object CanTeleport(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoTp, false) ? "You may not teleport in this area" : null;        

        private object canRemove(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoRemove, false) ? "You may not use the remover tool in this area" : null;
        
        private bool CanChat(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoChat, false) ? false : true;
        
        private object CanTrade(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoTrade, false) ? "You may not trade in this area" : null;
        
        private object canShop(BasePlayer player) => HasPlayerFlag(player, ZoneFlags.NoShop, false) ? "You may not use the store in this area" : null;        
        #endregion

        #region Zone Editing
        private void UpdateZoneDefinition(Zone.Definition zone, string[] args, BasePlayer player = null)
        {
            for (var i = 0; i < args.Length; i = i + 2)
            {
                object editvalue;
                switch (args[i].ToLower())
                {
                    case "name":
                        editvalue = zone.Name = args[i + 1];
                        break;
                    case "id":
                        editvalue = zone.Id = args[i + 1];
                        break;
                    case "comfort":
                        editvalue = zone.Comfort = Convert.ToSingle(args[i + 1]);
                        break;
                    case "temperature":
                        editvalue = zone.Temperature = Convert.ToSingle(args[i + 1]);
                        break;
                    case "radiation":
                        editvalue = zone.Radiation = Convert.ToSingle(args[i + 1]);                        
                        break;
                    case "radius":
                        editvalue = zone.Radius = Convert.ToSingle(args[i + 1]);
                        zone.Size = Vector3.zero;
                        break;
                    case "rotation":
                        object rotation = Convert.ToSingle(args[i + 1]);
                        if (rotation is float)
                            zone.Rotation = Quaternion.AngleAxis((float)rotation, Vector3.up).eulerAngles;
                        else
                        {
                            zone.Rotation = player?.GetNetworkRotation().eulerAngles ?? Vector3.zero;
                            zone.Rotation.x = 0;
                        }
                        editvalue = zone.Rotation;
                        break;
                    case "location":
                        if (player != null && args[i + 1].Equals("here", StringComparison.OrdinalIgnoreCase))
                        {
                            editvalue = zone.Location = player.transform.position;
                            break;
                        }
                        var loc = args[i + 1].Trim().Split(' ');
                        if (loc.Length == 3)
                            editvalue = zone.Location = new Vector3(Convert.ToSingle(loc[0]), Convert.ToSingle(loc[1]), Convert.ToSingle(loc[2]));
                        else
                        {
                            if (player != null) SendMessage(player, "Invalid location format, use: \"x y z\" or here");
                            continue;
                        }
                        break;
                    case "size":
                        var size = args[i + 1].Trim().Split(' ');
                        if (size.Length == 3)
                            editvalue = zone.Size = new Vector3(Convert.ToSingle(size[0]), Convert.ToSingle(size[1]), Convert.ToSingle(size[2]));
                        else
                        {
                            if (player != null) SendMessage(player, "Invalid size format, use: \"x y z\"");
                            continue;
                        }
                        break;
                    case "enter_message":
                        editvalue = zone.EnterMessage = args[i + 1];
                        break;
                    case "leave_message":
                        editvalue = zone.LeaveMessage = args[i + 1];
                        break;
                    case "permission":
                        string permission = args[i + 1];
                        if (!permission.StartsWith("zonemanager."))
                            permission = $"zonemanager.{permission}";
                        editvalue = zone.Permission = permission;
                        break;
                    case "ejectspawns":
                        editvalue = zone.EjectSpawns = args[i + 1];
                        break;
                    case "enabled":
                    case "enable":
                        editvalue = zone.Enabled = GetBoolValue(args[i + 1]);
                        break;
                    default:
                        try
                        {
                            var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), args[i], true);
                            var boolValue = GetBoolValue(args[i + 1]);
                            editvalue = boolValue;
                            if (boolValue) AddZoneFlag(zone, flag);
                            else RemoveZoneFlag(zone, flag);
                        }
                        catch
                        {
                            if (player != null) SendMessage(player, $"Unknown zone flag: {args[i]}");
                            continue;
                        }
                        break;
                }
                if (player != null) SendMessage(player, $"{args[i]} set to {editvalue}");
            }
        }
        #endregion

        #region API        
        private bool CreateOrUpdateZone(string zoneId, string[] args, Vector3 position = default(Vector3))
        {
            Zone.Definition definition;
            if (!zoneDefinitions.TryGetValue(zoneId, out definition))
                definition = new Zone.Definition { Id = zoneId, Radius = 20 };
            else
                storedData.ZoneDefinitions.Remove(definition);
            UpdateZoneDefinition(definition, args);

            if (position != default(Vector3))
                definition.Location = position;

            zoneDefinitions[zoneId] = definition;
            storedData.ZoneDefinitions.Add(definition);
            SaveData();

            if (definition.Location == null)
                return false;

            RefreshZone(zoneId);
            return true;
        }

        private bool EraseZone(string zoneId)
        {
            Zone.Definition definition;
            if (!zoneDefinitions.TryGetValue(zoneId, out definition))
                return false;

            storedData.ZoneDefinitions.Remove(definition);
            zoneDefinitions.Remove(zoneId);
            SaveData();
            RefreshZone(zoneId);
            return true;
        }

        private void SetZoneStatus(string zoneId, bool active) => GetZoneByID(zoneId)?.InitializeZone(active);
        private Vector3 GetZoneLocation(string zoneId) => GetZoneByID(zoneId)?.definition.Location ?? Vector3.zero;
        private object GetZoneRadius(string zoneID) => GetZoneByID(zoneID)?.definition.Radius;
        private object GetZoneSize(string zoneID) => GetZoneByID(zoneID)?.definition.Size;
        private object GetZoneName(string zoneID) => GetZoneByID(zoneID)?.definition.Name;
        private object CheckZoneID(string zoneID) => GetZoneByID(zoneID)?.definition.Id;
        private object GetZoneIDs() => zoneObjects.Keys.ToArray();

        private void AddToWhitelist(Zone zone, BasePlayer player) { zone.WhiteList.Add(player.userID); }
        private void RemoveFromWhitelist(Zone zone, BasePlayer player) { zone.WhiteList.Remove(player.userID); }
        private void AddToKeepinlist(Zone zone, BasePlayer player) { zone.KeepInList.Add(player.userID); }
        private void RemoveFromKeepinlist(Zone zone, BasePlayer player) { zone.KeepInList.Remove(player.userID); }
                
        private List<ulong> GetPlayersInZone(string zoneId)
        {
            var players = new List<ulong>();
            foreach (var pair in playerZones)
                players.AddRange(pair.Value.Where(zone => zone.definition.Id == zoneId).Select(zone => pair.Key.userID));
            return players;
        }

        private bool isPlayerInZone(string zoneId, BasePlayer player)
        {
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones)) return false;
            return zones.Any(zone => zone.definition.Id == zoneId);
        }

        private bool AddPlayerToZoneWhitelist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            AddToWhitelist(targetZone, player);
            return true;
        }

        private bool AddPlayerToZoneKeepinlist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            AddToKeepinlist(targetZone, player);
            return true;
        }

        private bool RemovePlayerFromZoneWhitelist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            RemoveFromWhitelist(targetZone, player);
            return true;
        }

        private bool RemovePlayerFromZoneKeepinlist(string zoneId, BasePlayer player)
        {
            var targetZone = GetZoneByID(zoneId);
            if (targetZone == null) return false;
            RemoveFromKeepinlist(targetZone, player);
            return true;
        }

        private List<string> ZoneFieldListRaw()
        {
            var list = new List<string> { "name", "ID", "radius", "rotation", "size", "Location", "enter_message", "leave_message", "radiation", "comfort", "temperature" };
            list.AddRange(Enum.GetNames(typeof(ZoneFlags)));
            return list;
        }

        private Dictionary<string, string> ZoneFieldList(string zoneId)
        {
            var zone = GetZoneByID(zoneId);
            if (zone == null) return null;
            var fieldlistzone = new Dictionary<string, string>
            {
                { "name", zone.definition.Name },
                { "ID", zone.definition.Id },
                { "comfort", zone.definition.Comfort.ToString() },
                { "temperature", zone.definition.Temperature.ToString() },
                { "radiation", zone.definition.Radiation.ToString() },
                { "radius", zone.definition.Radius.ToString() },
                { "rotation", zone.definition.Rotation.ToString() },
                { "size", zone.definition.Size.ToString() },
                { "Location", zone.definition.Location.ToString() },
                { "enter_message", zone.definition.EnterMessage },
                { "leave_message", zone.definition.LeaveMessage },
                { "permission", zone.definition.Permission },
                { "ejectspawns", zone.definition.EjectSpawns }
            };

            var values = Enum.GetValues(typeof(ZoneFlags));
            foreach (var value in values)
                fieldlistzone[Enum.GetName(typeof(ZoneFlags), value)] = HasZoneFlag(zone, (ZoneFlags)value).ToString();
            return fieldlistzone;
        }

        private void AddDisabledFlag(string flagString)
        {
            try
            {
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                disabledFlags |= flag;
            }
            catch
            {
            }
        }

        private void RemoveDisabledFlag(string flagString)
        {
            try
            {
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                disabledFlags &= ~flag;
            }
            catch
            {
            }
        }

        private void AddZoneDisabledFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                zone.disabledFlags |= flag;
                UpdateAllPlayers();
            }
            catch
            {
            }
        }

        private void RemoveZoneDisabledFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                zone.disabledFlags &= ~flag;
                UpdateAllPlayers();
            }
            catch
            {
            }
        }

        private bool HasFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                if (HasZoneFlag(zone, flag))
                    return true;
            }
            catch
            {
            }
            return false;
        }

        private void AddFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                AddZoneFlag(zone.definition, flag);
            }
            catch
            {
            }
        }

        private void RemoveFlag(string zoneId, string flagString)
        {
            try
            {
                var zone = GetZoneByID(zoneId);
                var flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                RemoveZoneFlag(zone.definition, flag);
            }
            catch
            {
            }
        }

        private Zone GetZoneByID(string zoneId)
        {
            return zoneObjects.ContainsKey(zoneId) ? zoneObjects[zoneId] : null;
        }

        private void CreateNewZone(Zone.Definition zonedef)
        {
            if (zonedef == null)
                return;
            Zone newZone = new GameObject().AddComponent<Zone>();
            newZone.SetInfo(zonedef);

            if (!zoneObjects.ContainsKey(zonedef.Id))
                zoneObjects.Add(zonedef.Id, newZone);
            else zoneObjects[zonedef.Id] = newZone;
        }

        private void RefreshZone(string zoneId)
        {
            Zone zone = GetZoneByID(zoneId);

            if (zone != null)            
                UnityEngine.Object.Destroy(zone);
            
            Zone.Definition zoneDef;
            if (zoneDefinitions.TryGetValue(zoneId, out zoneDef))
                CreateNewZone(zoneDef);
            else
            {
                if (zoneObjects.ContainsKey(zoneId))
                    zoneObjects.Remove(zoneId);
            }
        }

        private void UpdateAllPlayers()
        {
            BasePlayer[] players = playerTags.Keys.ToArray();
            for (var i = 0; i < players.Length; i++)
                UpdateFlags(players[i]);
        }

        private void UpdateFlags(BasePlayer player)
        {
            playerTags.Remove(player);
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones) || zones.Count == 0) return;
            var newFlags = ZoneFlags.None;
            foreach (var zone in zones)
                newFlags |= zone.definition.Flags & ~zone.disabledFlags;
            playerTags[player] = newFlags;
        }

        private bool HasPlayerFlag(BasePlayer player, ZoneFlags flag, bool adminBypass)
        {
            if (CanBypass(player, flag))
                return false;
            if (adminBypass && IsAdmin(player))
                return false;
            if ((disabledFlags & flag) == flag)
                return false;

            ZoneFlags tags;
            if (!playerTags.TryGetValue(player, out tags))
                return false;
            return (tags & flag) == flag;
        }
        
        private static BasePlayer FindPlayer(string nameOrIdOrIp)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrIdOrIp)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
                if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress == nameOrIdOrIp)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrIdOrIp)
                    return sleepingPlayer;
                if (sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.OrdinalIgnoreCase))
                    return sleepingPlayer;
            }
            return null;
        }

        private BasePlayer FindPlayerByRadius(Vector3 position, float rad)
        {
            var cachedColliders = Physics.OverlapSphere(position, rad, playersMask);
            return cachedColliders.Select(collider => collider.GetComponentInParent<BasePlayer>()).FirstOrDefault(player => player != null);
        }
        
        private string[] GetPlayerZoneIDs(BasePlayer player)
        {
            HashSet<Zone> zones;
            if (playerZones.TryGetValue(player, out zones))
                return zones.Select(x => x.definition.Id).ToArray();
            return null;
        }

        private bool EntityHasFlag(BaseEntity entity, string flagString)
        {
            try
            {
                ZoneFlags flag = (ZoneFlags)Enum.Parse(typeof(ZoneFlags), flagString, true);
                return EntityHasFlag(entity, flag);
            }
            catch
            {
                PrintError($"Invalid flag name ({flagString}) passed to EntityHasFlag function");
                return false;
            }
        }

        private bool EntityHasFlag(BaseEntity entity, ZoneFlags flag)
        {
            if (entity == null)
                return false;

            HashSet<Zone> zones;

            if (entity is BasePlayer)
                playerZones.TryGetValue(entity as BasePlayer, out zones);
            else if (entity is BaseNpc)
                npcZones.TryGetValue(entity as BaseNpc, out zones);
            else if (entity.GetComponent<ResourceDispenser>())
                resourceZones.TryGetValue(entity.GetComponent<ResourceDispenser>(), out zones);
            else if (entity is BaseCombatEntity)
                buildingZones.TryGetValue(entity as BaseCombatEntity, out zones);
            else otherZones.TryGetValue(entity, out zones);

            if (zones != null)
            {
                foreach (var zone in zones)
                {
                    if (HasZoneFlag(zone, flag))
                        return true;
                }
            }
            return false;
        }

        private List<string> GetEntityZones(BaseEntity entity)
        {
            if (entity == null) return null;

            HashSet<Zone> zones = null;
            ResourceDispenser disp = entity.GetComponent<ResourceDispenser>();

            if (disp != null)
                resourceZones.TryGetValue(disp, out zones);
            else if (entity is BasePlayer)
                playerZones.TryGetValue(entity as BasePlayer, out zones);
            else if (entity is BaseCombatEntity)
                buildingZones.TryGetValue(entity as BaseCombatEntity, out zones);
            else
                otherZones.TryGetValue(entity, out zones);

            List<string> zoneIds = new List<string>();

            if (zones != null)
                zoneIds.AddRange(zones.Select(x => x.definition.Id));  
            
            return zoneIds;
        }        
        #endregion

        #region Helpers
        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return rotation * (point - pivot) + pivot;
        }

        private static float GetSkyHour()
        {
            return TOD_Sky.Instance.Cycle.Hour;
        }

        private static void CancelDamage(HitInfo hitinfo)
        {
            hitinfo.damageTypes = new DamageTypeList();
            hitinfo.DoHitEffects = false;
            hitinfo.HitMaterial = 0;
        }

        private void ShowZone(BasePlayer player, string zoneId, float time = 30)
        {
            Zone zone = GetZoneByID(zoneId);
            if (zone == null)
                return;
            if (zone.definition.Size != Vector3.zero)
            {
                Vector3 center = zone.definition.Location;
                Quaternion rotation = Quaternion.Euler(zone.definition.Rotation);
                Vector3 size = zone.definition.Size / 2;
                Vector3 point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
                Vector3 point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
                Vector3 point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
                Vector3 point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
                Vector3 point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
                Vector3 point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
                Vector3 point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
                Vector3 point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

                player.SendConsoleCommand("ddraw.line", time, Color.blue, point1, point2);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point1, point3);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point1, point5);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point4, point2);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point4, point3);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point4, point8);

                player.SendConsoleCommand("ddraw.line", time, Color.blue, point5, point6);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point5, point7);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point6, point2);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point8, point6);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point8, point7);
                player.SendConsoleCommand("ddraw.line", time, Color.blue, point7, point3);
            }
            else player.SendConsoleCommand("ddraw.sphere", time, Color.blue, zone.definition.Location, zone.definition.Radius);
        }
        
        private void CheckSpawnedEntity(BaseNetworkable entity)
        {
            if (entity == null || entity.IsDestroyed) return;
            HashSet<Zone> zones = null;
            ZoneFlags flag = ZoneFlags.None;

            if (entity is LootContainer || entity is JunkPile)
            {
                flag = ZoneFlags.NoLootSpawns;
                otherZones.TryGetValue(entity as BaseEntity, out zones);
            }            
            else if (entity is BaseNpc)
            {
                flag = ZoneFlags.NoNPCSpawns;
                npcZones.TryGetValue(entity as BaseNpc, out zones);
            }
            else if (entity is NPCPlayer)
            {
                flag = ZoneFlags.NoNPCSpawns;
                playerZones.TryGetValue(entity as NPCPlayer, out zones);
            }
            else if (entity is DroppedItem || entity is WorldItem)
            {
                flag = ZoneFlags.NoDrop;
                otherZones.TryGetValue(entity as WorldItem, out zones);
            }
            else if (entity is DroppedItemContainer)
            {
                flag = ZoneFlags.NoDrop;
                buildingZones.TryGetValue(entity as DroppedItemContainer, out zones);
            }
            if (flag == ZoneFlags.None || zones == null) return;
            foreach (var zone in zones)
            {
                if (HasZoneFlag(zone, flag))
                {
                    if (entity is WorldItem)
                    {
                        (entity as WorldItem).item.Remove(0f);
                    }
                    entity.Kill(BaseNetworkable.DestroyMode.None);
                }
            }
        }
        #endregion

        #region Entity Management
        private void OnPlayerEnterZone(Zone zone, BasePlayer player)
        {
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones))
                playerZones[player] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            UpdateFlags(player);

            if ((!string.IsNullOrEmpty(zone.definition.Permission) && !permission.UserHasPermission(player.UserIDString, zone.definition.Permission)) || (HasZoneFlag(zone, ZoneFlags.Eject) && !CanBypass(player, ZoneFlags.Eject) && !IsAdmin(player)))
            {
                EjectPlayer(zone, player);
                SendMessage(player, msg("eject", player.UserIDString));
                return;
            }

            if (player.IsSleeping() && !player.IsConnected)
            {
                if (HasZoneFlag(zone, ZoneFlags.KillSleepers))
                {
                    if (!CanBypass(player, ZoneFlags.KillSleepers) && !IsAdmin(player))
                    {
                        player.Die();
                        return;
                    }
                }

                if (HasZoneFlag(zone, ZoneFlags.EjectSleepers))
                {
                    if (!CanBypass(player, ZoneFlags.EjectSleepers) && !IsAdmin(player))
                    {
                        EjectPlayer(zone, player);
                        return;
                    }                    
                }
            }

            if (!string.IsNullOrEmpty(zone.definition.EnterMessage))
            {
                if (PopupNotifications != null && usePopups)
                    PopupNotifications.Call("CreatePopupNotification", string.Format(zone.definition.EnterMessage, player.displayName), player);
                else
                    SendMessage(player, zone.definition.EnterMessage, player.displayName);
            }
            
            Interface.Oxide.CallHook("OnEnterZone", zone.definition.Id, player);
            if (HasPlayerFlag(player, ZoneFlags.Kill, true))
            {
                SendMessage(player, msg("kill", player.UserIDString));
                player.Die();
            }
        }

        private void OnPlayerExitZone(Zone zone, BasePlayer player, bool all = false)
        {
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(player, out zones)) return;
            if (!all)
            {
                zone.OnEntityKill(player);
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) playerZones.Remove(player);
                if (!string.IsNullOrEmpty(zone.definition.LeaveMessage))
                {
                    if (PopupNotifications != null && usePopups)
                        PopupNotifications.Call("CreatePopupNotification", string.Format(zone.definition.LeaveMessage, player.displayName), player);
                    else
                        SendMessage(player, zone.definition.LeaveMessage, player.displayName);
                }
                if (zone.KeepInList.Contains(player.userID))
                    AttractPlayer(zone, player);
                Interface.Oxide.CallHook("OnExitZone", zone.definition.Id, player);
            }
            else
            {
                foreach (Zone zone1 in zones)
                {
                    if (!string.IsNullOrEmpty(zone1.definition.LeaveMessage))
                    {
                        if (PopupNotifications != null && usePopups)
                            PopupNotifications.Call("CreatePopupNotification", string.Format(zone1.definition.LeaveMessage, player.displayName), player);
                        else SendMessage(player, zone1.definition.LeaveMessage, player.displayName);
                    }
                    if (zone1.KeepInList.Contains(player.userID))
                        AttractPlayer(zone1, player);
                    Interface.Oxide.CallHook("OnExitZone", zone1.definition.Id, player);
                }
                playerZones.Remove(player);
            }
            UpdateFlags(player);
        }

        private void OnResourceEnterZone(Zone zone, ResourceDispenser entity)
        {
            HashSet<Zone> zones;
            if (!resourceZones.TryGetValue(entity, out zones))
                resourceZones[entity] = zones = new HashSet<Zone>();
            zones.Add(zone);
        }

        private void OnResourceExitZone(Zone zone, ResourceDispenser resource, bool all = false)
        {
            HashSet<Zone> zones;
            if (!resourceZones.TryGetValue(resource, out zones)) return;
            if (!all)
            {
                if (!zones.Remove(zone))
                    return;
                if (zones.Count <= 0)
                    resourceZones.Remove(resource);
            }
            else resourceZones.Remove(resource);
        }

        private void OnNpcEnterZone(Zone zone, BaseNpc entity)
        {
            HashSet<Zone> zones;
            if (!npcZones.TryGetValue(entity, out zones))
                npcZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;

            if (HasZoneFlag(zone, ZoneFlags.NpcFreeze))            
                entity.CancelInvoke(new Action(entity.TickAi)); 
        }

        private void OnNpcExitZone(Zone zone, BaseNpc entity, bool all = false)
        {
            HashSet<Zone> zones;
            if (!npcZones.TryGetValue(entity, out zones)) return;
            if (!all)
            {
                if (!zones.Remove(zone)) return;
                if (zones.Count <= 0) npcZones.Remove(entity);
            }
            else 
            {
                foreach (Zone zone1 in zones)
                {
                    if (!HasZoneFlag(zone1, ZoneFlags.NpcFreeze)) continue;
                    entity.InvokeRandomized(new Action(entity.TickAi), 0.1f, 0.1f, 0.00500000035f);                   
                }
                npcZones.Remove(entity);
            }
        }

        private void OnBuildingEnterZone(Zone zone, BaseCombatEntity entity)
        {
            HashSet<Zone> zones;
            if (!buildingZones.TryGetValue(entity, out zones))
                buildingZones[entity] = zones = new HashSet<Zone>();
            if (!zones.Add(zone)) return;
            if (HasZoneFlag(zone, ZoneFlags.NoStability) && !CanBypass(entity.OwnerID, ZoneFlags.NoStability))
            {
                StabilityEntity block = entity as StabilityEntity;
                if (block != null) block.grounded = true;
            }            
        }

        private void OnBuildingExitZone(Zone zone, BaseCombatEntity entity, bool all = false)
        {
            HashSet<Zone> zones;
            if (!buildingZones.TryGetValue(entity, out zones))
                return;
            bool stability = false;
            if (!all)
            {
                zone.OnEntityKill(entity);
                if (!zones.Remove(zone))
                    return;
                stability = HasZoneFlag(zone, ZoneFlags.NoStability);
                if (zones.Count <= 0)
                    buildingZones.Remove(entity);
            }
            else
            {
                foreach (var zone1 in zones)
                {
                    zone1.OnEntityKill(entity);
                    stability |= HasZoneFlag(zone1, ZoneFlags.NoStability);
                }
                buildingZones.Remove(entity);
            }
            if (stability)
            {
                StabilityEntity block = entity as StabilityEntity;
                if (block == null)
                    return;
                GameObject prefab = GameManager.server.FindPrefab(PrefabAttribute.server.Find<Construction>(block.prefabID).fullName);
                block.grounded = prefab?.GetComponent<StabilityEntity>()?.grounded ?? false;
            }            
        }

        private void OnOtherEnterZone(Zone zone, BaseEntity entity)
        {
            HashSet<Zone> zones;
            if (!otherZones.TryGetValue(entity, out zones))
                otherZones[entity] = zones = new HashSet<Zone>();
            zones.Add(zone);            
        }

        private void OnOtherExitZone(Zone zone, BaseEntity entity, bool all = false)
        {
            HashSet<Zone> zones;
            if (!otherZones.TryGetValue(entity, out zones))
                return;
            if (zones.Contains(zone))
                zones.Remove(zone);
            if (zones.Count == 0)
                otherZones.Remove(entity);            
        }        
        #endregion

        #region Player Management
        private void EjectPlayer(Zone zone, BasePlayer player)
        {
            if (zone.WhiteList.Contains(player.userID) || zone.KeepInList.Contains(player.userID)) return;
            Vector3 newPos = Vector3.zero;
            if (!string.IsNullOrEmpty(zone.definition.EjectSpawns) && Spawns)
            {
                object success = Spawns.Call("GetRandomSpawn", zone.definition.EjectSpawns);
                if (success is Vector3)               
                    newPos = (Vector3)success;
            }
            if (newPos == Vector3.zero)
            {
                float dist;
                if (zone.definition.Size != Vector3.zero)
                    dist = zone.definition.Size.x > zone.definition.Size.z ? zone.definition.Size.x : zone.definition.Size.z;
                else
                    dist = zone.definition.Radius;
                newPos = zone.transform.position + (player.transform.position - zone.transform.position).normalized * (dist + 5f);
                newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);
            }
            player.MovePosition(newPos);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
            player.SendNetworkUpdateImmediate();
        }

        private void AttractPlayer(Zone zone, BasePlayer player)
        {
            float dist;
            if (zone.definition.Size != Vector3.zero)
                dist = zone.definition.Size.x > zone.definition.Size.z ? zone.definition.Size.x : zone.definition.Size.z;
            else dist = zone.definition.Radius;

            Vector3 newPos = zone.transform.position + (player.transform.position - zone.transform.position).normalized * (dist - 5f);
            newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);
            player.MovePosition(newPos);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
            player.SendNetworkUpdateImmediate();
        }

        private bool IsAdmin(BasePlayer player)
        {
            if (player?.net?.connection == null) return true;
            return player.net.connection.authLevel > 0;
        }

        private bool HasPermission(BasePlayer player, string permname) => IsAdmin(player) || permission.UserHasPermission(player.UserIDString, permname);
        
        private bool CanBypass(object player, ZoneFlags flag) => permission.UserHasPermission(player is BasePlayer ? (player as BasePlayer).UserIDString : player.ToString(), permIgnoreFlag + flag);
        #endregion

        #region Commands
        [ChatCommand("zone_add")]
        private void cmdChatZoneAdd(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            Zone.Definition definition = new Zone.Definition(player.transform.position) { Id = UnityEngine.Random.Range(1, 99999999).ToString() };

            CreateNewZone(definition);
            if (zoneDefinitions.ContainsKey(definition.Id))
                storedData.ZoneDefinitions.Remove(zoneDefinitions[definition.Id]);
            zoneDefinitions[definition.Id] = definition;
            lastPlayerZone[player.userID] = definition.Id;
            storedData.ZoneDefinitions.Add(definition);
            SaveData();
            ShowZone(player, definition.Id);

            SendMessage(player, "New zone created, you may now edit it: " + definition.Location);
        }

        [ChatCommand("zone_reset")]
        private void cmdChatZoneReset(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            zoneDefinitions.Clear();
            storedData.ZoneDefinitions.Clear();
            SaveData();
            Unload();
            SendMessage(player, "All Zones were removed");
        }

        [ChatCommand("zone_remove")]
        private void cmdChatZoneRemove(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }
            if (args.Length == 0)
            {
                SendMessage(player, "/zone_remove XXXXXID");
                return;
            }

            Zone.Definition definition;
            if (!zoneDefinitions.TryGetValue(args[0], out definition))
            {
                SendMessage(player, "This zone doesn't exist");
                return;
            }

            storedData.ZoneDefinitions.Remove(definition);
            zoneDefinitions.Remove(args[0]);
            SaveData();
            RefreshZone(args[0]);
            SendMessage(player, "Zone " + args[0] + " was removed");
        }

        [ChatCommand("zone_stats")]
        private void cmdChatZoneStats(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            SendMessage(player, "Players: {0}", playerZones.Count);
            SendMessage(player, "Buildings: {0}", buildingZones.Count);
            SendMessage(player, "Npcs: {0}", npcZones.Count);
            SendMessage(player, "Resources: {0}", resourceZones.Count);
            SendMessage(player, "Others: {0}", otherZones.Count);
        }

        [ChatCommand("zone_edit")]
        private void cmdChatZoneEdit(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            string zoneId;
            if (args.Length == 0)
            {
                HashSet<Zone> zones;
                if (!playerZones.TryGetValue(player, out zones) || zones.Count != 1)
                {
                    SendMessage(player, "/zone_edit XXXXXID");
                    return;
                }
                zoneId = zones.First().definition.Id;
            }
            else zoneId = args[0];

            if (!zoneDefinitions.ContainsKey(zoneId))
            {
                SendMessage(player, "This zone doesn't exist");
                return;
            }

            lastPlayerZone[player.userID] = zoneId;
            SendMessage(player, "Editing zone ID: " + zoneId);
            ShowZone(player, zoneId);
        }

        [ChatCommand("zone_player")]
        private void cmdChatZonePlayer(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            BasePlayer targetPlayer = player;
            if (args != null && args.Length > 0)
            {
                targetPlayer = FindPlayer(args[0]);
                if (targetPlayer == null)
                {
                    SendMessage(player, "Player not found");
                    return;
                }
            }

            ZoneFlags flags;
            playerTags.TryGetValue(targetPlayer, out flags);
            SendMessage(player, $"=== {targetPlayer.displayName} ===");
            SendMessage(player, $"Flags: {flags}");
            SendMessage(player, "========== Zone list ==========");
            HashSet<Zone> zones;
            if (!playerZones.TryGetValue(targetPlayer, out zones) || zones.Count == 0)
            {
                SendMessage(player, "empty");
                return;
            }

            foreach (var zone in zones)
                SendMessage(player, $"{zone.definition.Id}: {zone.definition.Name} - {zone.definition.Location}");
            UpdateFlags(targetPlayer);
        }

        [ChatCommand("zone_list")]
        private void cmdChatZoneList(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            SendMessage(player, "========== Zone list ==========");
            if (zoneDefinitions.Count == 0)
            {
                SendMessage(player, "empty");
                return;
            }

            foreach (var pair in zoneDefinitions)
                SendMessage(player, $"{pair.Key}: {pair.Value.Name} - {pair.Value.Location}");
        }

        [ChatCommand("zone")]
        private void cmdChatZone(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            string zoneId;
            if (!lastPlayerZone.TryGetValue(player.userID, out zoneId))
            {
                SendMessage(player, "You must first say: /zone_edit XXXXXID");
                return;
            }

            Zone.Definition zoneDefinition = zoneDefinitions[zoneId];
            if (args.Length < 1)
            {
                SendMessage(player, "/zone <option/flag> <value>");
                string message = $"<color={prefixColor}>Zone Name:</color> {zoneDefinition.Name}";
                message += $"\n<color={prefixColor}>Zone Enabled:</color> {zoneDefinition.Enabled}";
                message += $"\n<color={prefixColor}>Zone ID:</color> {zoneDefinition.Id}";
                message += $"\n<color={prefixColor}>Comfort:</color> {zoneDefinition.Comfort}";
                message += $"\n<color={prefixColor}>Temperature:</color> {zoneDefinition.Temperature}";
                message += $"\n<color={prefixColor}>Radiation:</color> {zoneDefinition.Radiation}";
                SendReply(player, message);

                message = $"<color={prefixColor}>Radius:</color> {zoneDefinition.Radius}";                
                message += $"\n<color={prefixColor}>Location:</color> {zoneDefinition.Location}";
                message += $"\n<color={prefixColor}>Size:</color> {zoneDefinition.Size}";
                message += $"\n<color={prefixColor}>Rotation:</color> {zoneDefinition.Rotation}";
                SendReply(player, message);

                message = $"<color={prefixColor}>Enter Message:</color> {zoneDefinition.EnterMessage}";
                message += $"\n<color={prefixColor}>Leave Message:</color> {zoneDefinition.LeaveMessage}";
                SendReply(player, message);

                message = $"<color={prefixColor}>Permission:</color> {zoneDefinition.Permission}";
                message += $"\n<color={prefixColor}>Eject Spawnfile:</color> {zoneDefinition.EjectSpawns}";
                SendReply(player, message);

                SendReply(player, $"<color={prefixColor}>Flags:</color> {zoneDefinition.Flags}");               
                ShowZone(player, zoneId);
                return;
            }
            if (args[0].ToLower() == "flags")
            {
                OpenFlagEditor(player, zoneId);
                return;
            }
            if (args.Length % 2 != 0)
            {
                SendMessage(player, "Value missing...");
                return;
            }
            UpdateZoneDefinition(zoneDefinition, args, player);
            RefreshZone(zoneId);
            SaveData();
            ShowZone(player, zoneId);
        }

        [ChatCommand("zone_flags")]
        private void cmdChatZoneFlags(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            string zoneId;
            if (!lastPlayerZone.TryGetValue(player.userID, out zoneId))
            {
                SendMessage(player, "You must first say: /zone_edit XXXXXID");
                return;
            }

            OpenFlagEditor(player, zoneId);
        }

        [ConsoleCommand("zone")]
        private void ccmdZone(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (!HasPermission(player, permZone))
            {
                SendMessage(player, "You don't have access to this command");
                return;
            }

            string zoneId = arg.GetString(0);
            Zone.Definition zoneDefinition;
            if (!arg.HasArgs(3) || !zoneDefinitions.TryGetValue(zoneId, out zoneDefinition))
            {
                SendMessage(player, "Zone ID not found or too few arguments supplied: zone <zoneid> <arg> <value>");
                return;
            }

            string[] args = new string[arg.Args.Length - 1];
            Array.Copy(arg.Args, 1, args, 0, args.Length);
            UpdateZoneDefinition(zoneDefinition, args, player);
            RefreshZone(zoneId);            
        }

        private void SendMessage(BasePlayer player, string message, params object[] args)
        {
            if (player != null)
            {
                if (args.Length > 0)
                    message = string.Format(message, args);
                SendReply(player, $"<color={prefixColor}>{prefix}</color>{message}");
            }
            else Puts(message);
        }
        #endregion

        #region UI
        const string ZMUI = "zmui.editor";
        #region Helper
        public static class UI
        {
            static public CuiElementContainer Container(string panel, string color, string min, string max, bool useCursor = false, string parent = "Overlay")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = min, AnchorMax = max},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panel
                    }
                };
                return container;
            }            
            static public void Panel(ref CuiElementContainer container, string panel, string color, string min, string max, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void Label(ref CuiElementContainer container, string panel, string text, int size, string min, string max, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = min, AnchorMax = max }
                },
                panel);

            }            
            static public void Button(ref CuiElementContainer container, string panel, string color, string text, int size, string min, string max, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = min, AnchorMax = max },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }           
            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.Substring(1);
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }
        #endregion

        #region Creation
        private void OpenFlagEditor(BasePlayer player, string zoneId, int page = 0)
        {
            Zone zone = GetZoneByID(zoneId);
            if (zone == null)
            {
                SendReply(player, $"Error getting zone object with ID: {zoneId}");
                CuiHelper.DestroyUi(player, ZMUI);
            }

            CuiElementContainer container = UI.Container(ZMUI, UI.Color("2b2b2b", 0.9f), "0 0", "1 1", true);
            UI.Label(ref container, ZMUI, $"Zone Flag Editor", 18, "0 0.92", "1 1");
            UI.Label(ref container, ZMUI, $"Zone ID: {zoneId}\nName: {zone.definition.Name}\n{(zone.definition.Size != Vector3.zero ? $"Box Size: {zone.definition.Size}\nRotation: {zone.definition.Rotation}" : $"Radius: {zone.definition.Radius}")}", 13, "0.05 0.8", "1 0.92", TextAnchor.UpperLeft);
            UI.Label(ref container, ZMUI, $"Comfort: {zone.definition.Comfort}\nRadiation: {zone.definition.Radiation}\nTemperature: {zone.definition.Temperature}\nZone Enabled: {zone.definition.Enabled}", 13, "0.25 0.8", "1 0.92", TextAnchor.UpperLeft);
            UI.Label(ref container, ZMUI, $"Permission: {zone.definition.Permission}\nEject Spawnfile: {zone.definition.EjectSpawns}\nEnter Message: {zone.definition.EnterMessage}\nExit Message: {zone.definition.LeaveMessage}", 13, "0.5 0.8", "1 0.92", TextAnchor.UpperLeft);
            UI.Button(ref container, ZMUI, UI.Color("#d85540", 1f), "Exit", 12, "0.95 0.96", "0.99 0.99", $"zmui.editflag {zoneId} exit");

            int count = 0;
            int startAt = page * 57;
            
            string[] flags = Enum.GetNames((typeof(ZoneFlags))).OrderBy(x => x).ToArray();
            for (int i = startAt; i < (startAt + 57 > flags.Length ? flags.Length : startAt + 57); i++)
            {
                string flagName = flags.ElementAt(i);
                bool value = HasFlag(zoneId, flagName);

                float[] position = GetButtonPosition(count);

                UI.Label(ref container, ZMUI, flagName, 12, $"{position[0]} {position[1]}", $"{position[0] + ((position[2] - position[0]) / 2)} {position[3]}");
                UI.Button(ref container, ZMUI, value ? UI.Color("#72E572", 1f) : UI.Color("#d85540", 1f), value ? "Enabled" : "Disabled", 12, $"{position[0] + ((position[2] - position[0]) / 2)} {position[1]}", $"{position[2]} {position[3]}", $"zmui.editflag {zoneId} {flagName} {!value} {page}");
                count++;
            }

            CuiHelper.DestroyUi(player, ZMUI);
            CuiHelper.AddUi(player, container);
        }

        private float[] GetButtonPosition(int i)
        {
            int rowNumber = i == 0 ? 0 : RowNumber(3, i);
            int columnNumber = i - (rowNumber * 3);

            float offsetX = 0.04f + ((0.01f + 0.293f) * columnNumber);
            float offsetY = (0.76f - (rowNumber * 0.04f));

            return new float[] { offsetX, offsetY, offsetX + 0.176f, offsetY + 0.03f };
        }

        private int RowNumber(int max, int count) => Mathf.FloorToInt(count / max);
        #endregion

        #region Commands
        [ConsoleCommand("zmui.editflag")]
        private void ccmdEditFlag(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            string zoneId = arg.GetString(0);

            if (arg.GetString(1) == "exit")
                CuiHelper.DestroyUi(player, ZMUI);
            else if (arg.GetString(1) == "page")
                OpenFlagEditor(player, zoneId, arg.GetInt(2));
            else
            {
                Zone zone = GetZoneByID(zoneId);
                if (zone == null)
                {
                    SendReply(player, $"Error getting zone object with ID: {zoneId}");
                    CuiHelper.DestroyUi(player, ZMUI);
                }

                ZoneFlags flag = (ZoneFlags)Enum.Parse((typeof(ZoneFlags)), arg.GetString(1));

                if (arg.GetBool(2))
                    AddZoneFlag(zone.definition, flag);
                else RemoveZoneFlag(zone.definition, flag);

                RefreshZone(zoneId);
                SaveData();

                OpenFlagEditor(player, zoneId, arg.GetInt(3));
            }
        }
        #endregion
        #endregion

        #region Config
        private bool usePopups = false;
        private bool Changed;
        private bool Initialized;
        private float AutolightOnTime;
        private float AutolightOffTime;
        private string prefix;
        private string prefixColor;

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

        private static bool GetBoolValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            value = value.Trim().ToLower();
            switch (value)
            {
                case "t":
                case "true":
                case "1":
                case "yes":
                case "y":
                case "on":
                    return true;
                default:
                    return false;
            }
        }
        private void LoadVariables()
        {
            AutolightOnTime = Convert.ToSingle(GetConfig("AutoLights", "Lights On Time", "18.0"));
            AutolightOffTime = Convert.ToSingle(GetConfig("AutoLights", "Lights Off Time", "8.0"));
            usePopups = Convert.ToBoolean(GetConfig("Notifications", "Use Popup Notifications", true));
            prefix = Convert.ToString(GetConfig("Chat", "Prefix", "ZoneManager: "));
            prefixColor = Convert.ToString(GetConfig("Chat", "Prefix Color (hex)", "#d85540"));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }
        #endregion

        #region Data Management
        private class StoredData
        {
            public readonly HashSet<Zone.Definition> ZoneDefinitions = new HashSet<Zone.Definition>();
        }

        private void SaveData()
        {
            ZoneManagerData.WriteObject(storedData);
        }

        private void LoadData()
        {
            zoneDefinitions.Clear();
            try
            {
                ZoneManagerData.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = ZoneManagerData.ReadObject<StoredData>();
                Puts("Loaded {0} Zone definitions", storedData.ZoneDefinitions.Count);
            }
            catch
            {
                Puts("Failed to load StoredData");
                storedData = new StoredData();
            }
            ZoneManagerData.Settings.NullValueHandling = NullValueHandling.Include;
            foreach (Zone.Definition zonedef in storedData.ZoneDefinitions)
                zoneDefinitions[zonedef.Id] = zonedef;
        }
        #endregion

        #region Localization
        string msg(string key, string playerId = null) => lang.GetMessage(key, this, playerId);

        Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["noBuild"] = "You are not allowed to build in this area!",
            ["noUpgrade"] = "You are not allowed to upgrade structures in this area!",
            ["noDeploy"] = "You are not allowed to deploy items in this area!",
            ["noCup"] = "You are not allowed to deploy cupboards in this area!",
            ["noChat"] = "You are not allowed to chat in this area!",
            ["noSuicide"] = "You are not allowed to suicide in this area!",
            ["noGather"] = "You are not allowed to gather in this area!",
            ["noLoot"] = "You are not allowed loot in this area!",
            ["noSignUpdates"] = "You can not update signs in this area!",
            ["noOvenToggle"] = "You can not toggle ovens and lights in this area!",
            ["noPickup"] = "You can not pick up objects in this area!",
            ["noVending"] = "You can not use vending machines in this area!",
            ["noStash"] = "You can not hide a stash in this area!",
            ["noCraft"] = "You can not craft in this area!",
            ["noMount"] = "You can not mount in this area!",
            ["eject"] = "You are not allowed in this area!",
            ["kill"] = "Access to this area is restricted!"
        };
        #endregion

        #region Vector3 Json Converter
        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    var values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                var o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }
        #endregion
    }
}
