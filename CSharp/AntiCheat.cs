// Reference: Oxide.Ext.Rust
 
using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core;
 
namespace Oxide.Plugins
{
    [Info("AntiCheat", "Reneb & Fix by DieWildeBetty", 1.6)]
    class AntiCheat : RustPlugin
    {
        private static readonly DateTime epoch = new DateTime(1970, 1, 1);
        private double lastCheck;
        private double currenttime;
        private float interval;
 
        private Dictionary<BasePlayer, Vector3> posSave;
        private Dictionary<BasePlayer, double> lastSpeedDetection;
        private Dictionary<BasePlayer, double> lastJumpDetection;
        private Dictionary<BasePlayer, int> JumpDetections;
        private Dictionary<BasePlayer, int> SpeedDetections;
        private Dictionary<BasePlayer, int> TimeLeft;
 
        private float Sminspeedpersecond;
        private int Sdetectionsbeforepunish;
        private bool Sban;
        private bool Skick;
 
        private float Jjumpheight;
        private int Jdetectionsbeforepunish;
        private double Jtimebeforereset;
        private bool Jban;
        private bool Jkick;
 
        private bool permanent;
        private int timetocheck;
        private bool checkadmins;
        private float ignorefps;
        private float fps;
 
        private Core.Configuration.DynamicConfigFile playerdata;
        private List<BasePlayer> tempPlayers;
        private bool Changed;
 
        void Loaded()
        {
            playerdata = Interface.GetMod().DataFileSystem.GetDatafile("AntiCheat_Data");
                posSave = new Dictionary<BasePlayer, Vector3>();
            tempPlayers = new List<BasePlayer>();
            lastCheck = CurrentTime();
           
            lastSpeedDetection = new Dictionary<BasePlayer, double>();
            lastJumpDetection = new Dictionary<BasePlayer, double>();
            JumpDetections = new Dictionary<BasePlayer, int>();
            SpeedDetections = new Dictionary<BasePlayer, int>();
            TimeLeft = new Dictionary<BasePlayer, int>();
        }
        float CurrentFPS()
        {
            return (1 / UnityEngine.Time.smoothDeltaTime);
        }
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("AntiCheat_Data");
        }
        double CurrentTime()
        {
            return System.DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        }
 
        void LoadVariables()
        {
            GetConfig("GeneralConfig", "Version", Version.ToString()); // TODO update check if new config required
            Sminspeedpersecond = Convert.ToSingle(GetConfig("AntiSpeedHack", "minSpeedPerSecond", 12));
            Sdetectionsbeforepunish = Convert.ToInt32(GetConfig("AntiSpeedHack", "detectionsBeforePunish", 3));
            Sban = Convert.ToBoolean(GetConfig("AntiSpeedHack", "punishByBan", true));
            Skick = Convert.ToBoolean(GetConfig("AntiSpeedHack", "punishByKick", false));
 
            Jjumpheight = Convert.ToSingle(GetConfig("AntiJumpHack", "jumpHeight", 5));
            Jdetectionsbeforepunish = Convert.ToInt32(GetConfig("AntiJumpHack", "detectionsBeforePunish", 2));
            Jtimebeforereset = Convert.ToDouble(GetConfig("AntiJumpHack", "timeBeforeReset", 120)); ;
            Jban = Convert.ToBoolean(GetConfig("AntiJumpHack", "punishByBan", true));
            Jkick = Convert.ToBoolean(GetConfig("AntiJumpHack", "punishByKick", false));
            checkadmins = Convert.ToBoolean(GetConfig("GeneralConfig", "CheckAdmins", false));
            permanent = Convert.ToBoolean(GetConfig("DetectionTime", "Permanent", false));
            timetocheck = Convert.ToInt32(GetConfig("DetectionTime", "TimeToCheck", 3600));
            ignorefps = Convert.ToSingle(GetConfig("DetectionTime", "IgnoreUnderFPS", 10));
 
            if (Changed)
            {
                ((Dictionary<string,object>)Config["GeneralConfig"])["Version"] = Version.ToString(); //updated to new version
                SaveConfig();
                Changed = false;
            }
        }
 
