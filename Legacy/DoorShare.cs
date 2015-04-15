using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.RustLegacy;
using Oxide.Core.Plugins;
using RustProto;

namespace Oxide.Plugins
{
    [Info("DoorShare", "Reneb", "1.0.0")]
    class DoorShare : RustLegacyPlugin
    {
        object cachedValue;

        [PluginReference]
        Plugin Share;

        object OnDoorToggle(BasicDoor door, ulong timestamp, Controllable controllable)
        {
            cachedValue = Interface.CallHook("isSharing", door.GetComponent<DeployableObject>().ownerID.ToString(), controllable.playerClient.userID.ToString());
            if (cachedValue is bool && (bool)cachedValue) return true;
            return null;
        }
    }
}
