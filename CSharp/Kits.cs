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
    [Info("Kits", "Reneb", "2.1.1")]
    class Kits : RustPlugin
    {
        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Plugin References
        //////////////////////////////////////////////////////////////////////////////////////////

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// cached Fields
        //////////////////////////////////////////////////////////////////////////////////////////


        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Fields
        //////////////////////////////////////////////////////////////////////////////////////////

        private DateTime epoch;

        private Dictionary<string, string> displaynameToShortname;
        private List<string> permNames = new List<string>();

        static int playerLayer = UnityEngine.LayerMask.GetMask(new string[] { "Player (Server)" });
        static int groundLayer = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Terrain", "World" });

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Configuration
        //////////////////////////////////////////////////////////////////////////////////////////

        static string noAccess = "You are not allowed to use this command";
        static List<object> permissionsList = GetDefaultPermList();
        static int authLevel = 1;
        static string itemNotFound = "Item not found: ";
        static string cantUseKit = "You are not allowed to use this kit";
        static string maxKitReached = "You've used all your tokens for this kit";
        static string unknownKit = "This kit doesn't exist";
        static string kitredeemed = "You've redeemed a kit";
        static string kitsreset = "All kits data from players were deleted";
        static string MessageCooldownTimer = "You must wait {0}s before using this kit again";

        static string guiKitminXcfg = "0.5";
        static string guiKitmaxXcfg = "0.8";
        static string guiKitmaxYcfg = "0.8";
        static string guiKitYheightcfg = "0.05";
        static string guiMsgminXcfg = "0.2";
        static string guiMsgmaxXcfg = "0.5";
        static string guiMsgminYcfg = "0.2";
        static string guiMsgmaxYcfg = "0.8";

        static double guiKitminX = 0.5;
        static double guiKitmaxX = 0.8;
        static double guiKitmaxY = 0.8;
        static double guiKitYheight = 0.05;
        static double guiMsgminX = 0.2;
        static double guiMsgmaxX = 0.5;
        static double guiMsgminY = 0.2;
        static double guiMsgmaxY = 0.8;
         
        static int retreiveType = 0;
        static Dictionary<string, object> npcKitList = GetDefaultNpcKit();
        static Dictionary<string, object> npcMessageList = GetDefaultNpcMessage();
        private Core.Configuration.DynamicConfigFile KitsConfig;
        private Core.Configuration.DynamicConfigFile KitsData;
        private bool Changed;

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<int>("Settings: authLevel", ref authLevel);
            CheckCfg<List<object>>("Settings: Permissions List", ref permissionsList);
            CheckCfg<string>("Messages: noAccess", ref noAccess);
            CheckCfg<string>("Messages: itemNotFound", ref itemNotFound);
            CheckCfg<string>("Messages: cantUseKit", ref cantUseKit);
            CheckCfg<string>("Messages: maxKitReached", ref maxKitReached);
            CheckCfg<string>("Messages: unknownKit", ref unknownKit);
            CheckCfg<string>("Messages: kitredeemed", ref kitredeemed);
            CheckCfg<string>("Messages: kitsreset", ref kitsreset);
            CheckCfg<string>("Messages: Kit in Cooldown", ref MessageCooldownTimer);

            CheckCfg<string>("GUI Kit: min x", ref guiKitminXcfg);
            CheckCfg<string>("GUI Kit: max x", ref guiKitmaxXcfg);
            CheckCfg<string>("GUI Kit: max y", ref guiKitmaxYcfg);
            CheckCfg<string>("GUI Kit: y height", ref guiKitYheightcfg);

            CheckCfg<string>("GUI Message: min x", ref guiMsgminXcfg);
            CheckCfg<string>("GUI Message: max x", ref guiMsgmaxXcfg);
            CheckCfg<string>("GUI Message: min y", ref guiMsgminYcfg);
            CheckCfg<string>("GUI Message: max y", ref guiMsgmaxYcfg);

            CheckCfg<Dictionary<string, object>>("NPC Kits", ref npcKitList);
            CheckCfg<Dictionary<string, object>>("NPC Messages", ref npcMessageList);

            SaveConfig();

        }

        void LoadDefaultConfig() { }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Data
        //////////////////////////////////////////////////////////////////////////////////////////

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

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Oxide Hooks
        //////////////////////////////////////////////////////////////////////////////////////////


        void Loaded()
        {
            epoch = new System.DateTime(1970, 1, 1);
            displaynameToShortname = new Dictionary<string, string>();
            foreach (var perm in permissionsList)
            {
                if (!permission.PermissionExists(perm.ToString())) permission.RegisterPermission(perm.ToString(), this);
                if (!permNames.Contains(perm.ToString())) permNames.Add(perm.ToString());
            }
            InitializeKits();

            guiKitminX = Convert.ToDouble(guiKitminXcfg);
            guiKitmaxX = Convert.ToDouble(guiKitmaxXcfg);
            guiKitmaxY = Convert.ToDouble(guiKitmaxYcfg);
            guiKitYheight = Convert.ToDouble(guiKitYheightcfg);

            guiMsgminX = Convert.ToDouble(guiMsgminXcfg);
            guiMsgmaxX = Convert.ToDouble(guiMsgmaxXcfg);
            guiMsgminY = Convert.ToDouble(guiMsgminYcfg);
            guiMsgmaxY = Convert.ToDouble(guiMsgmaxYcfg);

        }
        void OnServerInitialized()
        {
            InitializeTable();
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Methods
        //////////////////////////////////////////////////////////////////////////////////////////

        double CurrentTime() { return System.DateTime.UtcNow.Subtract(epoch).TotalSeconds; }


        private void InitializeTable()
        {
            displaynameToShortname.Clear();
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                displaynameToShortname.Add(itemdef.displayName.english.ToString().ToLower(), itemdef.shortname.ToString());
            }
        }



        static List<object> GetDefaultPermList()
        {
            var newobject = new List<object>();
            newobject.Add("vip");
            newobject.Add("donator");
            return newobject;
        }

        private void SendTheReply(object source, string msg)
        {
            if (source is BasePlayer)
                SendReply((BasePlayer)source, msg);
            else if (source is ConsoleSystem.Arg)
                SendReply((ConsoleSystem.Arg)source, msg);
            else
                Puts(msg);
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Permission related
        //////////////////////////////////////////////////////////////////////////////////////////

        bool hasAccess(BasePlayer player)
        {
            if (player.net.connection.authLevel >= authLevel)
                return true;
            return false;
        }
        bool hasVip(object source, string name)
        {
            if (!(source is BasePlayer)) return true;
            if (((BasePlayer)source).net.connection.authLevel >= authLevel) return true;
            return permission.UserHasPermission(((BasePlayer)source).userID.ToString(), name);
        }

        int GetSourceLevel(object source)
        {
            if (source is BasePlayer)
            {
                return ((BasePlayer)source).net.connection.authLevel;
            }
            return 2;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// NPC Related
        //////////////////////////////////////////////////////////////////////////////////////////
        static Dictionary<string, object> GetDefaultNpcMessage()
        {
            var newobject = new Dictionary<string, object>();
            newobject.Add("123456","Welcome on this server, Here is a list of free kits that you can get <color=red>only once each</color>\n\n                      <color=green>Enjoy your stay</color>");
            newobject.Add("999999", "<color=red>Only VIPs are allowed to get kits from me</color>");

            return newobject;
        }
        static Dictionary<string, object> GetDefaultNpcKit()
        {
            var newobject = new Dictionary<string, object>();
            var newlist = new List<object>();
            newlist.Add("stones");
            newlist.Add("building");
            newobject.Add("123456", newlist);

            newlist.Add("vipkit");
            newobject.Add("999999", newlist);

            return newobject;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// GiveItem
        //////////////////////////////////////////////////////////////////////////////////////////

        public object GiveItem(BasePlayer player, string itemname, int amount, ItemContainer pref)
        {
            itemname = itemname.ToLower();

            bool isBP = false;
            if (itemname.EndsWith(" bp"))
            {
                isBP = true;
                itemname = itemname.Substring(0, itemname.Length - 3);
            }
            if (displaynameToShortname.ContainsKey(itemname))
                itemname = displaynameToShortname[itemname];
            var definition = ItemManager.FindItemDefinition(itemname);
            if (definition == null)
                return string.Format("{0} {1}", itemNotFound, itemname);
            int giveamount = 0;
            int stack = (int)definition.stackable;
            if (isBP)
                stack = 1;
            if (stack < 1) stack = 1;
            for (var i = amount; i > 0; i = i - stack)
            {
                if (i >= stack)
                    giveamount = stack;
                else
                    giveamount = i;
                if (giveamount < 1) return true;
                player.inventory.GiveItem(ItemManager.CreateByItemID((int)definition.itemid, giveamount, isBP), pref);
            }
            return true;
        }



        void SendList(object source)
        {
            var kitEnum = KitsConfig.GetEnumerator();
            int authlevel = GetSourceLevel(source);

            while (kitEnum.MoveNext())
            {
                int kitlvl = 0;
                string kitdescription = string.Empty;
                string options = string.Empty;
                string kitname = string.Empty;
                options = string.Empty;
                kitname = kitEnum.Current.Key.ToString();
                var kitdata = kitEnum.Current.Value as Dictionary<string, object>;
                if (kitdata.ContainsKey("description"))
                    kitdescription = kitdata["description"].ToString();
                if (kitdata.ContainsKey("level"))
                    options = string.Format("{0} - {1}+", options, kitlvl.ToString());

                foreach (string name in permNames)
                {
                    if (kitdata.ContainsKey(name))
                    {
                        options = string.Format("{0} - {1}", options, name);
                        if (!hasVip(source, name)) kitlvl = authLevel;
                    }
                }

                if (kitdata.ContainsKey("npconly"))
                {
                    options = string.Format("{0} - NPC Only", options);
                    kitlvl = 2;
                }

                if (kitdata.ContainsKey("max"))
                {
                    options = string.Format("{0} - {1} max", options, kitdata["max"].ToString());
                }
                if (kitdata.ContainsKey("cooldown"))
                {
                    options = string.Format("{0} - {1}s cooldown", options, kitdata["cooldown"].ToString());
                }
                if (kitlvl <= authlevel)
                {
                    SendTheReply(source, string.Format("{0} - {1} {2}", kitname, kitdescription, options));
                }
            }
        }


        void cmdAddKit(BasePlayer player, string[] args)
        {
            if (args.Length < 3)
            {
                SendTheReply(player, "/kit add \"KITNAME\" \"DESCRIPTION\" -option1 -option2 etc, Everything you have in your inventory will be used in the kit");
                SendTheReply(player, "Options avaible:");
                SendTheReply(player, "-maxXX => max times someone can use this kit. Default is infinite.");
                SendTheReply(player, "-cooldownXX => cooldown of the kit. Default is none.");
                SendTheReply(player, "-npconly => Only allow kit retrieve from the NPC.");
                SendTheReply(player, "-authlevelXX => Level needed to use this plugin: 0, 1 or 2. Default is 0");
                foreach (string name in permNames)
                {
                    SendTheReply(player, string.Format("-{0} => Allow to give this kit only to {0}s", name));
                }
                return;
            }
            string kitname = args[1].ToString();
            string desription = args[2].ToString();
            int authlevel = 0;
            bool npconly = false;
            int max = -1;
            var vip = new List<string>();
            double cooldown = 0.0;
            if (KitsConfig[kitname] != null)
            {
                SendTheReply(player, string.Format("The kit {0} already exists. Delete it first or change the name.", kitname));
                return;
            }
            if (args.Length > 3)
            {
                object validoptions = VerifyOptions(args, out authlevel, out max, out cooldown, out vip, out npconly);
                if (validoptions is string)
                {
                    SendTheReply(player, (string)validoptions);
                    return;
                }
            }
            Dictionary<string, object> kitsitems = GetNewKitFromPlayer(player);
            Dictionary<string, object> newkit = new Dictionary<string, object>();
            newkit.Add("items", kitsitems);
            if (authlevel > 0)
                newkit.Add("level", authlevel);
            if (max >= 0)
                newkit.Add("max", max);
            foreach (string name in vip)
            {
                newkit.Add(name, true);
            }
            if (cooldown > 0.0)
                newkit.Add("cooldown", cooldown);
            if (npconly)
                newkit.Add("npconly", true);
            newkit.Add("description", desription);
            KitsConfig[kitname] = newkit;
            SaveKits();
        }
        Dictionary<string, object> GetNewKitFromPlayer(BasePlayer player)
        {
            Dictionary<string, object> kitsitems = new Dictionary<string, object>();
            List<object> wearList = new List<object>();
            List<object> mainList = new List<object>();
            List<object> beltList = new List<object>();

            ItemContainer wearcontainer = player.inventory.containerWear;
            ItemContainer maincontainer = player.inventory.containerMain;
            ItemContainer beltcontainer = player.inventory.containerBelt;

            string itemname = string.Empty;
            foreach (Item item in (List<Item>)wearcontainer.itemList)
            {
                Dictionary<string, object> newObject = new Dictionary<string, object>();


                newObject.Add(item.info.shortname.ToString(), (int)item.amount);
                wearList.Add(newObject);
            }

            foreach (Item item in (List<Item>)maincontainer.itemList)
            {
                Dictionary<string, object> newObject = new Dictionary<string, object>();
                itemname = item.info.shortname.ToString();
                if (item.IsBlueprint())
                    itemname = string.Format("{0} BP", itemname);
                newObject.Add(itemname, (int)item.amount);
                mainList.Add(newObject);
            }
            foreach (Item item in (List<Item>)beltcontainer.itemList)
            {
                Dictionary<string, object> newObject = new Dictionary<string, object>();
                itemname = item.info.shortname.ToString();
                if (item.IsBlueprint())
                    itemname = string.Format("{0} BP", itemname);
                newObject.Add(itemname, (int)item.amount);
                beltList.Add(newObject);
            }
            player.inventory.Strip();
            kitsitems.Add("wear", wearList);
            kitsitems.Add("main", mainList);
            kitsitems.Add("belt", beltList);
            return kitsitems;
        }
        object VerifyOptions(string[] args, out int authlevel, out int max, out double cooldown, out List<string> vip, out bool npconly)
        {
            authlevel = 0;
            max = -1;
            npconly = false;
            cooldown = 0.0;
            vip = new List<string>();
            bool error = true;
            for (var i = 3; i < args.Length; i++)
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
                else if (args[i].StartsWith("-npconly"))
                {
                    npconly = true;
                }
                else if (args[i].StartsWith("-authlevel"))
                {
                    substring = 10;
                    if (!(int.TryParse(args[i].Substring(substring), out authlevel)))
                        return string.Format("Wrong Number Value for : {0}", args[i].ToString());
                    if (authlevel > 2)
                        authlevel = 2;
                    if (authlevel < 0)
                        authlevel = 0;
                }
                else
                {
                    error = true;
                    foreach (string name in permNames)
                    {
                        if (args[i].StartsWith("-" + name))
                        {
                            if (!vip.Contains(name)) vip.Add(name);
                            error = false;
                        }
                    }
                    if (error)
                        return string.Format("Wrong Options: {0}", args[i].ToString());
                }
            }
            return true;
        }
        void cmdResetKits(BasePlayer player, string[] args)
        {
            KitsData.Clear();
            SendTheReply(player, "All kits data from players were deleted");
            SaveKitsData();
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            if (KitsConfig["autokit"] == null) return;
            object thereturn = Interface.GetMod().CallHook("canRedeemKit", new object[] { player });
            if (thereturn == null)
            {
                player.inventory.Strip();
                GiveKit(player, "autokit");
            }
        }
        void cmdRemoveKit(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                SendTheReply(player, "Kit must specify the name of the kit that you want to remove");
                return;
            }
            int authlevel = GetSourceLevel(player);
            int kitlvl = 0;
            string kitname = args[1].ToString();
            if (KitsConfig[kitname] == null)
            {
                SendTheReply(player, string.Format("The kit {0} doesn't exist", kitname));
                return;
            }

            var kitdata = (KitsConfig[kitname]) as Dictionary<string, object>;
            if (kitdata.ContainsKey("level"))
                kitlvl = (int)kitdata["level"];
            if (kitlvl > 2)
                kitlvl = 2;
            if (kitlvl > authlevel)
            {
                SendTheReply(player, "You don't have the level to remove this kit");
                return;
            }
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
            SendTheReply(player, string.Format("The kit {0} was successfully removed", kitname));
        }
        int GetKitLeft(BasePlayer player, string kitname, int max)
        {
            if (KitsData[player.userID.ToString()] == null) return max;
            var data = KitsData[player.userID.ToString()] as Dictionary<string, object>;
            if (!(data.ContainsKey(kitname))) return max;
            var currentkit = data[kitname] as Dictionary<string, object>;
            if (!(currentkit.ContainsKey("used"))) return max;
            return (max - (int)currentkit["used"]);
        }
        double GetKitTimeleft(BasePlayer player, string kitname, double max)
        {
            if (KitsData[player.userID.ToString()] == null) return 0.0;
            var data = KitsData[player.userID.ToString()] as Dictionary<string, object>;
            if (!(data.ContainsKey(kitname))) return 0.0;
            var currentkit = data[kitname] as Dictionary<string, object>;
            if (!(currentkit.ContainsKey("cooldown"))) return 0.0;
            return ((double)currentkit["cooldown"] - CurrentTime());
        }
        bool CheckIfCanRedeem(BasePlayer player, string kitname, out string reason)
        {
            reason = string.Empty;
            if (KitsConfig[kitname] == null)
            {
                reason = unknownKit;
                return false;
            }

            object thereturn = Interface.GetMod().CallHook("canRedeemKit", new object[1] { player });
            if (thereturn != null)
            {
                if (thereturn is string)
                {
                    reason = (string)thereturn;
                }
                return false;
            }

            Dictionary<string, object> kitdata = (KitsConfig[kitname]) as Dictionary<string, object>;
            double cooldown = 0.0;
            int kitleft = 1;
            int kitlvl = 0;

            if (kitdata.ContainsKey("level"))
                kitlvl = (int)kitdata["level"];
            if (kitlvl > player.net.connection.authLevel)
            {
                reason = cantUseKit;
                return false;
            }
            if (kitdata.ContainsKey("max"))
                kitleft = GetKitLeft(player, kitname, (int)(kitdata["max"]));
            if (kitleft <= 0)
            {
                reason = maxKitReached;
                return false;
            }
            foreach (string name in permNames)
            {
                if (kitdata.ContainsKey(name))
                {
                    if (!hasVip(player, name))
                    {
                        reason = cantUseKit;
                        return false;
                    }
                }
            }

            if (kitdata.ContainsKey("cooldown"))
                cooldown = GetKitTimeleft(player, kitname, (double)(kitdata["cooldown"]));
            if (cooldown > 0.0)
            {
                reason = string.Format(MessageCooldownTimer, cooldown.ToString());
                return false;
            }
            return true;
        }
        bool CheckAroundForNPC(BasePlayer player, string kitname)
        {
            var colliderArray = Physics.OverlapSphere(player.transform.position, 3f, playerLayer);
            foreach (Collider collider in colliderArray)
            {
                BasePlayer cachedPlayer = collider.GetComponentInParent<BasePlayer>();
                if (cachedPlayer == null) continue;
                if (player == cachedPlayer) continue;
                string userId = cachedPlayer.userID.ToString();
                
                if (!npcKitList.ContainsKey(userId)) continue;

                if (!((List<object>)npcKitList[userId]).Contains(kitname)) continue;
                
                /// NEED TO ADD A WALL COLLIDER CHECK TO MAKE SURE ITS NOT THROUGH WALL
                return true;
            }
            return false;

        }
        void TryGiveKit(BasePlayer player, string kitname, bool checkForNPC = false)
        {
            string reason;

            if (checkForNPC)
            {
                if (!CheckAroundForNPC(player, kitname))
                {
                    SendTheReply(player, "You must be next to the NPC to get this kit");
                    return;
                }
            }
            else
            {
                Dictionary<string, object> kitdata = (KitsConfig[kitname]) as Dictionary<string, object>;
               
                if ((bool)kitdata.ContainsKey("npconly"))
                {
                    SendTheReply(player, "You are not allowed to redeem this kit here, you must be next to the NPC");
                    return;
                }
            }
            if (!CheckIfCanRedeem(player, kitname, out reason))
            {
                SendTheReply(player, reason);
                return;
            }


            object wasGiven = GiveKit(player, kitname);
            if ((wasGiven is bool) && !((bool)wasGiven))
            {
                Puts(string.Format("An error occurred while giving the kit {0} to {1}", kitname, player.displayName.ToString()));
                return;
            }
            proccessKitGiven(player, kitname);
        }

        void proccessKitGiven(BasePlayer player, string kitname)
        {
            string userid = player.userID.ToString();
            if (KitsData[userid] == null)
            {
                (KitsData[userid]) = new Dictionary<string, object>();
            }
            Dictionary<string, object> kitdata = (KitsConfig[kitname]) as Dictionary<string, object>;
            var playerData = (KitsData[userid]) as Dictionary<string, object>;
            var currentKitData = new Dictionary<string, object>();
            bool write = false;
            if (kitdata.ContainsKey("max"))
            {
                currentKitData.Add("used", (((int)kitdata["max"] - GetKitLeft(player,kitname, (int)kitdata["max"])) + 1));
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
                SaveKitsData();
            }
        }
        bool isKit(string kitname)
        {
            if (KitsConfig[kitname] == null)
                return false;
            return true;
        }

        object GiveKit(BasePlayer player, string kitname)
        {
            if (KitsConfig[kitname] == null)
            {
                SendTheReply(player, unknownKit);
                return false;
            }
            var kitdata = (KitsConfig[kitname]) as Dictionary<string, object>;
            var kitsitems = kitdata["items"] as Dictionary<string, object>;
            List<object> wearList = kitsitems["wear"] as List<object>;
            List<object> mainList = kitsitems["main"] as List<object>;
            List<object> beltList = kitsitems["belt"] as List<object>;
            ItemContainer pref = player.inventory.containerWear;

            if (wearList.Count > 0)
            {
                pref = player.inventory.containerWear;
                foreach (object items in wearList)
                {
                    foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
                    {
                        GiveItem(player, (string)pair.Key, (int)pair.Value, pref);
                    }
                }
            }

            if (mainList.Count > 0)
            {
                pref = player.inventory.containerMain;
                foreach (object items in mainList)
                {
                    foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
                    {
                        GiveItem(player, (string)pair.Key, (int)pair.Value, pref);
                    }
                }
            }
            if (beltList.Count > 0)
            {
                pref = player.inventory.containerBelt;
                foreach (object items in beltList)
                {
                    foreach (KeyValuePair<string, object> pair in items as Dictionary<string, object>)
                    {
                        GiveItem(player, (string)pair.Key, (int)pair.Value, pref);
                    }
                }
            }
            SendTheReply(player, kitredeemed);
            return true;
        }

        public string overlayjson = @"[  
		                { 
							""name"": ""KitOverlay"",
                            ""parent"": ""Overlay"",
                            ""components"":
                            [
                                {
                                     ""type"":""UnityEngine.UI.Image"",
                                     ""color"":""0.1 0.1 0.1 1"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0 0"",
                                    ""anchormax"": ""1 1""
                                }
                            ]
                        },
                        {
                            ""parent"": ""KitOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""{msg}"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleLeft"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""{xmin} {ymin}"",
                                    ""anchormax"": ""{xmax} {ymax}""
                                }
                            ]
                        },
                    ]
                    ";
        string buttonjson = @"[
        				{
                            ""parent"": ""KitOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""{msg}"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""{xmin} {ymin}"",
                                    ""anchormax"": ""{xmax} {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""KitOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""close"":""KitOverlay"",
                                    ""command"":""kit.gui {guimsg}"",
                                    ""color"": ""{color}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""{xmin} {ymin}"",
                                    ""anchormax"": ""{xmax} {ymax}""
                                }
                            ]
                        }
                    ]
                    ";

        Dictionary<string, List<string>> playerGUI = new Dictionary<string, List<string>>();

        void DestroyAllGUI(BasePlayer player)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", "KitOverlay");
        }
        void RefreshKitPanel(BasePlayer player, List<object> kitsList, string npcId = "0")
        {
            DestroyAllGUI(player);
            int guipos = 0;
            var coverlay = npcMessageList.ContainsKey(npcId) ? overlayjson.Replace("{msg}", npcMessageList[npcId].ToString()).Replace("{xmin}", guiMsgminX.ToString()).Replace("{xmax}", guiMsgmaxX.ToString()).Replace("{ymin}", guiMsgminY.ToString()).Replace("{ymax}", guiMsgmaxY.ToString()) : overlayjson.Replace("{msg}", string.Empty);
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", coverlay);
            string reason = string.Empty;
            string localmsg = string.Empty;
            foreach (object kitname in kitsList)
            {
                string kitnamestring = kitname.ToString();
                string color = CheckIfCanRedeem(player, kitnamestring, out reason) ? "0 0.6 0 0.2" : "1 0 0 0.2";
                var kitdata = KitsConfig[kitnamestring] as Dictionary<string,object>;
                string msg = kitnamestring + " - " + (kitdata["description"] as string);
                localmsg = buttonjson.Replace("{guimsg}", string.Format("'{0}' '{1}'", kitnamestring, npcId)).Replace("{msg}", msg).Replace("{color}", color).Replace("{xmin}", guiKitminX.ToString()).Replace("{xmax}", guiKitmaxX.ToString()).Replace("{ymin}", (guiKitmaxY - (guipos + 1) * guiKitYheight).ToString()).Replace("{ymax}", (guiKitmaxY - guipos * guiKitYheight).ToString());
                CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", localmsg);
                guipos++;
            }
           
            localmsg = buttonjson.Replace("{guimsg}", "close").Replace("{msg}", "Close").Replace("{color}", "1 1 1 0.2").Replace("{xmin}", guiKitminX.ToString()).Replace("{xmax}", guiKitmaxX.ToString()).Replace("{ymin}", (guiKitmaxY - (guipos + 1) * guiKitYheight).ToString()).Replace("{ymax}", (guiKitmaxY - guipos * guiKitYheight).ToString());
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", localmsg);
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", new object[] { " " });
        }

        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (!npcKitList.ContainsKey(npc.userID.ToString())) return;
            RefreshKitPanel(player, npcKitList[npc.userID.ToString()] as List<object>, npc.userID.ToString());
        } 

        [ConsoleCommand("kit.gui")]
        void cmdConsoleKitGui(ConsoleSystem.Arg arg)
        {
            if (arg.connection == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
            if ((arg.Args == null) || (arg.Args != null && arg.Args.Length < 2))
            {
                SendReply(arg, "You are not allowed to use manually this command");
                return;
            }
            BasePlayer player = (BasePlayer)arg.connection.player;
            if (arg.Args[0] == "close")
            {
                DestroyAllGUI(player);
                return;
            }
            string kitname = arg.Args[0].Substring(1, arg.Args[0].Length - 2);
            TryGiveKit(player, kitname, true);
        }

        [ChatCommand("kit")]
        void cmdChatKits(BasePlayer player, string command, string[] args)
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
            TryGiveKit(player, args[0], false);
        }
    }
}
