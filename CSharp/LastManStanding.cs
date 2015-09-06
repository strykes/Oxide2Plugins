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
    [Info("LastManStanding", "Reneb", "1.0.0")]
    class LastManStanding : RustPlugin
    {
        ////////////////////////////////////////////////////////////
        // Setting all fields //////////////////////////////////////
        ////////////////////////////////////////////////////////////
        [PluginReference]
        Plugin EventManager;

        private bool useThisEvent;
        private bool EventStarted;
        private bool Changed;

        public string CurrentKit;
        private List<LMSPlayer> LMSPlayers = new List<LMSPlayer>();

        ////////////////////////////////////////////////////////////
        // DeathmatchPlayer class to store informations ////////////
        ////////////////////////////////////////////////////////////
        class LMSPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public int deaths;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                deaths = 0;
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
            Puts("Event LastManStanding: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }
        void Unload()
        {
            if (useThisEvent && EventStarted)
            {
                EventManager.Call("EndEvent", new object[] { });
                var objects = GameObject.FindObjectsOfType(typeof(LMSPlayer));
                if (objects != null)
                    foreach (var gameObj in objects)
                        GameObject.Destroy(gameObj);
            }
        }



        //////////////////////////////////////////////////////////////////////////////////////
        // Configurations ////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        static string DefaultKit = "lastmanstanding";
        static string EventName = "LastManStanding";
        static string EventZoneName = "LMS";
        static string EventSpawnFile = "LastManStandingSpawnfile";
        static int EventLives = 3;

        static float EventStartHealth = 100;

        static Dictionary<string, object> EventZoneConfig;

        static string EventMessageWon = "{0} IS THE LAST MAN STANDING";
        static string EventMessageNoMorePlayers = "Arena has no more players, auto-closing.";
        static string EventMessageKill = "{0} killed {1}. ({2} lives left)";
        static string EventMessageOpenBroadcast = "In Last Man Standing, the goal is to be the last survivor!";

        static int TokensAddKill = 1;
        static int TokensAddWon = 5;

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            CheckCfg<string>("LastManStanding - Kit - Default", ref DefaultKit);
            CheckCfg<string>("LastManStanding - Event - Name", ref EventName);
            CheckCfg<string>("LastManStanding - Event - SpawnFile", ref EventSpawnFile);
            CheckCfg<string>("LastManStanding - Zone - Name", ref EventZoneName);
            CheckCfg<int>("LastManStanding - Lives", ref EventLives);
            CheckCfgFloat("LastManStanding - Start Health", ref EventStartHealth);


            CheckCfg<string>("Messages - Won", ref EventMessageWon);
            CheckCfg<string>("Messages - Empty", ref EventMessageNoMorePlayers);
            CheckCfg<string>("Messages - Kill", ref EventMessageKill);
            CheckCfg<string>("Messages - Open Broadcast", ref EventMessageOpenBroadcast);

            CheckCfg<int>("Tokens - Per Kill", ref TokensAddKill);
            CheckCfg<int>("Tokens - On Win", ref TokensAddWon);

            CurrentKit = DefaultKit;

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
                EventManager.Call("GivePlayerKit", new object[] { player, CurrentKit });
                player.health = EventStartHealth;
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
                LMSPlayers.Clear();
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
                if (player.GetComponent<LMSPlayer>())
                    GameObject.Destroy(player.GetComponent<LMSPlayer>());
                LMSPlayers.Add(player.gameObject.AddComponent<LMSPlayer>());
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (useThisEvent)
            {
                if (player.GetComponent<LMSPlayer>())
                {
                    LMSPlayers.Remove(player.GetComponent<LMSPlayer>());
                    GameObject.Destroy(player.GetComponent<LMSPlayer>());
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
            	AddDeath(victim, hitinfo.Initiator);
            }
            return;
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

        void AddDeath(BasePlayer victim, BaseEntity attacker)
        {
            if (!victim.GetComponent<LMSPlayer>())
                return;

            victim.GetComponent<LMSPlayer>().deaths++;
            if(attacker != null)
            {
            	if(attacker.ToPlayer() != null)
            	{
            		BasePlayer player = attacker.ToPlayer();
            		EventManager.Call("AddTokens", player.userID.ToString(), TokensAddKill);
            		EventManager.Call("BroadcastEvent", string.Format(EventMessageKill, player.displayName, (EventLives - player.GetComponent<LMSPlayer>().deaths).ToString(), victim.displayName));
            	}
            }
            if( player.GetComponent<LMSPlayer>().deaths >= EventLives )
			{
				timer.Once(0.01f, () => EventManager.Call("LeaveEvent", player));
			} 
        }
        void CheckScores()
        {
            if (LMSPlayers.Count == 0)
            {
                var emptyobject = new object[] { };
                EventManager.Call("BroadcastEvent", EventMessageNoMorePlayers);
                EventManager.Call("CloseEvent", emptyobject);
                EventManager.Call("EndEvent", emptyobject);
                return;
            }
            
            BasePlayer winner = null;
            if(LMSPlayers.Count == 1)
            {
            	foreach (LMSPlayer lmsplayer in LMSPlayers)
				{
					winner = lmsplayer.player;
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
            for (var i = 1; i < 3; i++)
            {
                EventManager.Call("BroadcastToChat", winnerobjectmsg);
            }
            EventManager.Call("CloseEvent", emptyobject);
            EventManager.Call("EndEvent", emptyobject);
        }
    }
}

