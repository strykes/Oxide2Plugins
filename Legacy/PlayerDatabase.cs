// Reference: Oxide.Ext.RustLegacy
// Reference: Facepunch.ID
// Reference: Facepunch.MeshBatch
// Reference: Google.ProtocolBuffers
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using RustProto;

namespace Oxide.Plugins
{
    [Info("PlayerDatabase", "Reneb", "1.0.2")]
    class PlayerDatabase : RustLegacyPlugin
    {
        private Core.Configuration.DynamicConfigFile Data;


        void Loaded() { if(!permission.PermissionExists("admin")) permission.RegisterPermission("admin", this); LoadData(); }
        void LoadData() { Data = Interface.GetMod().DataFileSystem.GetDatafile("PlayerDatabase"); }
        void SaveData() { Interface.GetMod().DataFileSystem.SaveDatafile("PlayerDatabase"); }
        void OnServerSave() { SaveData(); }
        void Unload() { SaveData(); }
        void OnPlayerConnected(NetUser netuser) { RegisterPlayer(netuser); }
        void RegisterPlayer(NetUser netuser) { SetPlayerData(netuser.playerClient.userID.ToString(), "name", netuser.playerClient.userName); }

        object GetPlayerData(string userid, string key)
        {
            if (Data[userid] == null) return null;
            if (!((Dictionary<string, object>)Data[userid]).ContainsKey(key)) return null;
            return ((Dictionary<string, object>)Data[userid])[key];
        }

        void SetPlayerData(string userid, string key, object value)
        {
            if (Data[userid] == null) Data[userid] = new Dictionary<string, object>();
            if (!((Dictionary<string, object>)Data[userid]).ContainsKey(key))
                ((Dictionary<string, object>)Data[userid]).Add(key, value);
            else
                ((Dictionary<string, object>)Data[userid])[key] = value;
        }
        string[] FindAllPlayers(string name)
        {
            var returnlist = new List<string>();
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
