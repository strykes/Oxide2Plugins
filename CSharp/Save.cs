// Reference: Newtonsoft.Json
// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System.Reflection;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Save", "Reneb", 2.0)]
    class Save : RustPlugin
    {
        private int saveAuth;
        private bool Changed;
        private string noAccess;
        private string saved;

        void Loaded()
        {
            LoadVariables();   
        }
        private object GetConfig(string menu, string datavalue, object defaultValue)
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
        private void LoadVariables()
        {
            saveAuth = Convert.ToInt32(GetConfig("Settings", "authLevel", 1));
            noAccess = Convert.ToString(GetConfig("Messages", "noAccess", "You are not allowed to use this command"));
            saved = Convert.ToString(GetConfig("Messages", "saved", "World & Players saved"));
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }
        void LoadDefaultConfig()
        {
            Puts("Save: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }
        [ConsoleCommand("save.all")]
        void cmdConsoleSave(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < saveAuth)
                {
                    SendReply(arg, noAccess);
                    return;
                }
            }
            SaveRestore.Save();
            SendReply(arg, saved);
        }
    }
}
