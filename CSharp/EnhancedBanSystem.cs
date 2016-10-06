using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("EnhancedBanSystem", "Reneb", "5.0.0", ResourceId = 1951)]
    class EnhancedBanSystem : CovalencePlugin
    {
        [PluginReference] Plugin PlayerDatabase;

        ////////////////////////////////////////////////////////////
        // Static fields
        ////////////////////////////////////////////////////////////
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        char[] ipChrArray = new char[] { '.' };

        BanSystem banSystem = BanSystem.PlayerDatabase | BanSystem.SQLite;

        ////////////////////////////////////////////////////////////
        // Config fields
        ////////////////////////////////////////////////////////////

        static string Platform = "Steam";
        static string Server = "1.1.1.1:28015";
        static string Game = "Rust";

        string PermissionBan = "enhancedbansystem.ban";
        string PermissionUnban = "enhancedbansystem.unban";
        string PermissionBanlist = "enhancedbansystem.banlist";
        string PermissionKick = "enhancedbansystem.kick";

        static string SQLite_DB = "banlist.db";

        static string MySQL_Host = "localhost";
        static int MySQL_Port = 3306;
        static string MySQL_DB = "banlist";
        static string MySQL_User = "root";
        string MySQL_Pass = "toor";

        static string PlayerDatabase_IPFile = "EnhancedBanSystem_IPs.json";

        static string WebAPI_Ban = "http://webpage.com/api.php?action=ban&pass=mypassword&steamid={steamid}&name={name}&ip={ip}&reason={reason}&source={source}&tempban={expiration}";
        static string WebAPI_Unban = "http://webpage.com/api.php?action=unban&pass=mypassword&steamid={steamid}&name={name}&ip={ip}&name={name}&source={source}";
        static string WebAPI_IsBanned = "http://webpage.com/api.php?action=isbanned&pass=mypassword&steamid={steamid}&ip={ip}&time={time}&name={name}&game=Rust&server=rust.kortal.org:28015";
        static string WebAPI_Banlist = "http://webpage.com/banlist.php";

        static string BanDefaultReason = "Banned";

        ////////////////////////////////////////////////////////////
        // Enum & Class
        ////////////////////////////////////////////////////////////

        enum BanSystem
        {
            Files,
            PlayerDatabase,
            WebAPI,
            SQLite,
            MySQL
        }

        class BanData
        {
            public int id;
            public string steamid;
            public string ip;
            public string name;
            public string game;
            public string server;
            public string source;
            public double date;
            public double limit;
            public string reason;
            public string platform;

            public BanData() { }

            public BanData(object source, string userID, string name, string ip, string reason, double duration)
            {
                this.source = source is IPlayer ? ((IPlayer)source).Name : source is string ? (string)source : "Console";
                this.steamid = userID;
                this.name = name;
                this.ip = ip;
                this.reason = reason;
                this.limit = duration != 0.0 ? LogTime() + duration : 0.0;
                this.date = LogTime();
                this.platform = Platform;
                this.game = Game;
                this.server = Server;
            }
        }

        ////////////////////////////////////////////////////////////
        // General Methods
        ////////////////////////////////////////////////////////////

        static double LogTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        string GetMsg(string key, object steamid = null) { return lang.GetMessage(key, this, steamid == null ? null : steamid.ToString()); }

        bool hasPermission(IPlayer player, string permissionName)
        {
            if (player.IsAdmin) return true;
            return permission.UserHasPermission(player.Id.ToString(), permissionName);
        }

        bool isIPAddress(string arg)
        {
            int subIP;
            string[] strArray = arg.Split(ipChrArray);
            if (strArray.Length != 4)
            {
                return false;
            }
            foreach (string str in strArray)
            {
                if (str.Length == 0)
                {
                    return false;
                }
                if (!int.TryParse(str, out subIP) && str != "*")
                {
                    return false;
                }
                if (!(str == "*" || (subIP >= 0 && subIP <= 255)))
                {
                    return false;
                }
            }
            return true;
        }

        List<IPlayer> FindPlayers(string userIDorIP, object source, out string reason)
        {
            reason = string.Empty;
            var FoundPlayers = players.FindPlayers(userIDorIP).ToList();
            if(FoundPlayers.Count == 0)
            {
                reason = GetMsg("No player matching this name was found.", source is IPlayer ? ((IPlayer)source).Id.ToString() : null);
            }
            if (FoundPlayers.Count > 1)
            {
                foreach (var iplayer in FoundPlayers)
                {
                    reason += string.Format("{0} {1}\r\n", iplayer.Id, iplayer.Name);
                }
            }
            return FoundPlayers;
        }

        string GetPlayerIP(IPlayer iplayer)
        {
            if (iplayer.IsConnected) return iplayer.Address;

            return GetPlayerIP(iplayer.Id);
        }

        string GetPlayerIP(string userID)
        {
            if(PlayerDatabase != null)
            {
                return (string)PlayerDatabase.Call("GetPlayerData", userID, "ip") ?? string.Empty;
            }
            return string.Empty;
        }

        ////////////////////////////////////////////////////////////
        // Files
        ////////////////////////////////////////////////////////////
        void ExecuteBan_Files(BanData bandata)
        {

        }

        ////////////////////////////////////////////////////////////
        // PlayerDatabase
        ////////////////////////////////////////////////////////////
        void ExecuteBan_PlayerDatabase(BanData bandata)
        {
            if(bandata.steamid != string.Empty)
            {
                PlayerDatabase.Call("SetPlayerData", bandata.steamid, "Banned", bandata);
            }
            else
            {
                // Need to make a file for ips only
            }
        }

        ////////////////////////////////////////////////////////////
        // WebAPI
        ////////////////////////////////////////////////////////////
        void ExecuteBan_WebAPI(BanData bandata)
        {

        }

        ////////////////////////////////////////////////////////////
        // SQLite
        ////////////////////////////////////////////////////////////
        void ExecuteBan_SQLite(BanData bandata)
        {

        }

        ////////////////////////////////////////////////////////////
        // MySQL
        ////////////////////////////////////////////////////////////
        void ExecuteBan_MySQL(BanData bandata)
        {

        }

        ////////////////////////////////////////////////////////////
        // IsBanned
        ////////////////////////////////////////////////////////////


        ////////////////////////////////////////////////////////////
        // Ban
        ////////////////////////////////////////////////////////////


        string TryBan(object source, string[] args)
        {
            string ipaddress = isIPAddress(args[0]) ? args[0] : string.Empty;
            string steamid = string.Empty;
            string name = string.Empty;
            string errorreason = string.Empty;

            string reason = args.Length > 1 ? args[1] : BanDefaultReason;
            double duration = 0.0;
            if (args.Length > 2) double.TryParse(args[2], out duration);

            
            if (ipaddress != string.Empty)
            {
                return BanIP(source, ipaddress, reason, duration);
            }
            else
            {
                var foundplayers = FindPlayers(args[0], source, out errorreason);
                if(errorreason != string.Empty)
                {
                    return errorreason;
                }
                return BanPlayer(source, foundplayers[0], reason, duration);
            }
        }

        string BanPlayer(object source, IPlayer player, string reason, double duration)
        {
            var returnstring = string.Empty;
            BanData bandata;
            var address = GetPlayerIP(player);
            bool flag = isBannedPlayer(player.Id, address, out bandata);
            if(flag)
            {
                if (bandata.ip == address && player.Id == bandata.steamid)
                {
                    return GetMsg(string.Format("Player is already banned: {0} - {1} - {2}", bandata.steamid, bandata.name, bandata.ip), source is IPlayer ? ((IPlayer)source).Id.ToString() : null);
                }
                else
                {
                    returnstring = string.Format(GetMsg("Player was already banned as: {0} - {1} - {2}\n", source is IPlayer ? ((IPlayer)source).Id.ToString() : null), bandata.steamid, bandata.name, bandata.ip);
                }
            }
            returnstring += ExecuteBan(source, player.Id, player.Name, address, reason, duration);
            return returnstring;
        }

        string ExecuteBan(object source, string userID, string name, string ip, string reason, double duration)
        {
            var bandata = new BanData(source, userID, name, ip, reason, duration);
            if(banSystem.HasFlag(BanSystem.PlayerDatabase))
            {
                ExecuteBan_PlayerDatabase(bandata);
            }
            if(banSystem.HasFlag(BanSystem.Files))
            {
                ExecuteBan_Files(bandata);
            }
            if(banSystem.HasFlag(BanSystem.MySQL))
            {
                ExecuteBan_MySQL(bandata);
            }
            if(banSystem.HasFlag(BanSystem.SQLite))
            {
                ExecuteBan_SQLite(bandata);
            }
            if(banSystem.HasFlag(BanSystem.WebAPI))
            {
                ExecuteBan_WebAPI(bandata);
            }
        }

        ////////////////////////////////////////////////////////////
        // Commands
        ////////////////////////////////////////////////////////////

        [Command("ban", "player.ban")]
        void cmdBan(IPlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermissionBan))
            {
                player.Reply(GetMsg("You don't have the permission to use this command.", player.Id.ToString()));
                return;
            }
            if (args == null || (args.Length < 1))
            {
                player.Reply(GetMsg("Syntax: ban < Name | SteamID > < reason(optional) > < time in secondes(optional > ", player.Id.ToString()));
                return;
            }
            try
            {
                player.Reply(TryBan(player, args));
            } catch(Exception e)
            {
                player.Reply("ERROR:" + e.Message);
            }
        }
    }
}
