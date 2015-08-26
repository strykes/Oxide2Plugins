// Reference: NLua

using System.Collections.Generic;
using System;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using NLua;

namespace Oxide.Plugins
{
    [Info("GUIShop", "Reneb", "1.0.0")]
    class GUIShop : RustPlugin
    {
        [PluginReference("00-Economics")]
        Plugin Economics;

        void Loaded()
        { 
            InitializeTable();
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Workaround the Blocks of Economics. Hope This wont be needed in the future ////////
        //////////////////////////////////////////////////////////////////////////////////////

        private Dictionary<string, LuaFunction> EconomyApi = new Dictionary<string, LuaFunction>();
        void OnServerInitialized()
        {
            if (!Economics)
            {
                PrintWarning("Economics plugin not found. " + this.Name + "will not function!");
                return;
            }
            var apiFuncs = Economics.Call("GetEconomyAPI") as LuaTable;
            foreach (KeyValuePair<object, object> method in apiFuncs)
            {
                var value = method.Value as LuaFunction;
                if (value != null) EconomyApi.Add(method.Key.ToString(), value);
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Configs Manager ///////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        private static Dictionary<string, object> ShopCategories = DefaultShopCategories();
        private static Dictionary<string, object> Shops = DefaultShops();

        void Init()
        {
            CheckCfg<Dictionary<string,object>>("Shop - Shop Categories", ref ShopCategories);
            CheckCfg<Dictionary<string, object>>("Shop - Shop List", ref Shops);

            SaveConfig();
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Default Shops for Tutorial purpoise ///////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        static Dictionary<string, object> DefaultShops()
        {
            var shops = new Dictionary<string, object>();

            var shop1 = new Dictionary<string,object>();
            var items1 = new List<object>();
            items1.Add("Weapons");
            shop1.Add("buy", items1);
            shop1.Add("sell", items1);
            shop1.Add("description", "You currently have {0} coins to spend in this weapons shop");
            shop1.Add("name", "Weaponsmith Shop");

            var shop2 = new Dictionary<string, object>();
            var items2 = new List<object>();
            items2.Add("Builder");
            shop2.Add("buy", items2);
            shop2.Add("description", "You currently have {0} coins to spend in this builders shop");
            shop2.Add("name", "Build");

            var shop3 = new Dictionary<string, object>();
            var items3 = new List<object>();
            items3.Add("Fruits");
            items3.Add("Weapons");
            shop3.Add("buy", items3);
            shop3.Add("sell", items3);
            shop3.Add("description", "You currently have {0} coins to spend in this farmers market");
            shop3.Add("name", "Fruit Market");
            
            shops.Add("chat", shop2);
            shops.Add("5498734", shop1);
            shops.Add("1234567", shop3);

            return shops;
        }
        static Dictionary<string,object> DefaultShopCategories()
        {
            var dsc = new Dictionary<string, object>();
            
            var cat1 = new Dictionary<string, object>();

            var scat1 = new Dictionary<string, object>();
            scat1.Add("item", "assault rifle");
            scat1.Add("buy", "10");
            scat1.Add("sell", "8");
            scat1.Add("img", "http://vignette3.wikia.nocookie.net/play-rust/images/d/d1/Assault_Rifle_icon.png/revision/latest/scale-to-width-down/100?cb=20150405105940");

            var scat2 = new Dictionary<string, object>();
            scat2.Add("item", "bolt action rifle");
            scat2.Add("buy", "10");
            scat2.Add("sell", "8");
            scat2.Add("img", "http://vignette1.wikia.nocookie.net/play-rust/images/5/55/Bolt_Action_Rifle_icon.png/revision/latest/scale-to-width-down/100?cb=20150405111457");

            cat1.Add("Assault Rifle", scat1);
            cat1.Add("Bolt Action Rifle", scat2);

            var cat2 = new Dictionary<string, object>();
            var scat3 = new Dictionary<string, object>();
            scat3.Add("item", "kitbuild");
            scat3.Add("buy", "10");
            scat3.Add("sell", "8");
            scat3.Add("img", "http://oxidemod.org/data/resource_icons/0/715.jpg?1425682952");

            cat2.Add("Build Kit", scat3);

            var cat3 = new Dictionary<string, object>();
            var scat4 = new Dictionary<string, object>();
            scat4.Add("item", "apple");
            scat4.Add("buy", "1");
            scat4.Add("sell", "1");
            scat4.Add("img", "http://vignette2.wikia.nocookie.net/play-rust/images/d/dc/Apple_icon.png/revision/latest/scale-to-width-down/100?cb=20150405103640");

            var scat5 = new Dictionary<string, object>();
            scat5.Add("item", "blueberries");
            scat5.Add("buy", "1");
            scat5.Add("sell", "1");
            scat5.Add("img", "http://vignette1.wikia.nocookie.net/play-rust/images/f/f8/Blueberries_icon.png/revision/latest/scale-to-width-down/100?cb=20150405111338");

            cat3.Add("Apple", scat4);
            cat3.Add("BlueBerries", scat5);

            dsc.Add("Weapons", cat1);
            dsc.Add("Builder", cat2);
            dsc.Add("Fruits", cat3);

            return dsc;
        }



        //////////////////////////////////////////////////////////////////////////////////////
        // Item Management ///////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        Dictionary<string, string> displaynameToShortname = new Dictionary<string, string>();
        private void InitializeTable()
        {
            displaynameToShortname.Clear();
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                displaynameToShortname.Add(itemdef.displayName.english.ToString().ToLower(), itemdef.shortname.ToString());
            }
        }


        public string shopoverlay = @"[  
		                { 
							""name"": ""ShopOverlay"",
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
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""{shopname}"",
                                    ""fontSize"":30,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.3 0.80"",
                                    ""anchormax"": ""0.7 0.90""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""{shopdescription}"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.2 0.70"",
                                    ""anchormax"": ""0.8 0.79""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""Item"",
                                    ""fontSize"":20,
                                    ""align"": ""MiddleLeft"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.2 0.60"",
                                    ""anchormax"": ""0.4 0.65""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""Buy"",
                                    ""fontSize"":20,
                                    ""align"": ""MiddleLeft"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.55 0.60"",
                                    ""anchormax"": ""0.70 0.65""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""Sell"",
                                    ""fontSize"":20,
                                    ""align"": ""MiddleLeft"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.72 0.60"",
                                    ""anchormax"": ""0.87 0.65""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""Close"",
                                    ""fontSize"":20,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.5 0.15"",
                                    ""anchormax"": ""0.7 0.20""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""close"":""ShopOverlay"",
                                    ""color"": ""0.5 0.5 0.5 0.2"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.5 0.15"",
                                    ""anchormax"": ""0.7 0.20""
                                }
                            ]
                        },
                    ]
                    ";
                        
