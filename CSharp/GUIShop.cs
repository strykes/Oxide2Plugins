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

        int playersMask;

        //////////////////////////////////////////////////////////////////////////////////////
        // References ////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        [PluginReference("00-Economics")]
        Plugin Economics;

        [PluginReference]
        Plugin Kits;

        //////////////////////////////////////////////////////////////////////////////////////
        // Workaround the Blocks of Economics. Hope This wont be needed in the future
        // THX MUGHISI ///////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        private Dictionary<string, LuaFunction> EconomyApi = new Dictionary<string, LuaFunction>();
        void OnServerInitialized()
        {
            InitializeTable();
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

        static string MessageShowNoEconomics = "Couldn't get informations out of Economics. Is it installed?";
        static string MessageBought = "You've successfully bought {0}x {1}";
        static string MessageSold = "You've successfully sold {0}x {1}";
        static string MessageErrorNoShop = "This shop doesn't seem to exist.";
        static string MessageErrorNoActionShop = "You are not allowed to {0} in this shop";
        static string MessageErrorNoNPC = "The NPC owning this shop was not found around you";
        static string MessageErrorNoActionItem = "You are not allowed to {0} this item here";
        static string MessageErrorItemItem = "WARNING: The admin didn't set this item properly! (item)";
        static string MessageErrorItemNoValid = "WARNING: It seems like it's not a valid item";
        static string MessageErrorRedeemKit = "WARNING: There was an error while giving you this kit";
        static string MessageErrorBuyPrice = "WARNING: No buy price was given by the admin, you can't buy this item";
        static string MessageErrorSellPrice = "WARNING: No sell price was given by the admin, you can't sell this item";
        static string MessageErrorNotEnoughMoney = "You need {0} coins to buy {1} of {2}";
        static string MessageErrorNotEnoughSell = "You don't have enough of this item.";
        static string MessageErrorItemNoExist = "WARNING: The item you are trying to buy doesn't seem to exist";
        static string MessageErrorNPCRange = "You may not use the chat shop. You might need to find the NPC Shops.";

        void Init()
        {
            CheckCfg<Dictionary<string,object>>("Shop - Shop Categories", ref ShopCategories);
            CheckCfg<Dictionary<string, object>>("Shop - Shop List", ref Shops);
            CheckCfg<string>("Message - Error - No Econonomics", ref MessageShowNoEconomics);
            CheckCfg<string>("Message - Bought", ref MessageBought);
            CheckCfg<string>("Message - Sold", ref MessageSold);
            CheckCfg<string>("Message - Error - No Shop", ref MessageErrorNoShop);
            CheckCfg<string>("Message - Error - No Action In Shop", ref MessageErrorNoActionShop);
            CheckCfg<string>("Message - Error - No NPC", ref MessageErrorNoNPC);
            CheckCfg<string>("Message - Error - No Action Item", ref MessageErrorNoActionItem);
            CheckCfg<string>("Message - Error - Item Not Set Properly", ref MessageErrorItemItem);
            CheckCfg<string>("Message - Error - Item Not Valid", ref MessageErrorItemNoValid);
            CheckCfg<string>("Message - Error - Redeem Kit", ref MessageErrorRedeemKit);
            CheckCfg<string>("Message - Error - No Buy Price", ref MessageErrorBuyPrice);
            CheckCfg<string>("Message - Error - No Sell Price", ref MessageErrorSellPrice);
            CheckCfg<string>("Message - Error - Not Enough Money", ref MessageErrorNotEnoughMoney);
            CheckCfg<string>("Message - Error - Not Enough Items", ref MessageErrorNotEnoughSell);
            CheckCfg<string>("Message - Error - Item Doesnt Exist", ref MessageErrorItemNoExist);
            CheckCfg<string>("Message - Error - No Chat Shop", ref MessageErrorNPCRange);
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
            items1.Add("Assault Rifle");
            items1.Add("Bolt Action Rifle");
            shop1.Add("buy", items1);
            shop1.Add("sell", items1);
            shop1.Add("description", "You currently have {0} coins to spend in this weapons shop");
            shop1.Add("name", "Weaponsmith Shop");

            var shop2 = new Dictionary<string, object>();
            var items2 = new List<object>();
            items2.Add("Build Kit");
            shop2.Add("buy", items2);
            shop2.Add("description", "You currently have {0} coins to spend in this builders shop");
            shop2.Add("name", "Build");

            var shop3 = new Dictionary<string, object>();
            var items3 = new List<object>();
            items3.Add("Apple");
            items3.Add("BlueBerries");
            items3.Add("Assault Rifle");
            items3.Add("Bolt Action Rifle");
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

            dsc.Add("Assault Rifle", scat1);
            dsc.Add("Bolt Action Rifle", scat2);

            var scat3 = new Dictionary<string, object>();
            scat3.Add("item", "kitbuild");
            scat3.Add("buy", "10");
            scat3.Add("sell", "8");
            scat3.Add("img", "http://oxidemod.org/data/resource_icons/0/715.jpg?1425682952");

            dsc.Add("Build Kit", scat3);

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

            dsc.Add("Apple", scat4);
            dsc.Add("BlueBerries", scat5);

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

        //////////////////////////////////////////////////////////////////////////////////////
        // Oxide Hooks ///////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        void Loaded()
        {
            playersMask = UnityEngine.LayerMask.GetMask(new string[] { "Player (Server)" });
        }

        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (!Shops.ContainsKey(npc.userID.ToString())) return;
            ShowShop(player, npc.userID.ToString(), 0);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // GUI ///////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

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
                                    ""command"":""shop.buy {currentshop} {buyitem} 1"",
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
                                    ""command"":""shop.buy {currentshop} {buyitem} 10"",
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
                                    ""command"":""shop.buy {currentshop} {buyitem} 100"",
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
                                    ""command"":""shop.sell {currentshop} {sellitem} 1"",
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
                                    ""command"":""shop.sell {currentshop} {sellitem} 10"",
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
                                    ""command"":""shop.sell {currentshop} {sellitem} 100"",
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

        Hash<BasePlayer, double> shopPage = new Hash<BasePlayer, double>();
        void ShowShop(BasePlayer player, string shopid, double from = 0.0)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", "ShopOverlay");
            shopPage[player] = from;
            if (!Shops.ContainsKey(shopid))
            {
                SendReply(player, MessageErrorNoShop);
                return;
            }
            object playerData = EconomyApi["GetUserData"].Call("GetUserData",player.userID.ToString());
            if (playerData == null)
            {
                SendReply(player, MessageShowNoEconomics);
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
                if (current >= from && current < from + 7)
                {
                    var itemdata = ShopCategories[pair.Key] as Dictionary<string, object>;
                    double pos = 0.55 - 0.05 * (current - from);

                    var shopijson = shopitemjson.Replace("{ymin}", pos.ToString()).Replace("{ymax}", (pos + 0.05).ToString()).Replace("{itemname}", (string)pair.Key).Replace("{itemurl}", (string)itemdata["img"]);
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", shopijson);
                    if (pair.Value.ContainsKey("buy"))
                    {
                        string buycolor = "0 0.6 0 0.1";
                        var shopitembjson = shopitembuyjson.Replace("{buyprice}", (string)itemdata["buy"]).Replace("{ymin}", pos.ToString()).Replace("{ymax}", (pos + 0.05).ToString()).Replace("{buyitem}", string.Format("'{0}'", pair.Key)).Replace("{color}", buycolor).Replace("{currentshop}", string.Format("'{0}'", shopid));
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", shopitembjson);
                    }
                    if (pair.Value.ContainsKey("sell"))
                    {
                        string sellcolor = "1 0 0 0.1";
                        var shopitemsjson = shopitemselljson.Replace("{sellprice}", (string)itemdata["sell"]).Replace("{ymin}", pos.ToString()).Replace("{ymax}", (pos + 0.05).ToString()).Replace("{sellitem}", string.Format("'{0}'", pair.Key)).Replace("{color}", sellcolor).Replace("{currentshop}", string.Format("'{0}'", shopid));
                        CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", shopitemsjson);
                    }
                }
                current++;
            }
            double minfrom = from <= 7.0 ? 0.0 : from - 7;
            double maxfrom = from + 7 >= current ? from : from + 7;
            var chgpage = shopchangepage.Replace("{currentshop}", shopid).Replace("{shoppageplus}", (maxfrom).ToString()).Replace("{shoppageminus}", (minfrom).ToString());
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "AddUI", chgpage);
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Shop Functions ////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        object CanDoAction(BasePlayer player, string shop, string item, string ttype)
        {
            var shopdata = Shops[shop] as Dictionary<string, object>;
            if (!shopdata.ContainsKey(ttype))
            {
                return string.Format(MessageErrorNoActionShop, ttype);
            }
            var actiondata = shopdata[ttype] as List<object>;
            if (!actiondata.Contains(item))
            {
                return string.Format(MessageErrorNoActionItem, ttype);
            }
            return true;
        }
        bool CanFindNPC(Vector3 pos, string npcid)
        {
            foreach (Collider col in Physics.OverlapSphere(pos, 3f, playersMask))
            {
                if (col.GetComponentInParent<BasePlayer>() == null) continue;
                BasePlayer player = col.GetComponentInParent<BasePlayer>();
                if (player.userID.ToString() == npcid) return true;
            }
            return false;
        }
        object CanShop(BasePlayer player, string shopname)
        {
            if (!Shops.ContainsKey(shopname))
            {
                return MessageErrorNoShop;
            }
            if (shopname != "chat")
            {
                if (!CanFindNPC(player.transform.position, shopname))
                {
                    return MessageErrorNoNPC;
                }
            }
            return true;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Buy Functions /////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        object TryShopBuy(BasePlayer player, string shop, string item, int amount)
        {
            object success = CanShop(player, shop);
            if (success is string)
            {
                return success;
            }
            success = CanDoAction(player, shop, item, "buy");
            if (success is string)
            {
                return success;
            }
            success = CanBuy(player, item, amount);
            if (success is string)
            {
                return success;
            }
            success = TryGive(player, item, amount);
            if (success is string)
            {
                return success;
            }
            var itemdata = ShopCategories[item] as Dictionary<string, object>;
            Economics.Call("Withdraw", player, Convert.ToInt32(itemdata["buy"]) * amount);
            return true;
        }
        object TryGive(BasePlayer player, string item, int amount)
        {
            var itemdata = ShopCategories[item] as Dictionary<string, object>;
            if (!itemdata.ContainsKey("item"))
            {
                return MessageErrorItemNoValid;
            }
            string itemname = itemdata["item"].ToString();
            object iskit = Kits?.Call("isKit", itemname);

            if (iskit is bool && (bool)iskit)
            {
                object successkit = Kits.Call("GiveKit", player, itemname);
                if (successkit is bool && !(bool)successkit)
                {
                    return MessageErrorRedeemKit;
                }
                return true;
            }
            object success = GiveItem(player, itemname, amount, player.inventory.containerMain);
            if (success is string)
            {
                return success;
            }
            return true;
        }
        private object GiveItem(BasePlayer player, string itemname, int amount, ItemContainer pref)
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
                return MessageErrorItemNoExist;
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
        object CanBuy(BasePlayer player, string item, int amount)
        {
            object playerData = EconomyApi["GetUserData"].Call("GetUserData", player.userID.ToString());
            if (playerData == null)
            {
                return MessageShowNoEconomics;
            }
            var table = (((object[])playerData)[0]) as LuaTable;
            int playerCoins = Convert.ToInt32(table[1]);
            if (!ShopCategories.ContainsKey(item))
            {
                return MessageErrorItemNoValid;
            }

            var itemdata = ShopCategories[item] as Dictionary<string, object>;
            if (!itemdata.ContainsKey("buy"))
            {
                return MessageErrorBuyPrice;
            }
            int buyprice = Convert.ToInt32(itemdata["buy"]);

            if (playerCoins < buyprice * amount)
            {
                return string.Format(MessageErrorNotEnoughMoney, (buyprice * amount).ToString(), amount.ToString(), item);
            }
            return true;
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Sell Functions ////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////

        object TryShopSell(BasePlayer player, string shop, string item, int amount)
        {
            object success = CanShop(player, shop);
            if (success is string)
            {
                return success;
            }
            success = CanDoAction(player, shop, item, "sell");
            if (success is string)
            {
                return success;
            }
            success = CanSell(player, item, amount);
            if (success is string)
            {
                return success;
            }
            success = TrySell(player, item, amount);
            if (success is string)
            {
                return success;
            }
            var itemdata = ShopCategories[item] as Dictionary<string, object>;
            Economics.Call("Deposit", player, Convert.ToInt32(itemdata["sell"]) * amount);
            return true;
        }
        object TrySell(BasePlayer player, string item, int amount)
        {
            var itemdata = ShopCategories[item] as Dictionary<string, object>;
            if (!itemdata.ContainsKey("item"))
            {
                return MessageErrorItemItem;
            }
            string itemname = itemdata["item"].ToString();
            object iskit = Kits?.Call("isKit", itemname);

            if (iskit is bool && (bool)iskit)
            {
                return "You can't sell kits";
            }
            object success = TakeItem(player, itemname, amount);
            if (success is string)
            {
                return success;
            }
            return true;
        }
        private object TakeItem(BasePlayer player, string itemname, int amount)
        {
            itemname = itemname.ToLower();

            bool isBP = false;
            if (itemname.EndsWith(" bp"))
            {
                //isBP = true;
                //itemname = itemname.Substring(0, itemname.Length - 3);
                return "You can't sell blueprints";
            }
            if (displaynameToShortname.ContainsKey(itemname))
                itemname = displaynameToShortname[itemname];
            var definition = ItemManager.FindItemDefinition(itemname);
            if (definition == null)
                return MessageErrorItemNoExist;

            int pamount = player.inventory.GetAmount(definition.itemid);
            if (pamount < amount)
            {
                return MessageErrorNotEnoughSell;
            }

            player.inventory.Take(null, definition.itemid, amount);
            return true;
        }
        object CanSell(BasePlayer player, string item, int amount)
        {
            if (!ShopCategories.ContainsKey(item))
            {
                return MessageErrorItemNoValid;
            }
            var itemdata = ShopCategories[item] as Dictionary<string, object>;
            if (!itemdata.ContainsKey("sell"))
            {
                return MessageErrorSellPrice;
            }
            return true;
        }
        //////////////////////////////////////////////////////////////////////////////////////
        // Chat Commands /////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        [ChatCommand("shop")]
        void cmdShop(BasePlayer player, string command, string[] args)
        {
            if(!Shops.ContainsKey("chat"))
            {
                SendReply(player, MessageErrorNPCRange);
                return;
            }
            ShowShop(player, "chat");
        }
        
        //////////////////////////////////////////////////////////////////////////////////////
        // Console Commands //////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////////
        [ConsoleCommand("shop.show")]
        void ccmdShopShow(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 3) return;
            if (arg.connection == null) return;
            BasePlayer player = arg.connection.player as BasePlayer;
            if (player == null) return;
            string shopid = arg.Args[0].Replace("'", "");
            double shoppage = Convert.ToDouble(arg.Args[1]);
            ShowShop(player, shopid, shoppage);
        }

        [ConsoleCommand("shop.buy")]
        void ccmdShopBuy(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 3) return;
            if (arg.connection == null) return;
            BasePlayer player = arg.connection.player as BasePlayer;
            if (player == null) return;
            object success = Interface.Call("CanShop", player);
            if(success != null)
            {
                string message = "You are not allowed to shop at the moment";
                if (success is string)
                    message = (string)success;
                SendReply(player, message);
                return;
            }

            string shop = arg.Args[0].Replace("'", "");
            string item = arg.Args[1].Replace("'", "");
            int amount = Convert.ToInt32(arg.Args[2]);
            success = TryShopBuy(player, shop, item, amount);
            if(success is string)
            {
                SendReply(player, (string)success);
                return;
            }
            SendReply(player, string.Format(MessageBought, amount.ToString(), item));
            ShowShop(player, shop, shopPage[player]);
        }
        [ConsoleCommand("shop.sell")]
        void ccmdShopSell(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 3) return;
            if (arg.connection == null) return;
            BasePlayer player = arg.connection.player as BasePlayer;
            if (player == null) return;
            object success = Interface.Call("CanShop", player);
            if (success != null)
            {
                string message = "You are not allowed to shop at the moment";
                if (success is string)
                    message = (string)success;
                SendReply(player, message);
                return;
            }
            string shop = arg.Args[0].Replace("'", "");
            string item = arg.Args[1].Replace("'", "");
            int amount = Convert.ToInt32(arg.Args[2]);
            success = TryShopSell(player, shop, item, amount);
            if (success is string)
            {
                SendReply(player, (string)success);
                return;
            }
            SendReply(player, string.Format(MessageSold, amount.ToString(), item));
            ShowShop(player, shop, shopPage[player]);
        }
    }
}
