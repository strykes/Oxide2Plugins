// Reference: Oxide.Ext.Rust

using System.Reflection;
using System;
using System.Data;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Rust;

namespace Oxide.Plugins
{
    [Info("Event Manager", "Reneb", 1.0)]
    class EventManager : RustPlugin
    {
    	////////////////////////////////////////////////////////////
    	// Setting all fields //////////////////////////////////////
    	////////////////////////////////////////////////////////////
        private Core.Libraries.Plugins loadedPlugins;
        private Core.Plugins.Plugin spawnsplugin;
        
        
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
            player.transform.position = destination;
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();
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
            loadedPlugins = Interface.GetMod().GetLibrary<Core.Libraries.Plugins>("Plugins");
            EventGames = new List<string>();
            EventPlayers = new List<EventPlayer>();
            EventGameName = defaultGame;
        }
        void OnServerInitialized()
        {
            EventOpen = false;
            EventStarted = false;
            EventEnded = true;
            spawnsplugin = loadedPlugins.Find("Spawns");
            
        }
        void Unload()
        {
            EndEvent();
            var objects = GameObject.FindObjectsOfType(typeof(EventPlayer));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }
        void LoadDefaultConfig()
        {
            Puts("EventManager: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }
        void OnPlayerSpawn(BasePlayer player)
        {
        	if (!EventStarted) return;
            if (!(player.GetComponent<EventPlayer>())) return;
            if (player.GetComponent<EventPlayer>().inEvent)
            {
                loadedPlugins.CallHook("OnEventPlayerSpawn", new object[] { player });
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
            OnEntityAttack(player, hitinfo);
        }
        void OnMeleeAttack(BaseMelee melee, HitInfo hitinfo)
        {
            if (!EventStarted) return;
            object parent = melee.GetParentEntity();
            if (parent is BasePlayer)
            {
                OnEntityAttack(parent as BasePlayer, hitinfo);
            }
        }
        object OnEntityAttack(BasePlayer player, HitInfo hitinfo)
        {
            if (player.GetComponent<EventPlayer>() == null || !(player.GetComponent<EventPlayer>().inEvent))
            {
                return null;
            }
            else if (hitinfo.HitEntity != null)
            {
                loadedPlugins.CallHook("OnEventPlayerAttack", new object[] { player, hitinfo });
            }
            return null;
        }
        object OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (!EventStarted) return null;
            if (!(entity is BasePlayer)) return null;
            if ((entity as BasePlayer).GetComponent<EventPlayer>() == null) return null;
            loadedPlugins.CallHook("OnEventPlayerDeath", new object[] { (entity as BasePlayer), hitinfo });
            return null;
        }
    	void OnPlayerDisconnected(BasePlayer player,Network.Connection connection)
    	{
    		if (player.GetComponent<EventPlayer>() != null)
    		{
    			LeaveEvent(player);
    		}
    	}
        
        //////////////////////////////////////////////////////////////////////////////////////
    	// Configs Manager ///////////////////////////////////////////////////////////////////
    	//////////////////////////////////////////////////////////////////////////////////////
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
        void LoadVariables()
        {
            eventAuth = Convert.ToInt32(GetConfig("Settings", "authLevel", 1));
            defaultGame = Convert.ToString(GetConfig("Default", "Game", "Deathmatch"));
			
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
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
                    SendReply(arg, "You are not allowed to use this command");
                    return false;
                }
            }
            return true;
        }
        
