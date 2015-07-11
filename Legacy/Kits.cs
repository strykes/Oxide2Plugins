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
    [Info("Kits", "Reneb", "1.0.5")]
    class Kits : RustLegacyPlugin
    {
        private string noAccess;

        private string itemNotFound;
        private string cantUseKit;
        private string maxKitReached;
        private string unknownKit;
        private string kitredeemed;
        private string kitsreset;
        private bool shouldstrip;

        private DateTime epoch;
        private Core.Configuration.DynamicConfigFile KitsConfig;
        private Core.Configuration.DynamicConfigFile KitsData;
        private bool Changed;
        private Dictionary<string, ItemDataBlock> displaynameToDataBlock = new Dictionary<string, ItemDataBlock>();

        void Loaded()
        {
            epoch = new System.DateTime(1970, 1, 1);
            if (!permission.PermissionExists("vip")) permission.RegisterPermission("vip", this);
            if (!permission.PermissionExists("vip+")) permission.RegisterPermission("vip+", this);
            if (!permission.PermissionExists("vip++")) permission.RegisterPermission("vip++", this);
            LoadVariables();
            InitializeKits();
        }
        void OnServerInitialized()
        {
            InitializeTable();
        }
        double CurrentTime()
        {
            return System.DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        }
        private void InitializeKits()
        {
            KitsConfig = Interface.GetMod().DataFileSystem.GetDatafile("Kits_List");
            KitsData = Interface.GetMod().DataFileSystem.GetDatafile("Kits_Data");
        }
        private void SaveKits()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("Kits_List");
        }
        private void SaveKitsData()
        {
            Interface.GetMod().DataFileSystem.SaveDatafile("Kits_Data");
        }
        private void InitializeTable()
        {
            displaynameToDataBlock.Clear();
            foreach (ItemDataBlock itemdef in DatablockDictionary.All)
            {
                displaynameToDataBlock.Add(itemdef.name.ToString().ToLower(), itemdef);
            }
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
            noAccess = Convert.ToString(GetConfig("Messages", "noAccess", "You are not allowed to use this command"));
            itemNotFound = Convert.ToString(GetConfig("Messages", "itemNotFound", "Item not found: "));
            cantUseKit = Convert.ToString(GetConfig("Messages", "cantUseKit", "You are not allowed to use this kit"));
            maxKitReached = Convert.ToString(GetConfig("Messages", "maxKitReached", "You've used all your tokens for this kit"));
            unknownKit = Convert.ToString(GetConfig("Messages", "unknownKit", "This kit doesn't exist"));
            kitredeemed = Convert.ToString(GetConfig("Messages", "kitredeemed", "You've redeemed a kit"));
            kitsreset = Convert.ToString(GetConfig("Messages", "kitsreset", "All kits data from players were deleted"));
            shouldstrip = Convert.ToBoolean(GetConfig("Settings", "RemoveDefaultKit", "true"));
            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }
        void LoadDefaultConfig()
        {
            Puts("Kits: Creating a new config file");
            Config.Clear();
            LoadVariables();
        }
        bool hasAccess(NetUser netuser)
        {
            if (netuser.CanAdmin())
                return true;
            return false;
        }
        bool hasVip(NetUser netuser, string vipname)
        {
            if (netuser.CanAdmin()) return true;
            return permission.UserHasPermission(netuser.playerClient.userID.ToString(), vipname);
        }
        public object GiveItem(Inventory inventory, string itemname, int amount, Inventory.Slot.Preference pref)
        {
            itemname = itemname.ToLower();
            if (!displaynameToDataBlock.ContainsKey(itemname)) return false;
            ItemDataBlock datablock = displaynameToDataBlock[itemname];
            inventory.AddItemAmount(displaynameToDataBlock[itemname], amount, pref);
            return true;
        }
        void SendList(NetUser netuser)
        {
            var kitEnum = KitsConfig.GetEnumerator();
            bool isadmin = hasAccess(netuser);
            bool isvip = hasVip(netuser,"vip");
            bool isvipp = hasVip(netuser,"vip+");
            bool isvippp = hasVip(netuser, "vip++");

            while (kitEnum.MoveNext())
            {
                string kitdescription = string.Empty;
                string options = string.Empty;
                string kitname = string.Empty;
                options = string.Empty;
                kitname = kitEnum.Current.Key.ToString();
                var kitdata = kitEnum.Current.Value as Dictionary<string, object>;
                if (kitdata.ContainsKey("description"))
                    kitdescription = kitdata["description"].ToString();
                if (kitdata.ContainsKey("max"))
                {
                    options = string.Format("{0} - {1} max", options, kitdata["max"].ToString());
                }
                if (kitdata.ContainsKey("cooldown"))
                {
                    options = string.Format("{0} - {1}s cooldown", options, kitdata["cooldown"].ToString());
                }
                if (kitdata.ContainsKey("admin"))
                {
                    options = string.Format("{0} - {1}", options, "admin");
                    if (!isadmin) continue;
                }
                if (kitdata.ContainsKey("vip"))
                {
                    options = string.Format("{0} - {1}", options, "vip");
                    if (!isvip) continue;
                }
                if (kitdata.ContainsKey("vip+"))
                {
                    options = string.Format("{0} - {1}", options, "vip+");
                    if (!isvipp) continue;
                }
                if (kitdata.ContainsKey("vip++"))
                {
                    options = string.Format("{0} - {1}", options, "vip++");
                    if (!isvippp) continue;
                }
                SendReply(netuser, string.Format("{0} - {1} {2}", kitname, kitdescription, options));
            }
        }

        void cmdAddKit(NetUser netuser, string[] args)
        {
            if (args.Length < 3)
            {
                SendReply(netuser, "/kit add \"KITNAME\" \"DESCRIPTION\" -option1 -option2 etc, Everything you have in your inventory will be used in the kit");
                SendReply(netuser, "Options avaible:");
                SendReply(netuser, "-maxXX => max times someone can use this kit. Default is infinite.");
                SendReply(netuser, "-cooldownXX => cooldown of the kit. Default is none.");
                SendReply(netuser, "-vip => Allow to give this kit only to vip & admins");
                SendReply(netuser, "-admin => Allow to give this kit only to admins (set this for the autokit!!!!)");
                return;
            }
            string kitname = args[1].ToString();
            string description = args[2].ToString();
            bool vip = false;
            bool vipp = false;
            bool vippp = false;
            bool admin = false;
            int max = -1;
            double cooldown = 0.0;
            if (KitsConfig[kitname] != null)
            {
                SendReply(netuser, string.Format("The kit {0} already exists. Delete it first or change the name.", kitname));
                return;
            }
            if (args.Length > 3)
            {
                object validoptions = VerifyOptions(args, out admin, out vip, out vipp, out vippp, out max, out cooldown);
                if (validoptions is string)
                {
                    SendReply(netuser, (string)validoptions);
                    return;
                }
            }
            Dictionary<string, object> kitsitems = GetNewKitFromPlayer(netuser);
            Dictionary<string, object> newkit = new Dictionary<string, object>();
            newkit.Add("items", kitsitems);
            if (admin)
                newkit.Add("admin", true);
            if (vip)
                newkit.Add("vip", true);
            if (vipp)
                newkit.Add("vip+", true);
            if (vippp)
                newkit.Add("vip++", true);
            if (max >= 0)
                newkit.Add("max", max);
            if (cooldown > 0.0)
                newkit.Add("cooldown", cooldown);
            newkit.Add("description", description);
            KitsConfig[kitname] = newkit;
            SaveKits();
        }
        Dictionary<string, object> GetNewKitFromPlayer(NetUser netuser)
        {
            Dictionary<string, object> kitsitems = new Dictionary<string, object>();
            List<object> wearList = new List<object>();
            List<object> mainList = new List<object>();
            List<object> beltList = new List<object>();

            IInventoryItem item;
            var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
            for (int i = 0; i < 40; i++)
            {
                if(inv.GetItem(i, out item))
                {
                    Dictionary<string, object> newObject = new Dictionary<string, object>();
                    newObject.Add(item.datablock.name.ToString().ToLower(), item.datablock._splittable?(int)item.uses :1);
                    if (i>=0 && i<30)
                        mainList.Add(newObject);
                    else if(i>=30 && i < 36)
                        beltList.Add(newObject);
                    else
                        wearList.Add(newObject);
                }
            }
            inv.Clear();
            kitsitems.Add("wear", wearList);
            kitsitems.Add("main", mainList);
            kitsitems.Add("belt", beltList);
            return kitsitems;
        }
        object VerifyOptions(string[] args, out bool admin, out bool vip, out bool vipp, out bool vippp, out int max, out double cooldown)
        {
            max = -1;
            admin = false;
            vip = false;
            vipp = false;
            vippp = false;
            cooldown = 0.0;
            for (int i = 3; i < args.Length; i++)
            {
                int substring = 0;
                if (args[i].StartsWith("-max"))
                {
                    substring = 4;
                    if (!(int.TryParse(args[i].Substring(substring), out max)))
                        return string.Format("Wrong Number Value for : {0}", args[i].ToString());
                }
                else if (args[i].StartsWith("-cooldown"))
                {
                    substring = 9;
                    if (!(double.TryParse(args[i].Substring(substring), out cooldown)))
                        return string.Format("Wrong Number Value for : {0}", args[i].ToString());
                }
                else if (args[i] == "-vip")
                {
                    vip = true;
                }
                else if (args[i] == "-vip+")
                {
                    vipp = true;
                }
                else if (args[i] == "-vip++")
                {
                    vippp = true;
                }
                else if (args[i].StartsWith("-admin"))
                {
                    admin = true;
                }
                else
                    return string.Format("Wrong Options: {0}", args[i].ToString());
                return true;
            }
            return true;
        }
        void cmdResetKits(NetUser netuser, string[] args)
        {
            KitsData.Clear();
            SendReply(netuser, "All kits data from players were deleted");
            SaveKitsData();
        }
        void OnPlayerSpawn(PlayerClient player, bool useCamp, RustProto.Avatar avatar)
        {
            if (KitsConfig["autokit"] == null) return;
            if (avatar != null && avatar.HasPos && avatar.HasAng) return;
            object thereturn = Interface.GetMod().CallHook("canRedeemKit", new object[] { player.netUser });
            if (thereturn == null)
            {
                timer.Once(0.01f, () => StripAndGiveKit(player.netUser, "autokit"));
            }
        } 
        void StripAndGiveKit(NetUser netuser, string kitname)
        {
            if(shouldstrip) netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>().Clear();
            GiveKit(netuser, kitname);
        }
        void cmdRemoveKit(NetUser netuser, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(netuser, "Kit must specify the name of the kit that you want to remove");
                return;
            }
            int kitlvl = 0;
            string kitname = args[1].ToString();
            if (KitsConfig[kitname] == null)
            {
                SendReply(netuser, string.Format("The kit {0} doesn't exist", kitname));
                return;
            }
            var kitdata = (KitsConfig[kitname]) as Dictionary<string, object>;
            var newKits = new Dictionary<string, object>();
            var enumkits = KitsConfig.GetEnumerator();
            while (enumkits.MoveNext())
            {
                if (enumkits.Current.Key.ToString() != kitname && enumkits.Current.Value != null)
                {
                    newKits.Add(enumkits.Current.Key.ToString(), enumkits.Current.Value);
                }
            }
            KitsConfig.Clear();
            foreach (KeyValuePair<string, object> pair in newKits)
            {
                KitsConfig[pair.Key] = pair.Value;
            }
            SaveKits();
            SendReply(netuser, string.Format("The kit {0} was successfully removed", kitname));
        }
        int GetKitLeft(NetUser netuser, string kitname, int max)
        {
            if (KitsData[netuser.playerClient.userID.ToString()] == null) return max;
            var data = KitsData[netuser.playerClient.userID.ToString()] as Dictionary<string, object>;
            if (!(data.ContainsKey(kitname))) return max;
            var currentkit = data[kitname] as Dictionary<string, object>;
            if (!(currentkit.ContainsKey("used"))) return max;
            return (max - (int)currentkit["used"]);
        }
        double GetKitTimeleft(NetUser netuser, string kitname, double max)
        {
            if (KitsData[netuser.playerClient.userID.ToString()] == null) return 0.0;
            var data = KitsData[netuser.playerClient.userID.ToString()] as Dictionary<string, object>;
            if (!(data.ContainsKey(kitname))) return 0.0;
            var currentkit = data[kitname] as Dictionary<string, object>;
            if (!(currentkit.ContainsKey("cooldown"))) return 0.0;
            return ((double)currentkit["cooldown"] - CurrentTime());
        }
        void TryGiveKit(NetUser netuser, string kitname)
        {
            if (KitsConfig[kitname] == null)
            {
                SendReply(netuser, unknownKit);
                return;
            }
            object thereturn = Interface.GetMod().CallHook("canRedeemKit", netuser );
            if (thereturn != null)
            {
                if (thereturn is string)
                {
                    SendReply(netuser, (string)thereturn);
                }
                return;
            }

            Dictionary<string, object> kitdata = (KitsConfig[kitname]) as Dictionary<string, object>;
            double cooldown = 0.0;
            int kitleft = 1;
            if (kitdata.ContainsKey("max"))
                kitleft = GetKitLeft(netuser, kitname, (int)(kitdata["max"]));
            if (kitdata.ContainsKey("admin"))
                if(!hasAccess(netuser))
                {
                    SendReply(netuser, cantUseKit);
                    return;
                }
            if (kitdata.ContainsKey("vip"))
                if (!hasVip(netuser,"vip"))
                {
                    SendReply(netuser, cantUseKit);
                    return;
                }
            if (kitdata.ContainsKey("vip+"))
                if (!hasVip(netuser, "vip+"))
                {
                    SendReply(netuser, cantUseKit);
                    return;
                }
            if (kitdata.ContainsKey("vip++"))
                if (!hasVip(netuser, "vip++"))
                {
                    SendReply(netuser, cantUseKit);
                    return;
                }
            if (kitleft <= 0)
            {
                SendReply(netuser, maxKitReached);
                return;
            }
            if (kitdata.ContainsKey("cooldown"))
                cooldown = GetKitTimeleft(netuser, kitname, (double)(kitdata["cooldown"]));
            if (cooldown > 0.0)
            {
                SendReply(netuser, string.Format("You must wait {0}s before using this kit again", cooldown.ToString()));
                return;
            }
            object wasGiven = GiveKit(netuser, kitname);
            if ((wasGiven is bool) && !((bool)wasGiven))
            {
                Puts(string.Format("An error occurred while giving the kit {0} to {1}", kitname, netuser.playerClient.userName.ToString()));
                return;
            }
            proccessKitGiven(netuser, kitname, kitdata, kitleft);
        }

        void proccessKitGiven(NetUser netuser, string kitname, Dictionary<string, object> kitdata, int kitleft)
        {
            string userid = netuser.playerClient.userID.ToString();
            if (KitsData[userid] == null)
            {
                (KitsData[userid]) = new Dictionary<string, object>();
            }
            var playerData = (KitsData[userid]) as Dictionary<string, object>;
            var currentKitData = new Dictionary<string, object>();
            bool write = false;
            if (kitdata.ContainsKey("max"))
            {
                currentKitData.Add("used", (((int)kitdata["max"] - kitleft) + 1));
                write = true;
            }
            if (kitdata.ContainsKey("cooldown"))
            {
                currentKitData.Add("cooldown", ((double)kitdata["cooldown"] + CurrentTime()));
                write = true;
            }
            if (write)
            {
                if (playerData.ContainsKey(kitname))
                    playerData[kitname] = currentKitData;
                else
                    playerData.Add(kitname, currentKitData);
                KitsData[userid] = playerData;

            }
        }
        void OnServerSave()
        {
            SaveKitsData();
        }
        void Unload()
        {
            SaveKitsData();
        }
        object GiveKit(NetUser netuser, string kitname)
        {
            if (KitsConfig[kitname] == null)
            {
                SendReply(netuser, unknownKit);
                return false;
            }

            if (netuser.playerClient == null || netuser.playerClient.rootControllable == null) return false;

            var inv = netuser.playerClient.rootControllable.idMain.GetComponent<Inventory>();
            var kitdata = (KitsConfig[kitname]) as Dictionary<string, object>;
            var kitsitems = kitdata["items"] as Dictionary<string, object>;
            List<object> wearList = kitsitems["wear"] as List<object>;
            List<object> mainList = kitsitems["main"] as List<object>;
            List<object> beltList = kitsitems["belt"] as List<object>;
            Inventory.Slot.Preference pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor,false,Inventory.Slot.KindFlags.Belt);

            if (wearList.Count > 0)
            {
                pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Armor, false, Inventory.Slot.KindFlags.Belt);
                foreach (object items in wearList)
                {
                    foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
                    {
                        GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
                    }
                }
            }

            if (mainList.Count > 0)
            {
                pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Default, false, Inventory.Slot.KindFlags.Belt);
                foreach (object items in mainList)
                {
                    foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
                    {
                        GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
                    }
                }
            }
            if (beltList.Count > 0)
            {
                pref = Inventory.Slot.Preference.Define(Inventory.Slot.Kind.Belt, false, Inventory.Slot.KindFlags.Belt);
                foreach (object items in beltList)
                {
                    foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
                    {
                        GiveItem(inv, (string)pair.Key, (int)pair.Value, pref);
                    }
                }
            }
            SendReply(netuser, kitredeemed);
            return true;
        }
        [ChatCommand("kit")]
        void cmdChatKits(NetUser player, string command, string[] args)
        {
            if (args.Length > 0 && (args[0].ToString() == "add" || args[0].ToString() == "reset" || args[0].ToString() == "remove"))
            {
                if (!hasAccess(player))
                {
                    SendReply(player, noAccess);
                    return;
                }
                if (args[0].ToString() == "add")
                    cmdAddKit(player, args);
                else if (args[0].ToString() == "reset")
                    cmdResetKits(player, args);
                else if (args[0].ToString() == "remove")
                    cmdRemoveKit(player, args);
                return;
            }
            if (args.Length == 0)
            {
                SendList(player);
                return;
            }
            TryGiveKit(player, args[0]);
        }
    }
}
