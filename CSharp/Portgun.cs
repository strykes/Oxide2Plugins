// Reference: RustBuild
using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;


namespace Oxide.Plugins
{
    [Info("Portgun", "Reneb", "2.0.0")]
    class Portgun : RustPlugin
    {
        private FieldInfo serverinput;
        private FieldInfo lastPositionValue;
        private int collLayers;
        
        void Loaded()
        {
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            lastPositionValue = typeof(BasePlayer).GetField("lastPositionValue", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            if (!permission.PermissionExists(permissionPortgun)) permission.RegisterPermission(permissionPortgun, this);
        }
        void OnServerInitialized()
        {
            collLayers = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Deployed", "Tree", "Terrain", "Resource", "World", "Water" });
        }

        private static string permissionPortgun = "canportgun";
        private static int authLevel = 2;
         
        protected override void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<int>("Configure - Admin Level", ref authLevel);
            CheckCfg<string>("Configure - Permission", ref permissionPortgun);
            SaveConfig();
        }

        bool hasAccess( BasePlayer player )
        {
            if (player.net.connection.authLevel >= authLevel) return true;
            return permission.UserHasPermission(player.userID.ToString(), permissionPortgun);
        }

        [ChatCommand("p")]
        void cmdChatPortgun(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player))
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            InputState input = serverinput.GetValue(player) as InputState;
            Quaternion currentRot = Quaternion.Euler(input.current.aimAngles);
            Vector3 eyePos = player.eyes.position;
            RaycastHit hitInfo;
            if(!UnityEngine.Physics.Raycast(eyePos, currentRot * Vector3.forward, out hitInfo, Mathf.Infinity, collLayers))
            {
                SendReply(player, "Couldn't find a destination");
                return;
            }
            player.transform.position = hitInfo.point;
            lastPositionValue.SetValue(player, player.transform.position);
            player.ClientRPCPlayer(null, player, "ForcePositionTo", new object[] { player.transform.position });
        }
    }
}
