// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System.Reflection;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Jail", "Reneb", 1.0)]
    class Jail : RustPlugin
    {
        private static DateTime epoch;
        private static Oxide.Core.Configuration.DynamicConfigFile jaildata;
        private static Oxide.Core.Configuration.DynamicConfigFile jailconfig;
        private LayerMask intLayers;
        private static List<BasePlayer> inJailZone;
        private static System.Reflection.FieldInfo OwnerOf;
        private static bool hasSpawns;
        private static bool hasJail;
        void Loaded()
        {
            jaildata = Interface.GetMod().DataFileSystem.GetDatafile("Jail_inmates");
            jailconfig = Interface.GetMod().DataFileSystem.GetDatafile("Jail_config");
            epoch = new System.DateTime(1970, 1, 1);
            inJailZone = new List<BasePlayer>();
            hasSpawns = false;
            hasJail = false;
        }
        void OnServerInitialized()
        {
            var triggers = UnityEngine.Object.FindObjectsOfType<TriggerRadiation>();
            foreach (TriggerRadiation trigger in triggers)
            {
                intLayers = trigger.interestLayers;
                break;
            }
            LoadZone();
            LoadSpawnfile();
        }
        int CurrentTime()
        {
            return System.Convert.ToInt32(System.DateTime.UtcNow.Subtract(epoch).TotalSeconds);
        }
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("Jail_inmates");
        }
        void SaveDataConfig()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("Jail_config");
        }
        object FindCell(string userid)
        {
            if (!hasSpawns)
            {
                Puts("Jail Plugin Error: Trying to send someone to jail while no spawn points set.");
                return null;
            }
            var count = Interface.GetMod().CallHook("GetSpawnsCount", new object[] { jailconfig["jail_spawnfile"] });
            return Interface.GetMod().CallHook("GetRandomSpawnVector3", new object[] { jailconfig["jail_spawnfile"], count });
        }
        object FindFree()
        {
            if (!hasSpawns)
            {
                Puts("Jail Plugin Error: Trying to send someone to jail while no spawn points set.");
                return null;
            }
            var count = Interface.GetMod().CallHook("GetSpawnsCount", new object[] { jailconfig["free_spawnfile"] });
            return Interface.GetMod().CallHook("GetRandomSpawnVector3", new object[] { jailconfig["free_spawnfile"], count });
        }

        [ChatCommand("free")]
        void cmdChatFree(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command.");
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, "/free Username/SteamID");
                return;
            }
            var target = BasePlayer.Find(args[0]);
            if (target == null || target.net == null || target.net.connection == null)
            {
                SendReply(player, "Player not found");
                return;
            }
            jaildata[target.userID.ToString()] = null;
            SaveData();
            SendReply(player, "{0} was set free.", target.displayName);
            FreePlayerFromJail(target);
        }

        [ChatCommand("jail")]
        void cmdChatJail(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command.");
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, "/jail Username/SteamID optional:Temp");
                SendReply(player, "Temp is the temporary jail time for the player, in seconds.");
                return;
            }

            var target = BasePlayer.Find(args[0]);
            if (target == null || target.net == null || target.net.connection == null)
            {
                SendReply(player, "Player not found");
                return;
            }
            var jailtime = 0;
            var jailCell = new Dictionary<string, object>();
            var tempPoint = FindCell(target.userID.ToString());
            if (tempPoint == null)
            {
                Puts("null");
                return;
            }
            var spawnPoint = (Vector3)tempPoint;
            jailCell["x"] = spawnPoint.x.ToString();
            jailCell["y"] = spawnPoint.y.ToString();
            jailCell["z"] = spawnPoint.z.ToString();

            var inmate = new Dictionary<string, object>();
            if (args.Length > 1)
            {
                jailtime = System.Convert.ToInt32(args[1]);
            }
            if (jailtime != 0)
            {
                jailtime = CurrentTime() + jailtime;
            }
            inmate["jailtime"] = jailtime.ToString();
            inmate["jailCell"] = jailCell;
            jaildata[target.userID.ToString()] = inmate;
            SaveData();

            if (jailtime == 0)
            {
                SendReply(player, "{0} was sent to the jail.", target.displayName);
            }
            else
            {
                SendReply(player, "{0} was sent to the jail for {1} seconds.", target.displayName, (jailtime - CurrentTime()).ToString());
            }
            SendPlayerToJail(target);



        }
        [ChatCommand("jail_config")]
        void cmdChatJailConfig(BasePlayer player, string command, string[] args)
        {
            if (player.GetComponent<BaseNetworkable>().net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command.");
                return;
            }
            if (args.Length < 2)
            {
                SendReply(player, "/jail_config jail_spawnfile jailspawnfile => set the spawns where players will be jailed");
                SendReply(player, "/jail_config free_spawnfile freespawnfile => set the spawns where players will be freed");
                SendReply(player, "/jail_config zone RADIUS");
                SendReply(player, "You must stand in the center of the radius zone of the jail.");
                return;
            }
            if (args[0] == "zone")
            {
                float radius = Convert.ToSingle(args[1]);
                jailconfig["radius"] = radius.ToString();
                jailconfig["zonex"] = player.transform.position.x.ToString();
                jailconfig["zoney"] = player.transform.position.y.ToString();
                jailconfig["zonez"] = player.transform.position.z.ToString();
                SendReply(player, "New Zone {0} {1} {2} with Radius {3}", jailconfig["zonex"].ToString(), jailconfig["zoney"].ToString(), jailconfig["zonez"].ToString(), jailconfig["radius"].ToString());
                LoadZone();
            }
            else if (args[0] == "free_spawnfile")
            {
                var count = Interface.GetMod().CallHook("GetSpawnsCount", new object[] { args[1] });
                if (count == null)
                {
                    SendReply(player, "SpawnFile {0} is not a valid spawnfile", jailconfig["free_spawnfile"].ToString());
                    jailconfig["free_spawnfile"] = null;
                }
                else
                {
                    jailconfig["free_spawnfile"] = args[1];
                    SendReply(player, "New SpawnFile for Freed players: {0}", jailconfig["free_spawnfile"].ToString());
                    LoadSpawnfile();
                }
            }
            else if (args[0] == "jail_spawnfile")
            {
                var count = Interface.GetMod().CallHook("GetSpawnsCount", new object[] { args[1] });
                if (count == null)
                {
                    SendReply(player, "SpawnFile {0} is not a valid spawnfile", jailconfig["jail_spawnfile"].ToString());
                    jailconfig["jail_spawnfile"] = null;
                }
                else
                {
                    jailconfig["jail_spawnfile"] = args[1];
                    SendReply(player, "New SpawnFile for Jaild Players: {0}", jailconfig["jail_spawnfile"].ToString());
                    LoadSpawnfile();
                }
            }
            SaveDataConfig();
        }
        /*object CanOpenDoor(BasePlayer player, BaseLock door)
        {
            if (inJailZone.Contains(player))
            {
                if (jaildata[player.userID.ToString()] != null)
                {
                    return false;
                }
            }
            return null;
        }*/
        /*void OnPlayerLoot(PlayerLoot loot, BaseEntity entity)
        {
            if(OwnerOf == null)
                OwnerOf = loot.GetType().GetField("Owner", (BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance));
           
            if (OwnerOf.GetValue(loot) as BasePlayer)
            {
                var player = OwnerOf.GetValue(loot) as BasePlayer;
                if (inJailZone.Contains(player))
                {
                    if (jaildata[player.userID.ToString()] != null)
                    {
                        loot.entitySource.SendMessage("PlayerStoppedLooting", player, UnityEngine.SendMessageOptions.DontRequireReceiver);
                        loot.entitySource = null;
                        SendReply(player, "You are not allowed to loot this box while captive");
                    }
                }
            }
        }*/


        void LoadSpawnfile()
        {
            hasSpawns = false;
            var error = false;
            if (jailconfig["free_spawnfile"] == null)
            {
                Puts("Jail Plugin detected: You didn't configure a spawnfile yet, use /jail_config free_spawnfile FINENAME");
                error = true;
            }
            if (jailconfig["jail_spawnfile"] == null)
            {
                Puts("Jail Plugin detected: You didn't configure a spawnfile yet, use /jail_config jail_spawnfile FINENAME");
                error = true;
            }
            if (error == true)
            {
                Puts("Make sure that you have http://forum.rustoxide.com/resources/spawns-database.720/");
                return;
            }
            var count = Interface.GetMod().CallHook("GetSpawnsCount", new object[] { jailconfig["free_spawnfile"] });
            if (count == null)
            {
                Puts("SpawnFile {0} is not a valid free_spawnfile", jailconfig["free_spawnfile"].ToString());
                jailconfig["spawnfile"] = null;
                SaveDataConfig();
                return;
            }
            var count2 = Interface.GetMod().CallHook("GetSpawnsCount", new object[] { jailconfig["jail_spawnfile"] });
            if (count2 == null)
            {
                Puts("SpawnFile {0} is not a valid jail_spawnfile", jailconfig["jail_spawnfile"].ToString());
                jailconfig["jail_spawnfile"] = null;
                SaveDataConfig();
                return;
            }
            hasSpawns = true;
            Puts("Jail Plugin: {0} cell and {1} free spawns were detected and loaded.", count.ToString(), count2.ToString());
        }
        bool inJail(BaseEntity entity)
        {
            var triggers = UnityEngine.Object.FindObjectsOfType<TriggerBase>();
            foreach (TriggerBase trigger in triggers)
            {
                if (trigger.gameObject.name == "Jail")
                {
                    if (trigger.entityContents.Contains(entity))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        void ResetZones()
        {
            var zones = UnityEngine.Object.FindObjectsOfType<TriggerBase>();
            foreach (TriggerBase trigger in zones)
            {
                if (trigger.gameObject.name == "Jail")
                {
                    UnityEngine.Object.Destroy(trigger.gameObject);
                }
            }
        }
        void LoadZone()
        {
            ResetZones();
            hasJail = false;
            if (jailconfig["radius"] != null && jailconfig["zonex"] != null && jailconfig["zoney"] != null && jailconfig["zonez"] != null)
            {
                Vector3 newVector = new UnityEngine.Vector3();
                newVector.x = Convert.ToSingle(jailconfig["zonex"]);
                newVector.y = Convert.ToSingle(jailconfig["zoney"]);
                newVector.z = Convert.ToSingle(jailconfig["zonez"]);
                hasJail = true;
                CreateNewZone(newVector, Convert.ToSingle(jailconfig["radius"]));
                return;
            }
            Puts("Jail Plugin detected: You didn't set a jail yet, use /jail_config zone RADIUS, to set your new jails location");
        }
        void CreateNewZone(Vector3 center, float radius)
        {
            var new_zone = new UnityEngine.GameObject();
            new_zone.layer = LayerMask.NameToLayer("Trigger");
            new_zone.name = "Jail";
            new_zone.transform.position = new Vector3(center.x, center.y, center.z);
            var collider = new_zone.AddComponent<SphereCollider>();
            collider.radius = radius;
            new_zone.SetActive(true);
            var trigger_base = new_zone.AddComponent<TriggerBase>();
            trigger_base.interestLayers = intLayers;
        }
        void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.transform.position = destination;
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();
        }
        void SendPlayerBack(BasePlayer player, TriggerBase triggerbase)
        {
            var ejectDirection = player.transform.position - triggerbase.transform.position;
            Vector3 newpos = triggerbase.transform.position + ((ejectDirection / ejectDirection.magnitude) * (triggerbase.GetComponentInParent<UnityEngine.SphereCollider>().radius - 1));
            ForcePlayerPosition(player, newpos);
        }
        void SendPlayerAway(BasePlayer player, TriggerBase triggerbase)
        {
            var ejectDirection = player.transform.position - triggerbase.transform.position;
            Vector3 newpos = triggerbase.transform.position + ((ejectDirection / ejectDirection.magnitude) * (triggerbase.GetComponentInParent<UnityEngine.SphereCollider>().radius + 1));
            ForcePlayerPosition(player, newpos);
        }
        void FreePlayerFromJail(BasePlayer player)
        {
            if (!hasSpawns)
            {
                Puts("No Spawns, can't send {0} to jail", player.displayName);
                return;
            }
            var tempPoint = FindFree();
            if (tempPoint == null)
            {
                Puts("Couldn't find a spawnpoint to free {0} from jail", player.displayName);
                return;
            }
            var spawnPoint = (Vector3)tempPoint;
            player.StartSleeping();
            ForcePlayerPosition(player, spawnPoint);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendFullSnapshot();
            SendReply(player, "You were freed from jail");
        }
        void SendPlayerToJail(BasePlayer player)
        {
            if (!hasSpawns)
            {
                Puts("No Spawns, can't send {0} to jail", player.displayName);
                return;
            }
            var playerdata = jaildata[player.userID.ToString()] as Dictionary<string, object>;
            var jailcelldata = playerdata["jailCell"] as Dictionary<string, object>;
            Vector3 newPos = new Vector3(Convert.ToSingle(jailcelldata["x"]), Convert.ToSingle(jailcelldata["y"]), Convert.ToSingle(jailcelldata["z"]));
            player.StartSleeping();
            ForcePlayerPosition(player, newPos);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.UpdateNetworkGroup();
            player.SendFullSnapshot();
            SendReply(player, "You were sent to jail");
        }
        void OnPlayerSpawn(BasePlayer player)
        {
            if (jaildata[player.userID.ToString()] != null)
            {
                if (!inJail(player.GetComponent<BaseEntity>()))
                    SendPlayerToJail(player);
            }
        }
        void OnEntityLeave(TriggerBase triggerbase, BaseEntity entity)
        {
            if (triggerbase.gameObject.name == "Jail")
            {
                if (entity.GetComponentInParent<BasePlayer>())
                {
                    if (inJailZone.Contains(entity.GetComponentInParent<BasePlayer>()))
                        inJailZone.Remove(entity.GetComponentInParent<BasePlayer>());

                    if (jaildata[entity.GetComponentInParent<BasePlayer>().userID.ToString()] != null)
                    {
                        SendReply(entity.GetComponentInParent<BasePlayer>(), "You may not leave the Jail");
                        SendPlayerBack(entity.GetComponentInParent<BasePlayer>(), triggerbase);
                    }
                }
            }
        }
        void OnEntityEnter(TriggerBase triggerbase, BaseEntity entity)
        {
            if (triggerbase.gameObject.name == "Jail")
            {
                if (entity.GetComponentInParent<BasePlayer>())
                {
                    inJailZone.Add(entity.GetComponentInParent<BasePlayer>());
                    if (jaildata[entity.GetComponentInParent<BasePlayer>().userID.ToString()] == null)
                    {
                        if (entity.GetComponentInParent<BaseNetworkable>().net.connection.authLevel < 1)
                        {
                            SendReply(entity.GetComponentInParent<BasePlayer>(), "You are not allowed to enter the Jail");
                            SendPlayerAway(entity.GetComponentInParent<BasePlayer>(), triggerbase);
                        }
                        else
                        {
                            SendReply(entity.GetComponentInParent<BasePlayer>(), "Welcome {0} to the jail", entity.GetComponentInParent<BasePlayer>().displayName);
                        }
                    }
                }
            }
        }
        object OnEntityAttacked(BaseEntity entity, HitInfo info)
        {
            if (info == null) return null;
            if (info.Initiator == null) return null;
            var player = info.Initiator as BasePlayer;
            if (player == null) return null;
            if (jaildata[player.userID.ToString()] != null)
                return false;
            return null;
        }
        void PlayerInit(BasePlayer player)
        {
            if (jaildata[player.userID.ToString()] != null)
            {
                if (!inJail(player.GetComponent<BaseEntity>()))
                    SendPlayerToJail(player);
            }
        }
    }
}
