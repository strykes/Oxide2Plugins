// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("Jail", "Reneb", 2.0)]
    class Jail : RustPlugin
    {
        [PluginReference]
        Plugin ZoneManager;

        [PluginReference]
        Plugin spawns;
        ////////////////////////////////////////////
        /// FIELDS
        ////////////////////////////////////////////
        StoredData storedData;
        static Hash<string, JailInmate> jailinmates = new Hash<string, JailInmate>();
        public DateTime epoch = new System.DateTime(1970, 1, 1);
        bool hasSpawns = false;
        private Hash<BasePlayer, Plugins.Timer> TimersList = new Hash<BasePlayer, Plugins.Timer>();
        /////////////////////////////////////////
        /// Cached Fields, used to make the plugin faster
        /////////////////////////////////////////
        public BasePlayer cachedPlayer;
        public int cachedTime;
        public int cachedCount;
        public JailInmate cachedJail;
        public int cachedInterval;
        /////////////////////////////////////////
        // Data Management
        /////////////////////////////////////////
        class StoredData
        {
            public HashSet<JailInmate> JailInmates = new HashSet<JailInmate>();
            public StoredData()
            {
            }
        }
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("Jail", storedData);
        }
        void LoadData()
        {
            jailinmates.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Jail");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var jaildef in storedData.JailInmates)
                jailinmates[jaildef.userid] = jaildef;
        }



        public class JailInmate
        {
            public string userid;
            public string x;
            public string y;
            public string z;
            public string jx;
            public string jy;
            public string jz;
            public string expireTime;
            Vector3 jail_position;
            Vector3 free_position;
            int expire_time;

            public JailInmate()
            {
            }

            public JailInmate(BasePlayer player, Vector3 position, int expiretime = -1)
            {
                userid = player.userID.ToString();
                x = player.transform.position.x.ToString();
                y = player.transform.position.y.ToString();
                z = player.transform.position.z.ToString();
                jx = position.x.ToString();
                jy = position.y.ToString();
                jz = position.z.ToString();
                expireTime = expiretime.ToString();
            }
            public void UpdateJail(Vector3 position, int expiretime = -1)
            {
                jx = position.x.ToString();
                jy = position.y.ToString();
                jz = position.z.ToString();
                expireTime = expiretime.ToString();
            }
            public Vector3 GetJailPosition()
            {
                if (jail_position == default(Vector3)) jail_position = new Vector3(float.Parse(jx),float.Parse(jy),float.Parse(jz));
                return jail_position;
            }
            public Vector3 GetFreePosition()
            {
                if (free_position == default(Vector3)) free_position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return free_position;
            }
            public int GetExpireTime()
            {
                if (expire_time == 0) expire_time = int.Parse(expireTime);
                return expire_time;
            }
        }


        bool hasPermission(BasePlayer player) { if (player.net.connection.authLevel > 1) return true; return permission.UserHasPermission(player.userID.ToString(), "canjail"); }

        void Loaded()
        {
            LoadData();
            permission.RegisterPermission("canjail", this);
        }
        void Unload()
        {
            foreach (KeyValuePair<BasePlayer, Plugins.Timer> pair in TimersList)
            {
                pair.Value.Destroy();
            }
            TimersList.Clear();
        }
        int CurrentTime() { return System.Convert.ToInt32(System.DateTime.UtcNow.Subtract(epoch).TotalSeconds); }

        void AddPlayerToJail(BasePlayer player, int expiretime)
        {
            var tempPoint = FindCell(player.userID.ToString());
            if (tempPoint == null) { return; }
            JailInmate newjailmate;
            if (jailinmates[player.userID.ToString()] != null) { newjailmate = jailinmates[player.userID.ToString()]; newjailmate.UpdateJail((Vector3)tempPoint, expiretime); }
            else newjailmate = new JailInmate(player, (Vector3)tempPoint, expiretime);
            if (jailinmates[player.userID.ToString()] != null) storedData.JailInmates.Remove(jailinmates[player.userID.ToString()]);
            jailinmates[player.userID.ToString()] = newjailmate;
            storedData.JailInmates.Add(jailinmates[player.userID.ToString()]);
            SaveData();
        }
        void RemovePlayerFromJail(BasePlayer player)
        {
            if (jailinmates[player.userID.ToString()] != null) storedData.JailInmates.Remove(jailinmates[player.userID.ToString()]);
            jailinmates[player.userID.ToString()] = null;
            SaveData();
        }
        object FindCell(string userid)
        {
            if (spawns == null) { Puts(NoSpawnDatabase); return null; }
            if(spawnfile == null) { Puts(NoSpawnFile); return null; }
            var count = spawns.Call("GetSpawnsCount", spawnfile);
            if (count is bool) return null;
            return spawns.Call("GetRandomSpawnVector3", spawnfile, count);
        }
        void SendPlayerToJail(BasePlayer player)
        {
            if (jailinmates[player.userID.ToString()] == null) return;
            ZoneManager.Call("AddPlayerToZoneKeepinlist", "Jail", player);
            ForcePlayerPosition(player, jailinmates[player.userID.ToString()].GetJailPosition());
            SendReply(player, YouAreInJail);
        }
        void SendPlayerOutOfJail(BasePlayer player)
        {
            if (jailinmates[player.userID.ToString()] == null) return;
            cachedJail = jailinmates[player.userID.ToString()];
            ZoneManager.Call("RemovePlayerFromZoneKeepinlist", "Jail", player);
            ForcePlayerPosition(player, cachedJail.GetFreePosition());
            SendReply(player, YouAreFree);
        }
        void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.transform.position = destination;
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();
        }
        bool isInZone(BasePlayer player)
        {
            if (ZoneManager == null) return false;
            return (bool)ZoneManager.Call("isPlayerInZone", "Jail", player);
        }
        void OnEnterZone( string ZoneID, BasePlayer player )
        {
            if(ZoneID == "Jail")
            {
                if(hasPermission(player)) { SendReply(player, string.Format(WelcomeJail,player.displayName)); }
                else if (jailinmates[player.userID.ToString()] == null) { SendReply(player, KeepOut); }
            }
        }
        void OnExitZone(string ZoneID, BasePlayer player)
        {
            if (ZoneID == "Jail")
            {
                if (jailinmates[player.userID.ToString()] != null) { SendReply(player, KeepIn); }
            }
        }
        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (jailinmates[player.userID.ToString()] != null)
            {
                CheckPlayerExpireTime(player);
                if (!isInZone(player))
                    ForcePlayerPosition(player, jailinmates[player.userID.ToString()].GetJailPosition());
            }
        }
        void CheckPlayerExpireTime(BasePlayer player)
        {
            if (TimersList[player] != null) { TimersList[player].Destroy(); TimersList[player] = null; }
            if (!player.IsConnected()) return;
            if (player.IsDead()) return;
            if (jailinmates[player.userID.ToString()] == null) return;
            cachedJail = jailinmates[player.userID.ToString()];
            cachedInterval = cachedJail.GetExpireTime() - CurrentTime();
            if (cachedInterval < 1)
            {
                SendPlayerOutOfJail(player);
                RemovePlayerFromJail(player);
            }
            else
                TimersList[player] = timer.Once( (float)(cachedInterval + 1), () => CheckPlayerExpireTime(player));
        }
        [ChatCommand("jail_config")]
        void cmdChatJailConfig(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player)) { SendReply(player, NoPermission); return; }
            if(ZoneManager == null) { SendReply(player, NoZoneManager); return; }
            if (spawns == null) { SendReply(player, NoSpawnDatabase); return; }
            if (args.Length < 2)
            {
                SendReply(player, "/jail_config spawnfile jailspawnfile => set the spawns where players will be jailed");
                SendReply(player, "/jail_config zone RADIUS");
                SendReply(player, "You must stand in the center of the radius zone of the jail.");
                return;
            }
            switch(args[0].ToLower())
            {
                case "zone":
                    string[] zoneargs = new string[] { "name", "Jail", "eject", "true", "radius", args[1], "pvpgod", "true", "pvegod", "true", "sleepgod", "true", "undestr", "true", "nobuild", "true", "notp", "true", "nokits", "true", "nodeploy", "true", "nosuicide", "true" };
                    ZoneManager.Call("CreateOrUpdateZone", "Jail", zoneargs, player.transform.position);
                    SendReply(player, JailCreated);
                break;
                case "spawnfile":
                    var count = Interface.GetMod().CallHook("GetSpawnsCount", new object[] { args[1] });
                    if (count == null)
                    {
                        SendReply(player, "SpawnFile {0} is not a valid spawnfile", args[0].ToString());
                        Config["spawnfile"] = null;
                        spawnfile = null;
                    }
                    else
                    {
                        Config["spawnfile"] = args[1];
                        spawnfile = args[1];
                        SendReply(player, "New SpawnFile for Jaild Players: {0}", spawnfile);
                        LoadSpawnfile();
                    }
               break;
                default:
                    return;
                    break;

            }
            SaveConfig();
        }
        [ChatCommand("jail")]
        void cmdChatJail(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player)) { SendReply(player, NoPermission); return; }
            if (ZoneManager == null) { SendReply(player, NoZoneManager); return; }
            if(spawns == null) { SendReply(player, NoSpawnDatabase); return; }
            if (args.Length  == 0) { SendReply(player, "/jail PLAYER option:Time(seconds)"); return; }

            var target = FindPlayer(args[0].ToString());
            if (target is string) { SendReply(player, target.ToString()); return; }
            cachedPlayer = (BasePlayer)target;

            cachedTime = -1;
            if (args.Length > 1) int.TryParse(args[1], out cachedTime);
            if (cachedTime != -1) cachedTime += CurrentTime();

            AddPlayerToJail(cachedPlayer, cachedTime);
            SendPlayerToJail(cachedPlayer);

            CheckPlayerExpireTime(cachedPlayer);

            SendReply(player, string.Format("{0} was sent to jail",cachedPlayer.displayName.ToString()));
        }

        [ChatCommand("free")]
        void cmdChatFree(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player)) { SendReply(player, NoPermission); return; }
            if (ZoneManager == null) { SendReply(player, NoZoneManager); return; }
            if (spawns == null) { SendReply(player, NoSpawnDatabase); return; }
            if (args.Length == 0) { SendReply(player, "/jail PLAYER option:Time(seconds)"); return; }

            var target = FindPlayer(args[0].ToString());
            if (target is string) { SendReply(player, target.ToString()); return; }
            cachedPlayer = (BasePlayer)target;

            SendPlayerOutOfJail(cachedPlayer);
            RemovePlayerFromJail(cachedPlayer);

            CheckPlayerExpireTime(cachedPlayer);

            SendReply(player, string.Format("{0} was freed from jail", cachedPlayer.displayName.ToString()));
        }

        private static string NoPermission = "You don't have the permission to use this command";
        private static string NoZoneManager = "You can't use the Jail plugin without ZoneManager";
        private static string JailCreated = "You successfully created/updated the jail zone, use /zone_list for more informations";
        private static string noPlayersFound = "No Online player with this name was found";
        private static string NoSpawnDatabase = "No spawns set or no spawns database found http://forum.rustoxide.com/resources/spawns-database.720";
        private static string multiplePlayersFound = "Multiple players found";
        private static string spawnfile = null;
        private static string NoSpawnFile = "No SpawnFile - You must configure your spawnfile first: /jail_config spawnfile FILENAME";
        private static string JailsLoaded = "Jail Plugin: {0} cell spawns were detected and loaded";
        private static string YouAreInJail = "You were arrested and sent to jail";
        private static string YouAreFree = "You were freed from jail";
        private static string KeepOut = "Keep out, no visitors allowed in the jail";
        private static string WelcomeJail = "Welcome to the jail {0}";
        private static string KeepIn = "You are not allowed to leave the Jail";

        void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }



        void Init()
        {
            CheckCfg<string>("Message: No Permission", ref NoPermission);
            CheckCfg<string>("Message: No ZoneManager", ref NoZoneManager);
            CheckCfg<string>("Message: Jail Created", ref JailCreated);
            CheckCfg<string>("Message: No Player Found", ref noPlayersFound);
            CheckCfg<string>("Message: No SpawnDatabase", ref NoSpawnDatabase);
            CheckCfg<string>("Message: No SpawnFile", ref NoSpawnFile);
            CheckCfg<string>("Message: Loaded Cells", ref JailsLoaded);
            CheckCfg<string>("Message: Sent In Jail", ref YouAreInJail);
            CheckCfg<string>("Message: Freed", ref YouAreFree);
            CheckCfg<string>("Message: KeepOut", ref KeepOut);
            CheckCfg<string>("Message: Welcome ADMIN", ref WelcomeJail);
            CheckCfg<string>("spawnfile", ref spawnfile);
            CheckCfg<string>("Message: KeepIn", ref KeepIn);
            SaveConfig();
        }


        void LoadSpawnfile()
        {
            if (spawnfile == null) { Puts(NoSpawnFile); return; }
            var count = spawns.Call("GetSpawnsCount", spawnfile);
            if(count is bool)
            {
                Puts("{0} is not a valid spawnfile", spawnfile.ToString());
                Config["spawnfile"] = null;
                spawnfile = null;
                SaveConfig();
                return;
            }
            Puts(JailsLoaded, count.ToString());
        }






        private object FindPlayer(string tofind)
        {
            if (tofind.Length == 17)
            {
                ulong steamid;
                if (ulong.TryParse(tofind.ToString(), out steamid))
                {
                    return FindPlayerByID(steamid);
                }
            }
            List<BasePlayer> onlineplayers = BasePlayer.activePlayerList as List<BasePlayer>;
            object targetplayer = null;
            foreach (BasePlayer player in onlineplayers.ToArray())
            {

                if (player.displayName.ToString() == tofind)
                    return player;
                else if (player.displayName.ToString().Contains(tofind))
                {
                    if (targetplayer == null)
                        targetplayer = player;
                    else
                        return multiplePlayersFound;
                }
            }
            if (targetplayer == null)
                return noPlayersFound;
            return targetplayer;
        }
        private object FindPlayerByID(ulong steamid)
        {
            BasePlayer targetplayer = BasePlayer.FindByID(steamid);
            if (targetplayer != null)
            {
                return targetplayer;
            }
            targetplayer = BasePlayer.FindSleeping(steamid);
            if (targetplayer != null)
            {
                return targetplayer;
            }
            return null;
        }
    }
}
