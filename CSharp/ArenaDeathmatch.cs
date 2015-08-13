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
    [Info("Arena Deathmatch", "Reneb", "1.1.1", ResourceId = 741)]
    class ArenaDeathmatch : RustPlugin
    {
        ////////////////////////////////////////////////////////////
        // Setting all fields //////////////////////////////////////
        ////////////////////////////////////////////////////////////
        [PluginReference]
        Plugin EventManager;

        [PluginReference]
        Plugin ZoneManager;

        private bool useThisEvent;
        private bool EventStarted;
        private bool Changed;

        private string CurrentKit;
        private List<DeathmatchPlayer> DeathmatchPlayers;

        ////////////////////////////////////////////////////////////
        // DeathmatchPlayer class to store informations ////////////
        ////////////////////////////////////////////////////////////
        class DeathmatchPlayer : MonoBehaviour
        {
            public BasePlayer player;
            public int kills;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                enabled = false;
                kills = 0;
            }
        }


        //////////////////////////////////////////////////////////////////////////////////////
        // Oxide Hooks ///////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        void Loaded()
        {
            useThisEvent = false;
            EventStarted = false;
            DeathmatchPlayers = new List<DeathmatchPlayer>();
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
                var objects = GameObject.FindObjectsOfType(typeof(DeathmatchPlayer));
                if (objects != null)
                    foreach (var gameObj in objects)
                        GameObject.Destroy(gameObj);
            }
        }



        //////////////////////////////////////////////////////////////////////////////////////
        // Configurations ////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        static string DefaultKit = "deathmatch";
        static string EventName = "Deathmatch";
        static string EventSpawnFile = "DeathmatchSpawnfile";
        static int EventWinKills = 10;

        static float EventStartHealth = 100;

        static Dictionary<string, object> EventZoneConfig;

        static string EventMessageWon = "{0} WON THE DEATHMATCH";
        static string EventMessageNoMorePlayers = "Arena has no more players, auto-closing.";
        static string EventMessageKill = "{0} killed {3}. ({1}/{2} kills)";
        static string EventMessageOpenBroadcast = "In Deathmatch, it's a free for all, the goal is to kill as many players as possible!";

        static int TokensAddKill = 1;
        static int TokensAddWon = 5;


        private void LoadZoneConfig()
        {
            EventZoneConfig = new Dictionary<string, object>();

            EventZoneConfig.Add("eject", true);
            EventZoneConfig.Add("undestr", true);
            EventZoneConfig.Add("autolights", true);
            EventZoneConfig.Add("nobuild", true);
            EventZoneConfig.Add("nodeploy", true);
            EventZoneConfig.Add("nokits", true);
            EventZoneConfig.Add("notp", true);
            EventZoneConfig.Add("killsleepers", true);
            EventZoneConfig.Add("nosuicide", true);
            EventZoneConfig.Add("nocorpse", true);
            EventZoneConfig.Add("nowounded", true);

            timer.Once(1f, () =>
            {
                object zonefieldlist = ZoneManager?.Call("ZoneFieldListRaw");
                if (zonefieldlist == null)
                {
                    Debug.LogWarning("You don't have ZoneManager installed or is out of date, you may not use Arena Deathmatch");
                    return;
                }
                foreach (string fielditem in (List<string>)zonefieldlist)
                {
                    if (fielditem != "Location" && fielditem != "ID" && fielditem != "name")
                    {
                        if (!EventZoneConfig.ContainsKey(fielditem))
                            EventZoneConfig.Add(fielditem, false);

                        if (Config["Zone Settings - " + fielditem] is bool)
                            EventZoneConfig[fielditem] = (bool)Config["Zone Settings - " + fielditem];
                        else
                            Config["Zone Settings - " + fielditem] = EventZoneConfig[fielditem];
                    }
                }
            }
            );

        }
        private void LoadVariables()
        {

            LoadZoneConfig();

            LoadConfigVariables();


            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            CheckCfg<string>("DeathMatch - Kit - Default", ref DefaultKit);
            CheckCfg<string>("DeathMatch - Event - Name", ref EventName);
            CheckCfg<string>("DeathMatch - Event - SpawnFile", ref EventSpawnFile);
            CheckCfg<int>("DeathMatch - Win - Kills Needed", ref EventWinKills);
            CheckCfgFloat("DeathMatch - Start - Health", ref EventStartHealth);


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
        void OnSelectEventZone(MonoBehaviour monoplayer, string radius)
        {
            if (useThisEvent)
            {
                return;
            }
        }
        void OnPostZoneCreate(string name)
        {
            if (name == EventName)
            {
                string[] sendstring = new string[EventZoneConfig.Count * 2];
                int i = 0;
                foreach (KeyValuePair<string, object> pair in EventZoneConfig)
                {
                    sendstring[i] = pair.Key;
                    i++;
                    sendstring[i] = pair.Value.ToString();
                    i++;
                }
                EventManager.Call("UpdateZone", EventName, sendstring);
            }
        }
        object CanEventOpen()
        {
            if (useThisEvent)
            {

            }
            return null;
        }
        object CanEventStart()
        {
            return null;
        }
        object OnEventOpenPost()
        {
            if (useThisEvent)
                EventManager.Call("BroadcastEvent", new object[] { EventMessageOpenBroadcast });
            return null;
        }
        object OnEventClosePost()
        {
            return null;
        }
        object OnEventEndPre()
        {
            return null;
        }
        object OnEventEndPost()
        {
            if (useThisEvent)
            {
                EventStarted = false;
                DeathmatchPlayers.Clear();
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
        object OnEventStartPost()
        {
            return null;
        }
        object CanEventJoin()
        {
            return null;
        }
        object OnEventJoinPost(BasePlayer player)
        {
            if (useThisEvent)
            {
                if (player.GetComponent<DeathmatchPlayer>())
                    GameObject.Destroy(player.GetComponent<DeathmatchPlayer>());
                DeathmatchPlayers.Add(player.gameObject.AddComponent<DeathmatchPlayer>());
            }
            return null;
        }
        object OnEventLeavePost(BasePlayer player)
        {
            if (useThisEvent)
            {
                if (player.GetComponent<DeathmatchPlayer>())
                {
                    DeathmatchPlayers.Remove(player.GetComponent<DeathmatchPlayer>());
                    GameObject.Destroy(player.GetComponent<DeathmatchPlayer>());
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
        //////////////////////////////////////////////////////////////////////////////////////
        // End Of Event Manager Hooks ////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        void AddKill(BasePlayer player, BasePlayer victim)
        {
            if (!player.GetComponent<DeathmatchPlayer>())
                return;

            player.GetComponent<DeathmatchPlayer>().kills++;
            EventManager.Call("AddTokens", player.userID.ToString(), TokensAddKill);
            EventManager.Call("BroadcastEvent", string.Format(EventMessageKill, player.displayName, player.GetComponent<DeathmatchPlayer>().kills.ToString(), EventWinKills.ToString(), victim.displayName));
            CheckScores();
        }
        void CheckScores()
        {
            if (DeathmatchPlayers.Count == 0)
            {
                var emptyobject = new object[] { };
                EventManager.Call("BroadcastEvent", EventMessageNoMorePlayers);
                EventManager.Call("CloseEvent", emptyobject);
                EventManager.Call("EndEvent", emptyobject);
                return;
            }
            foreach (DeathmatchPlayer deathmatchplayer in DeathmatchPlayers)
            {
                if (deathmatchplayer == null) continue;
                if (deathmatchplayer.kills >= EventWinKills || DeathmatchPlayers.Count == 1)
                {
                    Winner(deathmatchplayer.player);
                }
            }
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

