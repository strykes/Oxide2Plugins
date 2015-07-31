using System.Reflection;
using System;
using System.Data;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("Event Manager", "Reneb", "1.2.0", ResourceId = 740)]
    class EventManager : RustPlugin
    {
        ////////////////////////////////////////////////////////////
        // Setting all fields //////////////////////////////////////
        ////////////////////////////////////////////////////////////
        [PluginReference]
        Plugin Spawns;

        [PluginReference]
        Plugin Kits;

        [PluginReference]
        Plugin ZoneManager;

        [PluginReference]
        Plugin DeadPlayersList;

        private string EventSpawnFile;
        private string EventGameName;
        private string itemname;
        

        private bool EventOpen;
        private bool EventStarted;
        private bool EventEnded;
        private bool EventPending;
        private int EventMaxPlayers = 0;
        private int EventMinPlayers = 0;
        private int EventAutoNum = -1;
        private bool isBP;
        private bool Changed;

        private List<string> EventGames;
        private List<EventPlayer> EventPlayers;

        private ItemDefinition itemdefinition;

        private int stackable;
        private int giveamount;
        
		
		public Oxide.Plugins.Timer AutoArenaTimer;
		public bool AutoEventLaunched = false;

        ////////////////////////////////////////////////////////////
        // EventPlayer class to store informations /////////////////
        ////////////////////////////////////////////////////////////
        class EventPlayer : MonoBehaviour
        {
            public BasePlayer player;

            public bool inEvent;
            public bool savedInventory;
            public bool savedHome;

            public Dictionary<string, int> Belt;
            public Dictionary<string, int> Wear;
            public Dictionary<string, int> Main;

            public Vector3 Home;

            void Awake()
            {
                inEvent = true;
                savedInventory = false;
                savedHome = false;
                player = GetComponent<BasePlayer>();
                Belt = new Dictionary<string, int>();
                Wear = new Dictionary<string, int>();
                Main = new Dictionary<string, int>();
            }

            public void SaveHome()
            {
                if (!savedHome)
                    Home = player.transform.position;
                savedHome = true;
            }
            public void TeleportHome()
            {
                if (!savedHome)
                    return;
                ForcePlayerPosition(player, Home);
                savedHome = false;
            }

            public void SaveInventory()
            {
                if (savedInventory)
                    return;
                var containerbelt = player.inventory.containerBelt.itemList;
                var containermain = player.inventory.containerMain.itemList;
                var containerwear = player.inventory.containerWear.itemList;
                string itemname = string.Empty;
                Belt.Clear();
                Main.Clear();
                Wear.Clear();

                foreach (Item item in containerbelt)
                {
                    itemname = MakeItemName(item);
                    if (!(Belt.ContainsKey(itemname)))
                        Belt.Add(itemname, 0);
                    Belt[itemname] = Belt[itemname] + item.amount;
                }

                foreach (Item item in containermain)
                {
                    itemname = MakeItemName(item);
                    if (!(Main.ContainsKey(itemname)))
                        Main.Add(itemname, 0);
                    Main[itemname] = Main[itemname] + item.amount;
                }
                foreach (Item item in containerwear)
                {
                    itemname = MakeItemName(item);
                    if (!(Wear.ContainsKey(itemname)))
                        Wear.Add(itemname, 0);
                    Wear[itemname] = Wear[itemname] + item.amount;
                }
                savedInventory = true;
            }

            public void RestoreInventory()
            {
                var containerbelt = player.inventory.containerBelt;
                var containermain = player.inventory.containerMain;
                var containerwear = player.inventory.containerWear;
                foreach (KeyValuePair<string, int> pair in Belt)
                {
                    GiveGoodItem(player, pair.Key, pair.Value, containerbelt);
                }
                foreach (KeyValuePair<string, int> pair in Main)
                {
                    GiveGoodItem(player, pair.Key, pair.Value, containermain);
                }
                foreach (KeyValuePair<string, int> pair in Wear)
                {
                    GiveGoodItem(player, pair.Key, pair.Value, containerwear);
                }
                savedInventory = false;
            }
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Some Static methods that can be called from the EventPlayer Class /////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        static void ForcePlayerPosition(BasePlayer player, Vector3 destination)
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
        static string MakeItemName(Item item)
        {
            return string.Format("{0} {1}", item.info.shortname, item.isBlueprint.ToString());
        }
        static void GetItemGood(string itemdata, out bool isBP, out string itemname)
        {
            if (!(bool.TryParse(itemdata.Substring(itemdata.IndexOf(" ") + 1), out isBP)))
                isBP = false;
            itemname = itemdata.Substring(0, itemdata.IndexOf(" "));
        }
        static void GiveGoodItem(BasePlayer player, string itemdata, int amount, ItemContainer container)
        {
            bool isBP;
            string itemname;
            GetItemGood(itemdata, out isBP, out itemname);
            GiveItem(player, itemname, amount, container, isBP);
        }
        static void GiveItem(BasePlayer player, string name, int amount, ItemContainer container, bool isBlueprint)
        {
            var itemdefinition = ItemManager.FindItemDefinition(name);
            if (itemdefinition != null)
            {
                int stackable = 1;
                if (itemdefinition.stackable == null || itemdefinition.stackable < 1) stackable = 1;
                else stackable = itemdefinition.stackable;
                for (var i = amount; i > 0; i = i - stackable)
                {
                    var giveamount = 0;
                    if (i >= stackable)
                        giveamount = stackable;
                    else
                        giveamount = i;
                    if (giveamount > 0)
                    {
                        player.inventory.GiveItem(ItemManager.CreateByItemID(itemdefinition.itemid, giveamount, isBlueprint), container);
                    }
                }
            }
        }


        //////////////////////////////////////////////////////////////////////////////////////
        // Oxide Hooks ///////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        void Loaded()
        {
            Changed = false;
            EventGames = new List<string>();
            EventPlayers = new List<EventPlayer>();
            LoadData();
        }
        void OnServerInitialized()
        {
            EventOpen = false;
            EventStarted = false;
            EventEnded = true;
            EventPending = false;
            EventGameName = defaultGame;
            timer.Once(0.1f, () => InitializeZones());
            timer.Once(0.2f, () => InitializeGames());
        }
        void InitializeGames()
        {
            Interface.CallHook("RegisterGame");
            SelectSpawnfile(defaultSpawnfile);
        }  
        void Unload()
        { 
            EndEvent();
            var objects = GameObject.FindObjectsOfType(typeof(EventPlayer));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            ResetZones();
        }
        
        void OnPlayerRespawned(BasePlayer player)
        {
            if (!(player.GetComponent<EventPlayer>())) return;
            if (player.GetComponent<EventPlayer>().inEvent)
            {
                if (!EventStarted) return;
                Interface.CallHook("OnEventPlayerSpawn", new object[] { player });
            }
            else
            {
                RedeemInventory(player);
                TeleportPlayerHome(player);
                TryErasePlayer(player);
            }
        }

        void OnPlayerAttack(BasePlayer player, HitInfo hitinfo)
        {
            if (!EventStarted) return;
            if (player.GetComponent<EventPlayer>() == null || !(player.GetComponent<EventPlayer>().inEvent))
            {
                return;
            }
            else if (hitinfo.HitEntity != null)
            {
                Interface.CallHook("OnEventPlayerAttack", new object[] { player, hitinfo });
            }
            return;
        }
        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (!EventStarted) return;
            if (!(entity is BasePlayer)) return;
            if ((entity as BasePlayer).GetComponent<EventPlayer>() == null) return;
            Interface.CallHook("OnEventPlayerDeath", new object[] { (entity as BasePlayer), hitinfo });
            return;
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player.GetComponent<EventPlayer>() != null)
            {
                LeaveEvent(player);
            }
        }
        ////////////////////////////////////////////////////////////
        // Zone Management
        ////////////////////////////////////////////////////////////
        void InitializeZones()
        {
            foreach (KeyValuePair<string, EventZone> pair in zonelogs)
            {
                InitializeZone(pair.Key);
            }
        }
        void InitializeZone(string name)
        {
            if (zonelogs[name] == null) return;
            ZoneManager?.Call("CreateOrUpdateZone", name, new string[] { "radius", zonelogs[name].radius }, zonelogs[name].GetPosition(), "undestr", "true", "nobuild", "true", "nodeploy", "true");
            if (EventGames.Contains(name))
                Interface.CallHook("OnPostZoneCreate", name);
        }
        void ResetZones()
        {
            foreach (string game in EventGames)
            {
                ZoneManager?.Call("EraseZone", game);
            }
        }
        void UpdateZone(string name, string[] args)
        {
            ZoneManager?.Call("CreateOrUpdateZone", name, args);
        }
        public class EventZone
        {
            public string name;
            public string x;
            public string y;
            public string z;
            public string radius;
            Vector3 position;

            public EventZone(string name, Vector3 position, float radius)
            {
                this.name = name;
                this.x = position.x.ToString();
                this.y = position.y.ToString();
                this.z = position.z.ToString();
                this.radius = radius.ToString();
            }
            public Vector3 GetPosition()
            {
                if (position == default(Vector3))
                    position = new Vector3(float.Parse(this.x), float.Parse(this.y), float.Parse(this.z));
                return position;
            }

        }

        static StoredData storedData;
        static Hash<string, EventZone> zonelogs = new Hash<string, EventZone>();
        static Hash<string, Reward> rewards = new Hash<string, Reward>();

        class StoredData
        {
            public HashSet<EventZone> ZoneLogs = new HashSet<EventZone>();
            public Hash<string, string> Tokens = new Hash<string, string>();
            public HashSet<Reward> Rewards = new HashSet<Reward>();

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
            Interface.GetMod().DataFileSystem.WriteObject("EventManager", storedData);
        }

        void LoadData()
        {
            zonelogs.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("EventManager");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var thelog in storedData.ZoneLogs)
            {
                zonelogs[thelog.name] = thelog;
            }
            foreach (var thelog in storedData.Rewards)
            {
                rewards[thelog.name] = thelog;
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Tokens Manager
        //////////////////////////////////////////////////////////////////////////////////////

        Dictionary<string, string> displaynameToShortname = new Dictionary<string, string>();

        private void InitializeTable()
        {
            displaynameToShortname.Clear();
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                displaynameToShortname.Add(itemdef.displayName.english.ToString().ToLower(), itemdef.shortname.ToString());
            }
        }
        object GiveReward(BasePlayer player, string rewardname, int amount)
        {
            if (rewards[rewardname] == null) return "This reward doesn't exist";
            amount = amount * (rewards[rewardname]).GetAmount();
            if (rewards[rewardname].IsKit())
            {
                if (Kits == null) return "Kits plugin couldn't be found";
                if (!(bool)Kits.Call("isKit", rewards[rewardname].item)) return "The kit doesn't exist anymore";
                for (int i = 1; i < amount; i++)
                {
                    Kits.Call("GiveKit", player, rewards[rewardname].item);
                }
                return (bool)Kits.Call("GiveKit", player, rewards[rewardname].item);
            }
            var definition = ItemManager.FindItemDefinition(rewards[rewardname].item);
            if (definition == null)
                return string.Format("Item not found {0}", rewards[rewardname].item);
            if (definition.stackable > 1)
                player.inventory.GiveItem(ItemManager.CreateByItemID((int)definition.itemid, amount, false), player.inventory.containerMain);
            else
            {
                for (int i = 0; i < amount; i++)
                {
                    player.inventory.GiveItem(ItemManager.CreateByItemID((int)definition.itemid, 1, false), player.inventory.containerMain);
                }
            }
            return true;
        }

        public class Reward
        {
            public string name;
            public string cost;
            public string kit;
            public string item;
            public string amount;

            public Reward()
            {

            }
            public Reward(string name, int cost, bool kit, string item, int amount)
            {
                this.name = name;
                this.cost = cost.ToString();
                this.kit = kit.ToString();
                this.item = item;
                this.amount = amount.ToString();
            }
            public int GetCost()
            {
                return int.Parse(cost);
            }
            public int GetAmount()
            {
                return int.Parse(amount);
            }
            public bool IsKit()
            {
                return Convert.ToBoolean(kit);
            }
        }
        void AddTokens(string userid, int amount)
        {
            storedData.Tokens[userid] = (GetTokens(userid) + amount).ToString();
        }

        int GetTokens(string userid)
        {
            if (storedData.Tokens[userid] == null)
                return 0;
            return int.Parse(storedData.Tokens[userid]);
        }

        void RemoveTokens(string userid, int amount)
        {
            storedData.Tokens[userid] = (GetTokens(userid) - amount).ToString();
        }

        void SetTokens(string userid, int amount)
        {
            storedData.Tokens[userid] = amount.ToString();
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Configs Manager ///////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }
        object GetConfig(string menu, string datavalue, object defaultValue)
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
        private static string MessagesPermissionsNotAllowed = "You are not allowed to use this command";
        private static string MessagesEventNotSet = "An Event game must first be chosen.";
        private static string MessagesEventNoSpawnFile = "A spawn file must first be loaded.";
        private static string MessagesEventAlreadyOpened = "The Event is already open.";
        private static string MessagesEventAlreadyClosed = "The Event is already closed.";
        private static string MessagesEventAlreadyStarted = "An Event game has already started.";


        private static string MessagesEventOpen = "The Event is now open for : {0} !  Type /event_join to join!";
        private static string MessagesEventClose = "The Event entrance is now closed!";
        private static string MessagesEventCancel = "The Event was cancelled!";
        private static string MessagesEventNoGamePlaying = "An Event game is not underway.";
        private static string MessagesEventEnd = "Event: {0} is now over!";
        private static string MessagesEventAlreadyJoined = "You are already in the Event.";
        private static string MessagesEventJoined = "{0} has joined the Event!  (Total Players: {1})";
        private static string MessagesEventLeft = "{0} has left the Event! (Total Players: {1})";
        private static string MessagesEventBegin = "Event: {0} is about to begin!";
        private static string MessagesEventNotInEvent = "You are not currently in the Event.";
        private static string MessagesEventNotAnEvent = "This Game {0} isn't registered, did you reload the game after loading Event - Core?";
        private static string MessagesEventCloseAndEnd = "The Event needs to be closed and ended before using this command.";


        private static string MessagesEventStatusOpen = "The Event {0} is currently opened for registration: /event_join";
        private static string MessagesEventStatusOpenStarted = "The Event {0} has started, but is still opened: /event_join";
        private static string MessagesEventStatusClosedEnd = "There is currently no event";
        private static string MessagesEventStatusClosedStarted = "The Event {0} has already started, it's too late to join.";

		private static string MessagesEventMaxPlayers = "The Event {0} has reached max players. You may not join for the moment";
		private static string MessagesEventMinPlayers = "The Event {0} has reached min players and will start in {1} seconds";
        private static bool EventAutoEvents = true;
        private static int EventAutoInterval = 600;
        private static List<Dictionary<string,object>> EventAutoConfig = CreateDefaultAutoConfig();
        
        private static string MessageRewardCurrentReward = "You currently have {0} for the /reward shop";
        private static string MessageRewardCurrent = "You have {0} tokens";
        private static string MessageRewardHelp = "/reward \"RewardName\" Amount";
        private static string MessageRewardItem = "Reward Name: {0} - Cost: <color={4}>{1}</color> - {2} - Amount: {3}";
        private static string MessageRewardWrong = "This reward doesn't exist";
        private static string MessageRewardNegative = "The amount to buy can't be 0 or negative.";
        private static string MessageRewardNotEnoughTokens = "You don't have enough tokens to buy {1} of {0}.";

        private static string noPlayerFound = "No players found";
        private static string multipleNames = "Multiple players found";

        private static string defaultGame = "Deathmatch";
        private static string defaultSpawnfile = "deathmatchspawns";
        private static int eventAuth = 1;

        void Init()
        {
            CheckCfg<int>("Settings - authLevel", ref eventAuth);

            CheckCfg<string>("Default - Game", ref defaultGame);
            CheckCfg<string>("Default - Spawnfile", ref defaultSpawnfile);
            
            CheckCfg<bool>("AutoEvents - Activated", ref EventAutoEvents);
            CheckCfg<int>("AutoEvents - Interval between 2 events", ref EventAutoInterval);
            CheckCfg<List<object>>("AutoEvents - Config", ref EventAutoConfig);

            CheckCfg<string>("Messages - Permissions - Not Allowed", ref MessagesPermissionsNotAllowed);
            CheckCfg<string>("Messages - Event Error - Not Set", ref MessagesEventNotSet);
            CheckCfg<string>("Messages - Event Error - No SpawnFile", ref MessagesEventNoSpawnFile);
            CheckCfg<string>("Messages - Event Error - Already Opened", ref MessagesEventAlreadyOpened);
            CheckCfg<string>("Messages - Event Error - Already Closed", ref MessagesEventAlreadyClosed);
            CheckCfg<string>("Messages - Event Error - No Games Undergoing", ref MessagesEventNoGamePlaying);
            CheckCfg<string>("Messages - Event Error - Already Joined", ref MessagesEventAlreadyJoined);
            CheckCfg<string>("Messages - Event Error - Already Started", ref MessagesEventAlreadyStarted);
            CheckCfg<string>("Messages - Event Error - Not In Event", ref MessagesEventNotInEvent);
            CheckCfg<string>("Messages - Event Error - Not Registered Event", ref MessagesEventNotAnEvent);
            CheckCfg<string>("Messages - Event Error - Close&End", ref MessagesEventCloseAndEnd);

            CheckCfg<string>("Messages - Error - No players found", ref noPlayerFound);
            CheckCfg<string>("Messages - Error - Multiple players found", ref multipleNames);

            CheckCfg<string>("Messages - Status - Closed & End", ref MessagesEventStatusClosedEnd);
            CheckCfg<string>("Messages - Status - Closed & Started", ref MessagesEventStatusClosedStarted);
            CheckCfg<string>("Messages - Status - Open", ref MessagesEventStatusOpen);
            CheckCfg<string>("Messages - Status - Open & Started", ref MessagesEventStatusOpenStarted);

            CheckCfg<string>("Messages - Event - Opened", ref MessagesEventOpen);
            CheckCfg<string>("Messages - Event - Closed", ref MessagesEventClose);
            CheckCfg<string>("Messages - Event - Cancelled", ref MessagesEventCancel);
            CheckCfg<string>("Messages - Event - End", ref MessagesEventEnd);
            CheckCfg<string>("Messages - Event - Join", ref MessagesEventJoined);
            CheckCfg<string>("Messages - Event - Begin", ref MessagesEventBegin);
            CheckCfg<string>("Messages - Event - Left", ref MessagesEventLeft);
            
            CheckCfg<string>("Messages - Event - MaxPlayersReached", ref MessagesEventMaxPlayers);
			CheckCfg<string>("Messages - Event - MinPlayersReached", ref MessagesEventMinPlayers);
			
            CheckCfg<string>("Messages - Reward - Message", ref MessageRewardCurrentReward);
            CheckCfg<string>("Messages - Reward - Current", ref MessageRewardCurrent);
            CheckCfg<string>("Messages - Reward - Help", ref MessageRewardHelp);
            CheckCfg<string>("Messages - Reward - Reward Description", ref MessageRewardItem);
            CheckCfg<string>("Messages - Reward - Doesnt Exist", ref MessageRewardWrong);
            CheckCfg<string>("Messages - Reward - Negative Amount", ref MessageRewardNegative);
            CheckCfg<string>("Messages - Reward - Not Enough Tokens", ref MessageRewardNotEnoughTokens);

            SaveConfig();
        }
		
		List<Dictionary<string,object>> CreateDefaultAutoConfig()
		{
			var newautoconfiglist = new List< Dictionary<string,object> >();
			var AutoDM = new Dictionary<string,object>();
			AutoDM.Add("gametype", "Deathmatch");
			AutoDM.Add("spawnfile", "deathmatchspawnfile");
			AutoDM.Add("minplayers", "2");
			AutoDM.Add("maxplayers", "10");
			
			var AutoBF = new Dictionary<string,object>();
			AutoBF.Add("gametype", "Battlefield");
			AutoBF.Add("spawnfile", "battlefieldspawnfile");
			AutoBF.Add("minplayers", "0");
			AutoBF.Add("maxplayers", "30");
			
			
			newautoconfiglist.Add( AutoDM );
			newautoconfiglist.Add( AutoBF );
			
			return newautoconfiglist;
		}

        //////////////////////////////////////////////////////////////////////////////////////
        // Some global methods ///////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        // hasAccess /////////////////////////////////////////////////////////////////////////
        bool hasAccess(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, MessagesPermissionsNotAllowed);
                    return false;
                }
            }
            return true;
        }

        // Broadcast To The General Chat /////////////////////////////////////////////////////
        void BroadcastToChat(string msg)
        {
            Debug.Log(msg);
            ConsoleSystem.Broadcast("chat.add", new object[] { 0, "<color=orange>Event:</color> " + msg });
        }

        // Broadcast To Players in Event /////////////////////////////////////////////////////
        void BroadcastEvent(string msg)
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                SendReply(eventplayer.player, msg.QuoteSafe());
            }
        }

        void TeleportAllPlayersToEvent()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                Interface.CallHook("OnEventPlayerSpawn", new object[] { eventplayer.player });
            }
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            TeleportPlayerToEvent(player);
        }
        void TeleportPlayerToEvent(BasePlayer player)
        {
            if (!(player.GetComponent<EventPlayer>())) return;
            var targetpos = Spawns.Call("GetRandomSpawn", new object[] { EventSpawnFile });
            if (targetpos is string)
                return;
            var newpos = Spawns.Call("EventChooseSpawn", new object[] { player, targetpos });
            if (newpos is Vector3)
                targetpos = newpos;
            ZoneManager?.Call("AddPlayerToZoneKeepinlist", EventGameName, player);
            ForcePlayerPosition(player, (Vector3)targetpos);
        }

        void SaveAllInventories()
        {
            foreach (EventPlayer player in EventPlayers)
            {
                player.SaveInventory();
            }
        }
        void SaveAllHomeLocations()
        {
            foreach (EventPlayer player in EventPlayers)
            {
                player.SaveHome();
            }
        }
        void SaveInventory(BasePlayer player)
        {
            var eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            eventplayer.SaveInventory();
        }
        void SaveHomeLocation(BasePlayer player)
        {
            EventPlayer eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            eventplayer.SaveHome();
        }
        void RedeemInventory(BasePlayer player)
        {
            EventPlayer eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (player.IsDead())
                return;
            if (eventplayer.savedInventory)
            {
                eventplayer.player.inventory.Strip();
                eventplayer.RestoreInventory();
            }
        }
        void TeleportPlayerHome(BasePlayer player)
        {
            EventPlayer eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (player.IsDead())
                return;
            if (eventplayer.savedHome)
            {
                eventplayer.TeleportHome();
            }
        }
        void TryErasePlayer(BasePlayer player)
        {
            var eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (!(eventplayer.inEvent) && !(eventplayer.savedHome) && !(eventplayer.savedInventory))
                GameObject.Destroy(eventplayer);
        }
        void GivePlayerKit(BasePlayer player, string GiveKit)
        {
            Kits.Call("GiveKit", player, GiveKit);
        }
        void EjectPlayer(BasePlayer player)
        {
            if (player.IsAlive())
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                player.CancelInvoke("WoundingEnd");
                player.health = 50f;
            }
            SendReply(player, string.Format(MessageRewardCurrentReward, GetTokens(player.userID.ToString()).ToString()));
        }
        
        void EjectAllPlayers()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                EjectPlayer(eventplayer.player);
                ZoneManager?.Call("RemovePlayerFromZoneKeepinlist", EventGameName, eventplayer.player);
                eventplayer.inEvent = false;
            }
        }
        void SendPlayersHome()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                TeleportPlayerHome(eventplayer.player);
            }
        }
        void RedeemPlayersInventory()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                RedeemInventory(eventplayer.player);
            }
        }
        void TryEraseAllPlayers()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
                TryErasePlayer(eventplayer.player);
            }
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Methods to Change the Arena Status ////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        object OpenEvent()
        {
            if (EventGameName == null) return MessagesEventNotSet;
            else if (EventSpawnFile == null) return MessagesEventNoSpawnFile;
            else if (EventOpen) return MessagesEventAlreadyOpened;
            object success = Spawns.Call("GetSpawnsCount", new object[] { EventSpawnFile });
            if (success is string)
            {
                return (string)success;
            }
            success = Interface.CallHook("CanEventOpen", new object[] { });
            if (success is string)
            {
                return (string)success;
            }
            EventOpen = true;
            EventPlayers.Clear();
            BroadcastToChat(string.Format(MessagesEventOpen, EventGameName));
            Interface.CallHook("OnEventOpenPost", new object[] { });
            return true;
        }
        object CloseEvent()
        {
            if (!EventOpen) return MessagesEventAlreadyClosed;
            EventOpen = false;
            Interface.CallHook("OnEventClosePost", new object[] { });
            if (EventStarted)
                BroadcastToChat(MessagesEventClose);
            else
                BroadcastToChat(MessagesEventCancel);
            return true;
        }
        object AutoEventNext()
        {
        	if( EventAutoConfig.Count == 0 ) {
        		AutoEventLaunched = false;
        		return "No Automatic Events Configured";
        	}
        	bool 
        	for( i = 0; i < EventAutoConfig.Count; i++ )
        	{
        		EventAutoNum++;
        		if(EventAutoNum >= EventAutoConfig.Count) EventAutoNum = 0;
        		
        		object success = SelectEvent((EventAutoConfig[EventAutoNum])["gametype"]);
				if (success is string) { continue; }
				
				object success = SelectSpawnfile((EventAutoConfig[EventAutoNum])["spawnfile"]);
				if (success is string) { continue; }
        		
        		
        		
        		
        	}
        
        }
        object LaunchEvent()
        {
        	// just activate it and take over from where it is currently.
        	AutoEventLaunched = true;
        	
        	if(!EventOpen && !EventStarted)
        	{
        		object success = AutoEventNext();
        		if(success is string)
        		{
        			// SEND MSG HERE
        			return;
        		}
        		// set next gametype
        		// set next spawnfile
        		// set next max players & min players
        		// 
        	}
        	else if(EventOpen && !EventStarted)
        	{
        		// start check for players
        		// start laiunch timer if min players reached
        	}
        	else if(EventStarted)
        	{
        		// add the time limit
        	}
        	
        	
        	
        
        }
        object EndEvent()
        {
            if (EventEnded) return MessagesEventNoGamePlaying;

            Interface.CallHook("OnEventEndPre", new object[] { });
            BroadcastToChat(string.Format(MessagesEventEnd, EventGameName));
            EventOpen = false;
            EventStarted = false;
            EventEnded = true;
			EventPending = false;

            SendPlayersHome();
            RedeemPlayersInventory();
            TryEraseAllPlayers();
            EjectAllPlayers();

            EventPlayers.Clear();
            Interface.CallHook("OnEventEndPost", new object[] { });
            return true;
        }
        object StartEvent()
        {
            if (EventGameName == null) return MessagesEventNotSet;
            else if (EventSpawnFile == null) return MessagesEventNoSpawnFile;
            else if (EventStarted) return MessagesEventAlreadyStarted;
            object success = Interface.CallHook("CanEventStart", new object[] { });
            if (success is string)
            {
                return (string)success;
            }
            Interface.CallHook("OnEventStartPre", new object[] { });
            BroadcastToChat(string.Format(MessagesEventBegin, EventGameName));
            EventStarted = true;
            EventEnded = false;
			ResetTimer();
            SaveAllInventories();
            SaveAllHomeLocations();
            TeleportAllPlayersToEvent();
            Interface.CallHook("OnEventStartPost", new object[] { });
            return true;
        }
        object JoinEvent(BasePlayer player)
        {
            if (player.GetComponent<EventPlayer>())
            {
                if (EventPlayers.Contains(player.GetComponent<EventPlayer>()))
                    return MessagesEventAlreadyJoined;
            }

            object success = Interface.CallHook("CanEventJoin", new object[] { player });
            if (success is string)
            {
                return (string)success;
            }

            EventPlayer event_player = player.GetComponent<EventPlayer>();
            if (event_player == null) event_player = player.gameObject.AddComponent<EventPlayer>();

            event_player.enabled = true;
            EventPlayers.Add(event_player);

            if (EventStarted)
            {
                SaveHomeLocation(player);
                SaveInventory(player);
                Interface.CallHook("OnEventPlayerSpawn", new object[] { player });
            }
            BroadcastToChat(string.Format(MessagesEventJoined, player.displayName.ToString(), EventPlayers.Count.ToString()));
            Interface.CallHook("OnEventJoinPost", new object[] { player });
            return true;
        }
        object CanEventJoin(BasePlayer player)
        {
        	if(!EventOpen)
        		return "The Event is currently closed.";
        	
        	if( EventMaxPlayers != 0 && EventPlayers.Count >= EventMaxPlayers )
        	{
        		return string.Format(MessagesEventMaxPlayers, EventGameName);
        	}
        	return null;
        }
        object OnEventJoinPost(BasePlayer player)
        {
        	if(!EventAutoEvents) return null;
        	
        	if( EventPlayers.Count >= EventMinPlayers && !EventStarted && EventEnded && !EventPending )
        	{
        		float timerStart = 30f;
        		object timetojoin = Interface.CallHook("GetEventConfig", "AutoEvent - TimeToJoin" );
        		if(timetojoin != null) { timerStart = Convert.ToSingle( timetojoin.ToString() ); }
        		BroadcastToChat(string.Format(MessagesAutoEventMinPlayers, EventGameName, timerStart.ToString()));
        		EventPending = true;
        		AutoArenaTimer = timer.Once( timerStart, () => StartEvent() );
        	}
        	return null;
        }
        object LeaveEvent(BasePlayer player)
        {
            if (player.GetComponent<EventPlayer>() == null)
            {
                return "You are not currently in the Event.";
            }
            if (!EventPlayers.Contains(player.GetComponent<EventPlayer>()))
            {
                return "You are not currently in the Event.";
            }

            player.GetComponent<EventPlayer>().inEvent = false;
            if (!EventEnded || !EventStarted)
            {
                BroadcastToChat(string.Format(MessagesEventLeft, player.displayName.ToString(), (EventPlayers.Count - 1).ToString()));
            }
            ZoneManager?.Call("RemovePlayerFromZoneKeepinlist", EventGameName, player);
            if (EventStarted)
            {
                player.inventory.Strip();
                RedeemInventory(player);
                TeleportPlayerHome(player);
                EventPlayers.Remove(player.GetComponent<EventPlayer>());
                EjectPlayer(player);
                TryErasePlayer(player);
                Interface.CallHook("OnEventLeavePost", new object[] { player });
            }
            else
            {
                EventPlayers.Remove(player.GetComponent<EventPlayer>());
                GameObject.Destroy(player.GetComponent<EventPlayer>());
            }
            return true;
        }

        object SelectEvent(string name)
        {
            if (!(EventGames.Contains(name))) return string.Format(MessagesEventNotAnEvent, name);
            if (EventStarted || EventOpen) return MessagesEventCloseAndEnd;
            EventGameName = name;
            Interface.CallHook("OnSelectEventGamePost", new object[] { name });
            return true;
        }
        object SelectSpawnfile(string name)
        {
            if (EventGameName == null || EventGameName == "") return MessagesEventNotSet;
            if (!(EventGames.Contains(EventGameName))) return string.Format(MessagesEventNotAnEvent, EventGameName.ToString());
            object success = Interface.CallHook("OnSelectSpawnFile", new object[] { name });
            if (success == null)
            {
                return string.Format(MessagesEventNotAnEvent, EventGameName.ToString());
            }
            EventSpawnFile = name;
            success = Spawns.Call("GetSpawnsCount", new object[] { EventSpawnFile });
            if (success is string)
            {
                EventSpawnFile = null;
                return (string)success;
            }

            return true;
        }
        object SelectNewZone(MonoBehaviour monoplayer, string radius)
        {
            if (EventGameName == null || EventGameName == "") return MessagesEventNotSet;
            if (!(EventGames.Contains(EventGameName))) return string.Format(MessagesEventNotAnEvent, EventGameName.ToString());
            if (EventStarted || EventOpen) return MessagesEventCloseAndEnd;
            Interface.CallHook("OnSelectEventZone", new object[] { monoplayer, radius });
            if (zonelogs[EventGameName] != null) storedData.ZoneLogs.Remove(zonelogs[EventGameName]);
            zonelogs[EventGameName] = new EventZone(EventGameName, monoplayer.transform.position, Convert.ToSingle(radius));
            storedData.ZoneLogs.Add(zonelogs[EventGameName]);
            InitializeZone(EventGameName);
            return true;
        }
        object RegisterEventGame(string name)
        {
            if (!(EventGames.Contains(name)))
                EventGames.Add(name);
            Puts(string.Format("Registered event game: {0}", name));
            Interface.CallHook("OnSelectEventGamePost", new object[] { EventGameName });
            
            if (EventGameName == name)
            {
                object success = SelectEvent(EventGameName);
                if (success is string)
                {
                    Puts((string)success);
                }
            }
            if (zonelogs[name] != null)
                timer.Once(0.5f, () => InitializeZone(name));
            return true;
        }

        object canRedeemKit(BasePlayer player)
        {
            if (!EventStarted) return null;
            EventPlayer eplayer = player.GetComponent<EventPlayer>();
            if (eplayer == null) return null;
            return false;
        }

        Dictionary<string, string> deadPlayers = new Dictionary<string, string>();
        bool FindPlayer(string name, out string targetid, out string targetname)
        {
            ulong userid;
            targetid = string.Empty;
            targetname = string.Empty;
            if (name.Length == 17 && ulong.TryParse(name, out userid))
            {
                targetid = name;
                return true;
            }

            foreach (BasePlayer player in Resources.FindObjectsOfTypeAll(typeof(BasePlayer)))
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
            deadPlayers = DeadPlayersList.Call("GetPlayerList", null) as Dictionary<string, string>;
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
		void ResetTimer()
		{
			AutoArenaTimer.Destroy();
			if(!EventAutoEvents) return;
		
		}
        //////////////////////////////////////////////////////////////////////////////////////
        // Chat Commands /////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        [ChatCommand("event_leave")]
        void cmdEventLeave(BasePlayer player, string command, string[] args)
        {
            object success = LeaveEvent(player);
            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }
        }
        [ChatCommand("event_join")]
        void cmdEventJoin(BasePlayer player, string command, string[] args)
        {
            object success = JoinEvent(player);
            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }
        }

        [ChatCommand("event")]
        void cmdEvent(BasePlayer player, string command, string[] args)
        {
            string message = string.Empty;
            if (!EventOpen && !EventStarted) message = MessagesEventStatusClosedEnd;
            else if (EventOpen && !EventStarted) message = MessagesEventStatusOpen;
            else if (EventOpen && EventStarted) message = MessagesEventStatusOpenStarted;
            else message = MessagesEventStatusClosedStarted;
            SendReply(player, string.Format(message, EventGameName));
        }
        

        [ChatCommand("reward")]
        void cmdEventReward(BasePlayer player, string command, string[] args)
        {
            int currenttokens = GetTokens(player.userID.ToString());
            if (args.Length == 0)
            {
                SendReply(player, string.Format(MessageRewardCurrent, currenttokens.ToString()));
                SendReply(player, MessageRewardHelp);
                foreach (KeyValuePair<string, Reward> pair in rewards)
                {
                    string color = "green";
                    if (pair.Value.GetCost() > currenttokens)
                        color = "red";
                    SendReply(player, string.Format(MessageRewardItem, pair.Value.name, pair.Value.cost, (Convert.ToBoolean(pair.Value.kit) ? "Kit : " : "Item : ") + pair.Value.item, pair.Value.amount, color.ToString() ));
                }
                return;
            }
            if (rewards[args[0]] == null)
            {
                SendReply(player, MessageRewardWrong);
                return;
            }
            int amount = 1;
            if (args.Length > 1) int.TryParse(args[1], out amount);
            if (amount < 1)
            {
                SendReply(player, MessageRewardNegative);
                return;
            }
            if (rewards[args[0]].GetCost() * amount > currenttokens)
            {
                SendReply(player, string.Format(MessageRewardNotEnoughTokens, args[0], amount.ToString()));
                return;
            }
            var success = GiveReward(player, args[0], amount);
            if (success is string)
            {
                SendReply(player, success.ToString());
                return;
            }
            if (success is bool && (bool)success)
                RemoveTokens(player.userID.ToString(), rewards[args[0]].GetCost() * amount);

        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Console Commands //////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        [ConsoleCommand("event.launch")]
        void ccmdEventLaunch(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            object success = LaunchEvent();
            if (success is string)
            {
                SendReply(arg, (string)success);
                return;
            }
            SendReply(arg, string.Format("Event \"{0}\" is now launched.", EventGameName));
        }
        [ConsoleCommand("event.open")]
        void ccmdEventOpen(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            object success = OpenEvent();
            if (success is string)
            {
                SendReply(arg, (string)success);
                return;
            }
            SendReply(arg, string.Format("Event \"{0}\" is now opened.", EventGameName));
        }
        [ConsoleCommand("event.start")]
        void ccmdEventStart(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            object success = StartEvent();
            if (success is string)
            {
                SendReply(arg, (string)success);
                return;
            }
            SendReply(arg, string.Format("Event \"{0}\" is now started.", EventGameName));
        }
        [ConsoleCommand("event.close")]
        void ccmdEventClose(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            object success = CloseEvent();
            if (success is string)
            {
                SendReply(arg, (string)success);
                return;
            }
            SendReply(arg, string.Format("Event \"{0}\" is now closed for entries.", EventGameName));
        }
        [ConsoleCommand("event.end")]
        void ccmdEventEnd(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            object success = EndEvent();
            if (success is string)
            {
                SendReply(arg, (string)success);
                return;
            }
            SendReply(arg, string.Format("Event \"{0}\" has ended.", EventGameName));
        }
        [ConsoleCommand("event.game")]
        void ccmdEventGame(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "event.game \"Game Name\"");
                return;
            }
            object success = SelectEvent((string)arg.Args[0]);
            if (success is string)
            {
                SendReply(arg, (string)success);
                return;
            }
            defaultGame = EventGameName;
            SaveConfig();
            SendReply(arg, string.Format("{0} is now the next Event game.", arg.Args[0].ToString()));
        }
        [ConsoleCommand("event.minplayers")]
        void ccmdEventminPlayers(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "event.minplayers XX");
                return;
            }
            object success = SelectMinplayers((string)arg.Args[0]);
            if (success is string)
            {
                SendReply(arg, (string)success);
                return; 
            }
            SendReply(arg, string.Format("Minimum Players for {0} is now {1} (this is only usefull for auto events).", arg.Args[0].ToString(), EventSpawnFile.ToString()));
        }
        [ConsoleCommand("event.maxplayers")]
        void ccmdEventMaxPlayers(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "event.maxplayers XX");
                return;
            }
            object success = SelectMaxplayers((string)arg.Args[0]);
            if (success is string)
            {
                SendReply(arg, (string)success);
                return; 
            }
            SendReply(arg, string.Format("Maximum Players for {0} is now {1}.", arg.Args[0].ToString(), EventSpawnFile.ToString()));
        }
        [ConsoleCommand("event.spawnfile")]
        void ccmdEventSpawnfile(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "event.spawnfile \"filename\"");
                return;
            }
            object success = SelectSpawnfile((string)arg.Args[0]);
            if (success is string)
            {
                SendReply(arg, (string)success);
                return; 
            }
            defaultSpawnfile = arg.Args[0];
            SaveConfig(); 
            SendReply(arg, string.Format("Spawnfile for {0} is now {1} .", EventGameName.ToString(), EventSpawnFile.ToString()));
        }
        [ConsoleCommand("event.zone")]
        void ccmdEventZone(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            if (arg.connection == null)
            {
                SendReply(arg, "To set the zone position & radius you must be connected");
                return;
            }
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "event.zone RADIUS");
                return;
            }
            object success = SelectNewZone(arg.connection.player, arg.Args[0]);
            if (success is string)
            {
                SendReply(arg, (string)success);
                return;
            }
            
            SendReply(arg, string.Format("New Zone Created for {0}: @ {1} {2} {3} with {4}m radius .", EventGameName.ToString(), arg.connection.player.transform.position.x.ToString(), arg.connection.player.transform.position.y.ToString(), arg.connection.player.transform.position.z.ToString(), arg.Args[0]));
        }
        [ConsoleCommand("event.reward")]
        void ccmdEventReward(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "Reward Related: event.reward add/list/remove");
                SendReply(arg, "Players Related: event.reward set/clear/give/take/check");
                return;
            }
            string targetid = string.Empty;
            string targetname = string.Empty;
            int amount = 0;
            bool foundtarget;
            switch (arg.Args[0])
            {
                case "check":
                    if(arg.Args.Length < 2)
                    {
                        SendReply(arg, "event.reward check PLAYERNAME/STEAMID");
                        return;
                    }
                    
                    foundtarget = FindPlayer(arg.Args[1], out targetid, out targetname);
                    if(!(bool)foundtarget)
                    {
                        SendReply(arg, targetid.ToString());
                        return;
                    }
                    SendReply(arg, string.Format("{0} {1} has {2} tokens",targetid, targetname, GetTokens(targetid).ToString()));
                break;
                case "clear":
                    if (arg.Args.Length < 2)
                    {
                        SendReply(arg, "You must confirm by saying: event.reward clear yes");
                        return;
                    }
                    if(arg.Args[1] != "yes")
                    {
                        SendReply(arg, "You must confirm clearing the players token list by added: yes, at the end.");
                        return;
                    }
                    storedData.Tokens.Clear();
                    SendReply(arg, "Cleared all player tokens!!!!!");
                    break;
                case "set":
                    if (arg.Args.Length < 3)
                    {
                        SendReply(arg, "event.reward set PLAYERNAME/STEAMID AMOUNT");
                        return;
                    }
                    
                    if(!int.TryParse(arg.Args[2], out amount))
                    {
                        SendReply(arg, "the amount needs to be a number");
                        return;
                    }
                    foundtarget = FindPlayer(arg.Args[1], out targetid, out targetname);
                    if (!(bool)foundtarget)
                    {
                        SendReply(arg, targetid.ToString());
                        return;
                    }
                    SetTokens(targetid, amount);
                    SendReply(arg, string.Format("{0} {1} now has {2} tokens", targetid, targetname, GetTokens(targetid).ToString()));
                    break;
                case "give":
                    if (arg.Args.Length < 3)
                    {
                        SendReply(arg, "event.reward give PLAYERNAME/STEAMID AMOUNT");
                        return;
                    }

                    if (!int.TryParse(arg.Args[2], out amount))
                    {
                        SendReply(arg, "the amount needs to be a number");
                        return;
                    }
                    foundtarget = FindPlayer(arg.Args[1], out targetid, out targetname);
                    if (!(bool)foundtarget)
                    {
                        SendReply(arg, targetid.ToString());
                        return;
                    }
                    AddTokens(targetid, amount);
                    SendReply(arg, string.Format("{0} {1} now has {2} tokens", targetid, targetname, GetTokens(targetid).ToString()));
                    break;
                case "take":
                    if (arg.Args.Length < 3)
                    {
                        SendReply(arg, "event.reward take PLAYERNAME/STEAMID AMOUNT");
                        return;
                    }

                    if (!int.TryParse(arg.Args[2], out amount))
                    {
                        SendReply(arg, "the amount needs to be a number");
                        return;
                    }
                    foundtarget = FindPlayer(arg.Args[1], out targetid, out targetname);
                    if (!(bool)foundtarget)
                    {
                        SendReply(arg, targetid.ToString());
                        return;
                    }
                    RemoveTokens(targetid, amount);
                    SendReply(arg, string.Format("{0} {1} now has {2} tokens", targetid, targetname, GetTokens(targetid).ToString()));
                    break;
                case "add":
                    if (arg.Args.Length < 5)
                    {
                        SendReply(arg, "event.reward add NAME COST ITEM/KIT AMOUNT");
                        return;
                    }
                    string rewardname = arg.Args[1];
                    int cost = 0;
                    if (!int.TryParse(arg.Args[2], out cost))
                    {
                        SendReply(arg, "The cost needs to be a number");
                        return;
                    }
                    if (cost < 1)
                    {
                        SendReply(arg, "The cost needs to be higher then 0");
                        return;
                    }

                    if (!int.TryParse(arg.Args[4], out amount))
                    {
                        SendReply(arg, "The amount needs to be a number");
                        return;
                    }
                    if (amount < 1)
                    {
                        SendReply(arg, "The amount needs to be higher then 0");
                        return;
                    }

                    bool kit = false;
                    string itemname = arg.Args[3].ToLower();
                    if (displaynameToShortname.ContainsKey(itemname))
                        itemname = displaynameToShortname[itemname];
                    var definition = ItemManager.FindItemDefinition(itemname);
                    if (definition == null)
                    {
                        kit = true;
                        if (Kits == null)
                        {
                            SendReply(arg, "This item doesn't exist and it seems like you don't have the kits plugin");
                            return;
                        }
                        var iskit = Kits.Call("isKit", itemname);
                        if (!(iskit is bool))
                        {
                            SendReply(arg, "Seems like you have an out dated Kits plugin");
                            return;
                        }
                        if (!(bool)iskit)
                        {
                            SendReply(arg, "This item doesn't exist and no kits match this name neither.");
                            return;
                        }
                    }
                    Reward reward = new Reward(rewardname, cost, kit, itemname, amount);
                    if (rewards[reward.name] != null) storedData.Rewards.Remove(rewards[reward.name]);
                    rewards[reward.name] = reward;
                    storedData.Rewards.Add(rewards[reward.name]);
                    SaveData();
                    SendReply(arg, string.Format("Reward Name: {0} - Cost: {1} - Name: {2} - Amount: {3}", reward.name, reward.cost, (Convert.ToBoolean(reward.kit) ? "Kit " : string.Empty) + reward.item, reward.amount));
                    break;

                case "list":
                    if (rewards.Count == 0)
                    {
                        SendReply(arg, "You dont have any rewards set yet.");
                        return;
                    }
                    foreach (KeyValuePair<string, Reward> pair in rewards)
                    {
                        SendReply(arg, string.Format("Reward Name: {0} - Cost: {1} - Name: {2} - Amount: {3}", pair.Value.name, pair.Value.cost, (Convert.ToBoolean(pair.Value.kit) ? "Kit " : string.Empty) + pair.Value.item, pair.Value.amount));
                    }
                    break;

                case "remove":
                    if (arg.Args.Length < 2)
                    {
                        SendReply(arg, "event.reward remove REWARDNAME");
                        return;
                    }
                    if (rewards[arg.Args[1]] == null)
                    {
                        SendReply(arg, "This reward doesn't exist");
                        return;
                    }
                    storedData.Rewards.Remove(rewards[arg.Args[1]]);
                    rewards[arg.Args[1]] = null;
                    SendReply(arg, "You've successfully removed this reward");
                 break;
                

            }
        }
    }
}
