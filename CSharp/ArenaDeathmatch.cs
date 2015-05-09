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
    [Info("Arena Deathmatch", "Reneb", "1.0.4", ResourceId = 741)]
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

        private string EventName;
        private string EventSpawnFile;
        
        private string DefaultKit;
        private string CurrentKit;

        private int EventWinKills;


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
        
        float EventStartHealth;
        
        bool EventZoneReject;
		bool EventZoneUndestr;
		bool EventZoneAutoLights;
		bool EventZoneNoBuild;
		bool EventZoneNoDeploy;
		bool EventZoneNoKits;
		bool EventZoneNoTP;
		bool EventZoneKillSleepers;
		bool EventZoneNoSuicide;
		bool EventZoneEraseCorpses;
        
        string EventMessageWon;
        string EventMessageNoMorePlayers;
        string EventMessageKill;
        string EventMessageOpenBroadcast;
        
        private void LoadVariables()
        {
            DefaultKit = Convert.ToString(GetConfig("Default", "Kit", "deathmatch"));
            EventName = Convert.ToString(GetConfig("Settings", "EventName", "Deathmatch"));
            EventSpawnFile = Convert.ToString(GetConfig("Settings", "EventSpawnFile", "DeathmatchSpawnfile"));
            EventWinKills = Convert.ToInt32(GetConfig("Settings", "Win Kills", "10"));
            EventStartHealth = Convert.ToSingle(GetConfig("Settings", "Start Health", "100"));
			
			EventZoneReject = Convert.ToBoolean(GetConfig("Zone Settings", "Reject None Players", "true"));
			EventZoneUndestr = Convert.ToBoolean(GetConfig("Zone Settings", "Undestructible", "true"));
			EventZoneAutoLights = Convert.ToBoolean(GetConfig("Zone Settings", "Auto Lights", "true"));
			EventZoneNoBuild = Convert.ToBoolean(GetConfig("Zone Settings", "Refuse Build", "true"));
			EventZoneNoDeploy = Convert.ToBoolean(GetConfig("Zone Settings", "Refuse Deploy", "true"));
			EventZoneNoKits = Convert.ToBoolean(GetConfig("Zone Settings", "Refuse Kit Redeem from /kit", "true"));
			EventZoneNoTP = Convert.ToBoolean(GetConfig("Zone Settings", "Refuse Teleportations", "true"));
			EventZoneKillSleepers = Convert.ToBoolean(GetConfig("Zone Settings", "Kill Sleepers", "true"));
			EventZoneNoSuicide = Convert.ToBoolean(GetConfig("Zone Settings", "Refuse Suicide", "true"));
			EventZoneEraseCorpses = Convert.ToBoolean(GetConfig("Zone Settings", "Erase Corpses", "true"));
			
			EventMessageWon = Convert.ToString(GetConfig("Messages", "Won", "{0} WON THE DEATHMATCH"));
			EventMessageNoMorePlayers = Convert.ToString(GetConfig("Messages", "Empty", "Arena has no more players, auto-closing."));
			EventMessageKill = Convert.ToString(GetConfig("Messages", "Kill", "{0} killed {3}. ({1}/{2} kills)"));
			EventMessageOpenBroadcast = Convert.ToString(GetConfig("Messages", "OpenBroadcast","In Deathmatch, it's a free for all, the goal is to kill as many players as possible!"));
            
            CurrentKit = DefaultKit;
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
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
            if(name == EventName)
            {
                EventManager.Call("UpdateZone", EventName, new string[] { "eject", EventZoneReject.ToString(), "undestr", EventZoneUndestr.ToString(), "autolights", EventZoneAutoLights.ToString(), "nobuild", EventZoneNoBuild.ToString(), "nodeploy", EventZoneNoDeploy.ToString(), "nokits", EventZoneNoKits.ToString(), "notp", EventZoneNoTP.ToString(), "killsleepers", EventZoneKillSleepers.ToString(), "nosuicide", EventZoneNoSuicide.ToString(), "nocorpse", EventZoneEraseCorpses.ToString() });
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
            EventManager.Call("BroadcastEvent", string.Format(EventMessageKill, player.displayName, player.GetComponent<DeathmatchPlayer>().kills.ToString(), EventWinKills.ToString(), victim.displayName));
            CheckScores();
        } 
        void CheckScores()
        {     
            if(DeathmatchPlayers.Count == 0)
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

