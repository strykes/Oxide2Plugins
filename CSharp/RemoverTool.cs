/*
TO DO:
- Time for activated Remover tool
- GUI
- Pay to remove
- Raid Blocker

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
        public string json = @"[  
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
					""text"":""{msg}"",
					""fontSize"":15,
					""align"": ""MiddleCenter"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0 0.5"",
					""anchormax"": ""1 0.9""
				}
			]
		},
		{
			""parent"": ""RemoveMsg"",
			""components"":
			[
				{
					""type"":""UnityEngine.UI.Text"",
					""text"":""{msg2}"",
					""fontSize"":15,
					""align"": ""MiddleCenter"",
				},
				{
					""type"":""RectTransform"",
					""anchormin"": ""0 0.1"",
					""anchormax"": ""1 0.5""
				}
			]
		}
		]
		";
			
		static FieldInfo serverinput;
		static FieldInfo buildingPrivlidges;
		static int constructionColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Deployable" });
		
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
        
        private Library RustIO;
        private MethodInfo isInstalled;
        private MethodInfo hasFriend;
        
        private bool RustIOIsInstalled() {
            if (RustIO == null) return false;
            return (bool)isInstalled.Invoke(RustIO, new object[] {});
        }
        private void InitializeRustIO() {
			if(!useRustIO) {
				RustIO = null;
				return;
			}
            RustIO = Interface.GetMod().GetLibrary<Library>("RustIO");
            if (RustIO == null || (isInstalled = RustIO.GetFunction("IsInstalled")) == null || (hasFriend = RustIO.GetFunction("HasFriend")) == null) {
                RustIO = null;
                Puts("{0}: {1}", Title, "Rust:IO is not present. You need to install Rust:IO first in order to use the RustIO option!");
            }
        }
        private bool HasFriend(string playerId, string friendId) {
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
                	if(playerFound == null)
                    	playerFound = player;
                    else
                    	return "Multiple players found";
                }
            }
            if(playerFound == null) return "No player found";
            return true;
        }
		
		class ToolRemover : MonoBehaviour
		{
			public BasePlayer player;
			public int endTime;
			public RemoveType removeType;
			public BasePlayer playerActivator;
			public float distance;
			
			InputState inputState;
			
			void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
            
            public void RefreshDestroy()
            {
            	CancelInvoke("DoDestroy");
            	Invoke("DoDestroy", endTime);
            }
            
            void DoDestroy()
            {
            	GameObject.Destroy(this);
            }
            
            void FixedUpdate()
            {
                inputState = serverinput.GetValue(player) as InputState;
                if (input.WasJustPressed(BUTTON.ATTACK))
                {
                	Ray ray = new Ray( player.eyes.position, Quaternion.Euler(input.current.aimAngles) * Vector3.forward );
                	TryRemove( player, ray, removeType, distance );
                }
            }
            
            void OnDestroy()
            {
                // SEND MESSAGE THAT REMOVE IS OFF
            }
			
		}
		static void TryRemove(BasePlayer player, Ray ray, RemoveType removeType, float distance)
		{
			BaseEntity removeObject = FindRemoveObject( ray, distance );
			if(removeObject == null)
			{
				SendReply(player, "Couldn't find anything to remove. Are you close enough?");
				return;
			}
			if (!CanRemoveEntity( player, removeObject, removeType ))
			{
				SendReply(player, "You have no rights to remove this");
				return;
			}
			if (!CanPay( player, removeObject, removeType ))
			{
				SendReply(player, "You don't have enough to pay for this remove");
				return;
			}
			Refund( player, removeObject, removeType );
			DoRemove( removeObject );
		}
		void DoRemove( BaseEntity removeObject )
		{
			removeObject.KillMessage();
		}
		void Refund( BasePlayer player, BaseEntity entity, RemoveType removeType )
		{
			if(removeType == RemoveType.All) return;
			if (refundDeployable && entity is WorldItem)
            {
                WorldItem worlditem = entity as WorldItem;
                if (worlditem.item != null && worlditem.item.info != null)
                    player.inventory.GiveItem(worlditem.item.info.itemid, 1, true);
            }
            else if(refundStructure && entity is BuildingBlock )
            {
            	BuildingBlock buildingblock = entity as BuildingBlock;
                if (buildingblock.blockDefinition == null) return;
                
                int buildingblockGrade = (int)buildingblock.grade;
				for (int i = buildingblockGrade; i >= 0; i--) {
					if (buildingblock.blockDefinition.grades[i] != null && refundPercentage.ContainsKey(i.ToString())) {
						decimal refundRate = decimal.Parse(refundPercentage[i.ToString()]) / 100.0;
						List<ItemAmount> currentCost = buildingblock.blockDefinition.grades[i].costToBuild as List<ItemAmount>;
						foreach (ItemAmount ia in currentCost) {
							player.inventory.GiveItem (ia.itemid, Convert.ToInt32((decimal) ia.amount * refundRate), true);
						}
					}
				}
            }
		}
		bool hasTotalAccess(BasePlayer player)
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
        bool CanPay( BasePlayer player, BaseEntity entity, RemoveType removeType )
        {
        	if(removeType == RemoveType.Admin || removeType == RemoveType.All) return true;
        	
        	return true;
        }
		bool CanRemoveEntity( BasePlayer player, BaseEntity entity, RemoveType removeType )
		{
			if(entity.isDestroyed) return false;
			if(removeType == RemoveType.Admin || removeType == RemoveType.All) return true;
			var externalPlugins = Interface.CallHook("canRemove", player);
            if (externalPlugins != null) { 
            	SendReply(player,externalPlugins is string ? (string)externalPlugins : "You are not allowed use the remover tool at the moment" ); 
            	return false; 
            }
            if(entity is BuildingBlock && useBuildingOwners)
            {
            	var returnhook = Interface.GetMod().CallHook("FindBlockData", new object[] { entity as BuildingBlock });
            	if(returnhook is string)
            	{
            		string ownerid = (string)returnhook;
            		if(player.userID.ToString() == ownerid) return true;
            		if (useRustIO && RustIOIsInstalled())
					{
						if(HasFriend(ownerid, player.userID.ToString()))
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
		BaseEntity FindRemoveObject( Ray ray, float distance )
		{
			RaycastHit hit = null;
			if( !UnityEngine.Physics.Raycast( ray, out hit, distance, constructionColl ) )
				return null
			return hit.collider.GetComponentInParent<BaseEntity>();
		}
		enum RemoveType
        {
            Normal,
            Admin,
            All
        }
        void EndRemoverTool( BasePlayer player )
        {
        	ToolRemover toolremover = player.GetComponent<ToolRemover>();
        	if(toolremover == null) return;
        	GameObject.Destroy(toolremover);
        }
        
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			BuildingBlock block = entity.GetComponent<BuildingBlock>();
			if(block == null) return;
			
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
            
            if(args.Length != 0)
            {
            	switch(args[0])
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
            			if(args.Length == 1)
            			{
            				SendReply(player, "/remove target PLAYERNAME/STEAMID optional:Time");
            				return;
            			}
            			BasePlayer tempTarget = null;
            			var success = FindOnlinePlayer( args[1], out tempTarget );
            			if(success is string)
            			{
            				SendReply(player, (string)success);
            				return;
            			}
            			target = tempTarget;
            			if(args.Length > 2) int.TryParse( args[2], out removeTime );

            		break;
            		default:
            			int.TryParse( args[0], out removeTime );
            		break;
            	}
            }
            
            if(removeTime > MaxRemoveTime) removeTime = MaxRemoveTime;
            
            ToolRemover toolremover = target.GetComponent<ToolRemover>();
            if(toolremove != null && args.Length == 0)
            {
            	EndRemoverTool( target );
            	SendReply(player, string.Format("{0}: Remover Tool Deactivated", target.displayName));
            	return;
            }
            
            if(toolremover == null)
            	toolremover = target.gameObject.AddComponent<ToolRemover>();
            
            toolremover.endTime = removeTime;
            toolremover.removeType = removetype;
            toolremover.playerActivator = player;
            toolremover.distance = float.Parse(distanceRemove);
            toolremover.RefreshDestroy();
        }
        
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
        	var dp = new Dictionary<string,object>();
        	
        	var dp0 = new Dictionary<string,object>();
        	dp0.Add("wood", "1");
        	dp.Add("0", dp0);
        	
        	var dp1 = new Dictionary<string,object>();
        	dp1.Add("wood", "100");
        	dp.Add("1", dp1);
        	
        	var dp2 = new Dictionary<string,object>();
        	dp2.Add("wood", "100");
        	dp2.Add("stone", "150");
        	dp.Add("2", dp2);
        	
        	var dp3 = new Dictionary<string,object>();
        	dp3.Add("wood", "100");
        	dp3.Add("stone", "50");
        	dp3.Add("metal fragments", "75");
        	dp.Add("3", dp3);
        	
        	var dp4 = new Dictionary<string,object>();
        	dp4.Add("wood", "250");
        	dp4.Add("stone", "350");
        	dp4.Add("metal fragments", "75");
        	dp4.Add("high quality metal", "25");
        	dp.Add("4", dp4);
        	
        	var dpdepoyable = new Dictionary<string,object>();
        	dpdepoyable.Add("wood", "50");
        	dp.Add("deployable", dpdepoyable);
        	
        	return dp;
        }
        
        static Dictionary<string, object> defaultRefund()
        {
        	var dr = new Dictionary<string,object>();
        	
        	dr.Add("0", "100.0");
        	dr.Add("1", "80.0");
        	dr.Add("2", "60.0");
        	dr.Add("3", "40.0");
        	dr.Add("4", "20.0");
        	
        	return dr;
        }
        
        private Dictionary<string, string> displaynameToShortname;
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
