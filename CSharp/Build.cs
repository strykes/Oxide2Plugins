// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Build", "Reneb", "2.0")]
    class Build : RustPlugin
    {
        private MethodInfo CreateEntity;
        private MethodInfo FindPrefab;
        private static FieldInfo serverinput;
        private static Dictionary<string, string> deployedToItem;
        private Dictionary<string, string> nameToBlockPrefab;

        class BuildPlayer : MonoBehaviour
        {
            
            public BasePlayer player;
            public InputState input;
            public string currentPrefab;
            public string currentType;
            public int currentHealth;
            public int currentGrade;

            void Awake()
            {
                input = serverinput.GetValue(GetComponent<BasePlayer>()) as InputState;
                player = GetComponent<BasePlayer>();
                enabled = true;
            }
            void FixedUpdate()
            {
                if (input.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    DoBuild(this);
                }
            }
        }
           

        /// CACHED VARIABLES
        private static Quaternion currentRot;
        private static Vector3 closestHitpoint;
        private static object closestEnt;
        private static Quaternion newRot;
        private string buildType;
        private string prefabName;

        /////////////////////////////////////////////////////
        ///  OXIDE HOOKS
        /////////////////////////////////////////////////////

        /////////////////////////////////////////////////////
        ///  Loaded()
        ///  When the plugin is loaded by Oxide
        /////////////////////////////////////////////////////

        void Loaded()
        {
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            deployedToItem = new Dictionary<string, string>();
            nameToBlockPrefab = new Dictionary<string, string>();
        }
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(BuildPlayer));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }   
        void OnServerInitialized()
        {
            var allItemsDef = UnityEngine.Resources.FindObjectsOfTypeAll<ItemDefinition>();
            foreach (ItemDefinition itemDef in allItemsDef)
            {
                if (itemDef.GetComponent<ItemModDeployable>() != null)
                {
                    deployedToItem.Add(itemDef.GetComponent<ItemModDeployable>().entityPrefab.targetObject.gameObject.name.ToString(), itemDef.shortname.ToString());
                }
            }
            InitializeBlocks();
        }
        void InitializeBlocks()
        {
            ConstructionSkin[] allSkins = UnityEngine.Resources.FindObjectsOfTypeAll<ConstructionSkin>();
            foreach (Construction construction in UnityEngine.Resources.FindObjectsOfTypeAll<Construction>())
            {

                Construction.Common item = new Construction.Common(construction, allSkins);
                nameToBlockPrefab.Add(item.name, item.fullname);
            }
        }
        /*void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player.GetComponent<BuildPlayer>() != null)
            {
                if (input.WasJustPressed(BUTTON.FIRE_SECONDARY))
                {
                    DoBuild(player);
                }
            }
        }
        */
        /////////////////////////////////////////////////////
        ///  GENERAL FUNCTIONS
        /////////////////////////////////////////////////////


        bool hasAccess(BasePlayer player)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return false;
            }
            return true;
        }

        bool GetCleanDeployedName(string sourcename, out string name)
        {
            name = "";
            if (sourcename.IndexOf("(Clone)", 0) > -1)
            {
                sourcename = sourcename.Substring(0, sourcename.IndexOf("(Clone)", 0));
                if (deployedToItem.ContainsKey(sourcename))
                {
                    name = deployedToItem[sourcename];
                    return true;
                }
            }
            return false;
        }

        
        static bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            var input = serverinput.GetValue(player) as InputState;
            if (input == null)
                return false;
            if (input.current == null)
                return false;
            if (input.current.aimAngles == null)
                return false;

            viewAngle = Quaternion.Euler(input.current.aimAngles);
            return true;
        }
        static bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            UnityEngine.Ray ray = new UnityEngine.Ray(sourceEye, sourceDir * Vector3.forward);

            var hits = UnityEngine.Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            foreach (var hit in hits)
            {
                if (hit.distance < closestdist)
                {
                    closestdist = hit.distance;
                    closestEnt = hit.collider;
                    closestHitpoint = hit.point;
                }
            }
            if (closestEnt is bool)
                return false;
            return true;
        }
        private static void SpawnStructure(UnityEngine.GameObject prefab, Vector3 pos, Quaternion angles)
        {
            UnityEngine.GameObject build = UnityEngine.Object.Instantiate(prefab);
            if (build == null) return;
            BuildingBlock block = build.GetComponent<BuildingBlock>();
            if (block == null) return;
            block.transform.position = pos;
            block.transform.rotation = angles;
            block.gameObject.SetActive(true);
            block.blockDefinition = Construction.Library.FindByPrefabID(block.prefabID);
            block.SetGrade(BuildingGrade.Enum.Stone);
            block.health = block.MaxHealth();
            block.Spawn(true);
        }
        private static bool isColliding(Vector3 position, float radius)
        {
            UnityEngine.Collider[] colliders = UnityEngine.Physics.OverlapSphere(position, radius);
            foreach (UnityEngine.Collider collider in colliders)
            {
                if (collider.GetComponentInParent<BuildingBlock>())
                    return true;
            }
            return false;
        }
        private static void DoBuild(BuildPlayer buildplayer)
        {
            BasePlayer player = buildplayer.player;
            if (!TryGetPlayerView(player, out currentRot))
            {
                return;
            }
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint))
            {
                return;
            }
            var baseentity = closestEnt as Collider;
            if (baseentity == null)
            {
                return;
            }
            // Check if what the player is looking at is a BuildingBlock (like a wall or something like that)
            var buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null)
            {
                return;
            }
            Construction.Socket[] socketArray = buildingblock.blockDefinition.sockets;
            float distance = 999999f;
            Vector3 newPos = new Vector3(0f, 0f, 0f);
            newRot = new Quaternion(0f, 0f, 0f, 0f);
            foreach (Construction.Socket socket in socketArray)
            {
                if (socket.type == Construction.Socket.Type.Foundation)
                {
                    var currentrelativepos = (buildingblock.transform.rotation * (socket.position * 2f)) + buildingblock.transform.position;
                    if (Vector3.Distance(currentrelativepos, closestHitpoint) < distance)
                    {
                        distance = Vector3.Distance(currentrelativepos, closestHitpoint);
                        newPos = currentrelativepos;
                        newRot = (buildingblock.transform.rotation * socket.rotation);
                    }
                }
            }
            if (isColliding(newPos, 1f))
            {
                return;
            }
            
            UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab("build/foundation");
            if (newPrefab == null)
            {
                return;
            }
            SpawnStructure(newPrefab, newPos, newRot);
        }

        bool TryGetBuildingPlans(string arg, out string buildType, out string prefabName)
        {
            prefabName = "";
            buildType = "";
            if (nameToBlockPrefab.ContainsKey(arg))
            {
                prefabName = nameToBlockPrefab[arg];
                buildType = "building";
                return true;
            }
            return false;
        }
        /////////////////////////////////////////////////////
        ///  CHAT COMMANDS
        /////////////////////////////////////////////////////
        [ChatCommand("build")]
        void cmdChatBuild(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            if (args == null || args.Length == 0)
            {
                if (player.GetComponent<BuildPlayer>())
                {
                    UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                    SendReply(player, "Building Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp");
                return;
            }
            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp");
                return;
            }
            BuildPlayer buildplayer;
            if (player.GetComponent<BuildPlayer>() == null)
                buildplayer = player.gameObject.AddComponent<BuildPlayer>();
            else
                buildplayer = player.GetComponent<BuildPlayer>();

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHealth = 500;
            buildplayer.currentGrade = 2;

            
        }
        [ChatCommand("buildhelp")]
        void cmdChatBuild(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (args == null || args.Length == 0)
            {
                SendReply(player, "======== Buildings ========");
                SendReply(player, "/buildhelp buildings");
                SendReply(player, "======== Deployables ========");
                SendReply(player, "/buildhelp deployables");
                SendReply(player, "======== Trees ========");
                SendReply(player, "/buildhelp trees");
                SendReply(player, "======== Animals ========");
                SendReply(player, "/buildhelp animals");
                return;
            }
            if (args[0].ToLower() == "buildings")
            {
                SendReply(player, "======== Buildings ========");
                SendReply(player, "/build STRUCTURE OPTIONAL:GRADE OPTIONAL:HEALTH");
                SendReply(player, "/build foundation => build a Twigs Foundation");
                SendReply(player, "/build foundation 2 => build a Stone Foundation");
                SendReply(player, "/build wall 3 1 => build a Metal Wall with 1 health");
            }
        }
    }
}
