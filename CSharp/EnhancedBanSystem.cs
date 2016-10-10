using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Ext.MySql;
using Oxide.Ext.SQLite;
using Oxide.Core.Configuration;
using Oxide.Core.Database;
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

        static BanSystem banSystem;

        static Hash<int, BanData> cachedBans = new Hash<int, BanData>();

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
        static bool SQLite_Cache = false;

        static string MySQL_Host = "localhost";
        static int MySQL_Port = 3306;
        static string MySQL_DB = "banlist";
        static string MySQL_User = "root";
        static string MySQL_Pass = "toor";
        static bool MySQL_Cache = false;

        static string PlayerDatabase_IPFile = "EnhancedBanSystem_IPs.json";

        static string WebAPI_Ban = "http://webpage.com/api.php?action=ban&pass=mypassword&id={id}&steamid={steamid}&name={name}&ip={ip}&reason={reason}&source={source}&game={game}&platform={platform}&server={server}&tempban={expiration}";
        static string WebAPI_Unban = "http://webpage.com/api.php?action=unban&pass=mypassword&steamid={steamid}&name={name}&ip={ip}&name={name}&source={source}";
        static string WebAPI_IsBanned = "http://webpage.com/api.php?action=isbanned&pass=mypassword&steamid={steamid}&ip={ip}&time={time}&name={name}&game=Rust&server=rust.kortal.org:28015";
        static string WebAPI_Banlist = "http://webpage.com/banlist.php";

        static string BanDefaultReason = "Banned";

        static string BanEvadeReason = "Ban Evade";

        static bool Kick_Broadcast = true;
        static bool Kick_Log = true;
        static bool Kick_OnBan = false;

        static bool Ban_Broadcast = true;
        static bool Ban_Log = true;

        static bool Ban_Escape = true;

        static bool Log_Denied = true;

        ////////////////////////////////////////////////////////////
        // ID Save
        ////////////////////////////////////////////////////////////

        static DynamicConfigFile Ban_ID_File;
        static int Ban_ID = 0;

        void Load_ID()
        {
            try
            {
                Ban_ID_File = Interface.GetMod().DataFileSystem.GetDatafile("EnhancedBanSystem_ID");

                Ban_ID = (int)Ban_ID_File["id"];
            }
            catch
            {
                Ban_ID = 0;
                Ban_ID_File["id"] = Ban_ID;
                Save_ID();
            }
        }

        void Save_ID()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("EnhancedBanSystem_ID");
        }

        static int GetNewID()
        {
            Ban_ID++;
            Ban_ID_File["id"] = Ban_ID;
            return Ban_ID;
        }

        ////////////////////////////////////////////////////////////
        // Enum & Class
        ////////////////////////////////////////////////////////////

        enum BanSystem
        {
            Files = 0x20,
            PlayerDatabase = 0x10,
            WebAPI = 8,
            SQLite = 4,
            MySQL = 2,
            Native =1
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
            public double expire;
            public string reason;
            public string platform;

            public BanData() { }

            public BanData(object source, string userID, string name, string ip, string reason, double duration)
            {
                this.id = GetNewID();
                this.source = source is IPlayer ? ((IPlayer)source).Name : source is string ? (string)source : "Console";
                this.steamid = userID;
                this.name = name;
                this.ip = ip;
                this.reason = reason;
                this.expire = duration != 0.0 ? LogTime() + duration : 0.0;
                this.date = LogTime();
                this.platform = Platform;
                this.game = Game;
                this.server = Server;
            }

            public BanData(int id, string source, string userID, string name, string ip, string reason, string duration)
            {
                this.id = id;
                this.source = source;
                this.steamid = userID;
                this.name = name;
                this.ip = ip;
                this.reason = reason;
                this.expire = double.Parse(duration);
                this.date = LogTime();
                this.platform = Platform;
                this.game = Game;
                this.server = Server;
            }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }

            public override string ToString()
            {
                return string.Format("{0} - {1} - {2} - {3} - {4}", steamid, name, ip, reason, expire == 0.0 ? "Permanent" : string.Format("Temporary: {0}s", (expire - LogTime()).ToString()));
            }
        }

        ////////////////////////////////////////////////////////////
        // General Methods
        ////////////////////////////////////////////////////////////

        static double LogTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        //string GetMsg(string key, object steamid = null) { return lang.GetMessage(key, this, steamid is IPlayer ? ((IPlayer)steamid).Id : steamid == null ? null : steamid.ToString()); }
        string GetMsg(string key, object steamid = null) { return key; }

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

        bool IPRange(string sourceIP, string targetIP)
        {
            string[] srcArray = sourceIP.Split(ipChrArray);
            string[] trgArray = targetIP.Split(ipChrArray);
            for (int i = 0; i < 4; i++)
            {
                if (srcArray[i] != trgArray[i] && srcArray[i] != "*")
                {
                    return false;
                }
            }
            return true;
        }

        List<IPlayer> FindPlayers(string userIDorNameorIP, object source, out string reason)
        {
            reason = string.Empty;
            var FoundPlayers = players.FindPlayers(userIDorNameorIP).ToList();
            if (FoundPlayers.Count == 0)
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

        List<IPlayer> FindConnectedPlayers(string userIDorNameorIP, object source, out string reason)
        {
            reason = string.Empty;
            var FoundPlayers = new List<IPlayer>();
            if (isIPAddress(userIDorNameorIP))
            {
                FoundPlayers = players.GetAllPlayers().Where(x => x.IsConnected).Where(w => IPRange(userIDorNameorIP, w.Address)).ToList();
            }
            else
            {
                FoundPlayers = players.FindConnectedPlayers(userIDorNameorIP).ToList();
                if (FoundPlayers.Count > 1)
                {
                    foreach (var iplayer in FoundPlayers)
                    {
                        reason += string.Format("{0} {1}\r\n", iplayer.Id, iplayer.Name);
                    }
                }
            }
            if (FoundPlayers.Count == 0)
            {
                reason = GetMsg("No player matching this name was found.", source is IPlayer ? ((IPlayer)source).Id.ToString() : null);
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
            if (PlayerDatabase != null)
            {
                return (string)PlayerDatabase.Call("GetPlayerData", userID, "ip") ?? string.Empty;
            }
            return string.Empty;
        }

        bool HasDelayedAnswer()
        {
            return BanSystemHasFlag(banSystem, BanSystem.MySQL) || BanSystemHasFlag(banSystem,BanSystem.SQLite) || (BanSystemHasFlag(banSystem,BanSystem.WebAPI));
        }

        bool BanSystemHasFlag(BanSystem b, BanSystem t)
        {
            return (b & t) == t;
        }

        ////////////////////////////////////////////////////////////
        // Oxide Hooks
        ////////////////////////////////////////////////////////////


        void Loaded()
        {
            banSystem  |= BanSystem.PlayerDatabase;
            banSystem |= BanSystem.SQLite;

            Load_ID();
            if (BanSystemHasFlag(banSystem,BanSystem.PlayerDatabase))
            {
                Load_PlayerDatabase();
            }
            if (BanSystemHasFlag(banSystem,BanSystem.Files))
            {
                Load_Files();
            }
            if (BanSystemHasFlag(banSystem,BanSystem.MySQL))
            {
                LoadMySQL();
            }
            if (BanSystemHasFlag(banSystem,BanSystem.SQLite))
            {
                Load_SQLite();
            }
            if (BanSystemHasFlag(banSystem,BanSystem.WebAPI))
            {
                Interface.Oxide.LogWarning("webapi");
                // Nothing to load
            }
            if (BanSystemHasFlag(banSystem, BanSystem.Native))
            {
                Interface.Oxide.LogWarning("natuve");
                // Nothing to load
            }
        }


        void OnServerSave()
        {
            Save_ID();
            if (BanSystemHasFlag(banSystem,BanSystem.PlayerDatabase))
            {
                Save_PlayerDatabaseIP();
            }
            if (BanSystemHasFlag(banSystem,BanSystem.Files))
            {
                Save_Files();
            }
            if (BanSystemHasFlag(banSystem,BanSystem.MySQL))
            {
                // Nothing to save
            }
            if (BanSystemHasFlag(banSystem,BanSystem.SQLite))
            {
                // Nothing to save
            }
            if (BanSystemHasFlag(banSystem,BanSystem.WebAPI))
            {
                // Nothing to save
            }
        }

        object CanUserLogin(string name, string steamid, string ip)
        {
            BanData bd = null;
#if RUST
            using (TimeWarning.New("CanUserLogin", 0.1f))
            {
#endif
                if (isBanned_NonDelayed(name, steamid, ip, Ban_Escape, out bd))
                {
                    return string.Format("You are banned from this server {0}", bd == null ? string.Empty : string.Format("({0})", bd.reason));
                }
#if RUST
            }
#endif
            return null;
        }

        void OnUserConnected(IPlayer player)
        {
            if (player == null) return;
            string ip = player.Address;
            string name = player.Name;
            string steamid = player.Id;

            isBanned_Delayed(name, steamid, ip, Ban_Escape);
        }

        ////////////////////////////////////////////////////////////
        // Files
        ////////////////////////////////////////////////////////////

        static StoredData storedData;

        class StoredData
        {
            public HashSet<string> Banlist = new HashSet<string>();

            public StoredData() { }
        }

        void Load_Files()
        {
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("EnhancedBanSystem");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var b in storedData.Banlist)
            {
                var bd = JsonConvert.DeserializeObject<BanData>(b);
                if (!cachedBans.ContainsKey(bd.id))
                    cachedBans.Add(bd.id, bd);
            }
        }

        void Save_Files()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("EnhancedBanSystem");
        }

        string ExecuteBan_Files(BanData bandata)
        {

            var f = cachedBans.Values.Where(x => x.ip == bandata.ip).Where(x => x.steamid == bandata.steamid).ToList();
            if (f.Count > 0)
            {
                return string.Format("Files: This ban already exists ({0})", f[0].ToString());
            }
            storedData.Banlist.Add(bandata.ToJson());

            if (!cachedBans.ContainsKey(bandata.id))
                cachedBans.Add(bandata.id, bandata);

            return string.Format("Files: Successfully added {0} to the banlist", bandata.ToString());
        }

        string Files_ExecuteUnban(string steamid, string name, string ip, out List<BanData> unbanList)
        {
            int i = 0;
            unbanList = new List<BanData>();
            if (ip != string.Empty)
            {
                unbanList = cachedBans.Values.Where(x => x.ip == ip).ToList();
            }
            else if (steamid != string.Empty)
            {
                unbanList = cachedBans.Values.Where(x => x.steamid == steamid).ToList();
            }
            else
            {
                unbanList = cachedBans.Values.Where(x => x.name == name).ToList();
                if (unbanList.Count == 0)
                {
                    var lname = name.ToLower();
                    unbanList = cachedBans.Values.Where(x => x.name.ToLower().Contains(lname)).ToList();
                    if (unbanList.Count > 1)
                    {
                        var ret = "Files: Multiple Bans Found:\n";
                        foreach (var b in unbanList)
                        {
                            ret += string.Format("{0} - {1} - {2}", b.steamid, b.name, b.reason);
                        }
                        return ret;
                    }
                }
            }
            foreach (var u in unbanList)
            {
                var json = u.ToJson();
                if (storedData.Banlist.Contains(json))
                {
                    i++;
                    storedData.Banlist.Remove(json);
                }
            }
            return string.Format("Files: {0} matching bans were removed", i.ToString());
        }

        bool Files_IsBanned(string steamid, string ip, out BanData bandata)
        {
            bandata = null;
            List<BanData> list = new List<BanData>();
            {
                // check by ID then IP
                var b_steamid = cachedBans.Values.Where(x => x.steamid == steamid).ToList();
                var b_steamid_ip = b_steamid.Where(w => w.ip == ip).ToList();
                if(b_steamid_ip.Count > 0)
                {
                    bandata = b_steamid_ip[0];
                }
                else if(b_steamid.Count > 0)
                {
                    bandata = b_steamid[0];
                }
            }
            if (bandata == null)
            {
                // check by IP & IP Range
                list = cachedBans.Values.Where(x => IPRange(x.ip, ip)).ToList();
                if (list.Count > 0)
                {
                    bandata = list[0];
                }
            }
            return bandata != null;
        }

        ////////////////////////////////////////////////////////////
        // PlayerDatabase
        ////////////////////////////////////////////////////////////

        static StoredIPData storedIPData;

        class StoredIPData
        {
            public HashSet<string> Banlist = new HashSet<string>();

            public StoredIPData() { }
        }

        void Load_PlayerDatabase()
        {
            if (PlayerDatabase == null) return;
            try
            {
                storedIPData = Interface.GetMod().DataFileSystem.ReadObject<StoredIPData>("EnhancedBanSystem_IPs");
            }
            catch
            {
                storedIPData = new StoredIPData();
            }
            foreach (var b in storedIPData.Banlist)
            {
                var bd = JsonConvert.DeserializeObject<BanData>(b);
                if(!cachedBans.ContainsKey(bd.id))
                    cachedBans.Add(bd.id, bd);
            }
            var getKnownPlayers = (List<string>)PlayerDatabase.Call("GetAllKnownPlayers");
            if (getKnownPlayers == null) return;

            List<BanData> list = new List<BanData>();
            foreach (var steamid in getKnownPlayers)
            {
                var success = PlayerDatabase.Call("GetPlayerDataRaw", steamid, "Banned");
                if (!(success is string)) continue;
                list = JsonConvert.DeserializeObject<List<BanData>>((string)success);
                foreach(var b in list)
                {
                    if(!cachedBans.ContainsKey(b.id))
                        cachedBans.Add(b.id, b);
                }
                
            }
        }

        void Save_PlayerDatabaseIP()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("EnhancedBanSystem");
        }


        string ExecuteBan_PlayerDatabase(BanData bandata)
        {
            if (bandata.steamid != string.Empty)
            {
                List<BanData> list = new List<BanData>();
                var success = PlayerDatabase.Call("GetPlayerDataRaw", bandata.steamid, "Banned");
                if (success is string)
                {
                    list = JsonConvert.DeserializeObject<List<BanData>>((string)success);
                }

                var f = list.Where(x => x.ip == bandata.ip).ToList();
                if (f.Count > 0)
                {
                    return string.Format("PlayerDatabase: This ban already exists ({0})", f[0].ToString());
                }
                f.Add(bandata);
                PlayerDatabase.Call("SetPlayerData", bandata.steamid, "Banned", f);
            }
            else
            {
                var f2 = cachedBans.Values.Where(x => x.ip == bandata.ip).Where(x => x.steamid == string.Empty).ToList();
                if(f2.Count > 0)
                {
                    return string.Format("PlayerDatabase: This ban already exists ({0})", f2[0].ToString());
                }
                storedIPData.Banlist.Add(bandata.ToJson());
            }
            if (!cachedBans.ContainsKey(bandata.id))
                cachedBans.Add(bandata.id, bandata);
            return string.Format("PlayerDatabase: Successfully added {0} to the banlist", bandata.ToString());
        }

        string PlayerDatabase_ExecuteUnban(string steamid, string name, string ip, out List<BanData> unbanList)
        {
            int i = 0;
            unbanList = new List<BanData>();
            if(ip != string.Empty)
            {
                unbanList = cachedBans.Values.Where(x => x.ip == ip).ToList();
            }
            else if(steamid != string.Empty)
            {
                unbanList = cachedBans.Values.Where(x => x.steamid == steamid).ToList();
            }
            else
            {
                unbanList = cachedBans.Values.Where(x => x.name == name).ToList();
                if(unbanList.Count == 0)
                {
                    var lname = name.ToLower();
                    unbanList = cachedBans.Values.Where(x => x.name.ToLower().Contains(lname)).ToList();
                    if(unbanList.Count > 1)
                    {
                        var ret = "PlayerDatabase: Multiple Bans Found:\n";
                        foreach(var b in unbanList)
                        {
                            ret += string.Format("{0} - {1} - {2}", b.steamid, b.name, b.reason);
                        }
                        return ret;
                    }
                }
            }
            foreach( var u in unbanList)
            {
                if(u.steamid == string.Empty)
                {
                    var json = u.ToJson();
                    if (storedIPData.Banlist.Contains(json))
                    {
                        i++;
                        storedIPData.Banlist.Remove(json);
                    }
                }
                else
                {
                    i++;
                    PlayerDatabase.Call("SetPlayerData", u.steamid, "Banned", new List<BanData>());
                }
            }
            return string.Format("PlayerDatabase: {0} matching bans were removed", i.ToString());
        }

        bool PlayerDatabase_IsBanned(string steamid, string ip, out BanData bandata)
        {
            bandata = null;
            List<BanData> list = new List<BanData>();
            {
                // check by ID then IP
                var b_steamid = PlayerDatabase.Call("GetPlayerDataRaw", steamid, "Banned");
                if (b_steamid is string)
                {
                    list = JsonConvert.DeserializeObject<List<BanData>>((string)b_steamid);
                }
                foreach(var b in list)
                {
                    if(b.ip == ip)
                    {
                        bandata = b;
                        return true;
                    }
                }
                if(list.Count > 0)
                {
                    bandata = list[0];
                }
            }
            if(bandata == null)
            {
                // check by IP & IP Range
                list = cachedBans.Values.Where(x => IPRange(x.ip, ip)).ToList();
                if(list.Count > 0)
                {
                    bandata = list[0];
                }
            }
            return bandata != null;
        }

        ////////////////////////////////////////////////////////////
        // WebAPI
        ////////////////////////////////////////////////////////////

        string FormatOnlineBansystem(string line, Dictionary<string, string> args)
        {
            foreach (KeyValuePair<string, string> pair in args)
            {
                line = line.Replace(pair.Key, pair.Value);
            }
            return line;
        }

        string ExecuteBan_WebAPI(object source, BanData bandata)
        {
            webrequest.EnqueueGet(FormatOnlineBansystem(WebAPI_Ban, new Dictionary<string, string> { { "{id}", bandata.id.ToString() }, { "{steamid}", bandata.steamid }, { "{name}", bandata.name }, { "{ip}", bandata.ip }, { "{reason}", bandata.reason }, { "{source}", bandata.source }, { "{expiration}", bandata.expire.ToString() }, { "{game}", bandata.game }, { "{platform}", bandata.platform }, { "{server}", bandata.server } }), (code, response) =>
            {
                if (response == null && code == 200)
                {
                    response = "WebAPI: Couldn't contact the WebAPI";
                }
                if (source is IPlayer) ((IPlayer)source).Reply(response);
                else Interface.Oxide.LogInfo(response);
            }, this);

            return string.Empty;
        }

        string WebAPI_ExecuteUnban(object source, string steamid, string name, string ip)
        {
            webrequest.EnqueueGet(FormatOnlineBansystem(WebAPI_Unban, new Dictionary<string, string> { { "{steamid}", steamid }, { "{name}", name }, { "{ip}", ip } }), (code, response) =>
            {
                if (response == null && code == 200)
                {
                    response = "WebAPI: Couldn't contact the WebAPI";
                }
                if (source is IPlayer) ((IPlayer)source).Reply(response);
                else Interface.Oxide.LogInfo(response);
            }, this);

            return string.Empty;
        }

        ////////////////////////////////////////////////////////////
        // SQLite
        ////////////////////////////////////////////////////////////

        Ext.SQLite.Libraries.SQLite Sqlite = Interface.GetMod().GetLibrary<Ext.SQLite.Libraries.SQLite>();
        Connection Sqlite_conn;

        void Load_SQLite()
        {
            try
            {
                Sqlite_conn = Sqlite.OpenDb(SQLite_DB, this);
                if (Sqlite_conn == null)
                {
                    Interface.Oxide.LogError("PlayerDatabase: Couldn't open the SQLite PlayerDatabase. ");
                    return;
                }
                Sqlite.Insert(Core.Database.Sql.Builder.Append("CREATE TABLE IF NOT EXISTS EnhancedBanSystem ( id INTEGER NOT NULL PRIMARY KEY UNIQUE, steamid TEXT, name TEXT, ip TEXT, reason TEXT, source TEXT, game TEXT, platform TEXT, server TEXT, expire INTEGER );"), Sqlite_conn);

                /*if (SQLite_Cache)
                {
                    Sqlite.Query(Core.Database.Sql.Builder.Append("SELECT * from EnhancedBanSystem"), Sqlite_conn, list =>
                    {
                        if (list == null) return;
                        foreach (var entry in list)
                        {
                            if (cachedBans.ContainsKey((int)entry["id"])) continue;
                            var bd = new BanData((int)entry["id"], (string)entry["source"], (string)entry["steamid"], (string)entry["name"], (string)entry["ip"], (string)entry["reason"], (string)entry["expire"]);
                            cachedBans.Add(bd.id, bd);
                        }
                    });
                }*/
            }
            catch (Exception e)
            {
                Interface.Oxide.LogError(e.Message);
            }
        }


        string SQLite_RawBan(BanData bandata)
        {
            try
            {
                Sqlite.Insert(Core.Database.Sql.Builder.Append("INSERT OR REPLACE INTO EnhancedBanSystem ( id, steamid, name, ip, reason, source, game, platform, server, expire ) VALUES ( @0, @1, @2, @3, @4, @5, @6, @7, @8, @9 )", bandata.id, bandata.steamid, bandata.name, bandata.ip, bandata.reason, bandata.source, bandata.game, bandata.platform, bandata.server, (int)bandata.expire), Sqlite_conn);
            }
            catch (Exception e)
            {
                return e.Message;
            }
            return string.Format("SQLite: Successfully added {0} to the banlist", bandata.ToString());
        }

        string SQLite_ExecuteBan(object source, BanData bandata)
        {
            if (bandata.steamid == string.Empty)
            {
                Sqlite.Query(Core.Database.Sql.Builder.Append("SELECT * from EnhancedBanSystem WHERE `ip` == @0 ", bandata.ip), Sqlite_conn, list =>
                {
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            var response = "SQLite: IP is already banned";
                            if (source is IPlayer) ((IPlayer)source).Reply(response);
                            else Interface.Oxide.LogInfo(response);
                            return;
                        }
                    }
                    var reponse2 = SQLite_RawBan(bandata);
                    if (source is IPlayer) ((IPlayer)source).Reply(reponse2);
                    else Interface.Oxide.LogInfo(reponse2);
                });
            }
            else
            {
                Sqlite.Query(Core.Database.Sql.Builder.Append("SELECT * from EnhancedBanSystem WHERE `steamid` == @0 AND `ip` == @1 ", bandata.steamid, bandata.ip), Sqlite_conn, list =>
                {
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            var response = "SQLite: Player is already banned";
                            if (source is IPlayer) ((IPlayer)source).Reply(response);
                            else Interface.Oxide.LogInfo(response);
                            return;
                        }
                    }
                    var reponse2 = SQLite_RawBan(bandata);
                    if (source is IPlayer) ((IPlayer)source).Reply(reponse2);
                    else Interface.Oxide.LogInfo(reponse2);
                });
            }
            return string.Empty;
        }

        string SQLite_ExecuteUnban(object source, string steamid, string name, string ip)
        {
            List<long> unbanList = new List<long>();
            if (ip != string.Empty)
            {
                Sqlite.Query(Core.Database.Sql.Builder.Append("SELECT * from EnhancedBanSystem WHERE `ip` == @0 ", ip), Sqlite_conn, list =>
                {
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            unbanList.Add((long)entry["id"]);
                        }
                    }
                    foreach (var id in unbanList)
                    {
                        Sqlite.Insert(Core.Database.Sql.Builder.Append("DELETE from EnhancedBanSystem WHERE `id` = @0", id), Sqlite_conn);
                    }
                    var returnstring = string.Format("SQLite: {0} bans were removed", unbanList.Count);
                    if (source is IPlayer) ((IPlayer)source).Reply(returnstring);
                    else Interface.Oxide.LogInfo(returnstring);
                });
            }
            else if(steamid != string.Empty)
            {
                Sqlite.Query(Core.Database.Sql.Builder.Append("SELECT * from EnhancedBanSystem WHERE `steamid` == @0", steamid), Sqlite_conn, list =>
                {
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            unbanList.Add((int)entry["id"]);
                        }
                    }
                    foreach (var id in unbanList)
                    {
                        Sqlite.Insert(Core.Database.Sql.Builder.Append("DELETE from EnhancedBanSystem WHERE `id` = @0", id), Sqlite_conn);
                    }
                    var returnstring = string.Format("SQLite: {0} bans were removed", unbanList.Count);
                    if (source is IPlayer) ((IPlayer)source).Reply(returnstring);
                    else Interface.Oxide.LogInfo(returnstring);
                });
            }
            else
            {
                Sqlite.Query(Core.Database.Sql.Builder.Append("SELECT * from EnhancedBanSystem WHERE `name` LIKE @0", "%" + name + "%"), Sqlite_conn, list =>
                {
                    List<Dictionary<string, object>> f = new List<Dictionary<string, object>>();
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            f.Add(entry);
                            unbanList.Add((long)entry["id"]);
                        }
                    }
                    if (unbanList.Count > 1)
                    {
                        string ret = "SQLite: Multiple bans found:";
                        foreach(var e in f)
                        {
                            ret += string.Format("{0} - {1} - {2}\n\r", (string)e["steamid"], (string)e["name"], (string)e["reason"]);
                        }
                        if (source is IPlayer) ((IPlayer)source).Reply(ret);
                        else Interface.Oxide.LogInfo(ret);
                        return;
                    }
                    foreach (var id in unbanList)
                    {
                        Sqlite.Insert(Core.Database.Sql.Builder.Append("DELETE from EnhancedBanSystem WHERE `id` = @0", id), Sqlite_conn);
                    }
                    var returnstring = string.Format("SQLite: {0} bans were removed", unbanList.Count);
                    if (source is IPlayer) ((IPlayer)source).Reply(returnstring);
                    else Interface.Oxide.LogInfo(returnstring);
                });
            }

            return string.Empty;
        }

        void SQLite_IsBanned(string name, string steamid, string ip, bool update)
        {
            Sqlite.Query(Core.Database.Sql.Builder.Append("SELECT * from EnhancedBanSystem WHERE `steamid` == @0", steamid), Sqlite_conn, list =>
            {
            var l = new List<Dictionary<string, object>>();
            if (list != null)
            {
                foreach (var entry in list)
                {
                    if ((string)entry["ip"] == ip)
                    {
                        timer.Once(0.1f, () => Kick(null, steamid, string.Format("You are banned from this server ({0})", (string)entry["reason"])));
                        return;
                    }
                    l.Add(entry);
                }
            }
            if (l.Count == 0)
            {
                Sqlite.Query(Core.Database.Sql.Builder.Append("SELECT * from EnhancedBanSystem WHERE `ip` == @0", ip), Sqlite_conn, list2 =>
                {
                    // Need to find a way to include range ban in here.
                });
            }
            
            });
        }

        ////////////////////////////////////////////////////////////
        // MySQL
        ////////////////////////////////////////////////////////////

        Ext.MySql.Libraries.MySql Sql = Interface.GetMod().GetLibrary<Ext.MySql.Libraries.MySql>();
        Connection Sql_conn;

        void LoadMySQL()
        {
            try
            {
                Sql_conn = Sql.OpenDb(MySQL_Host, MySQL_Port, MySQL_DB, MySQL_User, MySQL_Pass, this);
                if (Sql_conn == null || Sql_conn.Con == null)
                {
                    Interface.Oxide.LogError("Couldn't open the MySQL PlayerDatabase: " + Sql_conn.Con.State.ToString());
                    return;
                }
                Sql.Insert(Core.Database.Sql.Builder.Append("CREATE TABLE IF NOT EXISTS enhancedbansystem ( `id` int(11) NOT NULL, `steamid` VARCHAR(17),`name` VARCHAR(25),`ip` VARCHAR(15),`reason` VARCHAR(25),`source` VARCHAR(25), `game` VARCHAR(25) , `platform` VARCHAR(25), `server` VARCHAR(25), `expire` int(11) );"), Sql_conn);

               /* if (MySQL_Cache)
                {
                    Sql.Query(Core.Database.Sql.Builder.Append("SELECT * from enhancedbansystem"), Sql_conn, list =>
                    {
                        if (list == null) return;
                        foreach (var entry in list)
                        {
                            if (cachedBans.ContainsKey((int)entry["id"])) continue;
                            var bd = new BanData((int)entry["id"], (string)entry["source"], (string)entry["steamid"], (string)entry["name"], (string)entry["ip"], (string)entry["reason"], (string)entry["expire"]);
                            cachedBans.Add(bd.id, bd);
                        }
                    });
                }*/
            }
            catch (Exception e)
            {
                Interface.Oxide.LogError(e.Message);
            }
        }

        string MySQL_RawBan(BanData bandata)
        {
            try
            {
                Sql.Insert(Core.Database.Sql.Builder.Append("INSERT IGNORE INTO playerdatabase ( `id`, `steamid`,`name`,`ip`,`reason`,`source`,`game`,`platform`, `server`, `expire` ) VALUES ( @0, @1, @2, @3, @4, @5, @6, @7, @8, @9 )", bandata.id, bandata.steamid, bandata.name, bandata.ip, bandata.reason, bandata.source, bandata.game, bandata.platform, bandata.server, (int)bandata.expire), Sql_conn);
            }
            catch (Exception e)
            {
                return e.Message;
            }
            return string.Format("MySQL: Successfully added {0} to the banlist", bandata.ToString());
        }

        string MySQL_ExecuteBan(object source, BanData bandata)
        {
            if (bandata.steamid == string.Empty)
            {
                Sql.Query(Core.Database.Sql.Builder.Append("SELECT * from enhancedbansystem WHERE `ip` == @0 ", bandata.ip), Sql_conn, list =>
                {
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            var response = "MySQL: IP is already banned";
                            if (source is IPlayer) ((IPlayer)source).Reply(response);
                            else Interface.Oxide.LogInfo(response);
                            return;
                        }
                    }
                    var reponse2 = MySQL_RawBan(bandata);
                    if (source is IPlayer) ((IPlayer)source).Reply(reponse2);
                    else Interface.Oxide.LogInfo(reponse2);
                });
            }
            else
            {
                Sql.Query(Core.Database.Sql.Builder.Append("SELECT * from enhancedbansystem WHERE `steamid` == @0 AND `ip` == @1 ", bandata.steamid, bandata.ip), Sql_conn, list =>
                {
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            var response = "MySQL: Player is already banned";
                            if (source is IPlayer) ((IPlayer)source).Reply(response);
                            else Interface.Oxide.LogInfo(response);
                            return;
                        }
                    }
                    var reponse2 = MySQL_RawBan(bandata);
                    if (source is IPlayer) ((IPlayer)source).Reply(reponse2);
                    else Interface.Oxide.LogInfo(reponse2);
                });
            }
            return string.Empty;
        }

        string MySQL_ExecuteUnban(object source, string steamid, string name, string ip)
        {
            int i = 0;
            List<int> unbanList = new List<int>();
            if (ip != string.Empty)
            {
                Sql.Query(Core.Database.Sql.Builder.Append("SELECT * from enhancedbansystem WHERE `ip` == @0 ", ip), Sql_conn, list =>
                {
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            unbanList.Add((int)entry["id"]);
                            i++;
                        }

                    }
                });
            }
            else if (steamid != string.Empty)
            {
                Sql.Query(Core.Database.Sql.Builder.Append("SELECT * from enhancedbansystem WHERE `steamid` == @0", steamid), Sql_conn, list =>
                {
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            unbanList.Add((int)entry["id"]);
                            i++;
                        }
                    }
                });
            }
            else
            {
                Sql.Query(Core.Database.Sql.Builder.Append("SELECT * from enhancedbansystem WHERE `name` LIKE @0", "%" + name + "%"), Sql_conn, list =>
                {
                    List<Dictionary<string, object>> f = new List<Dictionary<string, object>>();
                    if (list != null)
                    {
                        foreach (var entry in list)
                        {
                            f.Add(entry);
                            unbanList.Add((int)entry["id"]);
                            i++;
                        }
                    }
                    if (unbanList.Count > 1)
                    {
                        string ret = "MySQL: Multiple bans found:";
                        foreach (var e in f)
                        {
                            ret += string.Format("{0} - {1} - {2}\n\r", (string)e["steamid"], (string)e["name"], (string)e["reason"]);
                        }
                        if (source is IPlayer) ((IPlayer)source).Reply(ret);
                        else Interface.Oxide.LogInfo(ret);
                        return;
                    }
                });
            }
            foreach (var id in unbanList)
            {
                Sql.Insert(Core.Database.Sql.Builder.Append("DELETE from enhancedbansystem WHERE `id` = @0", id), Sql_conn);
            }
            var returnstring = string.Format("MySQL: {0} bans were removed");
            if (source is IPlayer) ((IPlayer)source).Reply(returnstring);
            else Interface.Oxide.LogInfo(returnstring);

            return string.Empty;
        }

        ////////////////////////////////////////////////////////////
        // Native
        ////////////////////////////////////////////////////////////
        string ExecuteBan_Native(BanData bandata)
        {
            if (bandata.steamid == string.Empty) return "Native: IPs can't be banned";

            var iplayer = players.GetPlayer(bandata.steamid);
            if (iplayer == null) return "Native: player couldn't be found";

            if(iplayer.IsBanned) return "Native: player is already banned";

            iplayer.Ban(bandata.reason, TimeSpan.Parse((LogTime() - bandata.expire).ToString()));
            return string.Format("Native: Successfully added {0} to the banlist", bandata.steamid);
        }

        string Native_ExecuteUnban(string steamid, string name)
        {
            if(steamid == string.Empty)
            {
                if (name == string.Empty) return string.Empty;
                var f = players.FindPlayers(name).Where(x => x.IsBanned).ToList();
                if(f.Count == 0)
                {
                    return "Native: No banned players matching this name could be found";
                }
                if(f.Count > 1)
                {
                    var ret = string.Empty;
                    foreach(var p in f)
                    {
                        ret += string.Format("{0} - {1}\n", p.Id, p.Name);
                    }
                    return ret;
                }
                steamid = f[0].Id;
            }
            var b = players.GetPlayer(steamid);
            if(b == null)
            {
                return "Native: Coulnd't find this player";
            }
            if(!b.IsBanned) 
            {
                return "Native: This player isn't banned";
            }
            b.Unban();
            return string.Format("Native: {0} - {1} was successfully unbanned", b.Id, b.Name);
        }

        bool Native_IsBanned(string steamid)
        {
            var b = players.GetPlayer(steamid);
            if (b == null)
            {
                return false;
            }
            return b.IsBanned;
        }


        ////////////////////////////////////////////////////////////
        // Kick
        ////////////////////////////////////////////////////////////

        string Kick(object source, string target, string reason)
        {
            string r = string.Empty;
            var foundplayers = FindConnectedPlayers(target, source, out r);
            if(r != string.Empty)
            {
                return r;
            }
            
            var returnkick = string.Empty;
            foreach (var iplayer in foundplayers)
            {
                returnkick += ExecuteKick(source, iplayer, reason) + "\r\n";
            }

            return returnkick;
        }
        string TryKick(object source, string[] args)
        {
            string target = args[0];
            string reason = args.Length > 1 ? args[1] : "Kicked";
            return Kick(source, target, reason);
        }

        string ExecuteKick(object source, IPlayer player, string reason)
        {
            if(Kick_Broadcast)
                server.Broadcast(string.Format(GetMsg("{0} was kicked from the server ({1})", null), player.Name.ToString(), reason));

            if (Kick_Log)
                Interface.Oxide.LogWarning(string.Format(GetMsg("{0} was kicked from the server ({1})", null), player.Name.ToString(), reason));

            player.Kick(reason);

            return string.Format(GetMsg("{0} was kicked from the server ({1})", source), player.Name.ToString(), reason);
        }


        ////////////////////////////////////////////////////////////
        // IsBanned
        ////////////////////////////////////////////////////////////

        bool isBanned_NonDelayed(string name, string steamid, string ip, bool update, out BanData bandata)
        {
            bool denied = false;
            bandata = null;
            if (!denied && BanSystemHasFlag(banSystem, BanSystem.Native))
            {
                if (Native_IsBanned(steamid))
                {
                    if (Log_Denied)
                        Interface.Oxide.LogInfo(string.Format("Native: {0} - {1} - {2} was rejected from the server", steamid, name, ip));
                    denied = true;
                }
            }
            if (!denied && BanSystemHasFlag(banSystem, BanSystem.PlayerDatabase))
            {
                if(PlayerDatabase_IsBanned(steamid, ip, out bandata))
                {
                    if (Log_Denied)
                        Interface.Oxide.LogInfo(string.Format("PlayerDatabase: {0} - {1} - {2} was rejected from the server", steamid, name, ip));
                    denied = true;
                }
            }
            if (!denied && BanSystemHasFlag(banSystem, BanSystem.Files))
            {
                if (Files_IsBanned(steamid, ip, out bandata))
                {
                    if (Log_Denied)
                        Interface.Oxide.LogInfo(string.Format("Files: {0} - {1} - {2} was rejected from the server", steamid, name, ip));
                    denied = true;
                }
            }
            if(update && denied)
            {
                if(bandata != null && (bandata.ip != ip || bandata.steamid != steamid))
                    ExecuteBan("EBS", steamid, name, ip, BanEvadeReason, bandata.expire == 0.0 ? 0.0 : bandata.expire - LogTime());
                else if(bandata == null)
                    ExecuteBan("EBS", steamid, name, ip, BanEvadeReason, 0.0);
            }
            return bandata != null;
        }

        void isBanned_Delayed(string name, string steamid, string ip, bool update)
        {
            if (BanSystemHasFlag(banSystem, BanSystem.SQLite))
            {
                SQLite_IsBanned(name, steamid, ip, update);
            }
            if (BanSystemHasFlag(banSystem, BanSystem.MySQL))
            {
                MySQL_IsBanned(name, steamid, ip, update);
            }
            if (BanSystemHasFlag(banSystem, BanSystem.WebAPI))
            {
                WebAPI_IsBanned(name, steamid, ip, update);
            }
        }

        bool isBannedMatchedPlayer(string steamid, string ip, out BanData bandata)
        {
            bandata = null;

            var f1 = cachedBans.Values.Where(x => x.steamid == steamid).Where(x => x.ip == ip).ToList();
            if(f1.Count > 0)
            {
                bandata = f1[0];
                return true;
            }

            return false;
        }

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

        string BanIP(object source, string ip, string reason, double duration)
        {
            return ExecuteBan(source, string.Empty, string.Empty, ip, reason, duration);
        }

        string BanPlayer(object source, IPlayer player, string reason, double duration)
        {
            var address = GetPlayerIP(player);

            return ExecuteBan(source, player.Id, player.Name, address, reason, duration);
        }

        string ExecuteBan(object source, string userID, string name, string ip, string reason, double duration)
        {
            string returnstring = string.Empty;

            var bandata = new BanData(source, userID, name, ip, reason, duration);

            if(BanSystemHasFlag(banSystem,BanSystem.PlayerDatabase))
            {
                returnstring += ExecuteBan_PlayerDatabase(bandata);
            }
            if(BanSystemHasFlag(banSystem,BanSystem.Files))
            {
                returnstring += ExecuteBan_Files(bandata);
            }
            if(BanSystemHasFlag(banSystem,BanSystem.MySQL))
            {
                returnstring += MySQL_ExecuteBan(source, bandata);
            }
            if(BanSystemHasFlag(banSystem,BanSystem.SQLite))
            {
                returnstring += SQLite_ExecuteBan(source, bandata);
            }
            if(BanSystemHasFlag(banSystem,BanSystem.WebAPI))
            {
                returnstring += ExecuteBan_WebAPI(source, bandata);
            }
            if (BanSystemHasFlag(banSystem,BanSystem.Native) && duration == 0.0)
            {
                returnstring += ExecuteBan_Native(bandata);
            }
            
            if (Ban_Broadcast && name != string.Empty)
                server.Broadcast(string.Format(GetMsg("{0} was {2} banned from the server ({1})"), name, reason, duration == 0.0 ? GetMsg("permanently") : GetMsg("temporarily")));

            if (Ban_Log)
                Interface.Oxide.LogWarning(string.Format("{0} {1} was {2} banned from the server ({3})", userID != string.Empty ? userID : string.Empty, name != string.Empty ? name : ip, duration == 0.0 ? "permanently" : "temporarily", reason));

            if (Kick_OnBan)
                Kick(source, userID != string.Empty ? userID : ip, "Banned");

            return returnstring == string.Empty ? "FATAL ERROR 1" : returnstring;
        }

        ////////////////////////////////////////////////////////////
        // Unban
        ////////////////////////////////////////////////////////////

        string ExecuteUnban(object source, string steamid, string name, string ip)
        {
            string returnstring = string.Empty;
            List<BanData> unbanList = new List<BanData>();
            List<BanData> unbanList2 = new List<BanData>();

            if (BanSystemHasFlag(banSystem,BanSystem.PlayerDatabase))
            {
                returnstring += PlayerDatabase_ExecuteUnban(steamid, name, ip, out unbanList);
            }
            if (BanSystemHasFlag(banSystem,BanSystem.Files))
            {
                returnstring += Files_ExecuteUnban(steamid, name, ip, out unbanList2);
            }
            if (BanSystemHasFlag(banSystem,BanSystem.MySQL))
            {
                returnstring += MySQL_ExecuteUnban(source, steamid, name, ip);
            }
            if (BanSystemHasFlag(banSystem,BanSystem.SQLite))
            {
                returnstring += SQLite_ExecuteUnban(source, steamid, name, ip);
            }
            if (BanSystemHasFlag(banSystem,BanSystem.WebAPI))
            {
                returnstring += WebAPI_ExecuteUnban(source, steamid, name, ip);
            }
            if (BanSystemHasFlag(banSystem,BanSystem.Native))
            {
                returnstring += Native_ExecuteUnban(steamid, name);
            }

            foreach(var b in unbanList)
            {
                if (cachedBans.ContainsKey(b.id))
                    cachedBans.Remove(b.id);
            }
            foreach(var b in unbanList2)
            {
                if (cachedBans.ContainsKey(b.id))
                    cachedBans.Remove(b.id);
            }

            return returnstring;
        }

        string TryUnBan(object source, string[] args)
        {
            string ipaddress = isIPAddress(args[0]) ? args[0] : string.Empty;
            string steamid = string.Empty;
            ulong userID = 0L;
            string name = string.Empty;
            string errorreason = string.Empty;
            
            if (ipaddress != string.Empty)
            {
                return ExecuteUnban(source, string.Empty, string.Empty, ipaddress);
            }
            else
            {
                ulong.TryParse(args[0], out userID);
                return ExecuteUnban(source, userID != 0L ? args[0] : string.Empty, userID == 0L ? args[0] : string.Empty, string.Empty);
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
                player.Reply(GetMsg("Syntax: ban < Name | SteamID | IP | IP Range > < reason(optional) > < time in secondes(optional > ", player.Id.ToString()));
                return;
            }
            try
            {
                player.Reply(TryBan(player, args));
            }
            catch (Exception e)
            {
                player.Reply("ERROR:" + e.Message);
            }
        }

        [Command("kick", "player.kick")]
        void cmdKick(IPlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermissionKick))
            {
                player.Reply(GetMsg("You don't have the permission to use this command.", player.Id.ToString()));
                return;
            }
            if (args == null || (args.Length < 1))
            {
                player.Reply(GetMsg("Syntax: kick < Name | SteamID | IP | IP Range > < reason(optional) >", player.Id.ToString()));
                return;
            }
            try
            {
                player.Reply(TryKick(player, args));
            }
            catch (Exception e)
            {
                player.Reply("ERROR:" + e.Message);
            }
        }

        [Command("unban", "player.unban")]
        void cmdUnban(IPlayer player, string command, string[] args)
        {
            if (!hasPermission(player, PermissionUnban))
            {
                player.Reply(GetMsg("You don't have the permission to use this command.", player.Id.ToString()));
                return;
            }
            if (args == null || (args.Length < 1))
            {
                player.Reply(GetMsg("Syntax: unban < Name | SteamID | IP | IP Range >", player.Id.ToString()));
                return;
            }
            try
            {
                player.Reply(TryUnBan(player, args));
            }
            catch (Exception e)
            {
                player.Reply("ERROR:" + e.Message);
            }
        }
    }
}
