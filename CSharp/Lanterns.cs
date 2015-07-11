// Reference: Oxide.Ext.Rust
// Reference: NLua

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
    [Info("Lanterns", "Reneb", 2.0, ResourceId = 738)]
    class Lanterns : RustPlugin
    {
        private bool autoLanterns;
        private int authlevel;
        private float sunRise;
        private float sunSet;
        private bool isNight;
        private object oldDay;
        private TOD_Sky TODSky;
        private float lastCheck;

        void Loaded()
        {
            autoLanterns = true;
            authlevel = 1;
            sunRise = 6;
            sunSet = 18;
            isNight = false;
            oldDay = null;
        }

        void OnServerInitialized()
        {
            lastCheck = Time.realtimeSinceStartup + 2;
            TODSky = TOD_Sky.Instance;
        }
        void OnTick()
        {
            if (autoLanterns && (Time.realtimeSinceStartup > lastCheck))
            {
                lastCheck = Time.realtimeSinceStartup + 5;
                if (TODSky.Cycle.Hour >= sunRise && TODSky.Cycle.Hour <= sunSet)
                    isNight = false;
                else
                    isNight = true;
                if (oldDay == null || (isNight != (bool)oldDay))
                {
                    setAllLanterns(isNight);
                }
                oldDay = isNight;
            }
        }
        void setAllLanterns(bool Light)
        {
            var allWorldItems = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(WorldItem));
            foreach (WorldItem wolditem in allWorldItems)
            {
                if (wolditem.item != null && wolditem.item.info.shortname.ToString() == "lantern")
                {
                    wolditem.item.SwitchOnOff(Light, null);
                }
            }
        }
    }
}
