// Reference: Oxide.Ext.Rust

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
    [Info("Event Manager", "Reneb", "1.0.5", ResourceId = 740)]
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

        private string EventSpawnFile;
        private string EventGameName;
        private string itemname;
        private string defaultGame;
        private string defaultSpawnfile;

        private bool EventOpen;
        private bool EventStarted;
        private bool EventEnded;
        private bool isBP;
        private bool Changed;

        private List<string> EventGames;
        private List<EventPlayer> EventPlayers;

        private ItemDefinition itemdefinition;

        private int stackable;
        private int giveamount;
        private int eventAuth;


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
            EventGameName = defaultGame;
            LoadData();
        }
        void OnServerInitialized()
        {
            EventOpen = false;
            EventStarted = false;
            EventEnded = true;
            timer.Once(0.1f, () => InitializeZones());
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
        void LoadDefaultConfig()
        {
            Puts("EventManager: Creating a new config file");
            Config.Clear();
            LoadVariables();
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
            foreach(KeyValuePair<string, EventZone> pair in zonelogs)
            {
                InitializeZone(pair.Key);
            }
        }
        void InitializeZone(string name)
        {
            if (zonelogs[name] == null) return;
            ZoneManager?.Call("CreateOrUpdateZone", name, new string[] { "radius", zonelogs[name].radius }, zonelogs[name].GetPosition(), "undestr", "true", "nobuild", "true", "nodeploy", "true");
            if(EventGames.Contains(name))
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

        class StoredData
        {
            public HashSet<EventZone> ZoneLogs = new HashSet<EventZone>();

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
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Configs Manager ///////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

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
        static string MessagesPermissionsNotAllowed = "You are not allowed to use this command";
        static string MessagesEventNotSet = "An Event game must first be chosen.";
        static string MessagesEventNoSpawnFile = "A spawn file must first be loaded.";
        static string MessagesEventAlreadyOpened = "The Event is already open.";
        static string MessagesEventAlreadyClosed = "The Event is already closed.";
        static string MessagesEventAlreadyStarted = "An Event game has already started.";


        static string MessagesEventOpen = "The Event is now open for : {0} !  Type /event_join to join!";
        static string MessagesEventClose = "The Event entrance is now closed!";
        static string MessagesEventCancel = "The Event was cancelled!";
        static string MessagesEventNoGamePlaying = "An Event game is not underway.";
        static string MessagesEventEnd = "Event: {0} is now over!";
        static string MessagesEventAlreadyJoined = "You are already in the Event.";
        static string MessagesEventJoined = "{0} has joined the Event!  (Total Players: {1})";
        static string MessagesEventLeft = "{0} has left the Event! (Total Players: {1})";
        static string MessagesEventBegin = "Event: {0} is about to begin!";
        static string MessagesEventNotInEvent = "You are not currently in the Event.";
        static string MessagesEventNotAnEvent = "This Game {0} isn't registered, did you reload the game after loading Event - Core?";
        static string MessagesEventStatusClosed = "The Event is currently closed.";
        static string MessagesEventCloseAndEnd = "The Event needs to be closed and ended before using this command.";

        void LoadVariables()
        {
            eventAuth = Convert.ToInt32(GetConfig("Settings", "authLevel", 1));
            defaultGame = Convert.ToString(GetConfig("Default", "Game", "Deathmatch"));

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

            CheckCfg<string>("Messages - Status - Closed", ref MessagesEventStatusClosed);

            CheckCfg<string>("Messages - Event - Opened", ref MessagesEventOpen);
            CheckCfg<string>("Messages - Event - Closed", ref MessagesEventClose);
            CheckCfg<string>("Messages - Event - Cancelled", ref MessagesEventCancel);
            CheckCfg<string>("Messages - Event - End", ref MessagesEventEnd);
            CheckCfg<string>("Messages - Event - Join", ref MessagesEventJoined);
            CheckCfg<string>("Messages - Event - Begin", ref MessagesEventBegin);
            CheckCfg<string>("Messages - Event - Left", ref MessagesEventLeft);

            SaveConfig();
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
        void EjectAllPlayers()
        {
            foreach (EventPlayer eventplayer in EventPlayers)
            {
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
        object EndEvent()
        {
            if (EventEnded) return MessagesEventNoGamePlaying;

            Interface.CallHook("OnEventEndPre", new object[] { });
            BroadcastToChat(string.Format(MessagesEventEnd, EventGameName));
            EventOpen = false;
            EventStarted = false;
            EventEnded = true;


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
                if(EventPlayers.Contains(player.GetComponent<EventPlayer>()))
                    return MessagesEventAlreadyJoined;
            }
            if (!EventOpen)
                return "The Event is currently closed.";
            object success = Interface.CallHook("CanEventJoin", new object[] { player });
            if (success is string)
            {
                return (string)success;
            }

            EventPlayer event_player = player.GetComponent<EventPlayer>();
            if(event_player == null) event_player = player.gameObject.AddComponent<EventPlayer>();

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
        object LeaveEvent(BasePlayer player)
        {
            if (player.GetComponent<EventPlayer>() == null)
            {
                return "You are not currently in the Event.";
            }
            if(!EventPlayers.Contains(player.GetComponent<EventPlayer>()))
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
            if (!(EventGames.Contains(name))) return string.Format(MessagesEventNotAnEvent,name);
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
            if(zonelogs[name] != null)
                timer.Once(0.5f, () => InitializeZone(name));
            return true;
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
         
        //////////////////////////////////////////////////////////////////////////////////////
        // Console Commands //////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
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
            SendReply(arg, string.Format("{0} is now the next Event game.", arg.Args[0].ToString()));
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
            SendReply(arg, string.Format("Spawnfile for {0} is now {1} .", EventGameName.ToString(), EventSpawnFile.ToString()));
        }
        [ConsoleCommand("event.zone")]
        void ccmdEventZone(ConsoleSystem.Arg arg)
        {
            if (!hasAccess(arg)) return;
            if(arg.connection == null)
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
            SendReply(arg, string.Format("New Zone Created for {0}: @ {1} {2} {3} with {4}m radius .", EventGameName.ToString(), arg.connection.player.transform.position.x.ToString(), arg.connection.player.transform.position.y.ToString(), arg.connection.player.transform.position.z.ToString(), arg.Args[0] ));
        }
    }
}
