// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;
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
        private Core.Libraries.Plugins loadedPlugins;
        private Core.Plugins.Plugin eventPlugin;
        
        private bool launched;
        private bool useThisEvent;
        private bool EventStarted;
        private bool Changed;
        
        private string EventName; 
        private string EventSpawnFile;
        
        private int DefaultKit;
        
        private Dictionary<string, object> CurrentKit;
        
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
                enabled = true;
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
            loadedPlugins = Interface.GetMod().GetLibrary<Core.Libraries.Plugins>("Plugins");
            DeathmatchPlayers = new List<DeathmatchPlayer>();
        }
        void OnServerInitialized()
        {
            eventPlugin = loadedPlugins.Find("EventManager");
            if (eventPlugin == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
			LoadVariables();
            var success = eventPlugin.CallHook("RegisterEventGame", new object[] { EventName });
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
                eventPlugin.CallHook("EndEvent", new object[] { });
				var objects = GameObject.FindObjectsOfType(typeof(DeathmatchPlayer));
				if (objects != null)
					foreach (var gameObj in objects)
						GameObject.Destroy(gameObj);
            }
        }
        
        //////////////////////////////////////////////////////////////////////////////////////
    	// Configurations ////////////////////////////////////////////////////////////////////
    	//////////////////////////////////////////////////////////////////////////////////////
    	private void CreateDefaultKit()
    	{
    		var Kits = new List<object>();
            var kit1 = new Dictionary<string, object>();
            var main1 = new Dictionary<string, object>();
            main1.Add("ammo_rifle", 250);
            var belt1 = new Dictionary<string, object>();
            belt1.Add("rifle_bolt", 1);
            belt1.Add("bandage", 2);
            belt1.Add("syringe_medical", 2);
            var wear1 = new Dictionary<string, object>();
            wear1.Add("urban_boots", 1);
            wear1.Add("urban_jacket", 1);
            wear1.Add("urban_pants", 1);
            wear1.Add("burlap_gloves", 1);
            kit1.Add("main", main1);
            kit1.Add("belt", belt1);
            kit1.Add("wear", wear1);
            Kits.Add(kit1);
    		Config["Kits"] = Kits;
    		Changed = true;
    	}
        private void LoadVariables()
        {
            if(Config["Kits"] == null) CreateDefaultKit();
            DefaultKit = Convert.ToInt32(GetConfig("Default", "Kit", 1));
            EventName = Convert.ToString(GetConfig("Settings", "EventName", "Deathmatch"));
            EventSpawnFile = Convert.ToString(GetConfig("Settings", "EventSpawnFile", "DeathmatchSpawnfile"));
            
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
                if(EventSpawnFile != null && EventSpawnFile != "")
                    eventPlugin.CallHook("SelectSpawnfile", new object[] { EventSpawnFile });
            }
            else
                useThisEvent = false;
        }
        void OnEventPlayerSpawn(BasePlayer player)
        {
            if (useThisEvent && EventStarted)
            {
                player.inventory.Strip();
                eventPlugin.CallHook("GivePlayerKit", new object[] { player, CurrentKit });
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
                var Kits = Config["Kits"] as List<object>;
                if ((DefaultKit-1) >= Kits.Count )
                {
                    return string.Format("The default kit {0} doesn't exist", DefaultKit.ToString());
                }
                CurrentKit = Kits[(DefaultKit-1)] as Dictionary<string, object>;
            }
            return null;
        }
        object CanEventStart()
        {
            return null;
        }
        object OnEventOpenPost()
        {
            if(useThisEvent)
                eventPlugin.CallHook("BroadcastEvent", new object[] { "In Deathmatch, your inventory WILL be lost!  Do not join until you have put away your items!" });
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
            if (useThisEvent )
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
        	if(useThisEvent)
        	{
				if(hitinfo.Initiator != null)
				{
					BasePlayer attacker = hitinfo.Initiator.ToPlayer();
					if(attacker != null)
					{
						if(attacker != victim)
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
			CheckScores();
        }
        void CheckScores()
        {
        	foreach(DeathmatchPlayer deathmatchplayer in DeathmatchPlayers)
        	{
        		if(deathmatchplayer.kills >= 10)
        		{
        			Winner(deathmatchplayer.player);
        		}
        	}
        }
        void Winner(BasePlayer player)
        {
        	var winnerobjectmsg = new object[] { string.Format("{0} WON THE DEATHMATCH",player.displayName) };
        	var emptyobject = new object[] { };
        	for(var i = 1; i<10; i++)
        	{
        		eventPlugin.CallHook("BroadcastEvent", winnerobjectmsg);
        	}
        	eventPlugin.CallHook("CloseEvent",emptyobject);
        	eventPlugin.CallHook("EndEvent",emptyobject);
        }
    }
}
