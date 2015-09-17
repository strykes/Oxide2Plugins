using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Logging;
using Oxide.Core.Plugins;
using Rust; 

namespace Oxide.Plugins
{
    [Info("PlayerDatabase", "Reneb", "1.0.0")]
    class PlayerDatabase : RustPlugin
    {
        public static DataFileSystem datafile;
         
        string subDirectory = "playerdatabase/";
        Hash<string, Dictionary<string, Dictionary<string,object>>> playersData = new Hash<string, Dictionary<string, Dictionary<string, object>>>();
        List<string> changedPlayersData = new List<string>();

        ////////////////////////////////////////////////////////////
        // Known Players
        ////////////////////////////////////////////////////////////

        StoredData storedData;

        class StoredData
        {
            public HashSet<string> knownPlayers = new HashSet<string>();

            public StoredData() { }
        }

        void OnServerSave() { SaveData(); SavePlayerDatabase(); }

        void SaveData() { Interface.GetMod().DataFileSystem.WriteObject("PlayerDatabase", storedData); }

        void LoadData()
        {
            try { storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("PlayerDatabase"); }
            catch { storedData = new StoredData(); }
        }

        bool isKnownPlayer(BasePlayer player) { return isKnownPlayer(player.userID.ToString()); }
        bool isKnownPlayer(string userid) { return storedData.knownPlayers.Contains(userid); }

        void Loaded()
        {
            datafile = Interface.GetMod().DataFileSystem;
            LoadData();
            foreach(string userid in storedData.knownPlayers)
            {
                LoadPlayer(userid);
            }
        }

        void SavePlayerDatabase()
        {
            foreach(string userid in changedPlayersData)
            {
                string path = subDirectory + userid;
                datafile.WriteObject<Dictionary<string, Dictionary<string,object>>>(path, playersData[userid]);
            }
            changedPlayersData.Clear();
        }
        void Unload()
        {
            SavePlayerDatabase();
            SaveData();
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (!isKnownPlayer(player)) { LoadPlayer(player); }
            if (!playersData[player.userID.ToString()].ContainsKey("default"))
                playersData[player.userID.ToString()].Add("default", new Dictionary<string,object>());
            if (playersData[player.userID.ToString()]["default"].ContainsKey("name"))
                playersData[player.userID.ToString()]["default"]["name"] = player.displayName;
            else
                playersData[player.userID.ToString()]["default"].Add("name", player.displayName);
        }

        HashSet<string> GetAllKnownPlayers()
        {
            return storedData.knownPlayers;
        }

        object FindPlayer(string arg)
        { 
            ulong steamid = 0L;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            if(steamid != 0L && arg.Length == 17)
            {
                if (playersData[arg] == null) return "No players found with this steamid";
                else return arg;
            }

            string foundSteamID = string.Empty;
            string returnString = string.Empty;
            foreach(KeyValuePair<string, Dictionary<string, Dictionary<string, object>>> pair in playersData)
            { 
                if (pair.Value.ContainsKey("default"))
                    if(pair.Value["default"].ContainsKey("name"))
                    if (pair.Value["default"]["name"].ToString().ToLower() == lowerarg)
                        if (foundSteamID == string.Empty)
                        {
                            foundSteamID = pair.Key;
                        }
                        else
                        {
                            returnString = "Multiple players found: ";
                            foundSteamID += " " + pair.Key;
                        }

            }

            return returnString + foundSteamID;
        }
         
        void LoadPlayer(BasePlayer player) { LoadPlayer(player.userID.ToString()); }
        void LoadPlayer(string userid)
        {
            if (!storedData.knownPlayers.Contains(userid))
                storedData.knownPlayers.Add(userid);
            string path = subDirectory + userid;

            if (datafile.ExistsDatafile(path)) { }

            object profile;
            try 
            {
                profile = datafile.ReadObject<Dictionary<string, Dictionary<string, object>>>(path);
            }
            catch (Exception exception) {
                profile = new Dictionary<string, object>();
                datafile.WriteObject<Dictionary<string, Dictionary<string, object>>>(path, new Dictionary<string, Dictionary<string, object>>());
            }

            playersData[userid] = profile as Dictionary<string, Dictionary<string, object>>;
        }
          
        void SetPlayerData(string userid, string key, Dictionary<string, object> data)
        { 
            if (!playersData.ContainsKey(userid)) LoadPlayer(userid);
            Dictionary<string, Dictionary<string, object>> profile = playersData[userid];
              
            if (!profile.ContainsKey(key)) profile.Add(key, data);
            else profile[key] = data;
            playersData[userid] = profile;

            if (!changedPlayersData.Contains(userid))
                changedPlayersData.Add(userid);
        } 
        object GetPlayerData(string userid, string key)
        {
            if (!playersData.ContainsKey(userid)) return null;
            Dictionary<string, Dictionary<string,object>> profile =  playersData[userid];
            if (!profile.ContainsKey(key))
            {
                return null;
            }
            else
            {
                return profile[key];
            }
        }
    }
}
