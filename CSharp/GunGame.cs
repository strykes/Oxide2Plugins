using System.Collections.Generic;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Timers;
using Rust;

namespace Oxide.Plugins
{
    [Info("GunGame", "Reneb", "1.0.0")]
    class GunGame : RustPlugin
    {
        ////////////////////////////////////////////////////////////
        // Setting all fields //////////////////////////////////////
        ////////////////////////////////////////////////////////////
        [PluginReference]
        Plugin EventManager;


        private bool useThisEvent;
        private bool EventStarted;
        private bool Changed;

        private List<GungamePlayer> GungamePlayers = new List<GungamePlayer>();

        ////////////////////////////////////////////////////////////
        // GungamePlayer class to store informations ////////////
        ////////////////////////////////////////////////////////////
        class GungamePlayer : MonoBehaviour
        {
            public BasePlayer player;
            public int kills;
            public int level;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                kills = 0;
                level = 0;
            }
        }


        //////////////////////////////////////////////////////////////////////////////////////
        // Oxide Hooks ///////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        void Loaded()
        {
            useThisEvent = false;
            EventStarted = false;
        }
        void OnServerInitialized()
        {
            if (EventManager == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
            LoadVariables();
            RegisterGame();
        }
        void RegisterGame()
        {
            var success = EventManager.Call("RegisterEventGame", new object[] { EventName });
            if (success == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
        }
        void LoadDefaultConfig()
        {
            Puts("Event Deathmatch: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }
        void Unload()
        {
            if (useThisEvent && EventStarted)
            {
                EventManager.Call("EndEvent", new object[] { });
                var objects = GameObject.FindObjectsOfType(typeof(GungamePlayer));
                if (objects != null)
                    foreach (var gameObj in objects)
                        GameObject.Destroy(gameObj);
            }
        }



        //////////////////////////////////////////////////////////////////////////////////////
        // Configurations ////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        static string EventName = "Gungame";
        static string EventZoneName = "Gungame";
        static string EventSpawnFile = "GungameSpawnfile";

        static string EventMessageWon = "{0} WON THE GUNGAME";
        static string EventMessageNoMorePlayers = "Arena has no more players, auto-closing.";
        static string EventMessageKill = "{0} killed {3}. ({1}/{2} kills)";
        static string EventMessageOpenBroadcast = "In Gungame, it's a free for all, the goal is to get to the last level as fast as possible!";

        static int TokensAddKill = 1;
        static int TokensAddWon = 10;
		
		static Dictionary<string,object> GungameLevel = DefaultGungame();
		
        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            CheckCfg<string>("Gungame - Event - Name", ref EventName);
            CheckCfg<string>("Gungame - Event - SpawnFile", ref EventSpawnFile);
            CheckCfg<string>("Gungame - Zone - Name", ref EventZoneName);
            CheckCfgFloat("Gungame - Start - Health", ref EventStartHealth);


            CheckCfg<string>("Messages - Won", ref EventMessageWon);
            CheckCfg<string>("Messages - Empty", ref EventMessageNoMorePlayers);
            CheckCfg<string>("Messages - Kill", ref EventMessageKill);
            CheckCfg<string>("Messages - Open Broadcast", ref EventMessageOpenBroadcast);

            CheckCfg<int>("Tokens - Per Kill", ref TokensAddKill);
            CheckCfg<int>("Tokens - On Win", ref TokensAddWon);

            CheckCfg<Dictionary<string,object>>("Gungame - Levels", ref GungameLevel);

        }
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

        object GetEventConfig(string configname)
        {
            if (!useThisEvent) return null;
            if (Config[configname] == null) return null;
            return Config[configname];
        }
        
        static Dictionary<string,object> DefaultGungame()
        {
        	var defaultgg = new Dictionary<string,object>();
        	
        	var level0 = new Dictionary<string,object>();
        	level0.Add("kills", "2");
        	level0.Add("kit", "gglevel0");
        	level0.Add("health", "100");
        	defaultgg.Add("0", level0);
        	
        	var level1 = new Dictionary<string,object>();
        	level1.Add("kills", "2");
        	level1.Add("kit", "gglevel1");
        	level1.Add("health", "100");
        	defaultgg.Add("1", level1);
        	
        	var level2 = new Dictionary<string,object>();
        	level2.Add("kills", "2");
        	level2.Add("kit", "gglevel2");
        	level2.Add("health", "100");
        	defaultgg.Add("2", level2);
        	
        	var level3 = new Dictionary<string,object>();
        	level3.Add("kills", "2");
        	level3.Add("kit", "gglevel3");
        	level3.Add("health", "100");
        	defaultgg.Add("3", level3);
        	
        	var level4 = new Dictionary<string,object>();
        	level4.Add("kills", "2");
        	level4.Add("kit", "gglevel4");
        	level4.Add("health", "100");
        	defaultgg.Add("4", level4);
        	
        	var level5 = new Dictionary<string,object>();
        	level5.Add("kills", "2");
        	level5.Add("kit", "gglevel5");
        	level5.Add("health", "100");
        	defaultgg.Add("5", level5);
        	
        	var level6 = new Dictionary<string,object>();
        	level6.Add("kills", "2");
        	level6.Add("kit", "gglevel6");
        	level6.Add("health", "100");
        	defaultgg.Add("6", level6);
        
        	return defaultgg;
        }
         
        //////////////////////////////////////////////////////////////////////////////////////
        // Beginning Of Event Manager Hooks //////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        void OnSelectEventGamePost(string name)
        {
            if (EventName == name)
            {
                useThisEvent = true;
                if (EventSpawnFile != null && EventSpawnFile != "")
                    EventManager.Call("SelectSpawnfile", new object[] { EventSpawnFile });
            }
            else
                useThisEvent = false;
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (useThisEvent && EventStarted)
            {
                player.inventory.Strip();
                GungamePlayer ggplayer = player.GetComponent<GungamePlayer>();
                EventManager.Call("GivePlayerKit", new object[] { player, GungameLevel[ggplayer.level.ToString()]["kit"] });
                player.health = Convert.ToInt32(GungameLevel[ggplayer.level.ToString()]["health"]);
            }
        }
        object OnSelectSpawnFile(string name)
        {
            if (useThisEvent)
            {
                EventSpawnFile = name;
                return true;
            }
            return null;
        }
        object OnEventOpenPost()
        {
            if (useThisEvent)
                EventManager.Call("BroadcastEvent", new object[] { EventMessageOpenBroadcast });
            return null;
        }
        object OnEventEndPost()
        {
            if (useThisEvent)
            {
                EventStarted = false;
                GungamePlayers.Clear();
            }
            return null;
        }
        object OnEventStartPre()
        {
            if (useThisEvent)
            {
                EventStarted = true;
            }
            return null;
        }
        object OnSelectKit(string kitname)
        {
            if(useThisEvent)
            {
                CurrentKit = kitname;
                return true;
            }
            return null;
        }
        object OnEventJoinPost(BasePlayer player)
        {
            if (useThisEvent)
            {
                if (player.GetComponent<GungamePlayer>())
                    GameObject.Destroy(player.GetComponent<GungamePlayer>());
                GungamePlayers.Add(player.gameObject.AddComponent<GungamePlayer>());
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (useThisEvent)
            {
                if (player.GetComponent<GungamePlayer>())
                {
                    GungamePlayers.Remove(player.GetComponent<GungamePlayer>());
                    GameObject.Destroy(player.GetComponent<GungamePlayer>());
                    CheckScores();
                }
            }
            return null;
        }
        void OnEventPlayerAttack(BasePlayer attacker, HitInfo hitinfo)
        {
            if (useThisEvent)
            {
                if (!(hitinfo.HitEntity is BasePlayer))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                }
            }
        }

        void OnEventPlayerDeath(BasePlayer victim, HitInfo hitinfo)
        {
            if (useThisEvent)
            {
                if (hitinfo.Initiator != null)
                {
                    BasePlayer attacker = hitinfo.Initiator.ToPlayer();
                    if (attacker != null)
                    {
                        if (attacker != victim)
                        {
                            AddKill(attacker, victim);
                        }
                    }
                }
            }
            return;
        }
        object EventChooseSpawn(BasePlayer player, Vector3 destination)
        {
            return null;
        }
        object OnRequestZoneName()
        {
            if (useThisEvent)
            {
                return EventZoneName;
            }
            return null;
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // End Of Event Manager Hooks ////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        void AddKill(BasePlayer player, BasePlayer victim)
        {
            if (!player.GetComponent<GungamePlayer>())
                return;

            player.GetComponent<GungamePlayer>().kills++;
            EventManager.Call("AddTokens", player.userID.ToString(), TokensAddKill);
            EventManager.Call("BroadcastEvent", string.Format(EventMessageKill, player.displayName, player.GetComponent<GungamePlayer>().kills.ToString(), EventWinKills.ToString(), victim.displayName));
            CheckScores();
        }
        void CheckScores()
        {
            if (GungamePlayers.Count == 0)
            {
                var emptyobject = new object[] { };
                EventManager.Call("BroadcastEvent", EventMessageNoMorePlayers);
                EventManager.Call("CloseEvent", emptyobject);
                EventManager.Call("EndEvent", emptyobject);
                return;
            }
            BasePlayer winner = null;
            foreach (GungamePlayer GungamePlayer in GungamePlayers)
            {
                if (GungamePlayer == null) continue;
                if (GungamePlayer.kills >= EventWinKills || GungamePlayers.Count == 1)
                {
                    winner = GungamePlayer.player;
                    break;
                } 
            }
            if (winner == null) return;
            Winner(winner);
        }
        void Winner(BasePlayer player)
        {
            var winnerobjectmsg = new object[] { string.Format(EventMessageWon, player.displayName) };
            EventManager.Call("AddTokens", player.userID.ToString(), TokensAddWon);
            var emptyobject = new object[] { };
            for (var i = 1; i < 10; i++)
            {
                EventManager.Call("BroadcastEvent", winnerobjectmsg);
            }
            EventManager.Call("CloseEvent", emptyobject);
            EventManager.Call("EndEvent", emptyobject);
        }
    }
}

