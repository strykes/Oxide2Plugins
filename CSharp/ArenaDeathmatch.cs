// Requires: EventManager
using System.Collections.Generic;
using System;
using UnityEngine;
using Rust;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("ArenaDeathmatch", "Reneb", "1.2.22", ResourceId = 741)]
    class ArenaDeathmatch : RustPlugin
    {
        ////////////////////////////////////////////////////////////
        // Setting all fields //////////////////////////////////////
        ////////////////////////////////////////////////////////////
        [PluginReference]
        EventManager EventManager;        

        private bool useThisEvent;
        private bool EventStarted;
        private bool gameEnding;
        private bool Changed;

        private string CurrentKit;
        private int Scorelimit;

        private List<DeathmatchPlayer> DeathmatchPlayers = new List<DeathmatchPlayer>();

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
        }
        void OnServerInitialized()
        {
            if (EventManager == null)
            {
                Puts("Event plugin doesn't exist");
                return;
            }
            LoadVariables();
            //RegisterGame();
        }        
        protected override void LoadDefaultConfig()
        {
            Puts("Event Deathmatch: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }
        void Unload()
        {
            if (useThisEvent && EventStarted)            
                EventManager.EndEvent();

            var objects = UnityEngine.Object.FindObjectsOfType<DeathmatchPlayer>();
            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);
        }



        //////////////////////////////////////////////////////////////////////////////////////
        // Configurations ////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        static string DefaultKit = "deathmatch";
        static string EventName = "Deathmatch";
        static string EventZoneName = "Deathmatch";
        static string EventSpawnFile = "DeathmatchSpawnfile";
        static int EventWinKills = 10;

        static float EventStartHealth = 100;

        static Dictionary<string, object> EventZoneConfig;

        static string EventMessageWon = "{0} WON THE DEATHMATCH";
        static string EventMessageNoMorePlayers = "Arena has no more players, auto-closing.";
        static string EventMessageKill = "{0} killed {3}. ({1}/{2} kills)";
        static string EventMessageOpenBroadcast = "In Deathmatch, it's a free for all, the goal is to kill as many players as possible!";

        static bool ShowKillFeedChat = true;
        static bool ShowKillFeedUI = true;
        static bool ShowScoreboard = true;

        static int TokensAddKill = 1;
        static int TokensAddWon = 5;

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }
        private void LoadConfigVariables()
        {
            CheckCfg("DeathMatch - Kit - Default", ref DefaultKit);
            CheckCfg("DeathMatch - Event - Name", ref EventName);
            CheckCfg("DeathMatch - Event - SpawnFile", ref EventSpawnFile);
            CheckCfg("DeathMatch - Zone - Name", ref EventZoneName);
            CheckCfg("DeathMatch - Win - Kills Needed", ref EventWinKills);
            CheckCfgFloat("DeathMatch - Start - Health", ref EventStartHealth);


            CheckCfg("Messages - Won", ref EventMessageWon);
            CheckCfg("Messages - Empty", ref EventMessageNoMorePlayers);
            CheckCfg("Messages - Kill", ref EventMessageKill);
            CheckCfg("Messages - Open Broadcast", ref EventMessageOpenBroadcast);
            CheckCfg("Messages - Show kill feed in chat", ref ShowKillFeedChat);
            CheckCfg("Messages - Show kill feed UI", ref ShowKillFeedUI);
            CheckCfg("Scoreboard - Display Scoreboard", ref ShowScoreboard);

            CheckCfg("Tokens - Per Kill", ref TokensAddKill);
            CheckCfg("Tokens - On Win", ref TokensAddWon);

            CurrentKit = DefaultKit;
            Scorelimit = EventWinKills;

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
        #region Scoreboard

        private void UpdateScores() // Creating and updating the EM score board
        {
            if (useThisEvent && EventStarted && ShowScoreboard)
            {
                var sortedList = DeathmatchPlayers.OrderByDescending(pair => pair.kills).ToList(); // Sort the player list by the required value. In this case its sorted by kill count
                var scoreList = new Dictionary<ulong, EventManager.Scoreboard>();
                foreach (var entry in sortedList)
                {
                    if (scoreList.ContainsKey(entry.player.userID)) continue;
                    scoreList.Add(entry.player.userID, new EventManager.Scoreboard { Name = entry.player.displayName, Position = sortedList.IndexOf(entry), Score = entry.kills }); // Add all the event players to a Dictionary containing their name, current position, and the scoring value (kills)
                }
                EventManager.UpdateScoreboard(new EventManager.ScoreData { Additional = null, Scores = scoreList, ScoreType = "Kills" }); // Update the scoreboard via EventManager passing the type of score (in this case kills), the sorted score list, and any additional information (string) you want to pass to the scoreboard
            }
        }
        #endregion

        //////////////////////////////////////////////////////////////////////////////////////
        // Beginning Of Event Manager Hooks //////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        void RegisterGame() // This is the method that register the gamemode with EM. You need to send EM 2 pieces of information
        {
            EventManager.Events eventData = new EventManager.Events // This is the default event. When the game is registered EM will attempt to create a default Event Config using the settings from the game mode's config
            {
                CloseOnStart = false, // Closes event when it starts
                DisableItemPickup = false, // Disables the ability to pickup items
                EnemiesToSpawn = 0, // Number of enemies to spawn
                EventType = Title, // Event name
                GameMode = EventManager.GameMode.Normal, // Gamemode to play
                GameRounds = 0, // Rounds to play
                Kit = DefaultKit, // Default kit to set
                MaximumPlayers = 0, // Maximum players
                MinimumPlayers = 2, // Minimum players
                ScoreLimit = EventWinKills, // Scorelimit
                Spawnfile = EventSpawnFile, // Spawnfile for all players, or 1st team if multiple spawnfiles are required
                Spawnfile2 = null, // Spawnfile for second team
                SpawnType = EventManager.SpawnType.Consecutive, // Type of spawn system. Consecutive spawns players to points in order (stops players spawning on top of each other), random picks a random spawn point from the file
                RespawnType = EventManager.RespawnType.Timer, // Type of respawn mode (None is instant respawn, Timer is a timed respawn, Waves is a wave based respawn)
                RespawnTimer = 5, // Time in seconds before being respawned
                UseClassSelector = false, // Use class selector (disables default kit)
                WeaponSet = null, // Weapon set (for use in gungame)
                ZoneID = EventZoneName // Zone ID
            };
            EventManager.EventSetting eventSettings = new EventManager.EventSetting // These are the settings that allow/disallow game mode options and determines what is required for the event to run
            {
                CanChooseRespawn = true, // Allows the user to select a respawn type
                CanUseClassSelector = true, // Allow use of the Class Selector in this event
                CanPlayBattlefield = true, // Allow's the game type "battlefield" for this event
                ForceCloseOnStart = false, // Force's the event to close on start (overrides the option in event configs)
                IsRoundBased = false, // Tell's EM that this game mode is round based
                LockClothing = false, // Locks the players clothing slots to prevent them from switching shirts mid-game
                RequiresKit = true, // Tell's EM that a kit is required to play this event (unless Class Selector has been set in the event config)
                RequiresMultipleSpawns = false, // Tell's EM that this event requires multiple spawn files
                RequiresSpawns = true, // Tell's EM that this event requires a single spawnfile
                ScoreType = "Kills", // The type of score you wish to keep. Set a score type to allow score limits in your event, or leave it empty to have no score limit
                SpawnsEnemies = false // Set true if this event spawns enemies
            };
            var success = EventManager.RegisterEventGame(Title, eventSettings, eventData); // Now the required information is ready we register the game with EM
            if (success == null)
            {
                Puts("Event plugin doesn't exist"); // This will never happen
                return;
            }
        }
        void OnSelectEventGamePost(string name) // Called when a game type is chosen to play
        {
            if (Title == name) 
                useThisEvent = true;
            else useThisEvent = false;
        }
        void OnEventPlayerSpawn(BasePlayer player) // Called when a event player has spawned
        {
            if (useThisEvent && EventStarted && !gameEnding)
            {
                if (!player.GetComponent<DeathmatchPlayer>()) return;
                if (player.IsSleeping())
                {
                    player.EndSleeping();
                    timer.In(1, () => OnEventPlayerSpawn(player));
                    return;
                }                
                player.inventory.Strip();
                EventManager.GivePlayerKit(player, CurrentKit);
                player.health = EventStartHealth;
            }
        }  
        object CanEventOpen() // Called when a user is trying to open an event. Return null to allow, or return a string containing your reason to disallow
        {
            if (useThisEvent)
            {

            }
            return null;
        }
        object CanEventStart() // Called when a user is trying to start an event. Return null to allow, or return a string containing your reason to disallow
        {
            if (useThisEvent)
            {

            }
            return null;
        }
        void OnEventOpenPost() // Called once a event has been opened, no return behaviour
        {
            if (useThisEvent)
                EventManager.BroadcastToChat(EventMessageOpenBroadcast);
        }
        void OnEventCancel() // Called when a event has been cancelled, no return behaviour
        {
            if (useThisEvent && EventStarted)
                CheckScores(null, true);
        }
        void OnEventClosePost() // Called when a event has been closed, no return behaviour
        {
            if (useThisEvent)
            {

            }
        }
        void OnEventEndPre() // Called when a event has begun the process of ending, no return behaviour
        {
            if (useThisEvent && EventStarted)
            {
                CheckScores(null, true);
            }
        }
        void OnEventEndPost() // Called when a event has finished the process of ending, no return behaviour
        {
            if (useThisEvent)
            {
                EventStarted = false;
                DeathmatchPlayers.Clear();
            }
        }
        void OnEventStartPre() // Called when a event has begun the process of starting, no return behaviour
        {
            if (useThisEvent)
            {
                EventStarted = true;
                gameEnding = false;
            }
        }
        object OnEventStartPost( ) // Called when a event has finished the process of starting, no return behaviour
        {
            if (useThisEvent)
                UpdateScores();
            return null;
        }
        object CanEventJoin() // Called when a user attempts to join an event. Return null to allow, or a string containing the reason to disallow
        {
            if (useThisEvent)
            {

            }
            return null;
        }
        void OnSelectKit(string kitname) // Called when a new kit has been selected, this will update the kit name in the event to make sure the users get that kit type on spawn
        {
            if(useThisEvent)
            {
                CurrentKit = kitname;
            }
        }
        void OnEventJoinPost(BasePlayer player) // Called once a user has successfully joined an event
        {
            if (useThisEvent)
            {
                if (player.GetComponent<DeathmatchPlayer>())
                    UnityEngine.Object.Destroy(player.GetComponent<DeathmatchPlayer>());
                DeathmatchPlayers.Add(player.gameObject.AddComponent<DeathmatchPlayer>());
                EventManager.CreateScoreboard(player);
            }
        }
        void OnEventLeavePre(BasePlayer player) // Called when a user has begun the process of leaving an event
        {
            if (useThisEvent)
            {
            }
        }
        void OnEventLeavePost(BasePlayer player) // Called once a user has successfully left an event
        {
            if (useThisEvent)
            {
                if (player.GetComponent<DeathmatchPlayer>())
                {
                    DeathmatchPlayers.Remove(player.GetComponent<DeathmatchPlayer>());
                    UnityEngine.Object.Destroy(player.GetComponent<DeathmatchPlayer>());
                    CheckScores();
                }
            }
        }
        void OnPlayerSelectClass(BasePlayer player) // Called after a player recieves a kit from the class selector. Useful for adding items such as team shirts
        {            
        }

        void OnEventPlayerAttack(BasePlayer attacker, HitInfo hitinfo) // Called when a event player attacks something
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

        void OnEventPlayerDeath(BasePlayer victim, HitInfo hitinfo) // Called when a event player has died
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
        object EventChooseSpawn(BasePlayer player, Vector3 destination) // Called before a event player is teleported to a arena. this is useful when using multiple spawn files. Return a Vector3 location to override the default spawn location, or return null to do nothing
        {
            if (useThisEvent)
            {

            }
            return null;
        }
        void SetScoreLimit(int scoreLimit) => Scorelimit = scoreLimit; // Called by EM to set the event score limit
        object GetRespawnType() // Called when a event does not allow the user to set a respawn type, this allows you to manually set which type of respawn the event will run
        {
            return null;
        }
        object GetRespawnTime() // Called when a event does not allow the user to set a respawn type. This allows you to manually set the respawn timer for your event
        {
            return null;
        }
        void SetEnemyCount(int number) // Called before a event starts to set the enemy count (if applicable to your event)
        {
        }
        void SetGameRounds(int number) // Called before a event starts to set the round limit (if applicable to your event)
        {
        }
        object FreezeRespawn(BasePlayer player) // Called before the death screen appears for the user. Returning true will put the user in a constant state of respawn with no timer. To respawn the frozen players run the method 'EventManager.RespawnAllPlayers();'
        {            
            return null;
        }
        void SetEventZone(string zonename) // Called before a event starts to set the zone ID
        {

        }

        //////////////////////////////////////////////////////////////////////////////////////
        // End Of Event Manager Hooks ////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        void AddKill(BasePlayer player, BasePlayer victim)
        {
            if (gameEnding) return;
            if (!player.GetComponent<DeathmatchPlayer>())
                return;

            player.GetComponent<DeathmatchPlayer>().kills++;
            EventManager.AddTokens(player.userID, TokensAddKill);
            if (ShowKillFeedUI)
                EventManager.PopupMessage(string.Format(EventMessageKill, player.displayName, player.GetComponent<DeathmatchPlayer>().kills, Scorelimit, victim.displayName));
            else if (ShowKillFeedChat)
                PrintToChat(string.Format(EventMessageKill, player.displayName, player.GetComponent<DeathmatchPlayer>().kills, Scorelimit, victim.displayName));
            UpdateScores();
            CheckScores(player.GetComponent<DeathmatchPlayer>());
        }
        void CheckScores(DeathmatchPlayer player = null, bool timelimit = false)
        {
            if (gameEnding) return;
            if (player != null)
            {
                if (Scorelimit > 0 && player.kills >= Scorelimit)
                {
                    Winner(player.player);
                    return;
                }
            }
            if (DeathmatchPlayers.Count == 0)
            {
                gameEnding = true;
                EventManager.BroadcastToChat(EventMessageNoMorePlayers);
                EventManager.CloseEvent();
                EventManager.EndEvent();
                return;
            }
            if (DeathmatchPlayers.Count == 1)
            {
                Winner(DeathmatchPlayers[0].player);
                return;
            }
            
            if (timelimit)
            {
                BasePlayer winner = null;
                int score = 0;
                foreach (var dmPlayer in DeathmatchPlayers)
                {
                    if (dmPlayer.kills > score)
                    {
                        winner = dmPlayer.player;
                        score = dmPlayer.kills;
                    }
                }
                if (winner != null)
                    Winner(winner);
                return;
            }            
        }
        void Winner(BasePlayer player)
        {
            gameEnding = true;
            if (player != null)
            {
                EventManager.AddTokens(player.userID, TokensAddWon, true);
                EventManager.BroadcastToChat(string.Format(EventMessageWon, player.displayName));
            }
            if (EventManager._Started)
            {
                EventManager.CloseEvent();
                EventManager.EndEvent();
            }
        }
    }
}

