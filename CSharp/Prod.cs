// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Prod", "Reneb", 2.1, ResourceId = 928)]
    class Prod : RustPlugin
    {

        private int prodAuth;
        private string helpProd;
        private string noAccess;
        private string noTargetfound;
        private string noCupboardPlayers;
        private string Toolcupboard;
        private string noBlockOwnerfound;
        private string noCodeAccess;
        private string codeLockList;
        private string boxNeedsCode;

        private FieldInfo serverinput;
        private object deadplayers;
        private Oxide.Core.Libraries.Plugins pluginsfullib;
        private FieldInfo pluginslib;
        private FieldInfo codelockwhitelist;
        private Vector3 eyesAdjust;
        private bool Changed;
        Core.Configuration.DynamicConfigFile deadPlayersData;

        void Loaded()
        {
            LoadVariables();
            eyesAdjust = new Vector3(0f, 1.5f, 0f);
            pluginslib = typeof(OxideMod).GetField("libplugins", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            codelockwhitelist = typeof(CodeLock).GetField("whitelistPlayers", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }
        void OnServerInitialized()
        {
            pluginsfullib = (Oxide.Core.Libraries.Plugins)pluginslib.GetValue(Interface.GetMod());
            object findplugin = pluginsfullib.Find("deadPlayerList");
            if (findplugin == null)
                return;
            deadplayers = (Oxide.Core.Plugins.Plugin)findplugin;
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
            prodAuth = Convert.ToInt32(GetConfig("Prod", "authLevel", 1));
            helpProd = Convert.ToString(GetConfig("Messages", "helpProd", "/prod on a building or tool cupboard to know who owns it."));
            noAccess = Convert.ToString(GetConfig("Messages", "noAccess", "You don't have access to this command"));
            noTargetfound = Convert.ToString(GetConfig("Messages", "noTargetfound", "You must look at a tool cupboard or building"));
            noCupboardPlayers = Convert.ToString(GetConfig("Messages", "noCupboardPlayers", "No players has access to this cupboard"));
            Toolcupboard = Convert.ToString(GetConfig("Messages", "Toolcupboard", "Tool Cupboard"));
            noBlockOwnerfound = Convert.ToString(GetConfig("Messages", "noBlockOwnerfound", "No owner found for this building block"));
            noCodeAccess = Convert.ToString(GetConfig("Messages", "noCodeAccess", "No players has access to this Lock"));
            codeLockList = Convert.ToString(GetConfig("Messages", "codeLockList", "CodeLock whitelist:"));
            boxNeedsCode = Convert.ToString(GetConfig("Messages", "boxNeedsCode", "Can't find owners of a box without a Code Lock"));
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }
        void LoadDefaultConfig()
        {
            Puts("Prod: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }
        private bool hasAccess(BasePlayer player)
       {
           if(player.net.connection.authLevel < prodAuth)
               return false;
           return true;

       }
        [ChatCommand("prod")]
        void cmdChatProd(BasePlayer player, string command, string[] args)
        {
            if(!(hasAccess(player)))
            {
                SendReply(player, noAccess);
                return;
            }
            var input = serverinput.GetValue(player) as InputState;
            var currentRot = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
            var target = DoRay(player.transform.position + eyesAdjust, currentRot);
            if (target is bool)
            {
                SendReply(player, noTargetfound);
                return;
            }
            if (target as BuildingBlock)
            {
                GetBuildingblockOwner(player,(BuildingBlock)target);
            }
            else if (target as BuildingPrivlidge)
            {
                GetToolCupboardUsers(player, (BuildingPrivlidge)target);
            }
            else if (target as DeployedItem)
            {
                GetDeployedItemOwner(player, (DeployedItem)target);
            }
            else
            {
                GetStorageBoxCode(player, (StorageContainer)target);
            }
        }
        private void GetStorageBoxCode(BasePlayer player, StorageContainer box)
        {
            if (box.HasSlot(BaseEntity.Slot.Lock))
            {
                BaseLock thelock = box.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
                if (thelock as CodeLock)
                {
                    List<ulong> whitelisted = codelockwhitelist.GetValue(thelock as CodeLock) as List<ulong>;
                    SendReply(player, codeLockList);
                    if (whitelisted.Count == 0)
                    {
                        SendReply(player, noCodeAccess);
                        return;
                    }
                    foreach (ulong userid in whitelisted)
                    {
                        SendBasePlayerFind(player, userid);
                    }
                    return;
                }
            }
            SendReply(player, boxNeedsCode);
        }
        private void GetDeployedItemOwner(BasePlayer player, DeployedItem ditem)
        {
            if (ditem.item != null && ditem.item.info != null)
                SendReply(player, string.Format("{0} deployer: {1} - {2}", ditem.item.info.displayname.ToString(), ditem.deployerUserName.ToString(), ditem.deployerUserID.ToString()));
            else
                SendReply(player, string.Format("Item Deployer: {0} - {1}", ditem.deployerUserName.ToString(), ditem.deployerUserID.ToString()));
        }
        private object FindOwnerBlock(BuildingBlock block)
        {
            object returnhook = Interface.GetMod().CallHook("FindBlockData", new object[] { block });
            if (returnhook != null)
            {
                if (!(returnhook is bool))
                {
                    ulong ownerid = Convert.ToUInt64(returnhook);
                        return ownerid;
                }
            }
            return false;
        }
        private void SendBasePlayerFind(BasePlayer player, ulong ownerid)
        {
            BasePlayer targetplayer = BasePlayer.FindByID(ownerid);
            if (targetplayer != null)
            {
                SendReply(player, string.Format("{0} - {1} - Online", targetplayer.displayName.ToString(), ownerid.ToString()));
                return;
            }
            targetplayer = BasePlayer.FindSleeping(ownerid);
            if (targetplayer != null)
            {
                SendReply(player, string.Format("{0} - {1} - Sleeping", targetplayer.displayName.ToString(), ownerid.ToString()));
                return;
            }
            if (deadplayers is Oxide.Core.Plugins.Plugin)
            {
                /*object deadplayer = ((Oxide.Core.Plugins.Plugin)deadplayers).CallHook("FindDeadPlayerByID", new object[1] { ownerid.ToString() });

                if (!(deadplayer is bool))
                {
                    if (deadplayer != null)
                    {
                        Puts(deadplayer.ToString());
                        Dictionary<string, object> data = deadplayer as Dictionary<string, object>;
                        foreach (KeyValuePair<string, object> pair in data)
                        {
                            Puts(pair.Key.ToString());
                            //if (pair.Key.ToString() == "displayName")
                            //{
                              //  SendReply(player, string.Format("{0} - {1} - Dead", pair.Value.ToString(), ownerid.ToString()));
                            //}
                        }
                    }
                }*/
                deadPlayersData = Interface.GetMod().DataFileSystem.GetDatafile("deadPlayerList");
                if (deadPlayersData[ownerid.ToString()] != null)
                {
                    Dictionary<string, object> data = deadPlayersData[ownerid.ToString()] as Dictionary<string, object>;
                    SendReply(player, string.Format("{0} - {1} - Dead", data["displayName"].ToString(), ownerid.ToString()));
                    return;
                }
            }
            SendReply(player, string.Format("{0} - {1} - Dead", "Unknown Player", ownerid.ToString()));
        }
        private void GetBuildingblockOwner(BasePlayer player, BuildingBlock block)
        {
            object findownerblock = FindOwnerBlock(block);
            if (findownerblock is bool)
            {
                SendReply(player, noBlockOwnerfound);
                return;
            }
            ulong ownerid = (UInt64)findownerblock;
            SendBasePlayerFind(player, ownerid);
        }
        private void GetToolCupboardUsers(BasePlayer player, BuildingPrivlidge cupboard)
        {
            SendReply(player, string.Format("{0} - {1} {2} {3}",Toolcupboard,Math.Round(cupboard.transform.position.x).ToString(),Math.Round(cupboard.transform.position.y).ToString(),Math.Round(cupboard.transform.position.z).ToString()));
            if (cupboard.authorizedPlayers.Count == 0)
            {
                SendReply(player, noCupboardPlayers);
                return;
            }
            foreach (ProtoBuf.PlayerNameID pnid in cupboard.authorizedPlayers)
            {
                SendReply(player, string.Format("{0} - {1}", pnid.username.ToString(), pnid.userid.ToString()));
            }
        }
        private object DoRay(Vector3 Pos, Vector3 Aim)
        {
            var hits = UnityEngine.Physics.RaycastAll(Pos, Aim);
            float distance = 100000f;
            object target = false;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<BuildingBlock>() != null)
                {
                    if (hit.distance < distance)
                    {
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<BuildingBlock>();
                    }
                }
                else if (hit.collider.GetComponentInParent<BuildingPrivlidge>() != null)
                {
                    if (hit.distance < distance)
                    {
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<BuildingPrivlidge>();
                    }
                }
                else if (hit.collider.GetComponentInParent<DeployedItem>() != null)
                {
                    if (hit.distance < distance)
                    {
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<DeployedItem>();
                    }
                }
                else if (hit.collider.GetComponentInParent<StorageContainer>() != null)
                {
                    if (hit.distance < distance)
                    {
                        distance = hit.distance;
                        target = hit.collider.GetComponentInParent<StorageContainer>();
                    }
                }
            }
            return target;
        }

        void SendHelpText(BasePlayer player)
        {
            if(hasAccess(player))
                SendReply(player,helpProd);
        }
    }
}
