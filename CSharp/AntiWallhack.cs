// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Anti Wallhack", "Reneb & ownprox", 1.6)]
    class AntiWallhack : RustPlugin
    {
        private static DateTime epoch;
        private static double lastCheck;
        private static Dictionary<BaseEntity, Dictionary<TriggerBase, Vector3>> posSave;
        private static Dictionary<BaseEntity, double> lastDetection;
        private static Dictionary<BaseEntity, int> detectionsAmount;
        private static List<BasePlayer> players;
        private static Dictionary<BuildingBlock, double> buildingblockToAdd;
        private LayerMask intLayers;
        private static float distance;
        private bool punishbyBan;
        private bool punishbyKick;
        private float ignoreFPS;
        private double resetTime;
        private bool Changed;
        private int detectionsNeeded;
        private float healthlimit;
        private bool hasStarted;
        private bool ignoreAdmins;
        private double nextAddCheck;
        void Loaded()
        {
            buildingblockToAdd = new Dictionary<BuildingBlock, double>();
            posSave = new Dictionary<BaseEntity, Dictionary<TriggerBase, Vector3>>();
            lastDetection = new Dictionary<BaseEntity, double>();
            detectionsAmount = new Dictionary<BaseEntity, int>();
            distance = 0.8f;
            epoch = new System.DateTime(1970, 1, 1);
            lastCheck = CurrentTime();
            nextAddCheck = CurrentSecTime();
            hasStarted = false;
        }
        float CurrentFPS()
        {
            return (1 / UnityEngine.Time.smoothDeltaTime);
        }
        double CurrentTime()
        {
            return System.DateTime.UtcNow.Subtract(epoch).TotalMilliseconds;
        }
        double CurrentSecTime()
        {
            return System.DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        }
        void OnServerInitialized()
        {
            var triggers = UnityEngine.Object.FindObjectsOfType<TriggerRadiation>();
            foreach (TriggerRadiation trigger in triggers)
            {
                intLayers = trigger.GetComponent<TriggerBase>().interestLayers;
                break;
            }
            LoadVariables();
            hasStarted = true;
            RefreshAllWalls();
        }
        object GetConfig(string menu, string datavalue, object defaultValue)
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
        void LoadVariables()
        {
            GetConfig("GeneralConfig", "Version", Version.ToString()); // TODO update check if new config required
            punishbyBan = Convert.ToBoolean(GetConfig("Punish", "byBan", true));
            punishbyKick = Convert.ToBoolean(GetConfig("Punish", "byKick", true));
            ignoreAdmins = Convert.ToBoolean(GetConfig("AntiWallhack", "ignoreAdmins", true));
            ignoreFPS = Convert.ToSingle(GetConfig("Punish", "IgnoreUnderFPS", 20));
            resetTime = Convert.ToDouble(GetConfig("Punish", "resetDetections", 2)) * 1000;
            detectionsNeeded = Convert.ToInt32(GetConfig("Punish", "detectionsNeededToPunish", 3));
            healthlimit = Convert.ToSingle(GetConfig("AntiWallhack", "ignoreWallHealthInPercentage", 0.1));

            if (Changed)
            {
                ((Dictionary<string, object>)Config["GeneralConfig"])["Version"] = Version.ToString(); //updated to new version
                SaveConfig();
                Changed = false;
            }
        }
        void LoadDefaultConfig()
        {
            Puts("AntiWallhack: Creating a new config file");
            Config.Clear(); // force clean new config
            LoadVariables();
        }
        void OnEntitySpawn(UnityEngine.Object gameobject)
        {
            if (hasStarted)
            {
                if (gameobject as BuildingBlock)
                {
                    var buildingblock = gameobject as BuildingBlock;
                    if (buildingblock.blockDefinition.name == "wall" || buildingblock.blockDefinition.name == "door.hinged")
                    {
                        if (!buildingblockToAdd.ContainsKey(buildingblock)) //check if exists
                            buildingblockToAdd.Add(buildingblock, CurrentSecTime() + 600);// need to add a delay here or you cannot build more then 4 doors
                    }
                }
            }
        }
        void OnTick()
        {
            if (CurrentSecTime() > nextAddCheck)
            {
                nextAddCheck = CurrentSecTime() + 60; //adjust this abit
                var currenttime = CurrentSecTime();
                if (buildingblockToAdd.Count > 0)
                {
                    List<BuildingBlock> Temp = new List<BuildingBlock>();
                    foreach (KeyValuePair<BuildingBlock, double> pair in buildingblockToAdd)
                    {
                        if (pair.Value >= currenttime)
                        {
                            BuildingBlock buildingblock = pair.Key;
                            if (buildingblock != null && buildingblock.blockDefinition != null)
                            {
                                if (buildingblock.GetComponentInChildren<TriggerBase>() == null)
                                {
                                    buildingblock.GetComponentInChildren<MeshCollider>().gameObject.AddComponent<TriggerBase>();
                                    buildingblock.GetComponentInChildren<TriggerBase>().gameObject.layer = UnityEngine.LayerMask.NameToLayer("Trigger");
                                    buildingblock.GetComponentInChildren<TriggerBase>().interestLayers = intLayers;
                                }
                            }
                            Temp.Add(buildingblock);
                        }
                    }
                    foreach (BuildingBlock b in Temp)
                        buildingblockToAdd.Remove(b);

                }
                //bulding update might take abit might aswell grab new time
                currenttime = CurrentSecTime();
                //cleanup
                List<BaseEntity> TempDelete = new List<BaseEntity>();
                foreach (KeyValuePair<BaseEntity, double> p in lastDetection)
                    if ((currenttime - p.Value) >= 120000) TempDelete.Add(p.Key);
                foreach (BaseEntity b in TempDelete)
                {
                    detectionsAmount.Remove(b);
                    lastDetection.Remove(b);
                }
                TempDelete.Clear();
            }
        }

        void OnEntityEnter(TriggerBase triggerbase, BaseEntity entity)
        {
            if (!(hasStarted)) return;
            if (triggerbase.gameObject.name == "servercollision")
            {
                if (entity is BasePlayer)
                {
                    var player = entity.GetComponentInParent<BasePlayer>();
                    if (player != null && player.net != null) //check for more nulls
                    {
                        if (player.net.connection.authLevel < 1)
                        {
                            if (posSave.ContainsKey(entity) == false) { posSave.Add(entity, new Dictionary<TriggerBase, Vector3>()); }
                            posSave[entity][triggerbase] = entity.transform.position;
                        }
                    }
                }
            }
        }

        bool RayCast(BuildingBlock buildingblock, Vector3 origin, Vector3 destination)
        {
            var vect = destination - origin;
            if (origin.y - buildingblock.transform.position.y > 2)
                return false;
            var hits = UnityEngine.Physics.RaycastAll(origin, vect.normalized, distance);
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<BuildingBlock>() == buildingblock)
                    return true;
            }
            return false;

        }

        void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.transform.position = destination;
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();
        }

        void SendMsgAdmin(string msg)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.net != null)
                {
                    if (player.net.connection.authLevel > 0)
                    {
                        player.SendConsoleCommand("chat.add", new object[] { 0, msg.QuoteSafe() });
                    }
                }
            }
        }

        void OnEntityLeave(TriggerBase triggerbase, BaseEntity entity)
        {
            if (entity != null && triggerbase != null && triggerbase.gameObject.name == "servercollision")
            {
                if (entity.GetComponentInParent<BasePlayer>())
                {
                    if ((posSave.ContainsKey(entity) == true))
                    {
                        if (posSave[entity].ContainsKey(triggerbase))
                        {
                            if (RayCast(triggerbase.GetComponentInParent<BuildingBlock>(), posSave[entity][triggerbase], entity.transform.position))
                            {
                                if (lastDetection.ContainsKey(entity) == false) { lastDetection.Add(entity, 0); }
                                if (detectionsAmount.ContainsKey(entity) == false) { detectionsAmount.Add(entity, 0); }
                                var currenttime = CurrentTime();
                                BasePlayer player = entity.GetComponentInParent<BasePlayer>();
                                if (player == null) return;
                                if (triggerbase.GetComponentInParent<BuildingBlock>().blockDefinition.name == "wall")
                                {
                                    if (CurrentFPS() > ignoreFPS)
                                    {
                                        if ((currenttime - lastDetection[entity]) < resetTime)
                                        {
                                            detectionsAmount[entity] = detectionsAmount[entity] + 1;
                                            SendMsgAdmin(player.displayName + " was detected wallhacking");
                                            Puts("{0} was detected wallhacking @ to: {1} from: {2}", player.displayName, entity.transform.position.ToString(), posSave[entity][triggerbase].ToString());
                                            if (detectionsAmount[entity] >= detectionsNeeded)
                                            {
                                                if (punishbyBan)
                                                {
                                                    Interface.GetMod().CallHook("Ban", new object[] { null, player, "r-Wallhack2", false });
                                                    Puts(player.displayName + " was detected wallhacking and was banned for it");
                                                    SendMsgAdmin(player.displayName + " was detected wallhacking and was banned");
                                                }
                                                if (punishbyBan || punishbyKick)
                                                {
                                                    if (player.net != null)
                                                    {
                                                        if (!punishbyBan)
                                                        {
                                                            Puts(player.displayName + " was detected wallhacking and was kicked for it");
                                                            SendMsgAdmin(player.displayName + " was detected wallhacking and was kicked");
                                                        }
                                                        Network.Net.sv.Kick(player.net.connection, "Kicked from the server");
                                                    }
                                                }
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            detectionsAmount[entity] = 0;
                                        }

                                    }
                                    else
                                    {
                                        Puts(player.displayName + " was detected wallhacking but was ignored because of the low server FPS");
                                        SendMsgAdmin(player.displayName + " was detected wallhacking but was ignored because of the low server FPS");
                                    }
                                }
                                else
                                {
                                    Puts("{0} was detected walking threw a door @ to: {1} from: {2}", player.displayName, entity.transform.position.ToString(), posSave[entity][triggerbase].ToString());
                                    SendMsgAdmin(player.displayName + " was detected walking threw a door");
                                }
                                ForcePlayerBack(player, posSave[entity][triggerbase], player.transform.position);

                                lastDetection[entity] = currenttime;
                            }
                            posSave[entity].Remove(triggerbase);
                        }
                    }
                }
            }
        }
        void ForcePlayerBack(BasePlayer player, Vector3 entryposition, Vector3 exitposition)
        {
            var distance = Vector3.Distance(exitposition, entryposition) + 0.5f;
            var direction = (entryposition - exitposition).normalized;
            ForcePlayerPosition(player, exitposition + (direction * distance));
        }
        object RefreshAllWalls()
        {
            var allbuildings = UnityEngine.Resources.FindObjectsOfTypeAll<BuildingBlock>();
            var protectedwall = 0;
            foreach (BuildingBlock block in allbuildings)
            {
                if (block.blockDefinition != null && (block.blockDefinition.name == "wall" || block.blockDefinition.name == "door.hinged"))
                {
                    if (block.GetComponentInChildren<TriggerBase>() == null)
                    {
                        block.GetComponentInChildren<MeshCollider>().gameObject.AddComponent<TriggerBase>();
                        block.GetComponentInChildren<TriggerBase>().gameObject.layer = UnityEngine.LayerMask.NameToLayer("Trigger");
                        block.GetComponentInChildren<TriggerBase>().interestLayers = intLayers;
                        protectedwall++;
                    }
                }
            }
            return protectedwall;
        }
        [ConsoleCommand("wallhack.init")]
        void cmdWallhackInitialize(ConsoleSystem.Arg arg)
        {
            if (arg.connection != null)
            {
                if (arg.connection.authLevel < 1)
                {
                    SendReply(arg, "You are not allowed to use this command");
                    return;
                }
            }
            var protectedwall = RefreshAllWalls();
            SendReply(arg, string.Format("{0} walls were added to the anti wallhack protection", protectedwall.ToString()));
        }
    }
}
