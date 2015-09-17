/******************************************************************************
* Version 2.0 Changelog
*** Rewrote and cleaned up the plugin.*
*** Changed the method of logging time to log time every x minutes.*
*** Cleaned up the playtime command output.*
*** Cleaned up the lastseen command output.*
*** Cleaned up the mostonline command output.*
*** Added a message on login that says how long since the player last logged in.*
*** Fixed KeyNotFoundException when running lastseen on a user that doesn't exist.*
*** Added a prefix to all chat commands. "Play Time"*
*** Added BroadcastLastSeenOnConnect to config, so the broadcast can be disabled if need be.
*** Config will update with new values automatically in future updates.
******************************************************************************/

using System.Collections.Generic;
using System;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("PlayTime", "Waizujin & Reneb", "2.1.0")]
    [Description("Logs players play time and allows you to view the players play time with a command.")]
    public class PlayTime : RustPlugin
    {
        [PluginReference]
        Plugin PlayerDatabase;

        public string Prefix = "<color=red>Play Time:</color> ";
		public int SaveInterval { get { return Config.Get<int>("Save Interval"); } }
		public bool BroadcastLastSeenOnConnect { get { return Config.Get<bool>("Broadcast Last Seen on Connect"); } }

		protected override void LoadDefaultConfig()
		{
			PrintWarning("Creating a new configuration file.");

			Config["Save Interval"] = 300;
			Config["Broadcast Last Seen on Connect"] = true;
		}


        Dictionary<string,object> PlayTimeInfo(BasePlayer player)
        {
            var newinfo = new Dictionary<string, object>();
            long currentTimestamp = GrabCurrentTimestamp();
            newinfo.Add("LastPlayTimeIncrement",currentTimestamp.ToString());
            newinfo.Add("LastLogoutTime", "0");
            newinfo.Add("PlayTime","0");
            return newinfo;
        }

        private void OnServerInitialized()
        {
			var dirty = false;

			if (Config["Save Interval"] == null)
			{
				Config["Save Interval"] = 300;
				dirty = true;
			}

			if (Config["Broadcast Last Seen on Connect"] == null)
			{
				Config["Broadcast Last Seen on Connect"] = true;
				dirty = true;
			}

			if (dirty)
			{
				PrintWarning("Updating configuration file with new values.");
				SaveConfig();
			} 

            permission.RegisterPermission("canUsePlayTime", this);
            permission.RegisterPermission("canUseLastSeen", this);
            permission.RegisterPermission("canUseMostOnline", this);
        } 

        void OnPlayerInit(BasePlayer player)
        {
            long currentTimestamp = GrabCurrentTimestamp();
			var info = PlayTimeInfo(player);
            Dictionary<string, object> playertimePlayer = new Dictionary<string, object>();
            var success = PlayerDatabase.Call("GetPlayerData", player.userID.ToString(), "PlayTime");
            if (success is Dictionary<string,object>)
                playertimePlayer = (Dictionary<string, object>)success;
            if (BroadcastLastSeenOnConnect)
			{ 
                if (playertimePlayer.Count != 0)
                { 
                    long lastLogoutTime = long.Parse((string)playertimePlayer["LastLogoutTime"]);
                    long lastSeenDays = 0;
                    long lastSeenHours = 0;
                    long lastSeenMinutes = 0;
                    long lastSeenSeconds = currentTimestamp - lastLogoutTime;

                    if (lastSeenSeconds > 60)
                    {
                        lastSeenMinutes = lastSeenSeconds / 60;
                        lastSeenSeconds = lastSeenSeconds - (lastSeenMinutes * 60);
                    }

                    if (lastSeenMinutes > 60)
                    {
                        lastSeenHours = lastSeenMinutes / 60;
                        lastSeenMinutes = lastSeenMinutes - (lastSeenHours * 60);
                    }

                    if (lastSeenHours > 24)
                    {
                        lastSeenDays = lastSeenHours / 24;
                        lastSeenHours = lastSeenHours - (lastSeenDays * 24);
                    }

                    if (lastSeenDays > 0)
                    {
                        PrintToChat(Prefix + player.displayName + " was last seen " + lastSeenDays + " days " + lastSeenHours + " hours and " + lastSeenMinutes + " minutes ago.");
                        Puts(player.displayName + " was last seen " + lastSeenDays + " days " + lastSeenHours + " hours and " + lastSeenMinutes + " minutes ago.");
                    }
                    else if (lastSeenHours > 0)
                    {
                        PrintToChat(Prefix + player.displayName + " was last seen " + lastSeenHours + " hours and " + lastSeenMinutes + " minutes ago.");
                        Puts(player.displayName + " was last seen " + lastSeenDays + " days " + lastSeenHours + " hours and " + lastSeenMinutes + " minutes ago.");
                    }
                    else if (lastSeenMinutes > 0)
                    {
                        PrintToChat(Prefix + player.displayName + " was last seen " + lastSeenMinutes + " minutes ago.");
                        Puts(player.displayName + " was last seen " + lastSeenDays + " days " + lastSeenHours + " hours and " + lastSeenMinutes + " minutes ago.");
                    }
                    else
                    {
                        PrintToChat(Prefix + player.displayName + " was last seen " + lastSeenSeconds + " seconds ago.");
                        Puts(player.displayName + " was last seen " + lastSeenDays + " days " + lastSeenHours + " hours and " + lastSeenMinutes + " minutes ago.");
                    }
                }
                else
                {
                    PrintToChat(Prefix + player.displayName + " is the first time he connects.");
                    Puts(player.displayName + " is the first time he connects.");
                }
			}
             
			if (playertimePlayer.Count != 0)
			{
				Puts("Player already has a PlayTime log.");

                playertimePlayer["LastPlayTimeIncrement"] = currentTimestamp.ToString();
                PlayerDatabase.Call("SetPlayerData", player.userID.ToString(), "PlayTime", playertimePlayer);
            }
			else
			{
				Puts("Saving new player to PlayTime log.");
                PlayerDatabase.Call("SetPlayerData", player.userID.ToString(), "PlayTime", info);
            }
        }
        
        void OnPlayerDisconnected(BasePlayer player)
        {
            var playertimePlayer = new Dictionary<string,object>();
            var success = PlayerDatabase.Call("GetPlayerData", player.userID.ToString(), "PlayTime");
            if (success is Dictionary<string, object>)
                playertimePlayer = (Dictionary<string, object>)success;
            if (playertimePlayer.Count == 0) { return; }
            long currentTimestamp = GrabCurrentTimestamp();
            long lastIncrement = long.Parse((string)playertimePlayer["LastPlayTimeIncrement"]);
            long totalPlayed = currentTimestamp - lastIncrement + long.Parse((string)playertimePlayer["PlayTime"]);
            playertimePlayer["PlayTime"] = totalPlayed.ToString();
            playertimePlayer["LastPlayTimeIncrement"] = currentTimestamp.ToString();
            playertimePlayer["LastLogoutTime"] = currentTimestamp.ToString();
            PlayerDatabase.Call("SetPlayerData", player.userID.ToString(), "PlayTime", playertimePlayer);
        } 
        
        void Unload()
        {
            foreach (BasePlayer onlinePlayer in BasePlayer.activePlayerList)
            {
                long currentTimestamp = GrabCurrentTimestamp();
                long playerSteamID = FindPlayer(onlinePlayer.userID.ToString());

                if (playerSteamID == 0)
                {
					var info = PlayTimeInfo(onlinePlayer);
                    PlayerDatabase.Call("SetPlayerData", onlinePlayer.userID.ToString(), "PlayTime", info);
					continue;
                }

                var playertimePlayer = new Dictionary<string, object>();
                var success = PlayerDatabase.Call("GetPlayerData", onlinePlayer.userID.ToString(), "PlayTime");
                if (success is Dictionary<string, object>)
                    playertimePlayer = (Dictionary<string, object>)success;

                long playedTime = currentTimestamp - long.Parse((string)playertimePlayer["LastPlayTimeIncrement"]) + long.Parse((string)playertimePlayer["PlayTime"]);


                playertimePlayer["PlayTime"] = playedTime.ToString();
                playertimePlayer["LastPlayTimeIncrement"] = currentTimestamp.ToString();

                PlayerDatabase.Call("SetPlayerData", onlinePlayer.userID.ToString(), "PlayTime", playertimePlayer);
            }
        }

        [ChatCommand("playtime")]
        private void PlayTimeCommand(BasePlayer player, string command, string[] args)
        {
			if (!hasPermission(player, "canUsePlayTime") && args.Length > 0)
			{
				SendReply(player, Prefix + "You don't have permission to use this command.");
				return;
			}

			var queriedPlayer = "";

			if (args.Length == 0)
			{
				queriedPlayer = player.userID.ToString();
			}
			else
			{
				queriedPlayer = args[0];
			}

            long daysPlayed = 0;
            long hoursPlayed = 0;
            long minutesPlayed = 0;
            long secondsPlayed = 0;

			long playerSteamID = 0;

			playerSteamID = FindPlayer(queriedPlayer);

			if (playerSteamID == 0)
			{
				SendReply(player, Prefix + "The player '" + queriedPlayer + "' does not exist in the system.");

				return;
			}

            var playertimePlayer = new Dictionary<string, object>();
            var success = PlayerDatabase.Call("GetPlayerData", playerSteamID.ToString(), "PlayTime");
            if (success is Dictionary<string, object>)
                playertimePlayer = (Dictionary<string, object>)success;
            if (playertimePlayer.Count == 0) { SendReply(player, string.Format("Couldn't find the data for this player {0}", playerSteamID.ToString())); return; }

            var playerName = (PlayerDatabase.Call("GetPlayerData", playerSteamID.ToString(), "default") as Dictionary<string,object>)["name"] as string;

            long currentTimestamp = GrabCurrentTimestamp();
            secondsPlayed = currentTimestamp - long.Parse((string)playertimePlayer["LastPlayTimeIncrement"]) + long.Parse((string)playertimePlayer["PlayTime"]);

			if (secondsPlayed > 60)
			{
				minutesPlayed = secondsPlayed / 60;
				secondsPlayed = secondsPlayed - (minutesPlayed * 60);
			}

			if (minutesPlayed > 60)
			{
				hoursPlayed = minutesPlayed / 60;
				minutesPlayed = minutesPlayed - (hoursPlayed * 60);
			}

			if (hoursPlayed > 24)
			{
				daysPlayed = hoursPlayed / 24;
				hoursPlayed = hoursPlayed - (daysPlayed * 24);
			}

			if (daysPlayed > 0)
			{
				SendReply(player, Prefix + "The player " + playerName + " (" + playerSteamID + ") has played for " + daysPlayed + " days " + hoursPlayed + " hours and " + minutesPlayed + " minutes.");
			}
			else if (hoursPlayed > 0)
			{
				SendReply(player, Prefix + "The player " + playerName + " (" + playerSteamID + ") has played for " + hoursPlayed + " hours and " + minutesPlayed + " minutes.");
			}
			else if (minutesPlayed > 0)
			{
				SendReply(player, Prefix + "The player " + playerName + " (" + playerSteamID + ") has played for " + minutesPlayed + " minutes.");
			}
			else
			{
				SendReply(player, Prefix + "The player " + playerName + " (" + playerSteamID + ") has played for " + secondsPlayed + " seconds.");
			}
		}

		[ChatCommand("lastseen")]
		private void LastSeenCommand(BasePlayer player, string command, string[] args)
		{
			if (!hasPermission(player, "canUseLastSeen"))
			{
				SendReply(player, Prefix + "You don't have permission to use this command.");

				return;
			}

			if (args.Length == 0)
			{
				SendReply(player, Prefix + "Please enter a players name or steam 64 id.");

				return;
			}

            long currentTimestamp = GrabCurrentTimestamp();
			var queriedPlayer = args[0];
            long playerSteamID = 0;
            try { playerSteamID = FindPlayer(queriedPlayer); } catch (KeyNotFoundException e ) { return; }
             
			if (playerSteamID == 0)
			{
				SendReply(player, Prefix + "The player '" + queriedPlayer + "' does not exist in the system.");

				return;
			}

            var playerName = (PlayerDatabase.Call("GetPlayerData", playerSteamID.ToString(), "default") as Dictionary<string, object>)["name"] as string;

            foreach (BasePlayer onlinePlayer in BasePlayer.activePlayerList)
			{
				if (onlinePlayer.userID.ToString() == playerSteamID.ToString())
				{
					SendReply(player, Prefix + "The player " + playerName + " ( " + playerSteamID + ") is online right now!");

					return;
				}
			}
            var playertimePlayer = new Dictionary<string, object>();
            var success = PlayerDatabase.Call("GetPlayerData", playerSteamID.ToString(), "PlayTime");
            if (success is Dictionary<string, object>)
                playertimePlayer = (Dictionary<string, object>)success;
            if (playertimePlayer.Count == 0) { SendReply(player, string.Format("Couldn't find the data for this player {0}", playerSteamID.ToString())); return; }

            long lastLogoutTime = long.Parse((string)playertimePlayer["LastLogoutTime"]);
            long lastSeenDays = 0;
            long lastSeenHours = 0;
            long lastSeenMinutes = 0;
            long lastSeenSeconds = currentTimestamp - lastLogoutTime;

			if (lastSeenSeconds > 60)
			{
				lastSeenMinutes = lastSeenSeconds / 60;
				lastSeenSeconds = lastSeenSeconds - (lastSeenMinutes * 60);
			}

			if (lastSeenMinutes > 60)
			{
				lastSeenHours = lastSeenMinutes / 60;
				lastSeenMinutes = lastSeenMinutes - (lastSeenHours * 60);
			}

			if (lastSeenHours > 24)
			{
				lastSeenDays = lastSeenHours / 24;
				lastSeenHours = lastSeenHours - (lastSeenDays * 24);
			}

			if (lastSeenDays > 0)
			{
				SendReply(player, Prefix + "The player " + playerName + " ( " + playerSteamID + ") was last seen " + lastSeenDays + " days " + lastSeenHours + " hours and " + lastSeenMinutes + " minutes ago.");
			}
			else if (lastSeenHours > 0)
			{
				SendReply(player, Prefix + "The player " + playerName + " ( " + playerSteamID + ") was last seen " + lastSeenHours + " hours and " + lastSeenMinutes + " minutes ago.");
			}
			else if (lastSeenMinutes > 0)
			{
				SendReply(player, Prefix + "The player " + playerName + " ( " + playerSteamID + ") was last seen " + lastSeenMinutes + " minutes ago.");
			}
			else
			{
				SendReply(player, Prefix + "The player " + playerName + " ( " + playerSteamID + ") was last seen " + lastSeenSeconds + " seconds ago.");
			}
		}
        
		[ChatCommand("mostonline")]
		private void MostOnlineCommand(BasePlayer player, string command, string[] args)
		{
			if (!hasPermission(player, "canUseMostOnline"))
			{
				SendReply(player, Prefix + "You don't have permission to use this command.");

				return;
			}

			Dictionary<string, long> mostOnline = new Dictionary<string, long>();
            HashSet<string> knownPlayers = new HashSet<string>();
            var success = PlayerDatabase.Call("GetAllKnownPlayers");
            if (success is HashSet<string>)
                knownPlayers = (HashSet<string>)success;
            if(knownPlayers.Count == 0)
            {
                SendReply(player, "Couldn't get the list of players");
                return;
            }
            foreach (string playerID in knownPlayers)
			{
                var playertimePlayer = new Dictionary<string, object>();
                var successs = PlayerDatabase.Call("GetPlayerData", playerID, "PlayTime");
                if (successs is Dictionary<string, object>)
                    playertimePlayer = (Dictionary<string, object>)successs;
                if (playertimePlayer.Count == 0) { continue; }

                mostOnline.Add(playerID, long.Parse((string)playertimePlayer["PlayTime"]));
			}

            List<KeyValuePair<string, long>> sorted = (from kv in mostOnline orderby kv.Value descending select kv).Take(10).ToList();

			string highscore = "<color=red>Top 10 Most Online</color> \n" +
			"------------------------------ \n" +
			"Rank - Online Time (Days:Hours:Minutes) - <color=red>Player Name</color> \n" +
			"------------------------------ \n";
			int count = 0;
			foreach (KeyValuePair<string, long> kv in sorted)
			{
				count++;
				long daysPlayed = 0;
				long hoursPlayed = 0;
				long minutesPlayed = 0;
				long secondsPlayed = kv.Value;
				string daysPlayedString = "";
				string hoursPlayedString = "";
				string minutesPlayedString = "";

				if (secondsPlayed > 60)
				{
					minutesPlayed = secondsPlayed / 60;
					secondsPlayed = secondsPlayed - (minutesPlayed * 60);
				}

				if (minutesPlayed > 60)
				{
					hoursPlayed = minutesPlayed / 60;
					minutesPlayed = minutesPlayed - (hoursPlayed * 60);
				}

				if (hoursPlayed > 24)
				{
					daysPlayed = hoursPlayed / 24;
					hoursPlayed = hoursPlayed - (daysPlayed * 24);
				}

				if (daysPlayed < 10) { daysPlayedString = "0" + daysPlayed.ToString(); } else { daysPlayedString = daysPlayed.ToString(); }
				if (hoursPlayed < 10) { hoursPlayedString = "0" + hoursPlayed.ToString(); } else { hoursPlayedString = hoursPlayed.ToString(); }
				if (minutesPlayed < 10) { minutesPlayedString = "0" + minutesPlayed.ToString(); } else { minutesPlayedString = minutesPlayed.ToString(); }

				if (count < 10)
				{
					highscore += "  ";
				}
                string Name = string.Empty;
                var successss = PlayerDatabase.Call("GetPlayerData", kv.Key, "default");
                if (successss is Dictionary<string, object>)
                    Name = ((Dictionary<string, object>)successss)["name"] as string;
                highscore += count + ". " + daysPlayedString + ":" + hoursPlayedString + ":" + minutesPlayedString + " - <color=red>" + Name == string.Empty ? kv.Key : Name + "</color>\n";
			}

			SendReply(player, Prefix + highscore);
		}

		private long FindPlayer(string queriedPlayer)
        {
            long playerSteamID = 0;

            string success = PlayerDatabase.Call("FindPlayer", queriedPlayer) as string;
            if (success.Length == 17)
                playerSteamID = long.Parse(success);

            return playerSteamID;
        }  

        private static long GrabCurrentTimestamp()
        {
            long timestamp = 0;
            long ticks = DateTime.UtcNow.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks;
            ticks /= 10000000;
            timestamp = ticks;

            return timestamp;
        }

        bool hasPermission(BasePlayer player, string perm)
        {
            if (player.net.connection.authLevel > 1)
            {
                return true;
            }

            return permission.UserHasPermission(player.userID.ToString(), perm);
        }
    }
}
