// Requires: EventManager
using Oxide.Core;
using Oxide.Core.Configuration;
using Rust;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Plugins;
using System;

namespace Oxide.Plugins
{
    [Info("GunGame", "k1lly0u", "0.4.22", ResourceId = 1485)]
    class GunGame : RustPlugin
    {
        #region Fields
        [PluginReference] EventManager EventManager;

        ClassData classData;
        private DynamicConfigFile data;

        private List<GunGamePlayer> GunGamePlayers;
        private Dictionary<string, ItemDefinition> itemDefs;
        private Dictionary<ulong, SetCreator> setCreator;

        private string currentSet;
        private WeaponSet weaponSet;
        private bool usingGG;
        private bool hasStarted;
        private bool gameEnding;
        private bool downgradeDisabled;

        private Timer dgwDisableTimer;

        #endregion

        #region Classes
        class GunGamePlayer : MonoBehaviour
        {
            public BasePlayer player;
            public int kills;
            public int level;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                kills = 0;
                level = 1;
            }
        }
        class WeaponSet
        {
            public Dictionary<int, RankItem> Weapons = new Dictionary<int, RankItem>();
            public List<RankItem> PlayerGear = new List<RankItem>();
        }
        internal class RankItem
        {
            public string name;
            public string shortname;
            public ulong skin;
            public string container;
            public int amount;
            public int ammo;
            public string ammoType;
            public string[] contents = new string[0];
        }  
        class SetCreator
        {
            public string name;
            public WeaponSet set;
        }      
        #endregion

