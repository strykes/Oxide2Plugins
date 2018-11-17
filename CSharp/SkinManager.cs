// Reference: Rust.Workshop

using System;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Text;
using Rust;
using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Newtonsoft.Json;
using Oxide.Core.Database;

namespace Oxide.Plugins
{
    [Info("Skin Manager", "NoOne", "2.0.0")]
    class SkinManager : RustPlugin
    {
        [PluginReference]
        Plugin PlayerDatabase;

        static string defaultRestrictedSkin_Reason = "You can't use this skin.";
        //public string RemoveSpecialCharacters(string str) => Regex.Replace(str, "[^a-zA-Z0-9_.\x20]+", "", RegexOptions.Compiled);
        public string RemoveSpecialCharacters(string str) => str.Replace('[','<').Replace(']','>');

        static string redColor = "1 0 0 0.1";
        static string greenColor = "0 1 0 0.1";

        static FieldInfo SteamItems = typeof(SteamInventory).GetField("Items", BindingFlags.NonPublic | BindingFlags.Instance);

        List<ItemDefinition> skinableItems = null;

        bool hasPermission(BasePlayer player, string permissionName) => (player.IsAdmin) || permission.UserHasPermission(player.userID.ToString(), permissionName);

        List<ItemDefinition> GetSkinableItems()
        {
            if(skinableItems == null)
            {
                UpdateSkinableItems();
            }
            return skinableItems;
        }

        void UpdateSkinableItems()
        {
            skinableItems = ItemManager.itemList.Where(x => x.skins2.Length > 0).OrderBy(w => w.displayName.english).ToList();
        }

        void OnServerInitialized()
        {
            InitializePermissions();
            InitializeGroups();
            InitializeItemList();
            InitializeDefaultSkins();
            //InitializeWorkshopSkins();
            InitializeCache();
            
        }

        void OnEndLoadDefaultSkins()
        {
            LoadCfg();
            GetGroupConfigs();
        }

        #region initialize
        void InitializePermissions()
        {
            permission.RegisterPermission("skinmanager.admin", this);
            permission.RegisterPermission("skinmanager.use", this);
            permission.RegisterPermission("skinmanager.schema", this);
            permission.RegisterPermission("skinmanager.workshop", this);
        }

        void OnServerSave()
        { 
            SaveCfg();
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player.net.connection.ownerid != 0L)
            {
                if (!permission.UserHasGroup(player.userID.ToString(), "steam"))
                {
                    permission.AddUserGroup(player.userID.ToString(), "steam");
                }
            }
        }