        string shopitembuyjson = @"[
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""{buyprice}"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleLeft"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.55 {ymin}"",
                                    ""anchormax"": ""0.60 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""1"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.60 {ymin}"",
                                    ""anchormax"": ""0.63 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""close"":""ShopOverlay"",
                                    ""command"":""shop.buy {buyitem} 1"",
                                    ""color"": ""{color}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.60 {ymin}"",
                                    ""anchormax"": ""0.63 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""10"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.63 {ymin}"",
                                    ""anchormax"": ""0.66 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""close"":""ShopOverlay"",
                                    ""command"":""shop.buy {buyitem} 10"",
                                    ""color"": ""{color}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.63 {ymin}"",
                                    ""anchormax"": ""0.66 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""100"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.66 {ymin}"",
                                    ""anchormax"": ""0.69 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""close"":""ShopOverlay"",
                                    ""command"":""shop.buy {buyitem} 100"",
                                    ""color"": ""{color}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.66 {ymin}"",
                                    ""anchormax"": ""0.69 {ymax}""
                                }
                            ]
                        },
                    ]
                    ";
       
        string shopitemselljson = @"[
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""1"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.77 {ymin}"",
                                    ""anchormax"": ""0.80 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""{sellprice}"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleLeft"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.72 {ymin}"",
                                    ""anchormax"": ""0.77 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""close"":""ShopOverlay"",
                                    ""command"":""shop.sell {sellitem} 1"",
                                    ""color"": ""{color}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.77 {ymin}"",
                                    ""anchormax"": ""0.80 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""10"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.80 {ymin}"",
                                    ""anchormax"": ""0.83 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""close"":""ShopOverlay"",
                                    ""command"":""shop.buy {sellitem} 10"",
                                    ""color"": ""{color}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.80 {ymin}"",
                                    ""anchormax"": ""0.83 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""100"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.83 {ymin}"",
                                    ""anchormax"": ""0.86 {ymax}""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""close"":""ShopOverlay"",
                                    ""command"":""shop.buy {sellitem} 100"",
                                    ""color"": ""{color}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.83 {ymin}"",
                                    ""anchormax"": ""0.86 {ymax}""
                                }
                            ]
                        },
                    ]
                    ";
        string shopitemjson = @"[
        				{
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""{itemname}"",
                                    ""fontSize"":15,
                                    ""align"": ""MiddleLeft"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.2 {ymin}"",
                                    ""anchormax"": ""0.4 {ymax}""
                                }
                            ]
                        },
                        {
                                ""parent"": ""ShopOverlay"",
                                ""components"":
                                [
                                   {
                                        ""type"":""UnityEngine.UI.RawImage"",
                                        ""imagetype"": ""Tiled"",
                                        ""url"": ""{itemurl}"",
                                    },
                                    {
                                        ""type"":""RectTransform"",
                                        ""anchormin"": ""0.1 {ymin}"",
                                        ""anchormax"": ""0.13 {ymax}""
                                    }
                                ]
                        }
                    ]
                    ";
        string shopchangepage = @"[
{
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":""<<"",
                                    ""fontSize"":20,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.2 0.15"",
                                    ""anchormax"": ""0.3 0.20""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""color"": ""0.5 0.5 0.5 0.2"",
                                    ""command"":""shop.show {currentshop} {shoppageminus}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.2 0.15"",
                                    ""anchormax"": ""0.3 0.20""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Text"",
                                    ""text"":"">>"",
                                    ""fontSize"":20,
                                    ""align"": ""MiddleCenter"",
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.35 0.15"",
                                    ""anchormax"": ""0.45 0.20""
                                }
                            ]
                        },
                        {
                            ""parent"": ""ShopOverlay"",
                            ""components"":
                            [
                                {
                                    ""type"":""UnityEngine.UI.Button"",
                                    ""color"": ""0.5 0.5 0.5 0.2"",
                                    ""command"":""shop.show {currentshop} {shoppageplus}"",
                                    ""imagetype"": ""Tiled""
                                },
                                {
                                    ""type"":""RectTransform"",
                                    ""anchormin"": ""0.35 0.15"",
                                    ""anchormax"": ""0.45 0.20""
                                }
                            ]
                        },
                        
                    ]
                    ";
        void ShowShop(BasePlayer player, string shopid, double from = 0.0)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", "ShopOverlay");

            if (!Shops.ContainsKey(shopid))
            {
                SendReply(player, "This shop doesn't seem to exist.");
                return;
            }
            object playerData = EconomyApi["GetUserData"].Call("GetUserData",player.userID.ToString());
            if (playerData == null)
            {
                SendReply(player, "Couldn't get your shop data. Maybe the owner doesn't have Economics?");
                return;
            }
            var table = (((object[])playerData)[0]) as LuaTable;
            int playerCoins = Convert.ToInt32(table[1]);

            var shop = Shops[shopid] as Dictionary<string,object>;            

            Dictionary<string,Dictionary<string, bool>> itemslist = new Dictionary<string, Dictionary<string, bool>>();

            if (shop.ContainsKey("buy"))
            {
                foreach (string itemname in (List<object>)shop["buy"])
                {
                    if (!itemslist.ContainsKey(itemname))
                        itemslist.Add(itemname, new Dictionary<string, bool>());
                    itemslist[itemname].Add("buy", true);
                }
            }

            if (shop.ContainsKey("sell"))
            {
                foreach (string itemname in (List<object>)shop["sell"])
                {
                    if (!itemslist.ContainsKey(itemname))
                        itemslist.Add(itemname, new Dictionary<string, bool>());
                    itemslist[itemname].Add("sell", true);
                }
            }
            double current = 0.0;

            string soverlay = shopoverlay.Replace("{shopname}", (string)shop["name"]).Replace("{shopdescription}", string.Format((string)shop["description"], playerCoins.ToString()));
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", soverlay);

            if (from < 0) return;
            foreach (KeyValuePair<string, Dictionary<string, bool>> pair in itemslist)
            {
                if (!ShopCategories.ContainsKey(pair.Key)) continue;
                foreach (KeyValuePair<string, object> spair in (Dictionary<string, object>)ShopCategories[pair.Key])
                {
                    if (current >= from && current < from + 7)
                    {
                        var itemdata = spair.Value as Dictionary<string, object>;
                        double pos = 0.55 - 0.05 * (current - from);

                        var shopijson = shopitemjson.Replace("{ymin}", pos.ToString()).Replace("{ymax}", (pos + 0.05).ToString()).Replace("{itemname}", (string)spair.Key).Replace("{itemurl}", (string)itemdata["img"]);
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", shopijson);
                        if (pair.Value.ContainsKey("buy"))
                        {
                            string buycolor = "0 0.6 0 0.1";
                            var shopitembjson = shopitembuyjson.Replace("{buyprice}", (string)itemdata["buy"]).Replace("{ymin}", pos.ToString()).Replace("{ymax}", (pos + 0.05).ToString()).Replace("{buyitem}", (string)itemdata["item"]).Replace("{color}",buycolor);
                            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", shopitembjson);
                        }
                        if (pair.Value.ContainsKey("sell"))
                        {
                            string sellcolor = "1 0 0 0.1";
                            var shopitemsjson = shopitemselljson.Replace("{sellprice}", (string)itemdata["sell"]).Replace("{ymin}", pos.ToString()).Replace("{ymax}", (pos + 0.05).ToString()).Replace("{sellitem}", (string)itemdata["item"]).Replace("{color}", sellcolor);
                            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", shopitemsjson);
                        }
                    }
                    current++;
                }
            }
            double minfrom = from <= 7.0 ? 0.0 : from - 7;
            double maxfrom = from + 7 >= current ? from : from + 7;
            var chgpage = shopchangepage.Replace("{currentshop}", shopid).Replace("{shoppageplus}", (maxfrom).ToString()).Replace("{shoppageminus}", (minfrom).ToString());
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", chgpage);
        }


        [ChatCommand("shop")]
        void cmdShop(BasePlayer player, string command, string[] args)
        {
            if(!Shops.ContainsKey("chat"))
            {
                SendReply(player, "You may not use the chat shop. You might need to find the NPC Shops.");
                return;
            }
            ShowShop(player, "chat");
        }

        [ConsoleCommand("shop.show")]
        void ccmdRewardShow(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2) return;
            if (arg.connection == null) return;
            BasePlayer player = arg.connection.player as BasePlayer;
            if (player == null) return;
            string shopid = arg.Args[0].Replace("'", "");
            double shoppage = Convert.ToDouble(arg.Args[1]);
            ShowShop(player, shopid, shoppage);
        }
    }
}
