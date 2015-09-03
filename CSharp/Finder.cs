using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Logging;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Finder", "Reneb", "2.0.0")]
    class Finder : RustPlugin
    {
        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Plugin References
        //////////////////////////////////////////////////////////////////////////////////////////
        [PluginReference]
            Plugin DeadPlayersList;
        //////////////////////////////////////////////////////////////////////////////////////////
        ///// cached Fields
        //////////////////////////////////////////////////////////////////////////////////////////

        Dictionary<BasePlayer, Dictionary<string, Dictionary<string, object>>> cachedFind = new Dictionary<BasePlayer, Dictionary<string, Dictionary<string, object>>>();

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Fields
        //////////////////////////////////////////////////////////////////////////////////////////
        FieldInfo lastPositionValue;

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Configuration
        //////////////////////////////////////////////////////////////////////////////////////////

        static string noAccess = "You are not allowed to use this command";

        private bool Changed;

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<string>("Messages: noAccess", ref noAccess);

            SaveConfig();

        }

        void LoadDefaultConfig() { }

        object FindPlayer(string arg)
        {
            var findplayers = FindPlayers(arg);
            if (findplayers.Count == 0)
            {
                return "No players found";
            }
            if (findplayers.Count == 1)
            {
                foreach (KeyValuePair<string, Dictionary<string, object>> pair in findplayers)
                {
                    return pair.Value;
                }
            }
            return findplayers;
        }

        Dictionary<string,Dictionary<string, object>> FindPlayers( string arg )
        {
            var listPlayers = new Dictionary<string,Dictionary<string, object>>();

            ulong steamid = 0L;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach(BasePlayer player in Resources.FindObjectsOfTypeAll<BasePlayer>())
            {
                if(steamid != 0L)
                    if(player.userID == steamid)
                    {
                        listPlayers.Clear();
                        listPlayers.Add(steamid.ToString(), GetFinderDataFromPlayer(player));
                        return listPlayers;
                    }
                string lowername = player.displayName.ToLower();
                if (lowername.Contains(lowerarg))
                {
                    listPlayers.Add(player.userID.ToString(), GetFinderDataFromPlayer(player));
                }
            }

            if(DeadPlayersList != null)
            {
                var deadplayers = DeadPlayersList.Call("GetPlayerList") as Dictionary<string, string>;
                foreach(KeyValuePair<string, string> pair in deadplayers)
                {
                    if (steamid != 0L)
                        if (pair.Key == arg)
                        {
                            listPlayers.Clear();
                            listPlayers.Add(pair.Key.ToString(), GetFinderDataFromDeadPlayers(pair.Key));
                            return listPlayers;
                        }
                    string lowername = pair.Value.ToLower();
                    if (lowername.Contains(lowerarg))
                    {
                        listPlayers.Add(pair.Key.ToString(), GetFinderDataFromDeadPlayers(pair.Key));
                    }
                }
            }

            return listPlayers;
        }
        void Loaded()
        {
            lastPositionValue = typeof(BasePlayer).GetField("lastPositionValue", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }
        Dictionary<string, object> GetFinderDataFromDeadPlayers(string userid)
        {
            var playerData = new Dictionary<string, object>();

            playerData.Add("userid", userid);
            playerData.Add("name", (string)DeadPlayersList.Call("GetPlayerName", userid) );
            playerData.Add("pos", (Vector3)DeadPlayersList.Call("GetPlayerDeathPosition", userid) );
            playerData.Add("state", "Dead");
            playerData.Add("status", "Disconnected");

            return playerData;
        }
        Dictionary<string, object> GetFinderDataFromPlayer( BasePlayer player )
        {
            var playerData = new Dictionary<string, object>();

            playerData.Add("userid", player.userID.ToString());
            playerData.Add("name", player.displayName);
            playerData.Add("pos", player.transform.position);
            playerData.Add("state", player.IsDead() ? "Dead" : player.IsSleeping() ? "Sleeping" : player.IsSpectating() ? "Spectating" : "Alive" );
            playerData.Add("status", player.IsConnected() ? "Connected" : "Disconnected" );

            return playerData;
        }
        void ResetFind(BasePlayer player)
        {
            if (cachedFind.ContainsKey(player))
                cachedFind.Remove(player);
            cachedFind.Add(player, new Dictionary<string, Dictionary<string, object>>());
        }
        void AddFind(BasePlayer player, int count, Dictionary<string,object> data)
        {
            cachedFind[player].Add(count.ToString(), data);
        }
        object GetFind(BasePlayer player, string count)
        {
            if (!cachedFind.ContainsKey(player)) return "You didn't search for something yet";
            if (cachedFind[player].Count == 0) return "You didn't find anything";
            if (!cachedFind[player].ContainsKey(count)) return "This FindID is not valid";
            return cachedFind[player][count];
        }
        void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.transform.position = destination;
            lastPositionValue.SetValue(player, player.transform.position);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { destination });
        }
        Dictionary<string,object> GetBuildingPrivilegeData(BuildingPrivlidge bp)
        {
            var bpdata = new Dictionary<string, object>();

            bpdata.Add("pos", bp.transform.position);

            return bpdata;
        }
        List<Dictionary<string,object>> FindPrivileges( string userid )
        {
            var privileges = new List<Dictionary<string, object>>();
            ulong ulongid = ulong.Parse(userid);
            foreach (BuildingPrivlidge bp in Resources.FindObjectsOfTypeAll<BuildingPrivlidge>())
            {
                foreach(ProtoBuf.PlayerNameID pni in bp.authorizedPlayers)
                {
                    if(pni.userid == ulongid)
                    {
                        privileges.Add(GetBuildingPrivilegeData(bp));
                    }
                }
            }

            return privileges;
        }
        [ChatCommand("find")]
        void cmdChatFind(BasePlayer player, string command, string[] args)
        {
            if(args.Length < 2 || args == null)
            {
                SendReply(player, "/find player NAME/SteamID");
                SendReply(player, "/find tp FINDID");
                return;
            }
            int count = 0;
            switch (args[0].ToLower())
            {
                case "players":
                case "player":
                    ResetFind(player);

                    Dictionary<string,Dictionary<string, object>> success = FindPlayers(args[1]);
                    if(success.Count == 0)
                    {
                        SendReply(player, string.Format("No players found with: {0}", args[1]));
                        return;
                    }

                    
                    foreach(KeyValuePair<string, Dictionary<string,object>> pair in success)
                    {
                        AddFind(player, count, pair.Value);
                        SendReply(player, string.Format("{0} - {1} - {2} - {3} - {4}", count.ToString(), pair.Key, (string)pair.Value["name"], (string)pair.Value["state"], (string)pair.Value["status"]));
                        count++;
                    }
                break;
                case "privilege":
                case "cupboard":
                case "toolcupboard":
                    object successs = FindPlayer(args[1]);
                    if (successs is string)
                    {
                        SendReply(player, (string)successs);
                        return;
                    }
                    if(successs is Dictionary<string, Dictionary<string,object>>)
                    {
                        SendReply(player, "Multiple players found, use the SteamID or use a fuller name");
                        foreach (KeyValuePair<string, Dictionary<string, object>> pair in (Dictionary<string, Dictionary<string, object>>)successs)
                        {
                            SendReply(player, string.Format("{0} - {1} - {2} - {3}", pair.Key, (string)pair.Value["name"], (string)pair.Value["state"], (string)pair.Value["status"]));
                        }
                        return;
                    }
                    ResetFind(player);
                    List<Dictionary<string, object>> privileges = FindPrivileges((string)((Dictionary<string, object>)successs)["userid"]);
                    if(privileges.Count == 0)
                    {
                        SendReply(player, "No tool cupboard privileges found for this player");
                        return;
                    }
                    foreach(Dictionary<string,object> priv in privileges)
                    {
                        AddFind(player, count, priv);
                        SendReply(player, string.Format("{0} - {1}", count.ToString(), priv["pos"].ToString()));
                        count++;
                    }
                    break;
                case "tp":
                    object finddatar = GetFind(player, args[1]);
                    if(finddatar is string)
                    {
                        SendReply(player, (string)finddatar);
                        return;
                    }
                    var findData = finddatar as Dictionary<string, object>;
                    if(!findData.ContainsKey("pos"))
                    {
                        SendReply(player, "Couldn't find the position for this data");
                        return;
                    }
                    ForcePlayerPosition(player, (Vector3)findData["pos"]);
                    foreach(KeyValuePair<string,object> pair in findData)
                    {
                        SendReply(player, string.Format("{0} - {1}", pair.Key, pair.Value.ToString()));
                    }
                break;
            }
        }
    }
}
