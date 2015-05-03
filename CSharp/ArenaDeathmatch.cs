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
    [Info("Arena Deathmatch", "Reneb", 1.0)]
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
         
        private void LoadVariables()
        {
            DefaultKit = Convert.ToString(GetConfig("Default", "Kit", "deathmatch"));
            EventName = Convert.ToString(GetConfig("Settings", "EventName", "Deathmatch"));
            EventSpawnFile = Convert.ToString(GetConfig("Settings", "EventSpawnFile", "DeathmatchSpawnfile"));
            EventWinKills = Convert.ToInt32(GetConfig("Settings", "Win Kills", "10"));

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
                EventManager.Call("TeleportPlayerToEvent", new object[] { player });
                EventManager.Call("GivePlayerKit", new object[] { player, CurrentKit });
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
                EventManager.Call("BroadcastEvent", new object[] { "In Deathmatch, your inventory WILL be lost!  Do not join until you have put away your items!" });
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
                            
                            AddKill(attacker);
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

        void AddKill(BasePlayer player)
        {
            if (!player.GetComponent<DeathmatchPlayer>())
                return;

            player.GetComponent<DeathmatchPlayer>().kills++;
            EventManager.Call("BroadcastEvent", string.Format("{0} has {1}/{2} kills ", player.displayName, player.GetComponent<DeathmatchPlayer>().kills.ToString(), EventWinKills.ToString()));
            CheckScores();
        } 
        void CheckScores()
        {    
            if(DeathmatchPlayers.Count == 0)
            {
                var emptyobject = new object[] { };
                EventManager.Call("BroadcastEvent", "Arena has no more players, auto-closing.");
                EventManager.Call("CloseEvent", emptyobject);
                EventManager.Call("EndEvent", emptyobject);
                return;
            }
            foreach (DeathmatchPlayer deathmatchplayer in DeathmatchPlayers)
            {
                if (deathmatchplayer.kills >= EventWinKills || DeathmatchPlayers.Count == 1)
                {
                    Winner(deathmatchplayer.player);
                }
            }
        }
        void Winner(BasePlayer player)
        {
            var winnerobjectmsg = new object[] { string.Format("{0} WON THE DEATHMATCH", player.displayName) };
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

