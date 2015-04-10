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
using Oxide.Core.Plugins;
using RustProto;

namespace Oxide.Plugins
{
    [Info("Prod", "Reneb", "1.0.1")]
    class Prod : RustLegacyPlugin
    {
        [PluginReference]
        Plugin PlayerDatabase;

        RaycastHit cachedRaycast;
        Character cachedCharacter;
        bool cachedBoolean;
        Collider cachedCollider;
        StructureComponent cachedStructure;
        DeployableObject cachedDeployable;
        StructureMaster cachedMaster;
        Facepunch.MeshBatch.MeshBatchInstance cachedhitInstance;

        void Loaded() { if(!permission.PermissionExists("prod")) permission.RegisterPermission("prod", this); }
        bool hasAccess(NetUser netuser)
        {
            if (netuser.CanAdmin())
                return true;
            return permission.UserHasPermission(netuser.playerClient.userID.ToString(), "prod");
        }
        [ChatCommand("prod")]
        void cmdChatProd(NetUser netuser, string command, string[] args)
        {
            if (!hasAccess(netuser)) { SendReply(netuser, "You don't have access to this command"); return; }
            cachedCharacter = netuser.playerClient.rootControllable.idMain.GetComponent<Character>();
            if (!MeshBatchPhysics.Raycast(cachedCharacter.eyesRay, out cachedRaycast, out cachedBoolean, out cachedhitInstance)) { SendReply(netuser, "Are you looking at the sky?"); return; }
            if (cachedhitInstance != null)
            {
                cachedCollider = cachedhitInstance.physicalColliderReferenceOnly;
                if (cachedCollider == null) { SendReply(netuser, "Can't prod what you are looking at"); return; }
                cachedStructure = cachedCollider.GetComponent<StructureComponent>();
                if (cachedStructure != null && cachedStructure._master != null)
                {
                    cachedMaster = cachedStructure._master;
                    var name = PlayerDatabase?.Call("GetPlayerData", cachedMaster.ownerID.ToString(), "name");
                    SendReply(netuser, string.Format("{0} - {1} - {2}", cachedStructure.gameObject.name, cachedMaster.ownerID.ToString(), name == null ? "UnknownPlayer" : name.ToString()));
                    return;
                }
            }
            else 
            {
                cachedDeployable = cachedRaycast.collider.GetComponent<DeployableObject>();
                if (cachedDeployable != null)
                {
                    var name = PlayerDatabase?.Call("GetPlayerData", cachedDeployable.ownerID.ToString(), "name");
                    SendReply(netuser, string.Format("{0} - {1} - {2}", cachedDeployable.gameObject.name, cachedDeployable.ownerID.ToString(), name == null ? cachedDeployable.ownerName.ToString() : name.ToString()));
                    return;
                }
            }
            SendReply(netuser, string.Format("Can't prod what you are looking at: {0}",cachedRaycast.collider.gameObject.name));
        }
    }
}