    	// Broadcast To The General Chat /////////////////////////////////////////////////////
        void BroadcastToChat(string msg)
        {
            ConsoleSystem.Broadcast("chat.add \"SERVER\" " + msg.QuoteSafe() + " 1.0", new object[0]);
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
                TeleportPlayerToEvent(eventplayer.player);
            }
        } 
        void TeleportPlayerToEvent(BasePlayer player)
        {
            if (!(player.GetComponent<EventPlayer>())) return;
            var targetpos = spawnsplugin.CallHook("GetRandomSpawn", new object[] { EventSpawnFile });
            if (targetpos is string)
                return;
            var newpos = loadedPlugins.CallHook("EventChooseSpawn", new object[] { player, targetpos });
            if (newpos is Vector3)
                targetpos = (Vector3)newpos;
            timer.Once(0.2f, () => { ForcePlayerPosition(player, (Vector3)targetpos); loadedPlugins.CallHook("OnEventPlayerSpawn", new object[] { player }); });
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
            var eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            eventplayer.SaveHome();
        }
        void RedeemInventory(BasePlayer player)
        {
            var eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (player.IsDead())
                return;
            eventplayer.player.inventory.Strip();
            eventplayer.RestoreInventory();
        }
        void TeleportPlayerHome(BasePlayer player)
        {
            var eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (player.IsDead())
                return;
            eventplayer.TeleportHome();
        }
        void TryErasePlayer(BasePlayer player)
        {
            var eventplayer = player.GetComponent<EventPlayer>();
            if (eventplayer == null) return;
            if (!(eventplayer.inEvent) && !(eventplayer.savedHome) && !(eventplayer.savedInventory))
                GameObject.Destroy(eventplayer);
        }
        void GivePlayerKit(BasePlayer player, Dictionary<string, object> GiveKit)
        {
            var targetContainer = player.inventory.containerMain;
            foreach (KeyValuePair<string, object> pair in GiveKit)
            {
                if (pair.Key == "main")
                    targetContainer = player.inventory.containerMain;
                else if (pair.Key == "belt")
                    targetContainer = player.inventory.containerBelt;
                else if (pair.Key == "wear")
                    targetContainer = player.inventory.containerWear;
                else
                {
                    Puts(string.Format("Error in Event, Kit is invalid: {0} isn't a container to receive items", pair.Key));
                    return;
                }
                var GiveContainer = pair.Value as Dictionary<string, object>;
                foreach (KeyValuePair<string, object> itemkit in GiveContainer)
                {
                    GiveItem(player, itemkit.Key, Convert.ToInt32(itemkit.Value), targetContainer, false);
                }
            }
        }
        void EjectAllPlayers()
        {
        	foreach (EventPlayer eventplayer in EventPlayers)
            {
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
            if (EventGameName == null) return "An Event game must first be chosen.";
            else if (EventSpawnFile == null) return "A spawn file must first be loaded.";
            else if (EventOpen) return "The Event is already open.";
            object success = spawnsplugin.CallHook("GetSpawnsCount", new object[] { EventSpawnFile });
            if (success is string)
            {
                return (string)success;
            }
            success = loadedPlugins.CallHook("CanEventOpen", new object[] { });
            if (success is string)
            {
                return (string)success;
            }
            EventOpen = true;
            EventPlayers.Clear();
            BroadcastToChat(string.Format("The Event is now open for : {0} !  Type /event_join to join!", EventGameName));
            loadedPlugins.CallHook("OnEventOpenPost", new object[] { });
            return true;
        }
        object CloseEvent()
        {
            if (!EventOpen) return "The Event is already closed.";
            EventOpen = false;
            loadedPlugins.CallHook("OnEventClosePost", new object[] { });
            if (EventStarted)
                BroadcastToChat("The Event entrance is now closed!");
            else
                BroadcastToChat("The Event was cancelled!");
            return true;
        } 
        object EndEvent()
        {
            if (EventEnded) return "An Event game is not underway.";

            loadedPlugins.CallHook("OnEventEndPre", new object[] { });
            BroadcastToChat(string.Format("Event: {0} is now over!", EventGameName));
            EventOpen = false;
            EventStarted = false;
            EventEnded = true;
            
            
            SendPlayersHome();
            RedeemPlayersInventory();
            TryEraseAllPlayers();
            EjectAllPlayers();
            
            EventPlayers.Clear();
            loadedPlugins.CallHook("OnEventEndPost", new object[] { });
            return true;
        }
        object StartEvent()
        {
            if (EventGameName == null) return "An Event game must first be chosen.";
            else if (EventSpawnFile == null) return "A spawn file must first be loaded.";
            else if (EventStarted) return "An Event game has already started.";
            object success = loadedPlugins.CallHook("CanEventStart", new object[] { });
            if (success is string)
            {
                return (string)success;
            }
            loadedPlugins.CallHook("OnEventStartPre", new object[] { });
            BroadcastToChat(string.Format("Event: {0} is about to begin!", EventGameName));
            EventStarted = true;
            EventEnded = false;

            SaveAllInventories();
            SaveAllHomeLocations();
            TeleportAllPlayersToEvent();
            loadedPlugins.CallHook("OnEventStartPost", new object[] { });
            return true;
        }
        object JoinEvent(BasePlayer player)
        {
            if (player.GetComponent<EventPlayer>())
                return "You are already in the Event.";
            else if (!EventOpen)
                return "The Event is currently closed.";
            object success = loadedPlugins.CallHook("CanEventJoin", new object[] { player });
            if (success is string)
            {
                return (string)success;
            }
            var event_player = player.gameObject.AddComponent<EventPlayer>();
            event_player.enabled = true;
            EventPlayers.Add(event_player);

            if (EventStarted)
            {
                SaveHomeLocation(player);
                SaveInventory(player);
                TeleportPlayerToEvent(player);
            }
            BroadcastToChat(string.Format("{0} has joined the Event!  (Total Players: {1})", player.displayName.ToString(), EventPlayers.Count.ToString()));
            loadedPlugins.CallHook("OnEventJoinPost", new object[] { player });
            return true;
        }
        object LeaveEvent(BasePlayer player)
        {
            if (player.GetComponent<EventPlayer>() == null)
                return "You are not currently in the Event.";
            player.inventory.Strip();
			player.GetComponent<EventPlayer>().inEvent = false;
            if (!EventEnded)
                BroadcastToChat(string.Format("{0} has left the Event! (Total Players: {1})", player.displayName.ToString(), (EventPlayers.Count-1).ToString()));
            if (EventStarted)
            {
                RedeemInventory(player);
                TeleportPlayerHome(player);
                EventPlayers.Remove(player.GetComponent<EventPlayer>());
                TryErasePlayer(player);
                loadedPlugins.CallHook("OnEventLeavePost", new object[] { player });
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
            if (!(EventGames.Contains(name))) return "This Game isn't registered, did you reload the game after loading Event - Core?";
            if (EventStarted || EventOpen) return "The Event needs to be closed and ended before selecting a new game.";
            EventGameName = name;
            loadedPlugins.CallHook("OnSelectEventGamePost", new object[] { name });
            return true;
        }
        object SelectSpawnfile(string name)
        {
            if (EventGameName == null || EventGameName == "") return "You must select an Event game first";
            if (!(EventGames.Contains(EventGameName))) return string.Format("This Game {0} isn't registered, did you reload the game after loading Event - Core?",EventGameName.ToString());
            object success = loadedPlugins.CallHook("OnSelectSpawnFile", new object[] { name });
            if (success == null)
            {
                return string.Format("This Game {0} isn't registered, did you reload the game after loading Event - Core?", EventGameName.ToString());
            }
            EventSpawnFile = name;
            success = spawnsplugin.CallHook("GetSpawnsCount", new object[] { EventSpawnFile });
            if (success is string)
            {
                EventSpawnFile = null;
                return (string)success;
            }
            return true;
        }
        object RegisterEventGame(string name)
        {
            if (!(EventGames.Contains(name)))
                EventGames.Add(name);
            Puts(string.Format("Registered event game: {0}", name));
            loadedPlugins.CallHook("OnSelectEventGamePost", new object[] { EventGameName });
            if (EventGameName == name)
            {
                object success = SelectEvent(EventGameName);
                if(success is string)
                {
                    Puts((string)success);
                }
            }
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
    }
}