        void InitializeGroups()
        {
            if (!permission.GroupExists("steam"))
            {
                permission.CreateGroup("steam", "steam", 0);
            }


            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                DeleteAllUI(player);
                if (player.net.connection.ownerid != 0L)
                {
                    if (!permission.UserHasGroup(player.userID.ToString(), "steam"))
                    {
                        permission.AddUserGroup(player.userID.ToString(), "steam");
                    }
                }
            }
        }
        string unfavicon = "https://i.imgur.com/UQUmQSL.png";
        string favicon = "https://i.imgur.com/RJCNRE0.png";
        void InitializeCache()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                InitializePlayer(player.userID);
            }
        }

        Dictionary<ulong, PlayerSkinManager> cachedSkins = new Dictionary<ulong, PlayerSkinManager>();

        class PlayerSkinManager
        {
            public Dictionary<int, List<int>> favorites = new Dictionary<int, List<int>>();
            public Dictionary<int, int> skincraft = new Dictionary<int, int>();
        }

        void InitializePlayer(ulong userID)
        {

            if (cachedSkins.ContainsKey(userID)) return;

            if (PlayerDatabase != null)
            {
                object pdatabase = PlayerDatabase.Call("GetPlayerDataRaw", userID.ToString(), "SkinManager");
                if (pdatabase == null)
                {
                    cachedSkins.Add(userID, new PlayerSkinManager());
                }
                else
                {
                    cachedSkins.Add(userID, JsonConvert.DeserializeObject<PlayerSkinManager>((string)pdatabase));
                }
            }
            else
            {
                cachedSkins.Add(userID, new PlayerSkinManager());
            }
        }
        #endregion



        #region groups
        int GetPlayerGroup(BasePlayer player)
        {
            for (int i = 0; i < skinConfigs.Count; i++)
            {
                if (skinConfigs[i].groupType == GroupType.Admin)
                {
                    if (player.net.connection.authLevel == 2) return i;
                }
                else if (skinConfigs[i].groupType == GroupType.Moderator)
                {
                    if (player.net.connection.authLevel == 1) return i;
                }
                else
                {
                    if (permission.UserHasGroup(player.userID.ToString(), skinConfigs[i].oxideGroup)) return i;
                }

            }
            return -1;
        }

        #endregion

        #region ItemList
        // ItemList 

        Dictionary<string, int> ItemLists = new Dictionary<string, int>();

        void InitializeItemList()
        {
            // reset all default skins
            foreach (var it in ItemManager.GetItemDefinitions())
            {
                it._skins2 = new Facepunch.Steamworks.Inventory.Definition[0];
            }

            // initialize skin tag name to item shortname
            ItemLists.Add("bandana", ItemManager.FindItemDefinition("mask.bandana").itemid);
            ItemLists.Add("balaclava", ItemManager.FindItemDefinition("mask.balaclava").itemid);
            ItemLists.Add("beenie hat", ItemManager.FindItemDefinition("hat.beenie").itemid);
            ItemLists.Add("burlap pants", ItemManager.FindItemDefinition("burlap.trousers").itemid);
            ItemLists.Add("burlap headwrap", ItemManager.FindItemDefinition("burlap.headwrap").itemid);
            ItemLists.Add("leather gloves", ItemManager.FindItemDefinition("burlap.gloves").itemid);
            ItemLists.Add("burlap shirt", ItemManager.FindItemDefinition("burlap.shirt").itemid);
            ItemLists.Add("burlap shoes", ItemManager.FindItemDefinition("burlap.shoes").itemid);

            ItemLists.Add("boonie hat", ItemManager.FindItemDefinition("hat.boonie").itemid);
            ItemLists.Add("cap", ItemManager.FindItemDefinition("hat.cap").itemid);
            ItemLists.Add("collared shirt", ItemManager.FindItemDefinition("shirt.collared").itemid);
            ItemLists.Add("coffee can helmet", ItemManager.FindItemDefinition("coffeecan.helmet").itemid);
            ItemLists.Add("deer skull mask", ItemManager.FindItemDefinition("deer.skull.mask").itemid);
            ItemLists.Add("hide shoes", ItemManager.FindItemDefinition("attire.hide.boots").itemid);
            ItemLists.Add("hide skirt", ItemManager.FindItemDefinition("attire.hide.skirt").itemid);
            ItemLists.Add("hide shirt", ItemManager.FindItemDefinition("attire.hide.vest").itemid);
            ItemLists.Add("hide pants", ItemManager.FindItemDefinition("attire.hide.pants").itemid);
            ItemLists.Add("hide poncho", ItemManager.FindItemDefinition("attire.hide.poncho").itemid);
            ItemLists.Add("hide halterneck", ItemManager.FindItemDefinition("attire.hide.helterneck").itemid);

            ItemLists.Add("hoodie", ItemManager.FindItemDefinition("hoodie").itemid);
            ItemLists.Add("long tshirt", ItemManager.FindItemDefinition("tshirt.long").itemid);
            ItemLists.Add("metal chest plate", ItemManager.FindItemDefinition("metal.plate.torso").itemid);
            ItemLists.Add("metal facemask", ItemManager.FindItemDefinition("metal.facemask").itemid);
            ItemLists.Add("miner hat", ItemManager.FindItemDefinition("hat.miner").itemid);
            ItemLists.Add("pants", ItemManager.FindItemDefinition("pants").itemid);
            ItemLists.Add("roadsign pants", ItemManager.FindItemDefinition("roadsign.jacket").itemid);
            ItemLists.Add("roadsign vest", ItemManager.FindItemDefinition("roadsign.kilt").itemid);
            ItemLists.Add("riot helmet", ItemManager.FindItemDefinition("riot.helmet").itemid);
            ItemLists.Add("snow jacket", ItemManager.FindItemDefinition("jacket.snow").itemid);
            ItemLists.Add("shorts", ItemManager.FindItemDefinition("pants.shorts").itemid);
            ItemLists.Add("tank top", ItemManager.FindItemDefinition("shirt.tanktop").itemid);
            ItemLists.Add("tshirt", ItemManager.FindItemDefinition("tshirt").itemid);
            ItemLists.Add("vagabond jacket", ItemManager.FindItemDefinition("jacket").itemid);
            ItemLists.Add("work boots", ItemManager.FindItemDefinition("shoes.boots").itemid);
            ItemLists.Add("ak47", ItemManager.FindItemDefinition("rifle.ak").itemid);
            ItemLists.Add("bolt rifle", ItemManager.FindItemDefinition("rifle.bolt").itemid);
            ItemLists.Add("bone club", ItemManager.FindItemDefinition("bone.club").itemid);
            ItemLists.Add("bone knife", ItemManager.FindItemDefinition("knife.bone").itemid);
            ItemLists.Add("crossbow", ItemManager.FindItemDefinition("crossbow").itemid);
            ItemLists.Add("double barrel shotgun", ItemManager.FindItemDefinition("shotgun.double").itemid);
            ItemLists.Add("eoka pistol", ItemManager.FindItemDefinition("pistol.eoka").itemid);
            ItemLists.Add("f1 grenade", ItemManager.FindItemDefinition("grenade.f1").itemid);
            ItemLists.Add("longsword", ItemManager.FindItemDefinition("longsword").itemid);
            ItemLists.Add("pump shotgun", ItemManager.FindItemDefinition("shotgun.pump").itemid);
            ItemLists.Add("salvaged hammer", ItemManager.FindItemDefinition("hammer.salvaged").itemid);
            ItemLists.Add("salvaged icepick", ItemManager.FindItemDefinition("icepick.salvaged").itemid);
            ItemLists.Add("satchel charge", ItemManager.FindItemDefinition("explosive.satchel").itemid);
            ItemLists.Add("semi-automatic pistol", ItemManager.FindItemDefinition("pistol.semiauto").itemid);
            ItemLists.Add("waterpipe shotgun", ItemManager.FindItemDefinition("shotgun.waterpipe").itemid);
            ItemLists.Add("custom smg", ItemManager.FindItemDefinition("smg.2").itemid);
            ItemLists.Add("python", ItemManager.FindItemDefinition("pistol.python").itemid);
            ItemLists.Add("stone hatchet", ItemManager.FindItemDefinition("stonehatchet").itemid);
            ItemLists.Add("stone pick axe", ItemManager.FindItemDefinition("stone.pickaxe").itemid);
            ItemLists.Add("sword", ItemManager.FindItemDefinition("salvaged.sword").itemid);
            ItemLists.Add("thompson", ItemManager.FindItemDefinition("smg.thompson").itemid);
            ItemLists.Add("hammer", ItemManager.FindItemDefinition("hammer").itemid);
            ItemLists.Add("rock", ItemManager.FindItemDefinition("rock").itemid);
            ItemLists.Add("hatchet", ItemManager.FindItemDefinition("hatchet").itemid);
            ItemLists.Add("pick axe", ItemManager.FindItemDefinition("pickaxe").itemid);
            ItemLists.Add("revolver", ItemManager.FindItemDefinition("pistol.revolver").itemid);
            ItemLists.Add("rocket launcher", ItemManager.FindItemDefinition("rocket.launcher").itemid);
            ItemLists.Add("semi-automatic rifle", ItemManager.FindItemDefinition("rifle.semiauto").itemid);
            ItemLists.Add("lr300", ItemManager.FindItemDefinition("rifle.lr300").itemid);
            ItemLists.Add("armored door", ItemManager.FindItemDefinition("door.hinged.toptier").itemid);
            ItemLists.Add("concrete barricade", ItemManager.FindItemDefinition("barricade.concrete").itemid);
            ItemLists.Add("large wood box", ItemManager.FindItemDefinition("box.wooden.large").itemid);
            ItemLists.Add("reactive target", ItemManager.FindItemDefinition("target.reactive").itemid);
            ItemLists.Add("sandbag barricade", ItemManager.FindItemDefinition("barricade.sandbags").itemid);
            ItemLists.Add("sleeping bag", ItemManager.FindItemDefinition("sleepingbag").itemid);
            ItemLists.Add("sheet metal door", ItemManager.FindItemDefinition("door.hinged.metal").itemid);
            //ItemLists.Add("water purifier", ItemManager.FindItemDefinition("rock").itemid);
            ItemLists.Add("wood storage box", ItemManager.FindItemDefinition("box.wooden").itemid);
            ItemLists.Add("wooden door", ItemManager.FindItemDefinition("door.hinged.wood").itemid);
            ItemLists.Add("acoustic guitar", ItemManager.FindItemDefinition("fun.guitar").itemid);
            ItemLists.Add("rug", ItemManager.FindItemDefinition("rug").itemid);
            ItemLists.Add("bucket helmet", ItemManager.FindItemDefinition("bucket.helmet").itemid);
            ItemLists.Add("bearskin rug", ItemManager.FindItemDefinition("rug.bear").itemid);
            ItemLists.Add("furnace", ItemManager.FindItemDefinition("furnace").itemid);
            ItemLists.Add("mp5", ItemManager.FindItemDefinition("smg.mp5").itemid);
            ItemLists.Add("spinning wheel", ItemManager.FindItemDefinition("spinner.wheel").itemid);
            //ItemLists.Add("vending machine", ItemManager.FindItemDefinition("furnace").itemid);
            ItemLists.Add("fridge", ItemManager.FindItemDefinition("fridge").itemid);
            ItemLists.Add("chair", ItemManager.FindItemDefinition("chair").itemid);
            ItemLists.Add("table", ItemManager.FindItemDefinition("table").itemid);

            //ItemLists.Add("garage door", ItemManager.FindItemDefinition("rock").itemid);
            ItemLists.Add("locker", ItemManager.FindItemDefinition("locker").itemid);

            ItemLists = ItemLists.OrderBy(w => w.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
        }
        #endregion

        #region default skins
        // DEFAULT SKINS

        void InitializeDefaultSkins()
        {
            webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", string.Empty, (code, response) =>
            {
                if (!(response == null && code == 200))
                {
                    Rust.Workshop.ItemSchema schm = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response);
                    Rust.Workshop.ItemSchema.Item[] items = schm.items;
                    List<Facepunch.Steamworks.Inventory.Definition> defs = new List<Facepunch.Steamworks.Inventory.Definition>();
                    foreach (Rust.Workshop.ItemSchema.Item item in items)
                    {
                        if (item.itemshortname != string.Empty && item.type != "None")
                        {
                            var newitem = Global.SteamServer.Inventory.CreateDefinition(!string.IsNullOrEmpty(item.workshopdownload) ? int.Parse(item.workshopdownload) : (int)item.itemdefid);
                            newitem.Name = item.name;
                            newitem.Description = RemoveSpecialCharacters(item.description);
                            newitem.SetProperty("itemshortname", item.itemshortname);
                            if (!string.IsNullOrEmpty(item.workshopid)) newitem.SetProperty("workshopid", item.workshopid);
                            if (!string.IsNullOrEmpty(item.workshopdownload)) newitem.SetProperty("workshopdownload", item.workshopdownload);
                            newitem.SetProperty("image", item.icon_url);
                            newitem.SetProperty("image_large", item.icon_url_large);
                            newitem.SetProperty("source", "schema");
                            defs.Add(newitem);
                        }
                    }
                    Rust.Global.SteamServer.Inventory.Definitions = defs.ToArray();

                    foreach (var item in ItemManager.itemList)
                    {
                        item._skins2 = item.skins2.Concat((from x in Rust.Global.SteamServer.Inventory.Definitions
                                                           where (x.GetStringProperty("itemshortname") == item.shortname)
                                                           select x).OrderBy(w => w.Name)).ToArray<Facepunch.Steamworks.Inventory.Definition>();
                    }

                    Debug.Log(string.Format("SkinManager: Downloaded {0} default skins.", Rust.Global.SteamServer.Inventory.Definitions.Length.ToString()));
                    OnEndLoadDefaultSkins();
                }
            }, this);
        }
        #endregion

        #region workshop
        // WORKSHOP ITEMS
        static Core.SQLite.Libraries.SQLite Sqlite = Interface.GetMod().GetLibrary<Core.SQLite.Libraries.SQLite>();
        static Connection Sqlite_conn;
        static string sqlitename = "skinmanager.db";

        void LoadSQLite()
        {
            try
            {
                Sqlite_conn = Sqlite.OpenDb(sqlitename, this);
                if (Sqlite_conn == null)
                {
                    FatalError("Couldn't open the SQLite SkinWorkshop. ");
                    return;
                }
                Sqlite.Insert(Core.Database.Sql.Builder.Append("CREATE TABLE IF NOT EXISTS SkinWorkshop ( id INTEGER NOT NULL PRIMARY KEY UNIQUE, workshopdownload TEXT, workshopid TEXT, name TEXT, itemshortname TEXT, description TEXT, source TEXT);"), Sqlite_conn);

            }
            catch (Exception e)
            {
                FatalError(e.Message);
            }
        }

        void FatalError(string msg)
        {
            Interface.Oxide.LogError(msg);
        }

        void InitializeWorkshopSkins()
        {
            LoadSQLite();
            Sqlite.Insert(Core.Database.Sql.Builder.Append("DELETE from SkinWorkshop;"), Sqlite_conn);
            ServerMgr.Instance.StartCoroutine(RefreshWorkshopSkins());

        }
        public IEnumerator RefreshWorkshopSkins()
        {
            Facepunch.Steamworks.Workshop.Query workshopQuery = Global.SteamServer.Workshop.CreateQuery();
            workshopQuery.Page = 1;
            workshopQuery.PerPage = 500000;
            workshopQuery.RequireTags.Add("Version3");
            workshopQuery.RequireAllTags = true;
            workshopQuery.Run();
            float init = Time.realtimeSinceStartup;
            yield return new WaitWhile(new Func<bool>(() => workshopQuery.IsRunning));

            Interface.Oxide.LogInfo("SkinManager: Downloaded " + workshopQuery.Items.Length.ToString() + " skins from the workshop in " + (Time.realtimeSinceStartup - init).ToString() + "s");

            List<Facepunch.Steamworks.Inventory.Definition> InventoryDefinitions = new List<Facepunch.Steamworks.Inventory.Definition>();
            bool flag = false;

            var itemshash = new Hash<string, int>();

            foreach (var item in workshopQuery.Items)
            {
                if (item != null)
                {
                    int targetId = -1;
                    flag = false;
                    foreach (var tag in item.Tags)
                    {
                        if (ItemLists.ContainsKey(tag))
                        {
                            targetId = ItemLists[tag];
                            flag = true;
                        }
                    }
                    if (!flag)
                    {
                        Debug.Log("Couldn't find item short name for: " + item.Title + ": " + string.Join(" - ", item.Tags));
                        continue;
                    }

                    ItemDefinition targetItem = ItemManager.FindItemDefinition(targetId);
                    if (targetItem != null)
                    {

                        itemshash[targetItem.displayName.english]++;
                        var newitem = Global.SteamServer.Inventory.CreateDefinition(int.Parse(item.Id.ToString()));
                        newitem.Name = item.Title;
                        newitem.Description = RemoveSpecialCharacters(item.Description);
                        newitem.SetProperty("itemshortname", targetItem.shortname);
                        newitem.SetProperty("workshopid", item.OwnerId.ToString());
                        newitem.SetProperty("image", item.PreviewImageUrl.ToString());
                        newitem.SetProperty("workshopdownload", item.Id.ToString());
                        newitem.SetProperty("source", "workshop");
                        InventoryDefinitions.Add(newitem);
                        Sqlite.Insert(Core.Database.Sql.Builder.Append(string.Format("INSERT INTO `SkinWorkshop`(`workshopdownload`,`workshopid`,`name`,`itemshortname`,`description`,`source`) VALUES ( '{0}', '{1}', '{2}', '{3}', '{4}', '{5}');", MySql.Data.MySqlClient.MySqlHelper.EscapeString(item.Id.ToString()), MySql.Data.MySqlClient.MySqlHelper.EscapeString(item.OwnerId.ToString()), MySql.Data.MySqlClient.MySqlHelper.EscapeString(item.Title), MySql.Data.MySqlClient.MySqlHelper.EscapeString(targetItem.shortname), MySql.Data.MySqlClient.MySqlHelper.EscapeString(RemoveSpecialCharacters(item.Description)), MySql.Data.MySqlClient.MySqlHelper.EscapeString("workshop"))), Sqlite_conn);
                    }
                }
                workshopQuery.Dispose();
            }
            Debug.Log(InventoryDefinitions.Count.ToString() + " skins were collected from the workshop");
        }

        #endregion

        #region UI
        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string parent, string panelName, string color, string aMin, string aMax, bool useCursor)
            {
                var NewElement = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return NewElement;
            }
            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateImage(ref CuiElementContainer container, string panel, string url, string name, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiElement
                {
                    Name = name,
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = url
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = aMin,
                            AnchorMax = aMax
                        }
                    }
                });
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);

            }
            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }
        }
        #endregion

        List<SkinConfigs> skinConfigs = new List<SkinConfigs>();

        void LoadCfg()
        {
            try
            {
                skinConfigs = Interface.GetMod().DataFileSystem.ReadObject<List<SkinConfigs>>("SkinManager_Cfg");
            }
            catch
            {
                skinConfigs = new List<SkinConfigs>();
            }
            foreach(var s in skinConfigs)
            {
                s.InitializeSkins();
            }
        }
        void SaveCfg()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SkinManager_Cfg", skinConfigs);
        }


        Dictionary<ulong, PlayerSkinManager> playerSkinManager = new Dictionary<ulong, PlayerSkinManager>();

        
        public enum GroupType
        {
            Admin,
            Moderator,
            Group
        }

        public class SkinConfigs
        { 
            public GroupType groupType;
            public string oxideGroup;

            public bool SkinManager;
            public bool SkinChanger;

            public bool ChooseSkins;
            public Dictionary<int, int> DefaultSkins;
            public bool RandomSkins;

            public bool allowDefaultSkins;
            public bool allowWorkshopSkins;

            public List<int> RestrictedSkins;

            public SkinConfigs() {  }

            public SkinConfigs(GroupType groupType, string oxideGroup)
            {
                this.groupType = groupType;
                this.oxideGroup = oxideGroup;
                SkinManager = true;
                SkinChanger = true;
                ChooseSkins = true;
                DefaultSkins = new Dictionary<int, int>();
                RandomSkins = false;
                allowDefaultSkins = true;
                allowWorkshopSkins = true;
                RestrictedSkins = new List<int>();
                InitializeSkins();
            }

            public void InitializeSkins()
            {
                cachedRestrictedSkins = new Dictionary<int, bool>();
                foreach (ItemDefinition itemdef in ItemManager.itemList)
                {
                    if (itemdef.skins2 != null)
                    {
                        foreach (var skin in itemdef.skins2)
                        {
                            if (!cachedRestrictedSkins.ContainsKey(skin.Id))
                                cachedRestrictedSkins.Add(skin.Id, !IsSkinAllowed(this, itemdef.itemid, skin.Id));
                        }
                    }
                }
            }

            [JsonIgnore]
            public Dictionary<int, bool> cachedRestrictedSkins;
        }
        int GetCurrentSkin(BasePlayer player, int itemID, bool randomizable = false)
        {
            int currentGroup = GetPlayerGroup(player);
            if (currentGroup == -1)
            {
                return 0;
            }
            return GetCurrentSkinByGroup(player, GetGroupConfigs()[currentGroup], itemID);
        }


        int GetCurrentSkinByGroup(BasePlayer player, SkinConfigs currentGroup, int itemID, bool randomizable = false)
        {
            int targetSkin = 0;
            if (currentGroup.ChooseSkins)
            {
                if (GetChoosenSkin(player.userID, itemID, out targetSkin))
                {
                    return targetSkin;
                }
            }
            if (currentGroup.DefaultSkins.ContainsKey(itemID))
            {
                targetSkin = currentGroup.DefaultSkins[itemID];
            }
            else if (currentGroup.DefaultSkins.ContainsKey(itemID))
            {
                targetSkin = currentGroup.DefaultSkins[itemID];
            }
            else if (randomizable && currentGroup.RandomSkins)
            {
                targetSkin = GetRandomSkin(itemID, currentGroup.allowDefaultSkins, currentGroup.allowWorkshopSkins, currentGroup.RestrictedSkins);
            }
            return targetSkin;
        }

        int GetRandomSkin(int itemID, bool normalSkins, bool workshopSkins, List<int> restricted)
        {
            IEnumerable<Facepunch.Steamworks.Inventory.Definition> potentialSkins = GetUsuableSkins(ItemManager.FindItemDefinition(itemID), normalSkins, workshopSkins).Where(x => !restricted.Contains(x.Id));
            if (potentialSkins.Count() == 0) return 0;
            return potentialSkins.ElementAt(Oxide.Core.Random.Range(0, potentialSkins.Count() - 1)).Id;
        }

        bool GetChoosenSkin(ulong userid, int itemID, out int skinID)
        {
            skinID = 0;
            if (PlayerDatabase == null) return false;

            InitializePlayer(userid);

            if (cachedSkins[userid].skincraft.ContainsKey(itemID))
            {
                skinID = cachedSkins[userid].skincraft[itemID];
                return true;
            }
            return false;
        }

        List<SkinConfigs> GetGroupConfigs()
        {
            if (skinConfigs.Where(x => x.groupType == GroupType.Admin).Count() == 0)
            {
                skinConfigs.Add(new SkinConfigs(GroupType.Admin, string.Empty));
            }
            if (skinConfigs.Where(x => x.groupType == GroupType.Moderator).Count() == 0)
            {
                skinConfigs.Add(new SkinConfigs(GroupType.Moderator, string.Empty));
            }
            foreach (string oxideGroup in permission.GetGroups())
            {
                if (skinConfigs.Where(x => x.groupType == GroupType.Group && x.oxideGroup == oxideGroup).Count() == 0)
                {
                    skinConfigs.Add(new SkinConfigs(GroupType.Group, oxideGroup));
                }
            }
            return skinConfigs;
        }

        void SkinManagerUIPlayer(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkinPageContainer");



            CuiElementContainer page_container = UI.CreateElementContainer("SkinManager", "SkinPageContainer", "0.1 0.1 0.1 0", "0 0.05", "1 0.90", false);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", "<<", 16, "0 0", "0.1 0.05", "skin.select skinmanager itemlist previous", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", ">>", 16, "0.1 0", "0.2 0.05", "skin.select skinmanager itemlist next", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", "<<", 16, "0.4 0", "0.7 0.05", "skin.select skinmanager skinlist previous", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", ">>", 16, "0.7 0", "1 0.05", "skin.select skinmanager skinlist next", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", "Remove Skin", 16, "0.2 0", "0.4 0.05", "skin.select skinmanager skinclear", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", "Default Skin", 16, "0.2 0.05", "0.4 0.1", "skin.select skinmanager skindefault", TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, page_container);

            SkinManagerUIPlayer_UpdateItems(player);
        }

        void SkinManagerUIPlayer_UpdateItems(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkinPlayerItemList");
            CuiElementContainer page_container = UI.CreateElementContainer("SkinPageContainer", "SkinPlayerItemList", "0.1 0.1 0.1 0", "0 0.05", "0.2 1", false);

            List<ItemDefinition> listItems = GetSkinableItems();

            if (playerUI[player]["playeritemlistpage"] + 20 >= listItems.Count) playerUI[player]["playeritemlistpage"] = listItems.Count - 20;
            if (playerUI[player]["playeritemlistpage"] < 0) playerUI[player]["playeritemlistpage"] = 0;

            float o = 1f;
            for (int i = playerUI[player]["playeritemlistpage"]; i < playerUI[player]["playeritemlistpage"] + 20; i++)
            {
                if (i >= listItems.Count) break;
                UI.CreateButton(ref page_container, "SkinPlayerItemList", "0.5 0.5 0.5 0.1", listItems[i].displayName.english, 14, "0 " + (o - 0.05f).ToString(), "1 " + o.ToString(), string.Format("skin.select skinmanager item {0}", listItems[i].itemid.ToString()), TextAnchor.MiddleCenter);
                o -= 0.05f;
            }

            CuiHelper.AddUi(player, page_container);
        }

        Facepunch.Steamworks.Inventory.Definition[] GetUsuableSkins(ItemDefinition item, bool normal, bool workshop)
        {
            return item.skins2;
        }

        void SkinManagerUIPlayer_UpdateSkins(BasePlayer player)
        {
            SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["playergroup"]];
            CuiHelper.DestroyUi(player, "SkinPlayerSkinList");
            CuiElementContainer page_container = UI.CreateElementContainer("SkinPageContainer", "SkinPlayerSkinList", "0.1 0.1 0.1 0", "0.4 0.05", "1 1", false);

            ItemDefinition targetItem = ItemManager.FindItemDefinition(playerUI[player]["playeritem"]);
            Facepunch.Steamworks.Inventory.Definition[] usuableSkins = GetUsuableSkins(targetItem, targetConfig.allowDefaultSkins, targetConfig.allowWorkshopSkins);

            if (playerUI[player]["playerskinlistpage"] + 9 > usuableSkins.Length) playerUI[player]["playerskinlistpage"] = usuableSkins.Length - 10;
            if (playerUI[player]["playerskinlistpage"] < 0) playerUI[player]["playerskinlistpage"] = 0;

            float o = 1f;
            string color = "0.5 0.5 0.5 0";
            string description = string.Empty;
            for (int i = playerUI[player]["playerskinlistpage"]; i < playerUI[player]["playerskinlistpage"] + 10; i++)
            {
                if (i >= usuableSkins.Length) break;
                if (targetConfig.cachedRestrictedSkins[usuableSkins[i].Id])
                {
                    color = redColor;
                    description = defaultRestrictedSkin_Reason;
                }
                else
                {
                    color = "0.5 0.5 0.5 0";
                    description = usuableSkins[i].Description;
                }

                UI.CreateImage(ref page_container, "SkinPlayerSkinList", unfavicon, "IconUnfav", "0 " + (o - 0.075f).ToString(), "0.05 " + (o - 0.025f).ToString());
                UI.CreateImage(ref page_container, "SkinPlayerSkinList", usuableSkins[i].GetStringProperty("image"), "SkinIMG", "0.06 " + (o - 0.1f).ToString(), "0.16 " + o.ToString());
                UI.CreateLabel(ref page_container, "SkinPlayerSkinList", "1 1 1 1", usuableSkins[i].Name, 14, "0.18 " + (o - 0.1f).ToString(), "0.35 " + o.ToString(), TextAnchor.MiddleLeft);
                UI.CreateLabel(ref page_container, "SkinPlayerSkinList", "0.8 0.8 0.8 1", description, 14, "0.36 " + (o - 0.1f).ToString(), "1 " + o.ToString(), TextAnchor.MiddleLeft);
                UI.CreateButton(ref page_container, "SkinPlayerSkinList", color, string.Empty, 14, "0 " + (o - 0.1f).ToString(), "1 " + o.ToString(), string.Format("skin.select skinmanager skin {0}", usuableSkins[i].Id.ToString()), TextAnchor.MiddleCenter);
                o -= 0.1f;
            }
            CuiHelper.AddUi(player, page_container);
        }

        void SkinManagerUIPlayer_UpdateCurrentSkin(BasePlayer player)
        {
            SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["playergroup"]];
            CuiHelper.DestroyUi(player, "SkinPlayerSkinCurrent");
            CuiElementContainer page_container = UI.CreateElementContainer("SkinPageContainer", "SkinPlayerSkinCurrent", "0.1 0.1 0.1 0", "0.2 0.1", "0.4 1", false);

            ItemDefinition currentItem = ItemManager.FindItemDefinition(playerUI[player]["playeritem"]);
            if (playerUI[player]["playerskinid"] == 0)
                playerUI[player]["playerskinid"] = GetCurrentSkinByGroup(player, targetConfig, playerUI[player]["playeritem"], false);
            if (playerUI[player]["playerskinid"] != 0)
            {
                IEnumerable<Facepunch.Steamworks.Inventory.Definition> skins = currentItem.skins2.Where(w => w.Id == playerUI[player]["playerskinid"]);
                if (skins.Count() > 0)
                {
                    Facepunch.Steamworks.Inventory.Definition currentItemSkin = skins.ElementAt(0);
                    UI.CreateImage(ref page_container, "SkinPlayerSkinCurrent", currentItemSkin.GetStringProperty("image_large"), "SkinIMG", "0 0.3", "1 0.7");
                }
            }

            CuiHelper.AddUi(player, page_container);
        }

        void SetSkin(BasePlayer player, int itemID, int skinID)
        {
            if (cachedSkins[player.userID].skincraft.ContainsKey(itemID))
            {
                if (cachedSkins[player.userID].skincraft[itemID] == skinID) return;
                cachedSkins[player.userID].skincraft.Remove(itemID);
            }
            if (skinID != -1)
            {
                cachedSkins[player.userID].skincraft.Add(itemID, skinID);
            }
            if (PlayerDatabase != null)
            {
                PlayerDatabase.Call("SetPlayerData", player.userID.ToString(), "SkinManager", cachedSkins[player.userID]);
            }
        }

        bool IsSkinAllowed(BasePlayer player, int playerGroup, int itemID, int skinID, out string reason)
        {
            reason = defaultRestrictedSkin_Reason;
            SkinConfigs targetConfig = GetGroupConfigs()[playerGroup];
            return IsSkinAllowed(targetConfig, itemID, skinID);
        }

        static bool IsSkinAllowed(SkinConfigs targetConfig, int itemID, int skinID)
        {
            if (!targetConfig.ChooseSkins)
            {
                
                return false;
            }
            ItemDefinition targetItem = ItemManager.FindItemDefinition(itemID);
            var targetSkin = targetItem.skins2.Where(w => w.Id == skinID).ElementAt(0);
            if (!targetConfig.allowDefaultSkins && targetSkin.GetStringProperty("source") == "schema")
            {
                return false;
            }
            if (!targetConfig.allowWorkshopSkins && targetSkin.GetStringProperty("source") == "workshop")
            {
                return false;
            }
            if (targetConfig.RestrictedSkins.Contains(skinID))
            {
                return false;
            }
            return true;
        }

        #region AdminUI

        void SkinManagerUIAdmin_UpdateGroups(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkinAdminGroup");
            CuiElementContainer group_container = UI.CreateElementContainer("SkinAdminGroupPage", "SkinAdminGroup", "0.1 0.1 0.1 0", "0 0", "1 1", false);
            List<SkinConfigs> Groups = GetGroupConfigs();

            if (playerUI[player]["admingroup"] + 9 >= Groups.Count) playerUI[player]["admingroup"] = Groups.Count - 9;
            if (playerUI[player]["admingroup"] < 0) playerUI[player]["admingroup"] = 0;

            float o = 1f;
            for (int i = playerUI[player]["admingroup"]; i < playerUI[player]["admingroup"] + 9; i++)
            {
                if (i >= Groups.Count) break;
                string name = Groups[i].groupType == GroupType.Admin ? "Admin" : Groups[i].groupType == GroupType.Moderator ? "Moderator" : Groups[i].oxideGroup;
                string grouptype = ((int)Groups[i].groupType).ToString();
                UI.CreateButton(ref group_container, "SkinAdminGroup", "0.5 0.5 0.5 0.1", name, 14, "0 " + (o - 0.1f).ToString(), "0.9 " + o.ToString(), string.Format("skin.select admin group select {0} {1}", grouptype, name), TextAnchor.MiddleCenter);
                UI.CreateButton(ref group_container, "SkinAdminGroup", "0.5 0.5 0.5 0.1", "+", 14, "0.9 " + (o - 0.05f).ToString(), "1 " + o.ToString(), string.Format("skin.select admin group up {0} {1}", grouptype, name), TextAnchor.MiddleCenter);
                UI.CreateButton(ref group_container, "SkinAdminGroup", "0.5 0.5 0.5 0.1", "-", 14, "0.9 " + (o - 0.1f).ToString(), "1 " + (o - 0.05f).ToString(), string.Format("skin.select admin group down {0} {1}", grouptype, name), TextAnchor.MiddleCenter);
                o -= 0.1f;
            }
            CuiHelper.AddUi(player, group_container);
        }

        void SkinManagerUIAdmin_UpdateGroupPage(BasePlayer player)
        {
            SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["admingroupselect"]];
            CuiHelper.DestroyUi(player, "SkinAdminGroupCfgPage");
            CuiElementContainer page_container = UI.CreateElementContainer("SkinPageContainer", "SkinAdminGroupCfgPage", "0.1 0.1 0.1 0", "0.2 0.05", "0.4 1", false);

            UI.CreateButton(ref page_container, "SkinAdminGroupCfgPage", targetConfig.ChooseSkins ? greenColor : redColor, "Choose skins", 14, "0 0.9", "1 1", string.Format("skin.select admin group set ChooseSkins {0}", (!targetConfig.ChooseSkins).ToString()), TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinAdminGroupCfgPage", "0.5 0.5 0.5 0.1", "Default skins", 14, "0 0.8", "1 0.9", string.Format("skin.select admin group setDefaultSkins"), TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinAdminGroupCfgPage", targetConfig.RandomSkins ? greenColor : redColor, "Random skins", 14, "0 0.7", "1 0.8", string.Format("skin.select admin group set RandomSkins {0}", (!targetConfig.RandomSkins).ToString()), TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinAdminGroupCfgPage", targetConfig.allowDefaultSkins ? greenColor : redColor, "Allow normal skins", 14, "0 0.6", "1 0.7", string.Format("skin.select admin group set allowDefaultSkins {0}", (!targetConfig.allowDefaultSkins).ToString()), TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinAdminGroupCfgPage", targetConfig.allowWorkshopSkins ? greenColor : redColor, "Allow workshop skins", 14, "0 0.5", "1 0.6", string.Format("skin.select admin group set allowWorkshopSkins {0}", (!targetConfig.allowWorkshopSkins).ToString()), TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinAdminGroupCfgPage", "0.5 0.5 0.5 0.1", "Allowed skins", 14, "0 0.4", "1 0.5", string.Format("skin.select admin group setRestrictedSkins"), TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinAdminGroupCfgPage", targetConfig.SkinManager ? greenColor : redColor, "SkinManager Panel", 14, "0 0.3", "1 0.4", string.Format("skin.select admin group set SkinManager {0}", (!targetConfig.SkinManager).ToString()), TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinAdminGroupCfgPage", targetConfig.SkinChanger ? greenColor : redColor, "SkinChanger Panel", 14, "0 0.2", "1 0.3", string.Format("skin.select admin group set SkinChanger {0}", (!targetConfig.SkinChanger).ToString()), TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, page_container);
        }

        void SkinManagerUIAdmin_UpdateGroupItemList(BasePlayer player)
        {
            SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["admingroupselect"]];
            CuiHelper.DestroyUi(player, "SkinAdminGroupItemList");
            CuiElementContainer page_container = UI.CreateElementContainer("SkinPageContainer", "SkinAdminGroupItemList", "0.1 0.1 0.1 0", "0.4 0.05", "0.6 1", false);

            List<ItemDefinition> listItems = GetSkinableItems();

            if (playerUI[player]["admingroupitemlistpage"] + 20 >= listItems.Count) playerUI[player]["admingroupitemlistpage"] = listItems.Count - 20;
            if (playerUI[player]["admingroupitemlistpage"] < 0) playerUI[player]["admingroupitemlistpage"] = 0;

            float o = 1f;
            for (int i = playerUI[player]["admingroupitemlistpage"]; i < playerUI[player]["admingroupitemlistpage"] + 20; i++)
            {
                if (i >= listItems.Count) break;
                UI.CreateButton(ref page_container, "SkinAdminGroupItemList", "0.5 0.5 0.5 0.1", listItems[i].displayName.english, 14, "0 " + (o - 0.05f).ToString(), "1 " + o.ToString(), string.Format("skin.select admin group item {0}", listItems[i].itemid.ToString()), TextAnchor.MiddleCenter);
                o -= 0.05f;
            }

            CuiHelper.AddUi(player, page_container);
        }

        void SkinManagerUIAdmin_SetSkin(BasePlayer player, int skinID)
        {
            SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["admingroupselect"]];
            int targetItem = playerUI[player]["admingroupitemid"];
            if (playerUI[player]["admingroupitemlisttype"] == 0)
            {
                if (targetConfig.DefaultSkins.ContainsKey(targetItem)) targetConfig.DefaultSkins.Remove(targetItem);
                else targetConfig.DefaultSkins.Add(targetItem, skinID);
            }
            else
            {
                if (targetConfig.RestrictedSkins.Contains(skinID))
                {
                    targetConfig.RestrictedSkins.Remove(skinID);
                    targetConfig.cachedRestrictedSkins[skinID] = false;
                }
                else
                {
                    targetConfig.RestrictedSkins.Add(skinID);
                    targetConfig.cachedRestrictedSkins[skinID] = true;
                }
            }
        }

        void SkinManagerUIAdmin_UpdateGroupSkinlist(BasePlayer player)
        {
            SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["admingroupselect"]];
            CuiHelper.DestroyUi(player, "SkinAdminGroupSkinList");
            CuiElementContainer page_container = UI.CreateElementContainer("SkinPageContainer", "SkinAdminGroupSkinList", "0.1 0.1 0.1 0", "0.6 0.05", "1 1", false);

            ItemDefinition targetItem = ItemManager.FindItemDefinition(playerUI[player]["admingroupitemid"]);
            Facepunch.Steamworks.Inventory.Definition[] usuableSkins = null;
            if (playerUI[player]["admingroupitemlisttype"] == 0)
            {
                usuableSkins = targetItem.skins2;
            }
            else
            {
                usuableSkins = GetUsuableSkins(targetItem, targetConfig.allowDefaultSkins, targetConfig.allowWorkshopSkins);
            }


            if (playerUI[player]["admingroupskinlistpage"] + 20 >= usuableSkins.Length) playerUI[player]["admingroupskinlistpage"] = usuableSkins.Length - 20;
            if (playerUI[player]["admingroupskinlistpage"] < 0) playerUI[player]["admingroupskinlistpage"] = 0;

            float o = 1f;
            for (int i = playerUI[player]["admingroupskinlistpage"]; i < playerUI[player]["admingroupskinlistpage"] + 20; i++)
            {
                if (i >= usuableSkins.Length) break;
                string color = "0.5 0.5 0.5 0";
                if (playerUI[player]["admingroupitemlisttype"] == 0 && targetConfig.DefaultSkins.ContainsKey(playerUI[player]["admingroupitemid"]) && usuableSkins[i].Id == targetConfig.DefaultSkins[playerUI[player]["admingroupitemid"]])
                    color = greenColor;
                else if (playerUI[player]["admingroupitemlisttype"] == 1 && targetConfig.RestrictedSkins.Contains(usuableSkins[i].Id))
                    color = redColor;
                UI.CreateImage(ref page_container, "SkinAdminGroupSkinList", usuableSkins[i].GetStringProperty("image"), "SkinIMG", "0 " + (o - 0.05f).ToString(), "0.05 " + o.ToString());
                UI.CreateLabel(ref page_container, "SkinAdminGroupSkinList", "0.5 0.5 0.5 1", usuableSkins[i].Name, 14, "0.06 " + (o - 0.05f).ToString(), "1 " + o.ToString(), TextAnchor.MiddleLeft);
                UI.CreateButton(ref page_container, "SkinAdminGroupSkinList", color, string.Empty, 14, "0 " + (o - 0.05f).ToString(), "1 " + o.ToString(), string.Format("skin.select admin group skin {0}", usuableSkins[i].Id.ToString()), TextAnchor.MiddleCenter);
                o -= 0.05f;
            }
            CuiHelper.AddUi(player, page_container);
        }

        void SkinManagerUIAdmin(BasePlayer player)
        {

            CuiHelper.DestroyUi(player, "SkinPageContainer");
            CuiElementContainer page_container = UI.CreateElementContainer("SkinManager", "SkinPageContainer", "0.1 0.1 0.1 0", "0 0.05", "1 0.90", false);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", "<<", 16, "0 0", "0.1 0.05", "skin.select admin group previous", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", ">>", 16, "0.1 0", "0.2 0.05", "skin.select admin group next", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", "<<", 16, "0.4 0", "0.5 0.05", "skin.select admin itemlist previous", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", ">>", 16, "0.5 0", "0.6 0.05", "skin.select admin itemlist next", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", "<<", 16, "0.6 0", "0.8 0.05", "skin.select admin skinlist previous", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinPageContainer", "0.5 0.5 0.5 1", ">>", 16, "0.8 0", "1 0.05", "skin.select admin skinlist next", TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, page_container);

            CuiHelper.DestroyUi(player, "SkinAdminGroupPage");
            CuiElementContainer group_container = UI.CreateElementContainer("SkinPageContainer", "SkinAdminGroupPage", "0.1 0.1 0.1 0", "0 0.05", "0.20 1", false);
            CuiHelper.AddUi(player, group_container);

            SkinManagerUIAdmin_UpdateGroups(player);
        }

        void GroupUp(GroupType groupType, string oxideName, int movePos)
        {
            if (groupType != GroupType.Group) oxideName = string.Empty;
            int oldindex = GetGroupConfigs().IndexOf(GetGroupConfigs().Where(x => x.groupType == groupType && x.oxideGroup == oxideName).ToList()[0]);
            int newindex = oldindex + movePos;
            SkinConfigs skinCfg = skinConfigs[oldindex];
            skinConfigs.RemoveAt(oldindex);
            skinConfigs.Insert(newindex, skinCfg);
        }

        #endregion

        void SkinMenu(BasePlayer player, bool isAdmin)
        {
            DeleteAllUI(player);
            try
            {
                int targetGroup = GetPlayerGroup(player);
                if (targetGroup == -1)
                {
                    SendReply(player, "Couldn't find you in any groups.");
                    return;
                }
                playerUI[player]["playergroup"] = targetGroup;
                SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["playergroup"]];

                CuiElementContainer main_container = UI.CreateElementContainer("Overlay", "SkinManager", "0.1 0.1 0.1 0.90", "0 0", "1 1", true);

                if (targetConfig.SkinManager) UI.CreateButton(ref main_container, "SkinManager", "0.8 0.8 0.8 1", "<color=green>Skin Manager</color>", 30, "0 0.90", "0.25 1", "skin.select skinmanager", TextAnchor.MiddleCenter);
                if (targetConfig.SkinChanger) UI.CreateButton(ref main_container, "SkinManager", "0.8 0.8 0.8 1", "<color=green>Skin Changer</color>", 30, "0.25 0.90", "0.50 1", "skin.select skincontainer", TextAnchor.MiddleCenter);
                if (isAdmin) UI.CreateButton(ref main_container, "SkinManager", "0.8 0.8 0.8 1", "<color=green>Admin</color>", 30, "0.50 0.90", "1 1", "skin.select admin", TextAnchor.MiddleCenter);
                UI.CreateButton(ref main_container, "SkinManager", "0.5 0.5 0.5 1", "Close", 16, "0 0", "1 0.05", "skin.select close", TextAnchor.MiddleCenter);
                CuiHelper.AddUi(player, main_container);
                SkinManagerUIPlayer(player);
            }
            catch { }
        }


        object CanCraft(PlayerBlueprints playerBlueprints, ItemDefinition itemDefinition, int skinID)
        {
            bool item = false;
            try
            {
                BasePlayer player = playerBlueprints.gameObject.ToBaseEntity() as BasePlayer;
                
                InitializePlayer(player.userID);
                if(!playerUI.ContainsKey(player))
                {
                    DefaultValues(player);
                    playerUI[player]["playergroup"] = GetPlayerGroup(player);
                }
                if (player.currentCraftLevel < itemDefinition.Blueprint.workbenchLevelRequired)
                {
                    return false;
                }
                if(itemDefinition.Blueprint != null && itemDefinition.Blueprint.NeedsSteamItem)
                {
                    item = true;
                }
                if(itemDefinition.steamItem != null)
                {
                    item = true;
                }
                
                
                if (item)
                {
                    return true;
                }


                foreach (int num3 in ItemManager.defaultBlueprints)
                {
                    if (num3 == itemDefinition.itemid)
                    {
                        return true;
                    }
                }
                return (player.isServer && playerBlueprints.IsUnlocked(itemDefinition));
            }
            catch(Exception e)
            {
                Interface.Oxide.LogWarning(e.ToString());
                Interface.Oxide.LogWarning(e.StackTrace);
            }
            return null;
        }

        void OnItemCraft(ItemCraftTask item, BasePlayer player)
        {
            if (item.skinID != 0)
            {
                if (GetGroupConfigs()[playerUI[player]["playergroup"]].cachedRestrictedSkins.ContainsKey(item.skinID) && GetGroupConfigs()[playerUI[player]["playergroup"]].cachedRestrictedSkins[item.skinID])
                {
                    item.skinID = 0;
                }
            }
            if (item.skinID == 0)
            {
                int skinID = GetCurrentSkin(player, item.blueprint.targetItem.itemid, true);
                if (skinID != 0)
                {
                    item.skinID = skinID;
                }
            }
        }


        void DefaultValues(BasePlayer player)
        {
            playerUI[player] = new Hash<string, int>
            {
                {"admingroup", 0 },
                {"admingroupselect", 0 },
                {"admingroupitemlisttype", 0 },
                {"admingroupitemlistpage", 0 },
                {"admingroupitemid", 0 },
                {"admingroupskinlistpage", 0 },
                {"playergroup", -1 },
                {"playeritemlistpage", 0 },
                {"playeritem", 0 },
                {"playerskinlistpage",0 },
                {"playerskinid",0 }
            };
        }
        [ConsoleCommand("skin.select")]
        void ccmdSkinSelect(ConsoleSystem.Arg arg)
        {
            using (TimeWarning.New("SkinSelect", 0.01f))
            {
                var player = arg.Connection?.player as BasePlayer;
                if (player == null) return;
                if (arg.Args.Length == 0) return;

                switch (arg.Args[0])
                {
                    case "close":
                        DeleteAllUI(player);
                        break;
                    case "skincontainer":
                        if (arg.Args.Length == 1)
                        {
                            DeleteAllUI(player);
                            var newStorage = CreateContainer(player);
                            timer.Once(0.1f, () =>
                            {
                                InitializePlayerSkinBox(player, newStorage);
                                InitializePlayerSkinBox_UI(player);
                                ServerMgr.Instance.StartCoroutine(BoxSkinUpdate(newStorage));
                            });
                        }
                        else
                        {
                            switch (arg.Args[1])
                            {
                                case "skin":
                                    int newskinID = int.Parse(arg.Args[2]);
                                    string reason = string.Empty;
                                    if (IsSkinAllowed(player, playerUI[player]["playergroup"], playerUI[player]["playeritem"], newskinID, out reason))
                                    {
                                        playerUI[player]["playerskinid"] = newskinID;
                                    }
                                    else
                                    {
                                        SendReply(player, reason);
                                    }
                                    break;
                                case "clear":
                                    if (player.inventory.loot.containers[0].GetSlot(0) == null) return;
                                    player.inventory.loot.containers[0].GetSlot(0).skin = 0L;
                                    player.inventory.loot.SendImmediate();
                                    InitializePlayerSkinBox_UpdateItemCurrent(player);
                                    break;
                                case "set":
                                    if (player.inventory.loot.containers[0].GetSlot(0) == null) return;
                                    int playerskinid = int.Parse(playerUI[player]["playerskinid"].ToString());
                                    if (player.inventory.loot.containers[0].GetSlot(0).info.skins2.Where(x => x.Id == playerskinid).Count() == 0)
                                    {
                                        SendReply(player, "This skin is for another item.");
                                    }
                                    else
                                    {
                                        player.inventory.loot.containers[0].GetSlot(0).skin = ulong.Parse(playerUI[player]["playerskinid"].ToString());
                                        if (player.inventory.loot.containers[0].GetSlot(0).GetHeldEntity() != null)
                                        {
                                            player.inventory.loot.containers[0].GetSlot(0).GetHeldEntity().skinID = player.inventory.loot.containers[0].GetSlot(0).skin;
                                        }
                                        player.inventory.loot.SendImmediate();

                                        InitializePlayerSkinBox_UpdateItemCurrent(player);
                                    }
                                    break;
                                case "next":
                                    playerUI[player]["playerskinlistpage"] += 9;
                                    InitializePlayerSkinBox_UpdateItem(player);
                                    break;
                                case "previous":
                                    playerUI[player]["playerskinlistpage"] -= 9;
                                    InitializePlayerSkinBox_UpdateItem(player);
                                    break;
                                default: break;
                            }
                        }
                        break;
                    case "skinmanager":
                        if (arg.Args.Length == 1)
                        {
                            SkinManagerUIPlayer(player);
                        }
                        else
                        {
                            switch (arg.Args[1])
                            {
                                case "skindefault":
                                    playerUI[player]["playerskinid"] = 0;
                                    SetSkin(player, playerUI[player]["playeritem"], -1);
                                    SkinManagerUIPlayer_UpdateCurrentSkin(player);
                                    break;
                                case "skinclear":
                                    playerUI[player]["playerskinid"] = 0;
                                    SetSkin(player, playerUI[player]["playeritem"], 0);
                                    SkinManagerUIPlayer_UpdateCurrentSkin(player);
                                    break;
                                case "skin":
                                    int newskinID = int.Parse(arg.Args[2]);
                                    string reason = string.Empty;
                                    if (IsSkinAllowed(player, playerUI[player]["playergroup"], playerUI[player]["playeritem"], newskinID, out reason))
                                    {
                                        playerUI[player]["playerskinid"] = newskinID;
                                        SetSkin(player, playerUI[player]["playeritem"], newskinID);
                                        SkinManagerUIPlayer_UpdateCurrentSkin(player);
                                    }
                                    else
                                    {
                                        SendReply(player, reason);
                                    }
                                    break;
                                case "item":
                                    playerUI[player]["playeritem"] = int.Parse(arg.Args[2]);
                                    playerUI[player]["playerskinid"] = 0;
                                    SkinManagerUIPlayer_UpdateCurrentSkin(player);
                                    SkinManagerUIPlayer_UpdateSkins(player);
                                    break;
                                case "itemlist":
                                    if (arg.Args[2] == "next")
                                        playerUI[player]["playeritemlistpage"] += 20;
                                    else
                                        playerUI[player]["playeritemlistpage"] -= 20;
                                    playerUI[player]["playerskinlistpage"] = 0;
                                    SkinManagerUIPlayer_UpdateItems(player);
                                    break;
                                case "skinlist":
                                    if (arg.Args[2] == "next")
                                        playerUI[player]["playerskinlistpage"] += 20;
                                    else
                                        playerUI[player]["playerskinlistpage"] -= 20;
                                    SkinManagerUIPlayer_UpdateSkins(player);
                                    break;
                                default:

                                    break;
                            }
                        }
                        break;
                    case "admin":
                        if (!hasPermission(player, "skinmanager.admin")) { DeleteAllUI(player); return; }
                        if (arg.Args.Length == 1)
                        {
                            SkinManagerUIAdmin(player);
                        }
                        else
                        {
                            switch (arg.Args[1])
                            {
                                case "itemlist":
                                    if (arg.Args[2] == "previous")
                                        playerUI[player]["admingroupitemlistpage"] -= 20;
                                    else
                                        playerUI[player]["admingroupitemlistpage"] += 20;
                                    playerUI[player]["admingroupskinlistpage"] = 0;
                                    SkinManagerUIAdmin_UpdateGroupItemList(player);

                                    break;
                                case "skinlist":
                                    if (arg.Args[2] == "previous")
                                        playerUI[player]["admingroupskinlistpage"] -= 20;
                                    else
                                        playerUI[player]["admingroupskinlistpage"] += 20;
                                    SkinManagerUIAdmin_UpdateGroupSkinlist(player);
                                    break;
                                case "group":
                                    switch (arg.Args[2])
                                    {
                                        case "skin":
                                            SkinManagerUIAdmin_SetSkin(player, int.Parse(arg.Args[3]));
                                            SkinManagerUIAdmin_UpdateGroupSkinlist(player);
                                            break;
                                        case "up":
                                        case "down":
                                            GroupUp((GroupType)int.Parse(arg.Args[3]), arg.Args[4], arg.Args[2] == "up" ? -1 : 1);
                                            SkinManagerUIAdmin_UpdateGroups(player);
                                            break;

                                        case "previous":
                                            playerUI[player]["admingroup"] -= 9;
                                            SkinManagerUIAdmin_UpdateGroups(player);
                                            break;
                                        case "next":
                                            playerUI[player]["admingroup"] += 9;
                                            SkinManagerUIAdmin_UpdateGroups(player);
                                            break;
                                        case "item":
                                            playerUI[player]["admingroupitemid"] = int.Parse(arg.Args[3]);
                                            SkinManagerUIAdmin_UpdateGroupSkinlist(player);
                                            break;
                                        case "set":
                                            bool flag = bool.Parse(arg.Args[4]);
                                            SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["admingroupselect"]];
                                            switch (arg.Args[3])
                                            {
                                                case "SkinManager":
                                                    targetConfig.SkinManager = flag;
                                                    break;
                                                case "SkinChanger":
                                                    targetConfig.SkinChanger = flag;
                                                    break;
                                                case "ChooseSkins":
                                                    targetConfig.ChooseSkins = flag;
                                                    break;
                                                case "RandomSkins":
                                                    targetConfig.RandomSkins = flag;
                                                    break;
                                                case "allowDefaultSkins":
                                                    targetConfig.allowDefaultSkins = flag;
                                                    break;
                                                case "allowWorkshopSkins":
                                                    targetConfig.allowWorkshopSkins = flag;
                                                    break;
                                            }
                                            SkinManagerUIAdmin_UpdateGroupPage(player);
                                            break;
                                        case "setRestrictedSkins":
                                            playerUI[player]["admingroupitemlisttype"] = 1;
                                            SkinManagerUIAdmin_UpdateGroupItemList(player);
                                            break;
                                        case "setDefaultSkins":
                                            playerUI[player]["admingroupitemlisttype"] = 0;
                                            SkinManagerUIAdmin_UpdateGroupItemList(player);
                                            break;
                                        case "select":
                                            GroupType groupType = (GroupType)int.Parse(arg.Args[3]);
                                            string groupName = groupType == GroupType.Group ? arg.Args[4] : string.Empty;
                                            playerUI[player]["admingroupselect"] = GetGroupConfigs().IndexOf(GetGroupConfigs().Where(x => x.groupType == groupType && x.oxideGroup == groupName).ToList()[0]);
                                            SkinManagerUIAdmin_UpdateGroupPage(player);
                                            break;
                                        default:
                                            break;
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }

                        break;
                    default:
                        break;
                }
            }
        }

        #region UI

        Hash<BasePlayer, Hash<string, int>> playerUI = new Hash<BasePlayer, Hash<string, int>>();

        void DeleteAllUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkinManager");
            CuiHelper.DestroyUi(player, "SkinContainer");
            CuiHelper.DestroyUi(player, "SkinContainer2");
            CuiHelper.DestroyUi(player, "SkinContainer3");
        }

        #endregion


        #region commands
        [ChatCommand("skin")]
        void cmdSkin(BasePlayer player, string command, string[] args)
        {
            if(!hasPermission(player, "skinmanager.use"))
            {
                SendReply(player, "You are not allowed to use this command.");
                return;
            }
            DefaultValues(player);
            SkinMenu(player, hasPermission(player, "skinmanager.admin"));
        }

        #endregion

        // PART2 PLUGIN

        FieldInfo PositionChecks = typeof(PlayerLoot).GetField("PositionChecks", BindingFlags.NonPublic | BindingFlags.Instance);

        StorageContainer CreateContainer(BasePlayer player)
        {
            var newItemContainer = ItemManager.Create(ItemManager.FindItemDefinition("box.wooden"), 1);
            if (newItemContainer?.info.GetComponent<ItemModDeployable>() == null)
            {
                return null;
            }
            var deployable = newItemContainer.info.GetComponent<ItemModDeployable>().entityPrefab.resourcePath;
            if (deployable == null)
            {
                return null;
            }
            var newBaseEntity = GameManager.server.CreateEntity(deployable, player.transform.position, player.transform.rotation, true);
            if (newBaseEntity == null)
            {
                return null;
            }
            newBaseEntity.enableSaving = false;
            newBaseEntity.SetParent(player, 0);
            newBaseEntity.Spawn();
            var storage = newBaseEntity.GetComponent<StorageContainer>();
            if (storage == null)
            {
                return null;
            }
            storage.isLootable = true;
            storage.inventorySlots = 1;
            storage.inventory.capacity = 1;

            return storage;
        }
        IEnumerator BoxSkinUpdate(StorageContainer boxEntity)
        {
            Item item = null;
            bool hadItem = false;
            yield return new WaitWhile(new Func<bool>(() =>
            {
                if (boxEntity.inventory.GetSlot(0) != null)
                {
                    if (item != boxEntity.inventory.GetSlot(0))
                    {
                        item = boxEntity.inventory.GetSlot(0);
                        playerUI[boxEntity.GetParentEntity() as BasePlayer]["playeritem"] = item.info.itemid;
                        InitializePlayerSkinBox_UpdateItem(boxEntity.GetParentEntity() as BasePlayer);
                        InitializePlayerSkinBox_UpdateItemCurrent(boxEntity.GetParentEntity() as BasePlayer);
                        hadItem = true;
                    }
                }
                else if (hadItem)
                {
                    hadItem = false;
                    item = null;
                    CuiHelper.DestroyUi(boxEntity.GetParentEntity() as BasePlayer, "SkinContainerItemList");
                    CuiHelper.DestroyUi(boxEntity.GetParentEntity() as BasePlayer, "SkinContainer3");
                }
                return boxEntity.HasFlag(BaseEntity.Flags.Open);
            }));


            BaseEntity parent = boxEntity.GetParentEntity();
            if (parent != null)
            {
                DropUtil.DropItems(boxEntity.inventory, parent.transform.position, 1f);
                CuiHelper.DestroyUi(parent as BasePlayer, "SkinContainer");
                CuiHelper.DestroyUi(parent as BasePlayer, "SkinContainer2");
                CuiHelper.DestroyUi(parent as BasePlayer, "SkinContainer3");
                boxEntity.SetParent(null, 0);
            }
            else
            {
                DropUtil.DropItems(boxEntity.inventory, boxEntity.transform.position, 1f);
            }

            Debug.Log(boxEntity.ToString() + " destroying");
            boxEntity.Kill(BaseNetworkable.DestroyMode.None);
        }


        void InitializePlayerSkinBox_UpdateItemCurrent(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkinContainer3");

            CuiElementContainer page_container = UI.CreateElementContainer("Overlay", "SkinContainer3", "0.2 0.2 0.2 1", "0.8 0.2", "1 0.5", false);
            var item = player.inventory.loot.containers[0].GetSlot(0);
            if (item != null)
            {
                int skinID = int.Parse(item.skin.ToString());
                if (skinID != 0)
                {
                    if (item.info.skins2.Count() > 0)
                    {
                        IEnumerable<Facepunch.Steamworks.Inventory.Definition> skinList = item.info.skins2.Where(w => w.Id == skinID);
                        if (skinList.Count() > 0)
                        {
                            UI.CreateImage(ref page_container, "SkinContainer3", item.info.skins2.Where(w => w.Id == skinID).ElementAt(0).GetStringProperty("image"), "SkinIMG", "0 0", "1 1");
                        }
                    }
                }
            }

            CuiHelper.AddUi(player, page_container);
        }

        void InitializePlayerSkinBox_UpdateItem(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkinContainerItemList");

            SkinConfigs targetConfig = GetGroupConfigs()[playerUI[player]["playergroup"]];

            CuiElementContainer page_container = UI.CreateElementContainer("SkinContainer", "SkinContainerItemList", "0.1 0.1 0.1 0", "0 0.1", "1 0.90", false);

            ItemDefinition targetItem = ItemManager.FindItemDefinition(playerUI[player]["playeritem"]);
            Facepunch.Steamworks.Inventory.Definition[] usuableSkins = GetUsuableSkins(targetItem, targetConfig.allowDefaultSkins, targetConfig.allowWorkshopSkins);

            if (usuableSkins.Length == 0) return;

            if (playerUI[player]["playerskinlistpage"] + 9 > usuableSkins.Length) playerUI[player]["playerskinlistpage"] = usuableSkins.Length - 10;
            if (playerUI[player]["playerskinlistpage"] < 0) playerUI[player]["playerskinlistpage"] = 0;

            float o = 1f;
            string color = "0.5 0.5 0.5 0";
            string description = string.Empty;
            for (int i = playerUI[player]["playerskinlistpage"]; i < playerUI[player]["playerskinlistpage"] + 10; i++)
            { 
                if (i >= usuableSkins.Length) break;
                if (targetConfig.cachedRestrictedSkins[usuableSkins[i].Id])
                {
                    color = redColor;
                    description = defaultRestrictedSkin_Reason;
                }
                else
                {
                    color = "0.5 0.5 0.5 0";
                    description = usuableSkins[i].Description;
                }

                UI.CreateImage(ref page_container, "SkinContainerItemList", usuableSkins[i].GetStringProperty("image"), "SkinIMG", "0 " + (o - 0.1f).ToString(), "0.07 " + o.ToString());
                UI.CreateLabel(ref page_container, "SkinContainerItemList", "1 1 1 1", usuableSkins[i].Name, 14, "0.11 " + (o - 0.1f).ToString(), "0.3 " + o.ToString(), TextAnchor.MiddleLeft);
                UI.CreateLabel(ref page_container, "SkinContainerItemList", "0.8 0.8 0.8 1", description, 14, "0.31 " + (o - 0.1f).ToString(), "1 " + o.ToString(), TextAnchor.MiddleLeft);
                UI.CreateButton(ref page_container, "SkinContainerItemList", color, string.Empty, 14, "0 " + (o - 0.1f).ToString(), "1 " + o.ToString(), string.Format("skin.select skincontainer skin {0}", usuableSkins[i].Id.ToString()), TextAnchor.MiddleCenter);
                o -= 0.1f;
            }
            CuiHelper.AddUi(player, page_container);
        }

        void InitializePlayerSkinBox(BasePlayer player, StorageContainer newStorage)
        {
            newStorage.SetFlag(BaseEntity.Flags.Open, true, false);
            player.inventory.loot.StartLootingEntity(newStorage, false);
            player.inventory.loot.AddContainer(newStorage.inventory);
            player.inventory.loot.SendImmediate();
            player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", newStorage.panelName);
            player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

        }

        void InitializePlayerSkinBox_UI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkinContainer");
            CuiHelper.DestroyUi(player, "SkinContainer2");
            CuiHelper.DestroyUi(player, "SkinContainer3");

            CuiElementContainer page_container = UI.CreateElementContainer("Overlay", "SkinContainer", "0.1 0.1 0.1 1", "0.59 0.5", "1 1", false);
            UI.CreateLabel(ref page_container, "SkinContainer", "0 1 0 0.5", "Skin Manager", 30, "0.1 0.90", "0.9 1", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinContainer", "0.5 0.5 0.5 0.2", "<<", 16, "0 0.", "0.5 0.05", "skin.select skincontainer previous", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "SkinContainer", "0.5 0.5 0.5 0.2", ">>", 16, "0.5 0", "1 0.05", "skin.select skincontainer next", TextAnchor.MiddleCenter);

            CuiElementContainer page_container2 = UI.CreateElementContainer("Overlay", "SkinContainer2", "0.1 0.1 0.1 1", "0.59 0", "1 0.05", false);
            UI.CreateButton(ref page_container2, "SkinContainer2", "0.5 0.5 0.5 0.2", "Remove Skin", 16, "0.2 0.2", "0.5 0.8", "skin.select skincontainer clear", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container2, "SkinContainer2", "0.5 0.5 0.5 0.2", "Set Skin", 16, "0.5 0.2", "0.8 0.8", "skin.select skincontainer set", TextAnchor.MiddleCenter);



            CuiHelper.AddUi(player, page_container);
            CuiHelper.AddUi(player, page_container2);
        }

        #region admin


        #endregion
    }
}
