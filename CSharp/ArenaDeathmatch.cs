// Reference: Oxide.Ext.Rust

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
    [Info("Arena Deathmatch", "Reneb", "1.0.6", ResourceId = 741)]
    class ArenaDeathmatch : RustPlugin
    {
        ////////////////////////////////////////////////////////////
        // Setting all fields //////////////////////////////////////
        ////////////////////////////////////////////////////////////
        [PluginReference]
        Plugin EventManager;


        private bool launched;
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
            launched = false;
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
            var success = EventManager.Call("RegisterEventGame", new object[] { EventName });
            if (success == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
            launched = true;
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

        static bool EventZoneReject = true;
        static bool EventZoneUndestr = true;
        static bool EventZoneAutoLights = true;
        static bool EventZoneNoBuild = true;
        static bool EventZoneNoDeploy = true;
        static bool EventZoneNoKits = true;
        static bool EventZoneNoTP = true;
        static bool EventZoneKillSleepers = true;
        static bool EventZoneNoSuicide = true;
        static bool EventZoneEraseCorpses = true;
        static bool EventZoneBlockWounded = true;

        static string EventMessageWon = "{0} WON THE DEATHMATCH";
        static string EventMessageNoMorePlayers = "Arena has no more players, auto-closing.";
        static string EventMessageKill = "{0} killed {3}. ({1}/{2} kills)";
        static string EventMessageOpenBroadcast = "In Deathmatch, it's a free for all, the goal is to kill as many players as possible!";

        static int TokensAddKill = 1;
        static int TokensAddWon = 5;

        private void LoadVariables()
        {

            CheckCfg<string>("DeathMatch - Kit - Default", ref DefaultKit);
            CheckCfg<string>("DeathMatch - Event - Name", ref EventName);
            CheckCfg<string>("DeathMatch - Event - SpawnFile", ref EventSpawnFile);
            CheckCfg<int>("DeathMatch - Win - Kills Needed", ref EventWinKills);
            CheckCfgFloat("DeathMatch - Start - Health", ref EventStartHealth);
            CheckCfg<bool>("Zone Settings - Reject None Players", ref EventZoneReject);
            CheckCfg<bool>("Zone Settings - Undestructible", ref EventZoneUndestr);
            CheckCfg<bool>("Zone Settings - Auto Lights", ref EventZoneAutoLights);
            CheckCfg<bool>("Zone Settings - Refuse Build", ref EventZoneNoBuild);
            CheckCfg<bool>("Zone Settings - Refuse Deploy", ref EventZoneNoDeploy);
            CheckCfg<bool>("Zone Settings - Refuse Kit Redeem from /kit", ref EventZoneNoKits);
            CheckCfg<bool>("Zone Settings - Refuse Teleportations", ref EventZoneNoTP);
            CheckCfg<bool>("Zone Settings - Kill Sleepers", ref EventZoneKillSleepers);
            CheckCfg<bool>("Zone Settings - Refuse Suicide", ref EventZoneNoSuicide);
            CheckCfg<bool>("Zone Settings - Erase Corpses", ref EventZoneEraseCorpses);
            CheckCfg<bool>("Zone Settings - Block Wounded", ref EventZoneBlockWounded);

            CheckCfg<string>("Messages - Won", ref EventMessageWon);
            CheckCfg<string>("Messages - Empty", ref EventMessageNoMorePlayers);
            CheckCfg<string>("Messages - Kill", ref EventMessageKill);
            CheckCfg<string>("Messages - Open Broadcast", ref EventMessageOpenBroadcast);

            CheckCfg<int>("Tokens - Per Kill", ref TokensAddKill);
            CheckCfg<int>("Tokens - On Win", ref TokensAddWon);

            CurrentKit = DefaultKit;

            SaveConfig();
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
                EventManager.Call("UpdateZone", EventName, new string[] { "eject", EventZoneReject.ToString(), "undestr", EventZoneUndestr.ToString(), "autolights", EventZoneAutoLights.ToString(), "nobuild", EventZoneNoBuild.ToString(), "nodeploy", EventZoneNoDeploy.ToString(), "nokits", EventZoneNoKits.ToString(), "notp", EventZoneNoTP.ToString(), "killsleepers", EventZoneKillSleepers.ToString(), "nosuicide", EventZoneNoSuicide.ToString(), "nocorpse", EventZoneEraseCorpses.ToString(), "nowounded", EventZoneBlockWounded.ToString() });
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

