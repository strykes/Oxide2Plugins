// Reference: Newtonsoft.Json
// Reference: Oxide.Ext.MySql
// Reference: Oxide.Ext.SQLite

using System.Linq;
using Oxide.Ext.MySql;
using Oxide.Ext.SQLite;
using Oxide.Ext.SQLite.Libraries;
using System.Data.SQLite;
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Libraries;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("PlayerDatabase", "Reneb", "1.2.0")]
    class PlayerDatabase : RustLegacyPlugin
    {
        private Core.Configuration.DynamicConfigFile Data;

        Connection connection;
        SQLiteConnection sqconnection;

        Oxide.Ext.SQLite.Sql sqlite;
        Oxide.Ext.MySql.Sql sql;

        static string steamapikey = "";

        static bool useSQLITE = true;

        static bool useMysql = false;
        static int mysqlPort = 3306;
        static string mysqlHost = "localhost";
        static string mysqlUsername = "username";
        static string mysqlDatabase = "databasename";
        static string mysqlPass = "password";
        static string mysqlTable = "playerdatabase";


        private List<string> AvaibleUserID = new List<string>();
        private List<string> AvaibleKeys = new List<string>();

        void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        } 

        void Init()
        {
            CheckCfg<string>("Settings: SteamAPI Key http://steamcommunity.com/dev/apikey", ref steamapikey);

            CheckCfg<bool>("SQLite: activated (can't use with Mysql)", ref useSQLITE);

            CheckCfg<bool>("Mysql: activated (can't use with SQLite)", ref useMysql);
            CheckCfg<int>("Mysql: port", ref mysqlPort);
            CheckCfg<string>("Mysql: host", ref mysqlHost);
            CheckCfg<string>("Mysql: username", ref mysqlUsername);
            CheckCfg<string>("Mysql: database", ref mysqlDatabase);
            CheckCfg<string>("Mysql: password", ref mysqlPass);
            CheckCfg<string>("Mysql: table", ref mysqlTable);
            SaveConfig();
        } 


        void Loaded() {
            if (!permission.PermissionExists("admin")) permission.RegisterPermission("admin", this);
            LoadData();
            if(useSQLITE)
            {
                sqconnection = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").OpenDb("PlayerDatabaseSQL");
                sqlite = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").NewSql();
                sqlite.Append("create table if not exists playerdatabase (name text, steamid text)");
                Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Query(sqlite, sqconnection);
                sqlite.Append("PRAGMA encoding = 'UTF - 8'"); 
                Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Query(sqlite, sqconnection);
                sqlite.Append("SELECT steamid FROM playerdatabase");
                
                IEnumerable<Dictionary<string, object>> res = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Query(sqlite, sqconnection);
                
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (object key in obj.Values)
                    {
                        AvaibleUserID.Add(key.ToString());
                    }
                }
                sqlite.Append("SELECT * FROM playerdatabase");
                res = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Query(sqlite, sqconnection);
                
                foreach (Dictionary<string, object> obj in res)
                {
                    Debug.Log(obj.ToString());
                    foreach (string key in obj.Keys)
                    {
                        
                        AvaibleKeys.Add(key.ToString());
                    }
                    break;
                }
                if (AvaibleKeys.Count == 0)
                {
                    AvaibleKeys.Add("name");
                    AvaibleKeys.Add("steamid");
                }
            }
            else if(useMysql)
            {
                connection = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").OpenDb(mysqlHost, mysqlPort, mysqlDatabase, mysqlUsername, mysqlPass);
                sql = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").NewSql();
                sql.Append("SELECT steamid FROM `"+ mysqlTable + "`");
                IEnumerable<Dictionary<string, object>> res = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").Query(sql, connection);
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (object key in obj.Values)
                    {
                        AvaibleUserID.Add(key.ToString());
                    }
                }
                sql = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").NewSql();
                sql.Append("SELECT * FROM `" + mysqlTable + "`");
                res = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").Query(sql, connection);
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (string key in obj.Keys)
                    {
                        AvaibleKeys.Add(key.ToString());
                    }
                    break;
                }
                if(AvaibleKeys.Count == 0)
                {
                    AvaibleKeys.Add("name");
                    AvaibleKeys.Add("steamid");
                }
            }
        } 
        object CheckResponse(int code, string response)
        {
            if (code != 200) return null;
            var des = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<Dictionary<string, object>>>>>(response);
            if (des["response"] == null) return null;
            if (des["response"]["players"].Count == 0) return null;
            return des["response"]["players"][0]["personaname"].ToString();
        } 
        void LoadData() { Data = Interface.GetMod().DataFileSystem.GetDatafile("PlayerDatabase"); }
        void SaveData() { Interface.GetMod().DataFileSystem.SaveDatafile("PlayerDatabase"); }
        void OnServerSave() { SaveData(); }
        void Unload() { SaveData(); }
        void OnPlayerConnected(NetUser netuser) { RegisterPlayer(netuser); }
        void RegisterPlayer(NetUser netuser) { SetPlayerData(netuser.playerClient.userID.ToString(), "name", netuser.playerClient.userName); }

        object GetPlayerData(string userid, string key)
        {
            if (key == "name") return GetName(userid);
            if (useSQLITE)
            {
                string query = string.Format("SELECT {2} FROM {0} WHERE steamid = {1} LIMIT 1", "playerdatabase", userid, key);
                sqlite.Append(query);
                IEnumerable<Dictionary<string, object>> res = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Query(sqlite, sqconnection);
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (object value in obj.Values)
                    {
                        return value.ToString();
                    }
                }
            }
            else if (useMysql)
            {
                string query = string.Format("SELECT {2} FROM `{0}` WHERE `steamid` = '{1}' LIMIT 1", mysqlTable, userid, key);
                sql = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").NewSql();
                sql.Append(query);
                IEnumerable<Dictionary<string, object>> res = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").Query(sql, connection);
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (object value in obj.Values)
                    {
                        return value.ToString();
                    }
                }
            }
            else
            { 
                if (Data[userid] == null) return null;
                if (!((Dictionary<string, object>)Data[userid]).ContainsKey(key)) return null;
                return ((Dictionary<string, object>)Data[userid])[key];
            }
            return null;
        }
        object GetName(string userid)
        {
            if (useSQLITE)
            {
                string query = string.Format("SELECT name FROM {0} WHERE steamid = {1} LIMIT 1", "playerdatabase", userid);
                sqlite.Append(query);
                IEnumerable<Dictionary<string, object>> res = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Query(sqlite, sqconnection);
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (object value in obj.Values)
                    {
                        if (value != null || value != "")
                            return value.ToString();
                    }
                }
            }
            else if (useMysql)
            {
                string query = string.Format("SELECT name FROM `{0}` WHERE `steamid` = '{1}' LIMIT 1", mysqlTable, userid);
                sql = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").NewSql();
                sql.Append(query);
                IEnumerable<Dictionary<string, object>> res = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").Query(sql, connection);
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (object value in obj.Values)
                    {
                        if (value != null || value != "")
                            return value.ToString();
                    }
                }
            }
            else
            {
                if (Data[userid] != null && ((Dictionary<string, object>)Data[userid])["name"] != null)
                    return ((Dictionary<string, object>)Data[userid])["name"];
            }
            if (steamapikey.Length < 5) return null;
            var url = string.Format("https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}", steamapikey, userid);
            Interface.GetMod().GetLibrary<WebRequests>("WebRequests").EnqueueGet(url, (code, response) =>
            {
                var tempnewname = CheckResponse(code, response);
                if (tempnewname != null)
                {
                        SetPlayerData(userid, "name", tempnewname.ToString());
                }
            }
            , this);
            return null;
        }

        void SetPlayerData(string userid, string key, object value)
        {
            if(useSQLITE)
            {
                string query = string.Empty;
                if (!AvaibleKeys.Contains(key))
                {
                    query = string.Format("ALTER TABLE {0} ADD {1} VARCHAR(255);", "playerdatabase", key);
                    sqlite.Append(query);
                    Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Query(sqlite, sqconnection);
                    AvaibleKeys.Add(key);
                }
                
                if (!AvaibleUserID.Contains(userid))
                {
                    query = string.Format("INSERT INTO {0} (steamid) VALUES ('{1}');", "playerdatabase", userid);
                    sqlite.Append(query);
                    var test2 = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Update(sqlite, sqconnection);
                    Debug.Log(test2.ToString());
                    AvaibleUserID.Add(userid);
                }
                
                query = string.Format("UPDATE {0} SET {2} = '{3}' WHERE steamid = '{1}';", "playerdatabase", userid, key, value.ToString());
                sqlite.Append(query);
                var test = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Update(sqlite, sqconnection);
                Debug.Log(test.ToString());
            }
            else if (useMysql)
            {
                string query = string.Empty;
                if (!AvaibleKeys.Contains(key))
                {
                    query = string.Format("ALTER TABLE `{0}` ADD `{1}` VARCHAR(255) NOT NULL;", mysqlTable, key);
                    sql = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").NewSql();
                    sql.Append(query);
                    Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").Query(sql, connection);
                    AvaibleKeys.Add(key);
                }

                if (!AvaibleUserID.Contains(userid))
                {
                    query = string.Format("INSERT INTO `{0}` (`steamid`) VALUES('{1}');", mysqlTable, userid);
                    sql = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").NewSql();
                    sql.Append(query);
                    Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").Query(sql, connection);
                    AvaibleUserID.Add(userid);
                }

                query = string.Format("UPDATE `{0}` SET `{2}` = '{3}' WHERE `{0}`.`steamid` = {1};", mysqlTable, userid, key, value.ToString());
                sql = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").NewSql();
                sql.Append(query);
                
                Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").Query(sql, connection);
                
            }
            else
            {
                if (Data[userid] == null) Data[userid] = new Dictionary<string, object>();
                if (!((Dictionary<string, object>)Data[userid]).ContainsKey(key))
                    ((Dictionary<string, object>)Data[userid]).Add(key, value);
                else
                    ((Dictionary<string, object>)Data[userid])[key] = value;
            }
        }
        string[] FindAllPlayers(string name)
        {
            var returnlist = new List<string>();
            if(useSQLITE)
            {
                string query = string.Format("SELECT steamid FROM `{0}` WHERE `name` LIKE '%{1}%'", "playerdatabase", name);
                sqlite.Append(query);
                IEnumerable<Dictionary<string, object>> res = Interface.Oxide.GetLibrary<Ext.SQLite.Libraries.SQLite>("SQLite").Query(sqlite, sqconnection);
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (object value in obj.Values)
                    {
                        returnlist.Add(value.ToString());
                    }
                }
            }
            else if(useMysql)
            {
                string query = string.Format("SELECT steamid FROM `{0}` WHERE `name` LIKE '%{1}%'", mysqlTable, name);
                sql = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").NewSql();
                sql.Append(query);
                IEnumerable<Dictionary<string, object>> res = Interface.Oxide.GetLibrary<Ext.MySql.Libraries.MySql>("MySql").Query(sql, connection);
                foreach (Dictionary<string, object> obj in res)
                {
                    foreach (object value in obj.Values)
                    {
                        returnlist.Add(value.ToString());
                    }
                }
            }
            else
            {
                name = name.ToLower();
                Dictionary<string, object> currenttable;
                foreach (KeyValuePair<string, object> pair in Data)
                {
                    currenttable = pair.Value as Dictionary<string, object>;
                    if (currenttable.ContainsKey("name"))
                    {
                        if (currenttable["name"].ToString().ToLower().Contains(name))
                        {
                            returnlist.Add(pair.Key);
                        }
                    }
                }
            }
            return returnlist.ToArray();
        }
        bool hasAccess(NetUser netuser)
        {
            if (netuser.CanAdmin())
                return true;
            return permission.UserHasPermission(netuser.playerClient.userID.ToString(), "admin");
        }
        [ChatCommand("findname")]
        void cmdChatFindname(NetUser player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
            if (args.Length == 0) { SendReply(player, "/findname STEAMID"); return; }
            var name = GetPlayerData(args[0], "name");
            if (name == null) { SendReply(player, "Couldn't find this player name"); return; }
            SendReply(player, string.Format("{0} - {1}", args[0], name.ToString()));
        }
    }
}