        #region Oxide Hooks
        void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.GetFile("EventManager/GG_WeaponSets");
        }
        void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            GunGamePlayers = new List<GunGamePlayer>();
            setCreator = new Dictionary<ulong, SetCreator>();
            itemDefs = ItemManager.itemList.ToDictionary(x => x.shortname);
            usingGG = false;
            hasStarted = false;
            CheckValidSet();
            //timer.Once(3, ()=> RegisterGame());
        }
        void Unload()
        {            
            if (usingGG && hasStarted)
                EventManager.EndEvent();
            DestroyEvent();

            var objects = UnityEngine.Object.FindObjectsOfType<GunGamePlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }
        #endregion

        #region Scoreboard
        private void UpdateScores()
        {
            if (usingGG && hasStarted && configData.EventSettings.ShowScoreboard)
            {
                var sortedList = GunGamePlayers.OrderByDescending(pair => pair.level).ToList();
                var scoreList = new Dictionary<ulong, EventManager.Scoreboard>();
                foreach (var entry in sortedList)
                {
                    if (scoreList.ContainsKey(entry.player.userID)) continue;
                    scoreList.Add(entry.player.userID, new EventManager.Scoreboard { Name = entry.player.displayName, Position = sortedList.IndexOf(entry), Score = entry.level });
                }
                EventManager.UpdateScoreboard(new EventManager.ScoreData { Additional = null, Scores = scoreList, ScoreType = "Level" });
            }
        }
        #endregion

        #region Functions
        void CheckValidSet()
        {
            if (classData.WeaponConfigs.Count < 1)
                CreateDefaultConfig();
            if (!classData.WeaponConfigs.ContainsKey(configData.EventSettings.DefaultWeaponSet))
            {
                PrintError($"Unable to find the weapon set: {configData.EventSettings.DefaultWeaponSet}, using the default set");

                if (!classData.WeaponConfigs.ContainsKey("default"))
                    CreateDefaultConfig();
                weaponSet = classData.WeaponConfigs["default"];
                currentSet = "default";              
            }
            else
            {
                currentSet = configData.EventSettings.DefaultWeaponSet;
                weaponSet = classData.WeaponConfigs[currentSet];
            }
        }
       
        private void DestroyEvent()
        {
            hasStarted = false;

            if (dgwDisableTimer != null)
                dgwDisableTimer.Destroy();
        }
        #endregion

        #region EventManager Hooks
        void RegisterGame()
        {
            EventManager.Events eventData = new EventManager.Events
            {
                CloseOnStart = false,
                DisableItemPickup = true,
                EnemiesToSpawn = 0,
                EventType = Title,
                GameMode = EventManager.GameMode.Normal,
                GameRounds = 0,
                Kit = null,
                MaximumPlayers = 0,
                MinimumPlayers = 2,
                ScoreLimit = 0,
                Spawnfile = configData.EventSettings.DefaultSpawnfile,
                Spawnfile2 = null,
                SpawnType = EventManager.SpawnType.Consecutive,
                RespawnType = EventManager.RespawnType.None,
                RespawnTimer = 10,
                UseClassSelector = false,
                WeaponSet = currentSet,
                ZoneID = configData.EventSettings.DefaultZoneID
                
            };
            EventManager.EventSetting eventSettings = new EventManager.EventSetting
            {
                CanChooseRespawn = true,
                CanUseClassSelector = false,
                CanPlayBattlefield = false,
                ForceCloseOnStart = false,
                IsRoundBased = false,
                RequiresKit = false,
                RequiresMultipleSpawns = false,
                RequiresSpawns = true,
                ScoreType = null,
                SpawnsEnemies = false,
                LockClothing = false
            };
            var success = EventManager.RegisterEventGame(Title, eventSettings, eventData);
            if (success == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
        }
        void OnSelectEventGamePost(string name)
        {
            if (Title == name)            
                usingGG = true;  
            else usingGG = false;
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (usingGG && hasStarted && !gameEnding)
            {
                if (!player.GetComponent<GunGamePlayer>()) return;
                if (player.IsSleeping())
                {
                    player.EndSleeping();
                    timer.In(1, () => OnEventPlayerSpawn(player));
                    return;
                }
                player.health = configData.GameSettings.StartHealth;
                GiveGear(player);
            }
        } 
        object CanEventOpen()
        {
            if (usingGG)
            {
                if (weaponSet == null)
                    return "Invalid weapon set selected";
            }
            return null;
        }        
        object OnEventOpenPost()
        {
            if (usingGG)
            {
                EventManager.BroadcastToChat(EventMessageOpenBroadcast);
                EventManager._Event.UseClassSelector = false;                
            }
            return null;
        }
        object OnEventCancel()
        {
            if (usingGG && hasStarted)            
                CheckScores(null, false, true);
            return null;
        }        
        object OnEventEndPre()
        {
            if (usingGG)
            {
                if (usingGG && hasStarted)
                    CheckScores(null, false, true);                
            }
            return null;
        }
        object OnEventEndPost()
        {
            DestroyEvent();
            var objPlayers = UnityEngine.Object.FindObjectsOfType<GunGamePlayer>();
            if (objPlayers != null)
                foreach (var gameObj in objPlayers)
                    UnityEngine.Object.Destroy(gameObj);
            GunGamePlayers.Clear();
            return null;
        }
        object OnEventStartPre()
        {
            if (usingGG)
            {
                hasStarted = true;
                gameEnding = false;
            }
            return null;
        }
        object OnEventStartPost()
        {
            if (usingGG && hasStarted)
            {
                if (configData.GameSettings.UseDowngradeWeapon)
                {
                    downgradeDisabled = false;
                    if (configData.GameSettings.DisableDowngradeWeaponTimer != 0)
                        dgwDisableTimer = timer.Once(configData.GameSettings.DisableDowngradeWeaponTimer * 60, () =>
                        {
                            downgradeDisabled = true;
                            EventManager.BroadcastEvent("The downgrade weapon has been disabled!");
                        });
                }
                UpdateScores();
            }
            return null;
        }
        object OnSelectKit(string kitname)
        {
            if (usingGG)
            {
                Puts("No Kits required for this gamemode!");
            }
            return null;
        }
        object OnEventJoinPost(BasePlayer player)
        {
            if (usingGG)
            {
                if (player.GetComponent<GunGamePlayer>())
                    UnityEngine.Object.Destroy(player.GetComponent<GunGamePlayer>());
                GunGamePlayers.Add(player.gameObject.AddComponent<GunGamePlayer>());
                player.GetComponent<GunGamePlayer>().level = 1;
                if (configData.GameSettings.DowngradeNotification)
                    MacheteNotification(player);
                EventManager.CreateScoreboard(player);
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (usingGG)
            {
                var gunGamePlayer = player.GetComponent<GunGamePlayer>();
                if (gunGamePlayer)
                {
                    GunGamePlayers.Remove(gunGamePlayer);
                    UnityEngine.Object.Destroy(gunGamePlayer);
                    CheckScores();
                }
            }
            return null;
        }
        void OnEventPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if (usingGG && !(hitinfo.HitEntity is BasePlayer))
            {
                hitinfo.damageTypes = new DamageTypeList();
                hitinfo.DoHitEffects = false;
            }
        }
        void OnEventPlayerDeath(BasePlayer victim, HitInfo hitinfo)
        {
            if ((usingGG) && (hasStarted))
            {
                BasePlayer attacker = hitinfo?.Initiator?.ToPlayer();
                if (attacker != null && attacker != victim)
                {
                    if (!downgradeDisabled)
                    {
                        if (hitinfo.WeaponPrefab != null && hitinfo.WeaponPrefab.name.Contains(configData.GameSettings.DowngradeWeapon.shortname))
                        {
                            var vicplayerLevel = victim.GetComponent<GunGamePlayer>().level;
                            if (vicplayerLevel == 1)
                            {
                                SendReply(attacker, string.Format("You killed <color=orange>{0}</color> with a <color=orange>{1}</color> but they were already the lowest rank.", victim.displayName, configData.GameSettings.DowngradeWeapon.name));
                                return;
                            }
                            if (vicplayerLevel >= 2)
                            {
                                victim.GetComponent<GunGamePlayer>().level = (vicplayerLevel - 1);
                                SendReply(attacker, string.Format("You killed <color=orange>{0}</color> with a <color=orange>{1}</color> and they have lost a rank!", victim.displayName, configData.GameSettings.DowngradeWeapon.name));
                                SendReply(victim, string.Format("You were killed with a <color=orange>{0}</color> and have lost a rank!", configData.GameSettings.DowngradeWeapon.name));
                                return;
                            }
                        }
                    }
                    AddKill(attacker, victim, GetWeapon(hitinfo));
                    return;
                }
                if (attacker == null || attacker == victim)
                {                    
                    if (victim.GetComponent<GunGamePlayer>().level > 1)
                    {
                        victim.GetComponent<GunGamePlayer>().level--;
                        SendReply(victim, "Suicide is not a option so you have lost a rank!");                        
                    }
                }
            }
        } 
        #endregion

        #region GG Functions
        private void MacheteNotification(BasePlayer player)
        {
            if (!hasStarted) return;
            if (downgradeDisabled) return;
            if (player.GetComponent<GunGamePlayer>())
            {
                SendReply(player, string.Format("The downgrade weapon is enabled. Kills with a <color=orange>{0}</color> will lower the victims rank!", configData.GameSettings.DowngradeWeapon.name));
                timer.Once(120, () => MacheteNotification(player));
            }
        }
        private void GiveGear(BasePlayer player)
        {
            if (gameEnding) return;
            player.inventory.Strip();
            timer.In(0.001f, () =>
            {
                try
                {
                    GiveRankKit(player, player.GetComponent<GunGamePlayer>().level);
                    if (!downgradeDisabled)
                        GiveItem(player, configData.GameSettings.DowngradeWeapon);
                    foreach (var entry in weaponSet.PlayerGear)
                        GiveItem(player, entry);
                }
                catch(Exception e)
                {
                    Interface.Oxide.LogWarning(e.Message);
                    Interface.Oxide.LogWarning(e.StackTrace);
                }
            });
        }
        public void GiveRankKit(BasePlayer player, int rank)
        {
            RankItem rankItem;
            if (weaponSet.Weapons.TryGetValue(rank, out rankItem))
            {
                for (var i = 0; i < rankItem.amount; i++)
                    GiveItem(player, rankItem);
                SendReply(player, string.Format("You are Rank <color=orange>{0}</color> ({1})", rank, rankItem.name));
                return;
            }
            PrintError($"Weapon not found for rank: {rank}, Check your weapon set for errors!");
        }
        #endregion

        #region Item Creation
        private Item BuildItem(string shortname)
        {
            var definition = itemDefs[shortname];
            if (definition != null)
            {
                var item = ItemManager.Create(definition);
                if (item != null)
                    return item;
            }
            Puts($"Error building item: {shortname}: Invalid item shortname!");
            return null;
        }
        public void GiveItem(BasePlayer player, RankItem rankItem)
        {
            if (itemDefs.ContainsKey(rankItem.shortname))
            {
                var definition = itemDefs[rankItem.shortname];
                if (definition != null)
                {
                    var stack = definition.stackable;
                    if (stack < 1) stack = 1;
                    for (var i = rankItem.amount; i > 0; i = i - stack)
                    {
                        var giveamount = i >= stack ? stack : i;
                        if (giveamount < 1) return;
                        var item = ItemManager.Create(definition, giveamount, rankItem.skin);
                        if (item != null)
                        {
                            var weapon = item.GetHeldEntity() as BaseProjectile;
                            if (weapon != null)
                            {
                                if (!string.IsNullOrEmpty(rankItem.ammoType))
                                {
                                    var ammoType = itemDefs[rankItem.ammoType];
                                    if (ammoType != null)
                                        weapon.primaryMagazine.ammoType = ammoType;
                                }
                                var ammo = rankItem.ammo - weapon.primaryMagazine.capacity;
                                if (ammo <= 0)
                                    weapon.primaryMagazine.contents = rankItem.ammo;
                                else
                                {
                                    weapon.primaryMagazine.contents = weapon.primaryMagazine.capacity;
                                    GiveItem(player, new RankItem { shortname = weapon.primaryMagazine.ammoType.shortname, container = "main", amount = ammo });
                                }
                            }
                            if (rankItem.contents != null)
                                foreach (var content in rankItem.contents)
                                    BuildItem(content)?.MoveToContainer(item.contents);
                            ItemContainer cont;
                            switch (rankItem.container)
                            {
                                case "wear":
                                    cont = player.inventory.containerWear;
                                    break;
                                case "belt":
                                    cont = player.inventory.containerBelt;
                                    break;
                                default:
                                    cont = player.inventory.containerMain;
                                    break;
                            }
                            item.MoveToContainer(cont);
                            return;
                        }
                    }
                }
            }
            Puts("Error making item: " + rankItem.shortname);
        }        
        private string GetWeapon(HitInfo hitInfo, string def = "")
        {
            var item = hitInfo.Weapon?.GetItem();
            if (item == null && hitInfo.WeaponPrefab == null) return def;
            var shortname = item?.info.shortname ?? hitInfo.WeaponPrefab.name;
            if (shortname == "survey.charge" || shortname == "survey_charge.deployed") return "surveycharge";
            shortname = shortname.Replace(".prefab", string.Empty);
            shortname = shortname.Replace(".deployed", string.Empty);
            shortname = shortname.Replace(".entity", "");
            shortname = shortname.Replace("_", ".");            
            switch (shortname)
            {
                case "rocket.basic":
                case "rocket.fire":
                case "rocket.hv":
                case "rocket.smoke":
                    shortname = "rocket.launcher";
                    break;
            }
            return shortname;
        }
        #endregion

        #region Scoring
        void AddKill(BasePlayer player, BasePlayer victim, string shortname)
        {
            if (gameEnding) return;
            var gunGamePlayer = player.GetComponent<GunGamePlayer>();
            if (gunGamePlayer == null)
                return;

            var leveled = false;
            gunGamePlayer.kills++;
            RankItem rankItem;
            if (weaponSet.Weapons.TryGetValue(gunGamePlayer.level, out rankItem) && rankItem.shortname.Equals(shortname))
            {
                leveled = true;
                gunGamePlayer.level++;               
            }
            EventManager.AddTokens(player.userID, configData.EventSettings.TokensOnKill);
            SendMessage(string.Format(GGMessageKill, player.displayName, gunGamePlayer.kills, gunGamePlayer.level, victim.displayName));
            UpdateScores();
            CheckScores(player, leveled);            
        }
        void CheckScores(BasePlayer player = null, bool leveled = false, bool timelimit = false)
        {
            if (gameEnding) return;
            if (GunGamePlayers.Count == 0)
            {
                gameEnding = true;
                EventManager.BroadcastToChat(EventMessageNoMorePlayers);
                EventManager.CloseEvent();
                EventManager.EndEvent();
                return;
            }
            if (GunGamePlayers.Count == 1)
            {
                Winner(new List<BasePlayer> { GunGamePlayers[0].player });
                return;
            }

            if (player != null)
            {
                var ggPlayer = player.GetComponent<GunGamePlayer>();
                if (ggPlayer != null)
                {
                    if (ggPlayer.level >= weaponSet.Weapons.Count + 1)
                    {
                        Winner(new List<BasePlayer> { ggPlayer.player });
                        return;
                    }
                }
            }

            if (timelimit)
            {
                List<BasePlayer> winners = new List<BasePlayer>();
                int score = 0;
                foreach (var ggPlayer in GunGamePlayers)
                {
                    if (ggPlayer.level > score)
                    {
                        winners.Clear();
                        winners.Add(ggPlayer.player);
                        score = ggPlayer.level;
                    }
                    else if (ggPlayer.level == score)
                        winners.Add(ggPlayer.player);                    
                }                
                Winner(winners);
            } 

            if (player != null)
                GiveGear(player);
        }
        void Winner(List<BasePlayer> winners)
        {
            gameEnding = true;
            string winnerNames = "";
            for (int i = 0; i < winners.Count; i++)
            {
                EventManager.AddTokens(winners[i].userID, configData.EventSettings.TokensOnWin, true);
                winnerNames += winners[i].displayName;
                if (winners.Count > i + 1)
                    winnerNames += ", ";
            } 
            EventManager.BroadcastToChat(string.Format(EventMessageWon, winnerNames));
            EventManager.CloseEvent();
            EventManager.EndEvent();
        }
        #endregion

        #region API
        [HookMethod("GetWeaponSets")]
        public string[] GetWeaponSets() => classData.WeaponConfigs.Keys.ToArray();

        [HookMethod("ChangeWeaponSet")]
        private object ChangeWeaponSet(string name)
        {
            if (classData.WeaponConfigs.ContainsKey(name))
            {
                weaponSet = classData.WeaponConfigs[name];
                currentSet = name;
                return true;
            }
            return "Invalid weapon set";
        }
        #endregion

        #region Config        
        private ConfigData configData;
        class EventSettings
        {
            public string DefaultSpawnfile { get; set; }
            public string DefaultZoneID { get; set; }
            public string DefaultWeaponSet { get; set; }
            public int TokensOnKill { get; set; }
            public int TokensOnWin { get; set; }
            public bool ShowScoreboard { get; set; }
            public bool UseUINotifications { get; set; }
        }
        class GameSettings
        {
            public RankItem DowngradeWeapon { get; set; }
            public bool UseDowngradeWeapon { get; set; }
            public bool DowngradeNotification { get; set; }
            public int DisableDowngradeWeaponTimer { get; set; }
            public float StartHealth { get; set; }
            public bool CloseEventOnStart { get; set; }
        }
        
        class ConfigData
        {
            public EventSettings EventSettings { get; set; }
            public GameSettings GameSettings { get; set; }
        }
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                GameSettings = new GameSettings
                {
                    CloseEventOnStart = true,
                    DisableDowngradeWeaponTimer = 5,
                    DowngradeNotification = true,
                    DowngradeWeapon = new RankItem
                    {
                        amount = 1,
                        container = "belt",
                        shortname = "machete",
                        name = "Machete"
                    },
                    UseDowngradeWeapon = true,
                    StartHealth = 100
                },
                EventSettings = new EventSettings
                {
                    DefaultSpawnfile = "ggspawns",
                    DefaultWeaponSet = "default",
                    DefaultZoneID = "ggzone",
                    TokensOnKill = 1,
                    TokensOnWin = 5,
                    ShowScoreboard = true,
                    UseUINotifications = true
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

        #region Data Management
        void SaveData() => data.WriteObject(classData);
        void LoadData()
        {
            try
            {
                classData = data.ReadObject<ClassData>();                
            }
            catch
            {
                classData = new ClassData();
            }            
        }
        class ClassData
        {
            public Dictionary<string, WeaponSet> WeaponConfigs = new Dictionary<string, WeaponSet>();
        }        
        #endregion

        #region Default Configs
        void CreateDefaultConfig()
        {
            var DefaultConfig = new WeaponSet
            {
                PlayerGear = new List<RankItem>
                {
                    new RankItem
                        {
                            shortname = "shoes.boots",
                            container = "wear",
                            skin = 0,
                            amount = 1
                        },
                    new RankItem
                        {
                            shortname = "attire.hide.pants",
                            container = "wear",
                            skin = 0,
                            amount = 1
                        },                    
                    new RankItem
                        {
                            shortname = "riot.helmet",
                            container = "wear",
                            skin = 0,
                            amount = 1
                        }
                },
                Weapons = new Dictionary<int, RankItem>
                {
                     {
                        1, new RankItem
                        {
                            name = "Assault Rifle (holosight)",
                            shortname = "rifle.ak",
                            container = "belt",
                            ammoType = "ammo.rifle",
                            ammo = 120,
                            amount = 1,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        2, new RankItem
                        {
                            name = "Thompson (holosight)",
                            shortname = "smg.thompson",
                            container = "belt",
                            ammoType = "ammo.pistol",
                            amount = 1,
                            ammo = 120,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        3, new RankItem
                        {
                            name = "Pump Shotgun (holosight)",
                            shortname = "shotgun.pump",
                            container = "belt",
                            ammoType = "ammo.shotgun",
                            amount = 1,
                            ammo = 60,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        4, new RankItem
                        {
                            name = "SMG (holosight)",
                            shortname = "smg.2",
                            container = "belt",
                            ammoType = "ammo.pistol",
                            amount = 1,
                            ammo = 120,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        5, new RankItem
                        {
                            name = "Bolt Action Rifle (holosight)",
                            shortname = "rifle.bolt",
                            container = "belt",
                            amount = 1,
                            ammoType = "ammo.rifle",
                            ammo = 120,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        6, new RankItem
                        {
                            name = "Semi-Auto Rifle (holosight)",
                            shortname = "rifle.semiauto",
                            container = "belt",
                            ammoType = "ammo.rifle",
                            amount = 1,
                            ammo = 120,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        7, new RankItem
                        {
                            name = "Semi-Auto Pistol (holosight)",
                            shortname = "pistol.semiauto",
                            container = "belt",
                            ammoType = "ammo.pistol",
                            amount = 1,
                            ammo = 120,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        8, new RankItem
                        {
                            name = "Revolver (holosight)",
                            shortname = "pistol.revolver",
                            container = "belt",
                            amount = 1,
                            ammoType = "ammo.pistol",
                            ammo = 120,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        9, new RankItem
                        {
                            name = "Waterpipe Shotgun (holosight)",
                            shortname = "shotgun.waterpipe",
                            container = "belt",
                            amount = 1,
                            ammoType = "ammo.handmade.shell",
                            ammo = 40,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        10, new RankItem
                        {
                            name = "Hunting Bow",
                            shortname = "bow.hunting",
                            container = "belt",
                            amount = 1,
                            ammoType = "arrow.hv",
                            ammo = 40
                        }
                    },
                    {
                        11, new RankItem
                        {
                            name = "Eoka Pistol",
                            shortname = "pistol.eoka",
                            container = "belt",
                            amount = 1,
                            ammoType = "ammo.handmade.shell",
                            ammo = 40
                        }
                    },
                    {
                        12, new RankItem
                        {
                            name = "Stone Spear",
                            shortname = "spear.stone",
                            container = "belt",
                            amount = 2
                        }
                    },
                    {
                        13, new RankItem
                        {
                            name = "Salvaged Cleaver",
                            shortname = "salvaged.cleaver",
                            container = "belt",
                            amount = 2
                        }
                    },
                    {
                        14, new RankItem
                        {
                            name = "Mace",
                            shortname = "mace",
                            container = "belt",
                            amount = 2
                        }
                    },
                    {
                        15, new RankItem
                        {
                            name = "Bone Club",
                            shortname = "bone.club",
                            container = "belt",
                            amount = 2
                        }
                    },
                    {
                        16, new RankItem
                        {
                            name = "Bone Knife",
                            shortname = "knife.bone",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        17, new RankItem
                        {
                            name = "Longsword",
                            shortname = "longsword",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        18, new RankItem
                        {
                            name = "Salvaged Sword",
                            shortname = "salvaged.sword",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        19, new RankItem
                        {
                            name = "Icepick",
                            shortname = "icepick.salvaged",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        20, new RankItem
                        {
                            name = "Salvaged Axe",
                            shortname = "axe.salvaged",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        21, new RankItem
                        {
                            name = "Pickaxe",
                            shortname = "pickaxe",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        22, new RankItem
                        {
                            name = "Hatchet",
                            shortname = "hatchet",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        23, new RankItem
                        {
                            name = "Rock",
                            shortname = "rock",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        24, new RankItem
                        {
                            name = "Torch",
                            shortname = "torch",
                            container = "belt",
                            amount = 1
                        }
                    },
                    {
                        25, new RankItem
                        {
                            name = "Crossbow",
                            shortname = "crossbow",
                            container = "belt",
                            amount = 1,
                            ammoType = "arrow.hv",
                            ammo = 40,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        26, new RankItem
                        {
                            name = "LMG",
                            shortname = "lmg.m249",
                            container = "belt",
                            amount = 1,
                            ammoType = "ammo.rifle",
                            ammo = 120,
                            contents = new [] {"weapon.mod.holosight"}
                        }
                    },
                    {
                        27, new RankItem
                        {
                            name = "Timed Explosive",
                            shortname = "explosive.timed",
                            container = "belt",
                            amount = 20
                        }
                    },
                    {
                        28, new RankItem
                        {
                            name = "Survey Charge",
                            shortname = "surveycharge",
                            container = "belt",
                            amount = 20
                        }
                    },
                    {
                        29, new RankItem
                        {
                            name = "Grenade",
                            shortname = "grenade.f1",
                            container = "belt",
                            amount = 20
                        }
                    },
                    {
                        30, new RankItem
                        {
                            name = "Rocket Launcher",
                            shortname = "rocket.launcher",
                            container = "belt",
                            amount = 1,
                            ammoType = "ammo.rocket.basic",
                            ammo = 20
                        }
                    }
                }
            };
            classData.WeaponConfigs.Add("default", DefaultConfig);
            SaveData();
        }
        #endregion

        #region Messaging
        void SendMessage(string message)
        {
            if (configData.EventSettings.UseUINotifications)
                EventManager.PopupMessage(message);
            else PrintToChat(message);
        }
        string EventMessageWon = "{0} has won the event";
        string EventMessageNoMorePlayers = "The Gun Game Arena has no more players, auto-closing.";
        string GGMessageKill = "{3} was killed by {0}, who is now rank {2} with {1} kill(s)";
        string EventMessageOpenBroadcast = "Gungame : In GunGame, every player you kill will advance you 1 rank, each rank has a new weapon. But beware, if you are killed by a downgrade weapon you will lose a rank!";

        #endregion

        #region WeaponSet Creator
        [ChatCommand("gg")]
        private void cmdGunGame(BasePlayer player, string command, string[] args)
        {
            if (!isAuth(player)) return;
            if (args == null || args.Length == 0)
            {
                if (!setCreator.ContainsKey(player.userID))
                {
                    SendReply(player, "<color=orange>Gungame weapon set creator</color>");
                    SendReply(player, "You can create new weapon sets by following these instructions;");
                    SendReply(player, "First type <color=orange>\"/gg newset <name>\"</color> replacing <name> with the name of your new weapon set");
                }
                else
                {
                    SendReply(player, "Weapon sets consist of 2 parts. First is the weapons for each rank, and the second is the gear the players will receive");
                    SendReply(player, "For help adding weapons type <color=orange>\"/gg rank\"</color>");
                    SendReply(player, "For help adding player gear type <color=orange>\"/gg gear\"</color>");                    
                }                
                return;
            }
            switch (args[0].ToLower())
            {
                case "newset":
                    if (args.Length >= 2)
                    {
                        if (classData.WeaponConfigs.ContainsKey(args[1]))
                        {
                            SendReply(player, $"You already have a weapon set called <color=orange>{args[1]}</color>");
                            return;
                        }
                        if (!setCreator.ContainsKey(player.userID))
                            setCreator.Add(player.userID, new SetCreator());
                        setCreator[player.userID] = new SetCreator
                        {
                            name = args[1],
                            set = new WeaponSet()
                        };
                        SendReply(player, $"You are now creating a new weapon set called <color=orange>{args[1]}</color>");
                        SendReply(player, "You can now type <color=orange>\"/gg\"</color> to view your options");
                        SendReply(player, "Once you have finished type <color=orange>\"/gg save\"</color> to save your weapon set");                      
                    }
                    return;
                case "rank":
                    {
                        if (!setCreator.ContainsKey(player.userID))
                        {
                            SendReply(player, "<color=orange>You must start a new set before adding weapons</color>");
                            return;
                        }
                        if (args.Length >= 2 && args[1].ToLower() == "add")
                        {
                            int amount = 1;
                            if (args.Length == 3)
                                int.TryParse(args[2], out amount);
                            SaveWeapon(player, amount);
                            return;
                        }
                        else
                        {
                            SendReply(player, "To add a new weapon to your set, spawn the weapon you wish to use along with any attachments it will have. If the weapon has ammo fill the clip with your desired ammo type");
                            SendReply(player, "Once that is done place the weapon in your hands and type <color=orange>\"/gg rank add <opt:amount>\"</color> if the weapon takes ammo replace <opt:amount> with the amount of ammo you wish to supply. Ranks will be added in succession (ie rank 1 then rank 2 etc)");
                        }
                    }
                    return;
                case "gear":
                    if (!setCreator.ContainsKey(player.userID))
                    {
                        SendReply(player, "You must start a new set before adding weapons");
                        return;
                    }
                    if (args.Length == 2 && args[1].ToLower() == "set")
                    {
                        SetPlayerKit(player);
                    }
                    else
                    {
                        SendReply(player, "To add gear for your players set yourself up with attire, meds, armour and whatever else you want to players to have");
                        SendReply(player, "Once you are setup type <color=orange>\"/gg gear set\"</color> to set the player gear. Note that you can add meds and attire to this kit");
                    }                    
                    return;
                case "save":
                    if (setCreator.ContainsKey(player.userID))
                    {
                        if (setCreator[player.userID].set.Weapons.Count < 1)
                        {
                            SendReply(player, "<color=orange>You have not set any weapons yet</color>");
                            return;
                        }
                        if (setCreator[player.userID].set.PlayerGear.Count < 1)
                        {
                            SendReply(player, "<color=orange>You have not set the players gear yet</color>");
                            return;
                        }
                        classData.WeaponConfigs.Add(setCreator[player.userID].name, setCreator[player.userID].set);
                        SaveData();
                        SendReply(player, $"You have successfully saved a new weapon set called <color=orange>{setCreator[player.userID].name}</color>");
                        setCreator.Remove(player.userID);                        
                        return;
                    }
                    return;
                default:
                    break;
            }
        }
        private bool isAuth(BasePlayer player)
        {
            if (player.net.connection.authLevel >= 1) return true;
            return false;
        }
        private void SaveWeapon(BasePlayer player, int ammo = 1)
        {            
            var rank = setCreator[player.userID].set.Weapons.Count + 1;

            RankItem weaponEntry = new RankItem();
            Item item = player.GetActiveItem();
            if (item != null)
                if (item.info.category == ItemCategory.Weapon)
                {
                    BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                    if (weapon != null)
                        if (weapon.primaryMagazine != null)
                        {
                            List<string> mods = new List<string>();
                            if (item.contents != null)
                                foreach (var mod in item.contents.itemList)
                                    if (mod.info.itemid != 0) mods.Add(mod.info.shortname);
                            if (mods != null) weaponEntry.contents = mods.ToArray();

                            weaponEntry.ammoType = weapon.primaryMagazine.ammoType.shortname;
                            weaponEntry.ammo = ammo;
                        }

                    weaponEntry.amount = item.amount;
                    weaponEntry.container = "belt";
                    weaponEntry.name = item.info.displayName.english;
                    weaponEntry.shortname = item.info.shortname;
                    weaponEntry.skin = item.skin;

                    setCreator[player.userID].set.Weapons.Add(rank, weaponEntry);
                    SendReply(player, string.Format("You have successfully added <color=orange>{0}</color> as the weapon for Rank <color=orange>{1}</color>", weaponEntry.name, rank));
                    return;
                }
            SendReply(player, "<color=orange>Unable to save item.</color> You must put a weapon in your hands");
        }
        private void SetPlayerKit(BasePlayer player)
        {
            setCreator[player.userID].set.PlayerGear.Clear();

            foreach (var item in player.inventory.containerWear.itemList)
                SaveItem(player, item, "wear");

            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.info.category == ItemCategory.Medical || item.info.category == ItemCategory.Attire)
                    SaveItem(player, item, "main");                
                else SendReply(player, string.Format("Did not save <color=orange>{0}</color>, you may only save clothing and meds for player gear", item.info.displayName.translated));
            }

            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.info.category == ItemCategory.Medical || item.info.category == ItemCategory.Attire)
                    SaveItem(player, item, "belt");                
                else SendReply(player, string.Format("Did not save <color=orange>{0}</color>, you may only save clothing and meds for player gear", item.info.displayName.translated));
            }

            SaveConfig(configData);
            SendReply(player, "<color=orange>You have successfully saved a new player kit for Gungame</color>");
        }
        private void SaveItem(BasePlayer player, Item item, string cont)
        {
            RankItem gear = new RankItem
            {
                name = item.info.displayName.english,
                amount = item.amount,
                container = cont,
                shortname = item.info.shortname,
                skin = item.skin
            };
            setCreator[player.userID].set.PlayerGear.Add(gear);
        }
        #endregion
    }
}