        void LoadDefaultConfig()
        {
            Puts("AntiCheat: Creating a new config file");
            Config.Clear(); // force clean new config
            LoadVariables();
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
 
        void OnServerInitialized()
        {
            LoadVariables();
            StartAll();
        }
 
        void ShutdownAll()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                EndPlayer(player);
            }
        }
 
        void StartAll()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                InitPlayer(player);
            }
        }
        void Reset()
        {
            ShutdownAll();
            playerdata.Clear();
            SaveData();
            StartAll();
        }
        void RefreshAll()
        {
            ShutdownAll();
            foreach (var player in BasePlayer.activePlayerList)
            {
                playerdata[player.userID.ToString()] = null;
            }
            SaveData();
            StartAll();
        }
        void InitPlayer(BasePlayer player)
        {
            if (checkadmins || player.net.connection.authLevel < 1)
            {
                posSave[player] = player.transform.position;
                lastSpeedDetection.Add(player, 0);
                SpeedDetections.Add(player, 0);
                lastJumpDetection.Add(player, 0);
                JumpDetections.Add(player, 0);
                var timeleft = timetocheck;
                if (playerdata[player.userID.ToString()] != null)
                    timeleft = Convert.ToInt32(playerdata[player.userID.ToString()]);
                TimeLeft.Add(player, timeleft);
                tempPlayers.Add(player);
            }
            else
            {
                Puts(string.Format("{0} will not be checked by the AntiCheat as he is an admin", player.displayName));
            }
        }
        void EndPlayer(BasePlayer player)
        {
            tempPlayers.Remove(player);
            lastSpeedDetection.Remove(player);
            SpeedDetections.Remove(player);
            lastJumpDetection.Remove(player);
            JumpDetections.Remove(player);
            if(TimeLeft.ContainsKey(player))
                playerdata[player.userID.ToString()] = TimeLeft[player].ToString();
            TimeLeft.Remove(player);
            SaveData();
        }
        void OnPlayerInit(BasePlayer player)
        {
            InitPlayer(player);
        }
        void OnPlayerDisconnected(BasePlayer player)
        {
            EndPlayer(player);
        }
        void SendMsgAdmin(string msg)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.GetComponent<BaseNetworkable>().net.connection.authLevel > 0)
                {
                    player.SendConsoleCommand("chat.add", new object[] { 0, "SERVER", msg.QuoteSafe() } );
                }
            }
        }
        void BroadcastToChat(string msg)
        {
            //ConsoleSystem.Broadcast("chat.add", new object[] { 0, "SERVER" , msg.ToString() });
            ConsoleSystem.Broadcast("chat.add \"SERVER\" " + msg.QuoteSafe() + " 1.0", new object[0]);
        }
 
        void PunishForJump(BasePlayer player, float height)
        {
            var punishment = "kicked";
            SpeedDetections[player] = 0;
            if (Jban)
            {
                punishment = "banned";
                Interface.GetMod().CallHook("Ban", new object[] { null, player, string.Format("r-SuperJump {0}", height.ToString()), false });
            }
            if (Jban || Jkick)
            {
                BroadcastToChat(string.Format("{0} was detected super-jumping and was {1} from the server ({2}m/s)", player.displayName, punishment, height.ToString()));
                Network.Net.sv.Kick(player.net.connection, "Kicked from the server");
            }
        }
        void PunishForSpeed(BasePlayer player, float dist)
        {
            var punishment = "kicked";
            SpeedDetections[player] = 0;
            if (Sban)
            {
                punishment = "banned";
                Interface.GetMod().CallHook("Ban", new object[] { null, player, string.Format("r-Speedhack {0}",dist.ToString()), false });
            }
            if (Sban || Skick)
            {
                BroadcastToChat(string.Format("{0} was detected speedhacking and was {1} from the server ({2}m/s)",player.displayName,punishment,dist.ToString()));
                Network.Net.sv.Kick(player.net.connection, "Kicked from the server");
            }
        }
        void SpeedDetection(BasePlayer player, float dist)
        {
            if (lastSpeedDetection[player] == lastCheck)
            {
                SendMsgAdmin(string.Format("{0} is running at {1} m/s", player.displayName, dist));
                SpeedDetections[player] = SpeedDetections[player] + 1;
                if (SpeedDetections[player] >= Sdetectionsbeforepunish)
                {
                    PunishForSpeed(player, dist);
                }
            }
            lastSpeedDetection[player] = lastCheck;
        }
        void JumpDetection(BasePlayer player, float height)
        {
            if (currenttime < (lastJumpDetection[player] + Jtimebeforereset))
            {
                SendMsgAdmin(string.Format("{0} made a jump of {1} m", player.displayName, height));
                JumpDetections[player] = JumpDetections[player] + 1;
                if (JumpDetections[player] >= Jdetectionsbeforepunish)
                {
                    PunishForJump(player, height);
                }
            }
            lastJumpDetection[player] = lastCheck;
        }
        void CheckTimeLeft(BasePlayer player)
        {
            if (!permanent)
            {
                if (TimeLeft[player] <= 0)
                {
                    EndPlayer(player);
                    return;
                }
                TimeLeft[player] = TimeLeft[player] - 1;
            }
        }
        void CheckAllPlayers(List<BasePlayer> players)
        {
            foreach (BasePlayer player in players.ToArray())
            {
                if (player == null)
                {
                    tempPlayers.Remove(player);
                }
                else
                {
                    if (fps > ignorefps)
                    {
                        float height = (player.transform.position.y - posSave[player].y) / interval;
                        float dist = Vector3.Distance(posSave[player], player.transform.position) / interval;

                        if (Math.Abs(height) < 3.5)
                        {
                            if (dist > Sminspeedpersecond)
                                SpeedDetection(player, dist);
                        }
                        else if (height > Jjumpheight)
                        {
                            if (Math.Abs((dist - height)) <= 12)
                                JumpDetection(player, height);
                        }
                    }
                    posSave[player] = player.transform.position;
                    CheckTimeLeft(player);
                }
            }
            lastCheck = currenttime;
        }

        void OnTick()
        {
            if ((CurrentTime() - lastCheck) >= 1)
            {
                fps = CurrentFPS();
                var players = tempPlayers;
                currenttime = CurrentTime();
                interval = (float)(currenttime - lastCheck);
                CheckAllPlayers(players);
            }
        }
        object CheckPlayer(string tofind)
        {
            var target = BasePlayer.Find(tofind);
            if (target == null || target.net == null || target.net.connection == null)
            {
                return false;
            }
            EndPlayer(target);
            playerdata[target.userID.ToString()] = null;
            InitPlayer(target);
            return target.displayName;
        }
 
        [ConsoleCommand("ac_reset")]
        void cmdConsoleReset(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, "You are not allowed to use this command");
                    return;
                }
            }
            Reset();
            SendReply(arg, "AntiCheat resetted");
        }
 
        [ChatCommand("ac_reset")]
        void cmdChatReset(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            Reset();
            SendReply(player, "AntiCheat resetted");
        }
 
        [ConsoleCommand("ac_checkall")]
        void cmdConsoleCheckAll(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, "You are not allowed to use this command");
                    return;
                }
            }
            RefreshAll();
            SendReply(arg, "All players will now be checked");
        }
 
        [ChatCommand("ac_checkall")]
        void cmdChatCheckAll(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            RefreshAll();
            SendReply(player, "All players will now be checked");
        }
 
        [ConsoleCommand("ac_check")]
        void cmdConsoleCheck(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, "You are not allowed to use this command");
                    return;
                }
            }
            if (arg.Args.Length < 1)
            {
                SendReply(arg, "You must select a player to check");
                return;
            }
            var startedcheck = CheckPlayer(arg.Args[0]);
            if (startedcheck is bool)
            {
                SendReply(arg, "Target player not found");
            }
            else
            {
                startedcheck = (string)startedcheck;
                SendReply(arg, string.Format("Checking {0}", startedcheck));
            }
        }
 
        [ChatCommand("ac_check")]
        void cmdChatCheck(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, "You must select a player to check");
                return;
            }
            var startedcheck = CheckPlayer(args[0]);
            if (startedcheck is Boolean)
            {
                SendReply(player, "Target player not found");
            }
            else
            {
                startedcheck = (string)startedcheck;
                SendReply(player, string.Format("Checking {0}", startedcheck));
            }
        }
    }
}
