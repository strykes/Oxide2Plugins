/*
TO DO:
- Time for activated Remover tool
- Raid Blocker
- remove all
*/

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RemoverTool", "Reneb", "3.0.0", ResourceId = 651)]
    class RemoverTool : RustPlugin
    {
        public static string json = @"[  
		{ 
			""name"": ""RemoveMsg"",
			""parent"": ""Overlay"",
			""components"":
			[
				{
					 ""type"":""UnityEngine.UI.Image"",
					 ""color"":""0.1 0.1 0.1 0.7"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""{xmin} {ymin}"",
					""anchormax"": ""{xmax} {ymax}""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""Remover Tool"",
					""fontSize"":15,
					""align"": ""MiddleCenter"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.0 0.83"",
					""anchormax"": ""1.0 0.98""
				}
			]
		},
        {
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""Time left"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.05 0.65"",
					""anchormax"": ""0.3 0.80""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""{timeleft}s"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.4 0.65"",
					""anchormax"": ""1.0 0.80""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""Entity"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.05 0.50"",
					""anchormax"": ""0.3 0.65""
				}
			]
		},
        {
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""{entity}"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.4 0.50"",
					""anchormax"": ""1.0 0.65""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""Cost"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.05 0.0"",
					""anchormax"": ""0.3 0.50""
				}
			]
		},
        {
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""{cost}"",
					""fontSize"":15,
					""align"": ""MiddleLeft"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0.4 0.0"",
					""anchormax"": ""1.0 0.5""
				}
			]
		}
		]
		";

        static FieldInfo serverinput;
        static FieldInfo buildingPrivlidges;
        static int constructionColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Deployable", "Prevent Building" });

        void Loaded()
        {
            json = json.Replace("{xmin}", xmin).Replace("{xmax}", xmax).Replace("{ymin}", ymin).Replace("{ymax}", ymax);
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            buildingPrivlidges = typeof(BasePlayer).GetField("buildingPrivlidges", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            InitializeRustIO();
        }

        void OnServerInitialized()
        {
            InitializeTable();
        }

        private static Library RustIO;
        private static MethodInfo isInstalled;
        private static MethodInfo hasFriend;

        private static bool RustIOIsInstalled()
        {
            if (RustIO == null) return false;
            return (bool)isInstalled.Invoke(RustIO, new object[] { });
        }
        private void InitializeRustIO()
        {
            if (!useRustIO)
            {
                RustIO = null;
                return;
            }
            RustIO = Interface.GetMod().GetLibrary<Library>("RustIO");
            if (RustIO == null || (isInstalled = RustIO.GetFunction("IsInstalled")) == null || (hasFriend = RustIO.GetFunction("HasFriend")) == null)
            {
                RustIO = null;
                Puts("{0}: {1}", Title, "Rust:IO is not present. You need to install Rust:IO first in order to use the RustIO option!");
            }
        }
        private static bool HasFriend(string playerId, string friendId)
        {
            if (RustIO == null) return false;
            return (bool)hasFriend.Invoke(RustIO, new object[] { playerId, friendId });
        }

        object FindOnlinePlayer(string arg, out BasePlayer playerFound)
        {
            playerFound = null;

            ulong steamid = 0L;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (steamid != 0L)
                    if (player.userID == steamid)
                    {
                        playerFound = player;
                        return true;
                    }
                string lowername = player.displayName.ToLower();
                if (lowername.Contains(lowerarg))
                {
                    if (playerFound == null)
                        playerFound = player;
                    else
                        return "Multiple players found";
                }
            }
            if (playerFound == null) return "No player found";
            return true;
        }

        class ToolRemover : MonoBehaviour
        {
            public BasePlayer player;
            public int endTime;
            public int timeLeft;
            public RemoveType removeType;
            public BasePlayer playerActivator;
            public float distance;
            public float lastUpdate;

            public InputState inputState;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                lastUpdate = UnityEngine.Time.realtimeSinceStartup;
            }

            public void RefreshDestroy()
            {
                timeLeft = endTime;
                CancelInvoke("DoDestroy");
                CancelInvoke("RefreshRemoveGui");
                Invoke("DoDestroy", endTime);
                InvokeRepeating("RefreshRemoveGui", 1, 1);
            }

            void DoDestroy()
            {
                GameObject.Destroy(this);
            }

            void RefreshRemoveGui()
            {
                timeLeft--;
                RefreshGUI(this);
            }

            void FixedUpdate()
            {
                if (!player.IsConnected() || player.IsDead()) { GameObject.Destroy(this); return; }
                inputState = serverinput.GetValue(player) as InputState;
                if (inputState.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    float currentTime = UnityEngine.Time.realtimeSinceStartup;
                    if (lastUpdate + 0.5f < currentTime)
                    {
                        lastUpdate = currentTime;
                        Ray ray = new Ray(player.eyes.position, Quaternion.Euler(inputState.current.aimAngles) * Vector3.forward);
                        TryRemove(player, ray, removeType, distance);
                    }
                }
            }

            void OnDestroy()
            {
                if(player.net != null)
                    CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = player.net.connection }, null, "DestroyUI", "RemoveMsg");
            }

        }
        static void RefreshGUI(ToolRemover toolPlayer)
        {
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = toolPlayer.player.net.connection }, null, "DestroyUI", "RemoveMsg");
            string cost = string.Empty;
            string entity = string.Empty;

            toolPlayer.inputState = serverinput.GetValue(toolPlayer.player) as InputState;
            Ray ray = new Ray(toolPlayer.player.eyes.position, Quaternion.Euler(toolPlayer.inputState.current.aimAngles) * Vector3.forward);

            BaseEntity removeObject = FindRemoveObject(ray, toolPlayer.distance);
            if(removeObject != null)
            {
                entity = removeObject.ToString();
                entity = entity.Substring(entity.LastIndexOf("/") + 1).Replace(".prefab","").Replace("_deployed", "").Replace(".deployed", "");
                entity = entity.Substring(0, entity.IndexOf("["));
                Dictionary<string, object> costList = GetCost(removeObject);
                foreach(KeyValuePair<string,object> pair in costList)
                {
                    cost += string.Format("{0} x{1}\n", pair.Key, pair.Value.ToString());
                }
            }

            string pjson = json.Replace("{entity}", entity).Replace("{cost}", cost).Replace("{timeleft}", toolPlayer.timeLeft.ToString());
            CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo() { connection = toolPlayer.player.net.connection }, null, "AddUI", pjson);
        }
        static void TryRemove(BasePlayer player, Ray ray, RemoveType removeType, float distance)
        {
            BaseEntity removeObject = FindRemoveObject(ray, distance);
            if (removeObject == null)
            {
                PrintToChat(player, "Couldn't find anything to remove. Are you close enough?");
                return;
            }
            if (!CanRemoveEntity(player, removeObject, removeType))
            {
                PrintToChat(player, "You have no rights to remove this");
                return;
            }
            if (!CanPay(player, removeObject, removeType))
            {
                PrintToChat(player, "You don't have enough to pay for this remove");
                return;
            }
            Refund(player, removeObject, removeType);
            DoRemove(removeObject);
        }
        static void PrintToChat(BasePlayer player, string message)
        {
            player.SendConsoleCommand("chat.add", new object[] { 0, message, 1f });
        }
        static void DoRemove(BaseEntity removeObject)
        {
            removeObject.KillMessage();
        }
        static void Refund(BasePlayer player, BaseEntity entity, RemoveType removeType)
        {
            if (removeType == RemoveType.All) return;
            if (refundDeployable && entity is WorldItem)
            {
                WorldItem worlditem = entity as WorldItem;
                if (worlditem.item != null && worlditem.item.info != null)
                    player.inventory.GiveItem(worlditem.item.info.itemid, 1, true);
            }
            else if (refundStructure && entity is BuildingBlock)
            {
                BuildingBlock buildingblock = entity as BuildingBlock;
                if (buildingblock.blockDefinition == null) return;

                int buildingblockGrade = (int)buildingblock.grade;
                for (int i = buildingblockGrade; i >= 0; i--)
                {
                    if (buildingblock.blockDefinition.grades[i] != null && refundPercentage.ContainsKey(i.ToString()))
                    {
                        decimal refundRate = decimal.Parse((string)refundPercentage[i.ToString()]) / 100.0m;
                        List<ItemAmount> currentCost = buildingblock.blockDefinition.grades[i].costToBuild as List<ItemAmount>;
                        foreach (ItemAmount ia in currentCost)
                        {
                            player.inventory.GiveItem(ia.itemid, Convert.ToInt32((decimal)ia.amount * refundRate), true);
                        }
                    }
                }
            }
        }
        static bool hasTotalAccess(BasePlayer player)
        {
            List<BuildingPrivlidge> playerpriv = buildingPrivlidges.GetValue(player) as List<BuildingPrivlidge>;
            if (playerpriv.Count == 0)
            {
                return false;
            }
            foreach (BuildingPrivlidge priv in playerpriv.ToArray())
            {
                List<ProtoBuf.PlayerNameID> authorized = priv.authorizedPlayers;
                bool foundplayer = false;
                foreach (ProtoBuf.PlayerNameID pni in authorized.ToArray())
                {
                    if (pni.userid == player.userID)
                        foundplayer = true;
                }
                if (!foundplayer)
                {
                    return false;
                }
            }
            return true;
        }
        static bool CanPay(BasePlayer player, BaseEntity entity, RemoveType removeType)
        {
            if (removeType == RemoveType.Admin || removeType == RemoveType.All) return true;
            Dictionary<string, object> cost = GetCost(entity);
            
            foreach(KeyValuePair<string,object> pair in cost)
            {
                string itemname = pair.Key.ToLower();
                if (displaynameToShortname.ContainsKey(itemname))
                    itemname = displaynameToShortname[itemname];
                ItemDefinition itemdef = ItemManager.FindItemDefinition(itemname);
                if (itemdef == null) continue;
                int amount = player.inventory.GetAmount(itemdef.itemid);
                if (amount < Convert.ToInt32(pair.Value))
                    return false;
            }
            return true;
        }
        static Dictionary<string,object> GetCost(BaseEntity entity)
        {
            Dictionary<string, object> cost = new Dictionary<string, object>();
            if (entity.GetComponent<BuildingBlock>() != null)
            {
                BuildingBlock block = entity.GetComponent<BuildingBlock>();
                string grade = ((int)block.grade).ToString();
                if (!payForRemove.ContainsKey(grade)) return cost;
                cost = payForRemove[grade] as Dictionary<string,object>;
            }
            else if(entity.GetComponent<Deployable>() != null)
            {
                if (!payForRemove.ContainsKey("deployable")) return cost;
                cost = payForRemove["deployable"] as Dictionary<string, object>;
            }
            return cost;
        }
        static bool CanRemoveEntity(BasePlayer player, BaseEntity entity, RemoveType removeType)
        {
            if (entity.isDestroyed) return false;
            if (removeType == RemoveType.Admin || removeType == RemoveType.All) return true;
            var externalPlugins = Interface.CallHook("canRemove", player);
            if (externalPlugins != null)
            {
                PrintToChat(player, externalPlugins is string ? (string)externalPlugins : "You are not allowed use the remover tool at the moment");
                return false;
            }
            if (entity is BuildingBlock && useBuildingOwners)
            {
                var returnhook = Interface.GetMod().CallHook("FindBlockData", new object[] { entity as BuildingBlock });
                if (returnhook is string)
                {
                    string ownerid = (string)returnhook;
                    if (player.userID.ToString() == ownerid) return true;
                    if (useRustIO && RustIOIsInstalled())
                    {
                        if (HasFriend(ownerid, player.userID.ToString()))
                        {
                            return true;
                        }
                    }
                }
            }
            if (useToolCupboard)
                if (hasTotalAccess(player))
                    return true;

            return false;
        }
        static BaseEntity FindRemoveObject(Ray ray, float distance)
        {
            RaycastHit hit;
            if (!UnityEngine.Physics.Raycast(ray, out hit, distance, constructionColl))
                return null;
            return hit.collider.GetComponentInParent<BaseEntity>();
        }
        enum RemoveType
        {
            Normal,
            Admin,
            All
        }
        void EndRemoverTool(BasePlayer player)
        {
            ToolRemover toolremover = player.GetComponent<ToolRemover>();
            if (toolremover == null) return;
            GameObject.Destroy(toolremover);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            BuildingBlock block = entity.GetComponent<BuildingBlock>();
            if (block == null) return;

            // DO SOME CHECKS TO SEE IF ITS A RAID OR SOMETHING
            // SPHERECAST ALL PLAYERS TO BLOCK THERE REMOVE
        }

        [ChatCommand("remove")]
        void cmdChatRemove(BasePlayer player, string command, string[] args)
        {
            int removeTime = RemoveTimeDefault;
            BasePlayer target = player;
            RemoveType removetype = RemoveType.Normal;
            int distanceRemove = playerDistanceRemove;

            if (args.Length != 0)
            {
                switch (args[0])
                {
                    case "admin":
                        removetype = RemoveType.Admin;
                        distanceRemove = adminDistanceRemove;
                        break;
                    case "all":
                        removetype = RemoveType.All;
                        distanceRemove = allDistanceRemove;
                        break;
                    case "target":
                        if (args.Length == 1)
                        {
                            SendReply(player, "/remove target PLAYERNAME/STEAMID optional:Time");
                            return;
                        }
                        BasePlayer tempTarget = null;
                        var success = FindOnlinePlayer(args[1], out tempTarget);
                        if (success is string)
                        {
                            SendReply(player, (string)success);
                            return;
                        }
                        target = tempTarget;
                        if (args.Length > 2) int.TryParse(args[2], out removeTime);

                        break;
                    default:
                        int.TryParse(args[0], out removeTime);
                        break;
                }
            }

            if (removeTime > MaxRemoveTime) removeTime = MaxRemoveTime;

            ToolRemover toolremover = target.GetComponent<ToolRemover>();
            if (toolremover != null && args.Length == 0)
            {
                EndRemoverTool(target);
                SendReply(player, string.Format("{0}: Remover Tool Deactivated", target.displayName));
                return;
            }

            if (toolremover == null)
                toolremover = target.gameObject.AddComponent<ToolRemover>();

            toolremover.endTime = removeTime;
            toolremover.removeType = removetype;
            toolremover.playerActivator = player;
            toolremover.distance = (int)distanceRemove;
            toolremover.RefreshDestroy();
        }
        static string xmin = "0.1";
        static string xmax = "0.4";
        static string ymin = "0.65";
        static string ymax = "0.90";

        static int RemoveTimeDefault = 30;
        static int MaxRemoveTime = 120;
        static int playerDistanceRemove = 3;
        static int adminDistanceRemove = 20;
        static int allDistanceRemove = 300;

        static bool useBuildingOwners = true;
        static bool useRustIO = true;
        static bool useToolCupboard = true;

        static bool useRaidBlocker = true;
        static int RaidBlockerTime = 300;
        static int RaidBlockerRadius = 80;

        static bool usePay = true;
        static bool payDeployable = true;
        static bool payStructure = true;
        static Dictionary<string, object> payForRemove = defaultPay();

        static bool useRefund = true;
        static bool refundDeployable = true;
        static bool refundStructure = true;
        static Dictionary<string, object> refundPercentage = defaultRefund();

        static Dictionary<string, object> defaultPay()
        {
            var dp = new Dictionary<string, object>();

            var dp0 = new Dictionary<string, object>();
            dp0.Add("wood", "1");
            dp.Add("0", dp0);

            var dp1 = new Dictionary<string, object>();
            dp1.Add("wood", "100");
            dp.Add("1", dp1);

            var dp2 = new Dictionary<string, object>();
            dp2.Add("wood", "100");
            dp2.Add("stone", "150");
            dp.Add("2", dp2);

            var dp3 = new Dictionary<string, object>();
            dp3.Add("wood", "100");
            dp3.Add("stone", "50");
            dp3.Add("metal fragments", "75");
            dp.Add("3", dp3);

            var dp4 = new Dictionary<string, object>();
            dp4.Add("wood", "250");
            dp4.Add("stone", "350");
            dp4.Add("metal fragments", "75");
            dp4.Add("high quality metal", "25");
            dp.Add("4", dp4);

            var dpdepoyable = new Dictionary<string, object>();
            dpdepoyable.Add("wood", "50");
            dp.Add("deployable", dpdepoyable);

            return dp;
        }

        static Dictionary<string, object> defaultRefund()
        {
            var dr = new Dictionary<string, object>();

            dr.Add("0", "100.0");
            dr.Add("1", "80.0");
            dr.Add("2", "60.0");
            dr.Add("3", "40.0");
            dr.Add("4", "20.0");

            return dr;
        }

        private static Dictionary<string, string> displaynameToShortname = new Dictionary<string, string>();
        private void InitializeTable()
        {
            displaynameToShortname.Clear();
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                displaynameToShortname.Add(itemdef.displayName.english.ToString().ToLower(), itemdef.shortname.ToString());
            }
        }
    }
}
