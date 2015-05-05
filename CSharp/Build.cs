// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Build", "Reneb", "1.0.10")]
    class Build : RustPlugin
    { 
        class BuildPlayer : MonoBehaviour
        {
            
            public BasePlayer player;
            public InputState input;
            public string currentPrefab;
            public string currentType;
            public float currentHealth;
            public Quaternion currentRotate;
            public BuildingGrade.Enum currentGrade;
            public bool ispressed;
            public float lastTickPress;
            public float currentHeightAdjustment;
            public string selection;
            void Awake()
            {
                input = serverinput.GetValue(GetComponent<BasePlayer>()) as InputState;
                player = GetComponent<BasePlayer>();
                enabled = true;
                ispressed = false;

            }
            void Update()
            {
                if (input.WasJustPressed(BUTTON.FIRE_SECONDARY) && !ispressed)
                {
                    
                    lastTickPress = Time.realtimeSinceStartup;
                    ispressed = true;
                    DoAction(this);
                }
                else if (input.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    if((Time.realtimeSinceStartup - lastTickPress) > 1)
                    {
                        DoAction(this);
                    }
                }
                else
                {
                    ispressed = false;
                }
            }
        }
        enum SocketType
        {
            Wall,
            Floor,
            Block,
            FloorTriangle,
            Support
        }


        private MethodInfo CreateEntity;
        private MethodInfo FindPrefab;
        private static FieldInfo serverinput;
        private static Dictionary<string, string> deployedToItem;
        private Dictionary<string, string> nameToBlockPrefab;
        private static Dictionary<string, SocketType> nameToSockets;
        private static Dictionary<SocketType, object> TypeToType;
        private static List<string> resourcesList;
        private static Dictionary<string, string> animalList;
        /// CACHED VARIABLES
        /// 

        private static Quaternion currentRot;
        private static Vector3 closestHitpoint;
        private static object closestEnt;
        private static Quaternion newRot;
        private string buildType;
        private string prefabName;
        private static int defaultGrade;
        private static float defaultHealth;
        private static Vector3 newPos;
        private static float distance;
        private static Dictionary<SocketType, object> sourceSockets;
        private static SocketType targetsocket;
        private static SocketType sourcesocket;
        private static Dictionary<Vector3, Quaternion>  newsockets;
        private static Vector3 VectorUP;
        private static float heightAdjustment;
        private static BasePlayer currentplayer;
        private static Collider currentCollider;
        private static BaseNetworkable currentBaseNet;
        private static List<object> houseList;
        private static List<Vector3> checkFrom;
        private static BuildingBlock fbuildingblock;
        private static BuildingBlock buildingblock;
        private static Item newItem;

        private static Quaternion defaultQuaternion = new Quaternion(0f, 0f, 0f, 1f);
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
            VectorUP = new Vector3(0f, 1f, 0f);
        }

        /////////////////////////////////////////////////////
        ///  Unload()
        ///  When the plugin is unloaded by Oxide
        /////////////////////////////////////////////////////
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(BuildPlayer));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        /////////////////////////////////////////////////////
        ///  Loaded()
        ///  When the server has been initialized and all plugins loaded
        /////////////////////////////////////////////////////
        void OnServerInitialized()
        {
            InitializeBlocks();
            InitializeSockets();
            InitializeDeployables();
            InitializeResources();
            InitializeAnimals();
        }

        /////////////////////////////////////////////////////
        /// Get all animals in an easy list to convert from shortname to full prefabname
        /////////////////////////////////////////////////////
        void InitializeAnimals()
        {
            animalList = new Dictionary<string, string>();
            string[] resourcefiles = GameManifest.Get().resourceFiles;
            foreach (string resourcefile in resourcefiles)
            {
                if (resourcefile.Contains("autospawn/animals"))
                {
                    animalList.Add(resourcefile.Substring(26),resourcefile.Substring(8));
                } 
            }
        }

        /////////////////////////////////////////////////////
        /// Get all resources in an easy list to convert from ID to full prefabname
        /////////////////////////////////////////////////////
        void InitializeResources()
        {
            resourcesList = new List<string>();
            string[] resourcefiles = GameManifest.Get().resourceFiles;
            foreach (string resourcefile in resourcefiles)
            {
                if (resourcefile.Contains("autospawn/resource"))
                {
                    resourcesList.Add(resourcefile.Substring(8));
                }
            }
        }

        /////////////////////////////////////////////////////
        /// Get all deployables in an easy list to convert from shortname to fullname
        /////////////////////////////////////////////////////
        void InitializeDeployables()
        {
            var allItemsDef = UnityEngine.Resources.FindObjectsOfTypeAll<ItemDefinition>();
            foreach (ItemDefinition itemDef in allItemsDef)
            {
                if (itemDef.GetComponent<ItemModDeployable>() != null)
                {
                    deployedToItem.Add(itemDef.displayName.english.ToString().ToLower(), itemDef.shortname.ToString());
                }
            }

        }

        /////////////////////////////////////////////////////
        /// Create New sockets that wont match Rusts, this is exaustive
        /// But at least we can add new sockets later on
        /////////////////////////////////////////////////////
        void InitializeSockets()
        {
            // PrefabName to SocketType 
            nameToSockets = new Dictionary<string, SocketType>();

            // Get all possible sockets from the SocketType
            TypeToType = new Dictionary<SocketType, object>();

            // Sockets that can connect on a Floor / Foundation type
            var FloorType = new Dictionary<SocketType, object>();

            // Floor to Floor sockets
            var FloortoFloor = new Dictionary<Vector3, Quaternion>();
            FloortoFloor.Add(new Vector3(0f, 0f, -3f), new Quaternion(0f, 1f, 0f, 0f));

            //FloortoFloor.Add(new Vector3(-3f, 0f, 0f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            FloortoFloor.Add(new Vector3(-3f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));
            FloortoFloor.Add(new Vector3(0f, 0f, 3f), new Quaternion(0f, 0f, 0f, 1f));
            //FloortoFloor.Add(new Vector3(3f, 0f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            FloortoFloor.Add(new Vector3(3f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            // Floor to FloorTriangle sockets
            var FloortoFT = new Dictionary<Vector3, Quaternion>();
            FloortoFT.Add(new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 1f, 0f, 0f));
            FloortoFT.Add(new Vector3(-1.5f, 0f, 0f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            FloortoFT.Add(new Vector3(0f, 0f, 1.5f), new Quaternion(0f, 0f, 0f, 1f));
            FloortoFT.Add(new Vector3(1.5f, 0f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));

            // Floor to Wall sockets
            var FloortoWall = new Dictionary<Vector3, Quaternion>();
            FloortoWall.Add(new Vector3(0f, 0f, 1.5f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            FloortoWall.Add(new Vector3(-1.5f, 0f, 0f), new Quaternion(0f,1f, 0f, 0f));
            FloortoWall.Add(new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            FloortoWall.Add(new Vector3(1.5f, 0f, 0f), new Quaternion(0f, 0f, 0f,1f));

            // Floor to Support (Pillar) sockets
            var FloortoSupport = new Dictionary<Vector3, Quaternion>();
            FloortoSupport.Add(new Vector3(1.5f, 0f, 1.5f), new Quaternion(0f, 0f,0f, 1f));
            FloortoSupport.Add(new Vector3(-1.5f, 0f, 1.5f), new Quaternion(0f, 0f, 0f, 1f));
            FloortoSupport.Add(new Vector3(1.5f, 0f, -1.5f), new Quaternion(0f, 0.0f, 0f, 1f));
            FloortoSupport.Add(new Vector3(-1.5f, 0f, -1.5f), new Quaternion(0f, 0f, 0f, 1f));

            // Floor to Blocks sockets
            var FloorToBlock = new Dictionary<Vector3, Quaternion>();
            FloorToBlock.Add(new Vector3(0f, 0.1f, 0f), new Quaternion(0f, 1f, 0f, 0f));

            // Adding all informations from the Floor type into the main table
            FloorType.Add(SocketType.Block, FloorToBlock);
            FloorType.Add(SocketType.Support, FloortoSupport);
            FloorType.Add(SocketType.Wall, FloortoWall);
            FloorType.Add(SocketType.Floor, FloortoFloor);
            FloorType.Add(SocketType.FloorTriangle, FloortoFT);
            TypeToType.Add(SocketType.Floor, FloorType);

            // Sockets that can connect on a Wall type
            var WallType = new Dictionary<SocketType, object>();

            // Wall to Wall sockets
            var WallToWall = new Dictionary<Vector3, Quaternion>();
            WallToWall.Add(new Vector3(0f, 0f, -3f), new Quaternion(0f, 1f, 0f, 0f));
            WallToWall.Add(new Vector3(0f, 0f, 3f), new Quaternion(0f, 0f, 0f, 1f));

            // Wall to Wall Floor sockets
            var WallToFloor = new Dictionary<Vector3, Quaternion>();
            WallToFloor.Add(new Vector3(1.5f, 3f, 0f), new Quaternion(0f, 0.7071068f, 0f, -0.7071068f));
            WallToFloor.Add(new Vector3(-1.5f, 3f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
             
            // Adding all informations from the Wall type into the main table
            // Note that you can't add blocks or supports on a wall
            WallType.Add(SocketType.Floor, WallToFloor);
            WallType.Add(SocketType.Wall, WallToWall);
            TypeToType.Add(SocketType.Wall, WallType);

            // Sockets that can connect on a Block type
            var BlockType = new Dictionary<SocketType, object>();

            // Block to Block sockets
            var BlockToBlock = new Dictionary<Vector3, Quaternion>();
            BlockToBlock.Add(new Vector3(0f, 1.5f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            // For safety reasons i didn't put pillars or walls here
            // If needed it could easily be added
            BlockType.Add(SocketType.Block, BlockToBlock);
            TypeToType.Add(SocketType.Block, BlockType);

            // Sockets that can connect on a Floor/Foundation Triangles  type
            var FloorTriangleType = new Dictionary<SocketType, object>();

            // Floor Triangles to Floor Triangles type
            var FTtoFT = new Dictionary<Vector3, Quaternion>();
            FTtoFT.Add(new Vector3(0f, 0f, 0f), new Quaternion(0f,1f, 0f, 0.0000001629207f));
            FTtoFT.Add(new Vector3(-0.75f, 0f, 1.299038f), new Quaternion(0f, 0.4999998f, 0f, -0.8660255f));
            FTtoFT.Add(new Vector3(0.75f, 0f, 1.299038f), new Quaternion(0f, 0.5000001f, 0f, 0.8660254f));
            FloorTriangleType.Add(SocketType.FloorTriangle, FTtoFT);

            // Floor Triangles to Wall type
            var FTtoWall = new Dictionary<Vector3, Quaternion>();
            FTtoWall.Add(new Vector3(0f, 0f, 0f), new Quaternion(0f, 0.7f, 0f, 0.7000001629207f)); 
            FTtoWall.Add(new Vector3(-0.75f, 0f, 1.299038f), new Quaternion(0f, 0.96593f, 0f, -0.25882f));
            FTtoWall.Add(new Vector3(0.75f, 0f, 1.299038f), new Quaternion(0f, -0.25882f, 0f, 0.96593f));
            FloorTriangleType.Add(SocketType.Wall, FTtoWall);

            // Floor Triangles to Floor type is a big fail, need to work on that still
           /* var FTtoFloor = new Dictionary<Vector3, Quaternion>();
            FTtoFloor.Add(new Vector3(0f, 0f, 0f), new Quaternion(0f, 0.7f, 0f, 0.7000001629207f)); 
            FTtoFloor.Add(new Vector3(-0.75f, 0f, 1.299038f), new Quaternion(0f, 0.96593f, 0f, -0.25882f));
            FTtoFloor.Add(new Vector3(0.75f, 0f, 1.299038f), new Quaternion(0f, -0.25882f, 0f, 0.96593f));
            FloorTriangleType.Add(SocketType.Floor, FTtoFloor);
            */

            // So at the moment only Floor and Foundation triangles can connect to easy other.
            TypeToType.Add(SocketType.FloorTriangle, FloorTriangleType);

            nameToSockets.Add("build/foundation", SocketType.Floor);
            nameToSockets.Add("build/foundation.triangle", SocketType.FloorTriangle);
            nameToSockets.Add("build/floor.triangle", SocketType.FloorTriangle);
            nameToSockets.Add("build/roof", SocketType.Floor);
            nameToSockets.Add("build/floor", SocketType.Floor);
            nameToSockets.Add("build/wall", SocketType.Wall);
            nameToSockets.Add("build/wall.doorway", SocketType.Wall);
            nameToSockets.Add("build/wall.window", SocketType.Wall);
            nameToSockets.Add("build/wall.low", SocketType.Wall);
            nameToSockets.Add("build/pillar", SocketType.Support);
            nameToSockets.Add("build/block.halfheight", SocketType.Block);
            nameToSockets.Add("build/block.halfheight.slanted", SocketType.Block);
            nameToSockets.Add("build/block.stair.lshape", SocketType.Block);
            nameToSockets.Add("build/block.stair.ushape", SocketType.Block);
            // Foundation steps are fucked up, i need to look how this works more
            //nameToSockets.Add("build/foundation.steps", SocketType.Floor);

        }
        /////////////////////////////////////////////////////
        /// Get all blocknames from shortname to full prefabname
        /////////////////////////////////////////////////////
        void InitializeBlocks()
        {
            foreach (Construction construction in PrefabAttribute.server.GetAll<Construction>())
            {
                    /*if (construction.name == "foundation.triangle")
                    {
                        Construction.Socket[] socketArray = item.sockets;

                        foreach (Construction.Socket socket in socketArray) 
                        {
                            //Puts(string.Format("{0} {1} {2} {3}", socket.name, socket.type.ToString(), socket.position.ToString(), socket.rotation.w.ToString()));
                            Puts(string.Format("{0} {1} {2} {3} {4}", socket.name, socket.type.ToString(), socket.position.x.ToString(), socket.position.y.ToString(), socket.position.z.ToString()));
                        }
                        Puts("================");
                    }*/
                nameToBlockPrefab.Add(construction.hierachyName, construction.fullName);
            } 
        }

        /////////////////////////////////////////////////////
        ///  GENERAL FUNCTIONS
        /////////////////////////////////////////////////////
        /////////////////////////////////////////////////////
        ///  hasAccess( BasePlayer player )
        ///  Checks if the player has access to this command
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

        /////////////////////////////////////////////////////
        ///  TryGetPlayerView( BasePlayer player, out Quaternion viewAngle )
        ///  Get the angle on which the player is looking at
        ///  Notice that this is very usefull for spectating modes as the default player.transform.rotation doesn't work in this case.
        /////////////////////////////////////////////////////
        static bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            var input = serverinput.GetValue(player) as InputState;
            if (input == null)
                return false;
            if (input.current == null)
                return false;

            viewAngle = Quaternion.Euler(input.current.aimAngles);
            return true;
        }

        /////////////////////////////////////////////////////
        ///  TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        ///  Get the closest entity that the player is looking at
        /////////////////////////////////////////////////////
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
                if (hit.collider.GetComponentInParent<TriggerBase>() == null)
                {
                    if (hit.distance < closestdist)
                    {
                        closestdist = hit.distance;
                        closestEnt = hit.collider;
                        closestHitpoint = hit.point;
                    }
                }
            }
            if (closestEnt is bool)
                return false;
            return true;
        }

        /////////////////////////////////////////////////////
        ///  SpawnDeployable()
        ///  Function to spawn a deployable
        /////////////////////////////////////////////////////
        private static void SpawnDeployable(string prefab, Vector3 pos, Quaternion angles, BasePlayer player)
        {
            newItem = ItemManager.CreateByName(prefab, 1);
            if (newItem == null)
            {
                return;
            }
            if (newItem.info.GetComponent<ItemModDeployable>() == null)
            {
                return;
            }
            var deployable = newItem.info.GetComponent<ItemModDeployable>().entityPrefab.targetObject.GetComponent<Deployable>();
            if (deployable == null)
            {
                return;
            }
            var newBaseEntity = GameManager.server.CreateEntity(deployable.gameObject, pos, angles);
            if (newBaseEntity == null)
            {
                return;
            }
            newBaseEntity.SendMessage("SetDeployedBy", player, UnityEngine.SendMessageOptions.DontRequireReceiver);
            newBaseEntity.SendMessage("InitializeItem", newItem, UnityEngine.SendMessageOptions.DontRequireReceiver);
            newBaseEntity.Spawn(true);
        }

        /////////////////////////////////////////////////////
        ///  SpawnStructure()
        ///  Function to spawn a block structure
        /////////////////////////////////////////////////////
        private static void SpawnStructure(string prefabname, Vector3 pos, Quaternion angles, BuildingGrade.Enum grade, float health)
        {
            UnityEngine.GameObject prefab = GameManager.server.FindPrefab(prefabname);
            if (prefab == null)
            {
                return;
            }
            UnityEngine.GameObject build = UnityEngine.Object.Instantiate(prefab);
            if (build == null) return;
            BuildingBlock block = build.GetComponent<BuildingBlock>();
            if (block == null) return;
            block.transform.position = pos;
            block.transform.rotation = angles;
            block.gameObject.SetActive(true);
            block.blockDefinition = PrefabAttribute.server.Find<Construction>(block.prefabID);
            block.Spawn(true);
            block.SetGrade(grade);
            if(health <= 0f)
                block.health = block.MaxHealth();
            else
                block.health = health;
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            
        }

        /////////////////////////////////////////////////////
        ///  SpawnDeployable()
        ///  Function to spawn a resource (tree, barrel, ores)
        /////////////////////////////////////////////////////
        private static void SpawnResource(string prefab, Vector3 pos, Quaternion angles) 
        {
            UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab(prefab);
            if (newPrefab == null)
            {
                return;
            }
            BaseEntity entity = GameManager.server.CreateEntity(newPrefab, pos, angles);
            if (entity == null) return;
            entity.Spawn(true);
        }

        /////////////////////////////////////////////////////
        ///  isColliding()
        ///  Check if you already placed the structure
        /////////////////////////////////////////////////////
        private static bool isColliding(string name, Vector3 position, float radius)
        {
            UnityEngine.Collider[] colliders = UnityEngine.Physics.OverlapSphere(position, radius);
            foreach (UnityEngine.Collider collider in colliders)
            {
                if (collider.GetComponentInParent<BuildingBlock>())
                {
                    if (collider.GetComponentInParent<BuildingBlock>().blockDefinition.fullName == name)
                            if (Vector3.Distance(collider.transform.position, position) < 0.6f)
                                return true;
                }
            }
            return false;
        }
        /////////////////////////////////////////////////////
        ///  SetGrade(BuildingBlock block, BuildingGrade.Enum level)
        ///  Change grade level of a block
        /////////////////////////////////////////////////////
        private static void SetGrade(BuildingBlock block, BuildingGrade.Enum level)
        {
            block.SetGrade(level);
            block.health = block.MaxHealth();
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        /////////////////////////////////////////////////////
        ///  SetHealth(BuildingBlock block)
        ///  Set max health for a block
        /////////////////////////////////////////////////////
        private static void SetHealth(BuildingBlock block)
        {
            block.health = block.MaxHealth();
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        /////////////////////////////////////////////////////
        ///  DoAction(BuildPlayer buildplayer)
        ///  Called from the BuildPlayer, will handle all different building types
        /////////////////////////////////////////////////////
        private static void DoAction(BuildPlayer buildplayer)
        {
            currentplayer = buildplayer.player;
            if (!TryGetPlayerView(currentplayer, out currentRot))
            {
                return;
            }
            if (!TryGetClosestRayPoint(currentplayer.transform.position, currentRot, out closestEnt, out closestHitpoint))
            {
                return;
            }
            currentCollider = closestEnt as Collider;
            if (currentCollider == null)
            {
                return;
            }
            switch (buildplayer.currentType)
            {
                case "building":
                    DoBuild(buildplayer, currentplayer, currentCollider);
                break;
                case "buildup":
                DoBuildUp(buildplayer, currentplayer, currentCollider);
                break;
                case "deploy":
                DoDeploy(buildplayer, currentplayer, currentCollider);
                break;
                case "plant":
                case "animal":
                DoPlant(buildplayer, currentplayer, currentCollider);
                break;
                case "grade":
                DoGrade(buildplayer, currentplayer, currentCollider);
                break;
                case "heal":
                DoHeal(buildplayer, currentplayer, currentCollider);
                break;
                case "erase":
                DoErase(buildplayer, currentplayer, currentCollider);
                break;
                case "rotate":
                    DoRotation(buildplayer, currentplayer, currentCollider);
                break;
                case "spawning":
                DoSpawn(buildplayer, currentplayer, currentCollider);
                break;
                default:
                return;
            }
        }
        /////////////////////////////////////////////////////
        ///  DoErase(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        ///  Erase function
        /////////////////////////////////////////////////////
        private static void DoErase(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            currentBaseNet = baseentity.GetComponentInParent<BaseNetworkable>();
            if (currentBaseNet == null)
                return;
            currentBaseNet.Kill(BaseNetworkable.DestroyMode.Gib);
        }
        /////////////////////////////////////////////////////
        ///  DoPlant(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        ///  Spawn Trees, Barrels, Animals, Resources
        /////////////////////////////////////////////////////
        private static void DoPlant(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            newPos = closestHitpoint + (VectorUP * buildplayer.currentHeightAdjustment);
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            SpawnResource(buildplayer.currentPrefab, newPos, newRot);
        }
        /////////////////////////////////////////////////////
        ///  DoDeploy(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        ///  Deploy Deployables
        /////////////////////////////////////////////////////
        private static void DoDeploy(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            newPos = closestHitpoint + (VectorUP * buildplayer.currentHeightAdjustment);
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            SpawnDeployable(buildplayer.currentPrefab, newPos, newRot, currentplayer);
        }

        /////////////////////////////////////////////////////
        ///  DoGrade(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        ///  Set building grade
        /////////////////////////////////////////////////////
        private static void DoGrade(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            fbuildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (fbuildingblock == null)
            {
                return;
            }
            SetGrade(fbuildingblock, buildplayer.currentGrade);
            if (buildplayer.selection == "select")
            {
                return;
            }

            houseList = new List<object>();
            checkFrom = new List<Vector3>();
            houseList.Add(fbuildingblock);
            checkFrom.Add(fbuildingblock.transform.position);

            int current = 0;
            while (true)
            {
                current++;
                if (current > checkFrom.Count)
                    break;
                var hits = UnityEngine.Physics.OverlapSphere(checkFrom[current - 1], 3.1f);
                foreach (var hit in hits)
                {
                    if (hit.GetComponentInParent<BuildingBlock>() != null)
                    {
                        fbuildingblock = hit.GetComponentInParent<BuildingBlock>();
                        if (!(houseList.Contains(fbuildingblock)))
                        {
                            houseList.Add(fbuildingblock);
                            checkFrom.Add(fbuildingblock.transform.position);
                            SetGrade(fbuildingblock, buildplayer.currentGrade);
                        }
                    }
                }
            }
        }
        static void DoRotation(BuildingBlock block, Quaternion defaultRotation)
        {
            if (block.blockDefinition == null) return;
            UnityEngine.Transform transform = block.transform;
            if (defaultRotation == defaultQuaternion)
                transform.localRotation *= Quaternion.Euler(block.blockDefinition.rotationAmount);
            else
                transform.localRotation *= defaultRotation;
            block.ClientRPC(null, "UpdateConditionalModels", new object[0]);
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }
        private static void DoRotation(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            var buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null)
            {
                return;
            }
            DoRotation(buildingblock, buildplayer.currentRotate);
            if (buildplayer.selection == "select")
            {
                return;
            }

            houseList = new List<object>();
            checkFrom = new List<Vector3>();
            houseList.Add(buildingblock);
            checkFrom.Add(buildingblock.transform.position);

            int current = 0;
            while (true)
            {
                current++;
                if (current > checkFrom.Count)
                    break;
                var hits = UnityEngine.Physics.OverlapSphere(checkFrom[current - 1], 3.1f);
                foreach (var hit in hits)
                {
                    if (hit.GetComponentInParent<BuildingBlock>() != null)
                    {
                        fbuildingblock = hit.GetComponentInParent<BuildingBlock>();
                        if (!(houseList.Contains(fbuildingblock)))
                        {
                            houseList.Add(fbuildingblock);
                            checkFrom.Add(fbuildingblock.transform.position);
                            DoRotation(fbuildingblock, buildplayer.currentRotate);
                        }
                    }
                }
            }
        }

        /////////////////////////////////////////////////////
        ///  DoHeal(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        ///  Set max health to building
        /////////////////////////////////////////////////////
        private static void DoHeal(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            var buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null)
            {
                return;
            }
            SetHealth(buildingblock);
            if (buildplayer.selection == "select")
            {
                return;
            }

            houseList = new List<object>();
            checkFrom = new List<Vector3>();
            houseList.Add(buildingblock);
            checkFrom.Add(buildingblock.transform.position);

            int current = 0;
            while (true)
            {
                current++;
                if (current > checkFrom.Count)
                    break;
                var hits = UnityEngine.Physics.OverlapSphere(checkFrom[current - 1], 3.1f);
                foreach (var hit in hits)
                {
                    if (hit.GetComponentInParent<BuildingBlock>() != null)
                    {
                        fbuildingblock = hit.GetComponentInParent<BuildingBlock>();
                        if (!(houseList.Contains(fbuildingblock)))
                        {
                            houseList.Add(fbuildingblock);
                            checkFrom.Add(fbuildingblock.transform.position);
                            SetHealth(fbuildingblock);
                        }
                    }
                }
            }
        }

        /////////////////////////////////////////////////////
        ///  DoSpawn(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        ///  Raw spawning building elements, no AI here
        /////////////////////////////////////////////////////
        private static void DoSpawn(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            newPos = closestHitpoint + (VectorUP * buildplayer.currentHeightAdjustment);
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            SpawnStructure(buildplayer.currentPrefab, newPos, newRot, buildplayer.currentGrade, buildplayer.currentHealth);
        }

        /////////////////////////////////////////////////////
        ///  DoBuildUp(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        ///  Raw buildup, you can build anything on top of each other, exept the position, there is no AI
        /////////////////////////////////////////////////////
        private static void DoBuildUp(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            fbuildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (fbuildingblock == null)
            {
                return;
            }
            newPos = fbuildingblock.transform.position + (VectorUP * buildplayer.currentHeightAdjustment);
            newRot = fbuildingblock.transform.rotation;
            if (isColliding(buildplayer.currentPrefab, newPos, 1f))
            {
                return;
            }
            SpawnStructure(buildplayer.currentPrefab, newPos, newRot, buildplayer.currentGrade, buildplayer.currentHealth);
        }

        /////////////////////////////////////////////////////
        ///  DoBuild(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        ///  Fully AIed Build :) see the InitializeSockets for more informations
        /////////////////////////////////////////////////////
        private static void DoBuild(BuildPlayer buildplayer, BasePlayer player, Collider baseentity)
        {
            fbuildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (fbuildingblock == null)
            {
                return;
            }
            distance = 999999f;
            Vector3 newPos = new Vector3(0f, 0f, 0f);
            newRot = new Quaternion(0f, 0f, 0f, 0f);
            ///  Checks if this building has a socket hooked to it self
            ///  If not ... well it won't be able to be built via AI
            if (nameToSockets.ContainsKey(fbuildingblock.blockDefinition.fullName))
            {
                sourcesocket = (SocketType)nameToSockets[fbuildingblock.blockDefinition.fullName];
                // Gets all Sockets that can be connected to the source building
                if (TypeToType.ContainsKey(sourcesocket))
                {
                    sourceSockets = TypeToType[sourcesocket] as Dictionary<SocketType, object>;
                    targetsocket = (SocketType)nameToSockets[buildplayer.currentPrefab];
                    // Checks if the newly built structure can be connected to the source building
                    if (sourceSockets.ContainsKey(targetsocket))
                    {
                        newsockets = sourceSockets[targetsocket] as Dictionary<Vector3, Quaternion>;
                        // Get all the sockets that can be hooked to the source building via the new structure element
                        foreach (KeyValuePair<Vector3, Quaternion> pair in newsockets)
                        {
                            var currentrelativepos = (fbuildingblock.transform.rotation * pair.Key) + fbuildingblock.transform.position;
                            if (Vector3.Distance(currentrelativepos, closestHitpoint) < distance)
                            {
                                // Get the socket that is the closest to where the player is aiming at
                                distance = Vector3.Distance(currentrelativepos, closestHitpoint);
                                newPos = currentrelativepos + (VectorUP * buildplayer.currentHeightAdjustment);
                                newRot = (fbuildingblock.transform.rotation * pair.Value);
                            }
                        }
                    }
                }
            }
            if (newPos.x == 0f)
                return;
            // Checks if the element has already been built to prevent multiple structure elements on one spot
            if (isColliding(buildplayer.currentPrefab,newPos, 1f))
                return;

            SpawnStructure(buildplayer.currentPrefab, newPos, newRot, buildplayer.currentGrade, buildplayer.currentHealth);
        }

        /////////////////////////////////////////////////////
        ///  TryGetBuildingPlans(string arg, out string buildType, out string prefabName)
        ///  Checks if the argument of every commands are valid or not
        /////////////////////////////////////////////////////
        bool TryGetBuildingPlans(string arg, out string buildType, out string prefabName)
        {
            prefabName = "";
            buildType = "";
            int intbuilding = 0;
            if (nameToBlockPrefab.ContainsKey(arg))
            {
                prefabName = nameToBlockPrefab[arg];
                buildType = "building";
                return true;
            }
            else if (deployedToItem.ContainsKey(arg.ToLower()))
            {
                prefabName = deployedToItem[arg.ToLower()];
                buildType = "deploy";
                return true;
            }
            else if (deployedToItem.ContainsValue(arg.ToLower()))
            {
                prefabName = arg.ToLower();
                buildType = "deploy";
                return true;
            }
            else if (animalList.ContainsKey(arg.ToLower()))
            {
                prefabName = animalList[arg.ToLower()];
                buildType = "animal";
                return true;
            }
            else if (int.TryParse(arg, out intbuilding))
            {
                if (intbuilding <= resourcesList.Count )
                {
                    prefabName = resourcesList[intbuilding];
                    buildType = "plant";
                    return true;
                }
            }
            return false;
        }

        /////////////////////////////////////////////////////
        ///  GetGrade(int lvl)
        ///  Convert grade number written by the players into the BuildingGrade.Enum used by rust
        /////////////////////////////////////////////////////
        BuildingGrade.Enum GetGrade(int lvl)
        {
            if (lvl == 0)
                return BuildingGrade.Enum.Twigs;
            else if (lvl == 1)
                return BuildingGrade.Enum.Wood;
            else if (lvl == 2)
                return BuildingGrade.Enum.Stone;
            else if (lvl == 3)
                return BuildingGrade.Enum.Metal;
            return BuildingGrade.Enum.TopTier;
        }

        /////////////////////////////////////////////////////
        ///  hasNoArguments(BasePlayer player, string[] args)
        ///  Action when no arguments were written in the commands
        /////////////////////////////////////////////////////
        bool hasNoArguments(BasePlayer player, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                if (player.GetComponent<BuildPlayer>())
                {
                    UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                    SendReply(player, "Build Tool Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp");
                return true;
            }
            return false;
        } 

        /////////////////////////////////////////////////////
        ///  GetBuildPlayer(BasePlayer player)
        ///  Create or Get the BuildPlayer from a player, where all informations of current action is stored
        /////////////////////////////////////////////////////
        BuildPlayer GetBuildPlayer(BasePlayer player)
        {
            if (player.GetComponent<BuildPlayer>() == null)
                return player.gameObject.AddComponent<BuildPlayer>();
            else
                return player.GetComponent<BuildPlayer>();
        }
        /////////////////////////////////////////////////////
        ///  CHAT COMMANDS
        /////////////////////////////////////////////////////
        [ChatCommand("build")]
        void cmdChatBuild(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (hasNoArguments(player, args)) return;
            
            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            if (buildType != "building")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            BuildPlayer buildplayer = GetBuildPlayer(player);
            
            defaultGrade = 0;
            defaultHealth = -1f;
            heightAdjustment = 0f;

            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);
            if (args.Length > 2) int.TryParse(args[2], out defaultGrade);
            if (args.Length > 3) float.TryParse(args[3], out defaultHealth);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHealth = defaultHealth;
            buildplayer.currentGrade = GetGrade(defaultGrade);
            buildplayer.currentHeightAdjustment = heightAdjustment;
            SendReply(player, string.Format("Building Tool AIBuild: {0} - HeightAdjustment: {1} - Grade: {2} - Health: {3}", args[0], heightAdjustment.ToString(), buildplayer.currentGrade.ToString(), buildplayer.currentHealth.ToString()));
        }
        [ChatCommand("spawn")]
        void cmdChatSpawn(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (hasNoArguments(player, args)) return;

            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            if (buildType == "building") buildType = "spawning";
            else
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            BuildPlayer buildplayer = GetBuildPlayer(player);

            defaultGrade = 0;
            defaultHealth = -1f;
            heightAdjustment = 0f;

            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);
            if (args.Length > 2) int.TryParse(args[2], out defaultGrade);
            if (args.Length > 3) float.TryParse(args[3], out defaultHealth);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHealth = defaultHealth;
            buildplayer.currentGrade = GetGrade(defaultGrade);
            buildplayer.currentHeightAdjustment = heightAdjustment;
            SendReply(player, string.Format("Building Tool RawSpawning: {0} - HeightAdjustment: {1} - Grade: {2} - Health: {3}", args[0], heightAdjustment.ToString(), buildplayer.currentGrade.ToString(), buildplayer.currentHealth.ToString()));
        }
        [ChatCommand("deploy")]
        void cmdChatDeploy(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (hasNoArguments(player, args)) return;

            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp deployables");
                return;
            }
            if (buildType != "deploy")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp deployables");
                return;
            }
            BuildPlayer buildplayer = GetBuildPlayer(player);

            heightAdjustment = 0f;
            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            SendReply(player, string.Format("Building Tool Deploying: {0} - Height Adjustment: {1}", buildplayer.currentPrefab, buildplayer.currentHeightAdjustment.ToString()));
        }
        [ChatCommand("erase")]
        void cmdChatErase(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            BuildPlayer buildplayer = GetBuildPlayer(player);
            if (buildplayer.currentType != null && buildplayer.currentType == "erase")
            {
                UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                SendReply(player, "Building Tool: Remove Deactivated");
            }
            else
            {
                buildplayer.currentType = "erase";
                SendReply(player, "Building Tool: Remove Activated");
            }
        }
        [ChatCommand("plant")]
        void cmdChatPlant(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (hasNoArguments(player, args)) return;

            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp resources");
                return;
            }
            if (buildType != "plant")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp resources");
                return;
            }
            BuildPlayer buildplayer = GetBuildPlayer(player);

            heightAdjustment = 0f;
            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            SendReply(player, string.Format("Building Tool Planting: {0} - HeightAdjustment: {1}", prefabName, buildplayer.currentHeightAdjustment.ToString()));
        }
        [ChatCommand("animal")]
        void cmdChatAnimal(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (hasNoArguments(player, args)) return;

            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp animals");
                return;
            }
            if (buildType != "animal")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp animals");
                return;
            }
            BuildPlayer buildplayer = GetBuildPlayer(player);

            heightAdjustment = 0f;
            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            buildplayer.currentHeightAdjustment = heightAdjustment;

            SendReply(player, string.Format("Building Tool Spawning Animals: {0} - HeightAdjustment: {1}", prefabName, heightAdjustment.ToString()));
        }
        [ChatCommand("buildup")]
        void cmdChatBuildup(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (hasNoArguments(player, args)) return;

            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            if (buildType != "building")
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            BuildPlayer buildplayer = GetBuildPlayer(player);

            buildType = "buildup";
            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            defaultGrade = 0;
            defaultHealth = -1f;
            heightAdjustment = 3f;

            if(args.Length > 1) float.TryParse(args[1], out heightAdjustment);
            if(args.Length > 2) int.TryParse(args[2],out defaultGrade);
            if(args.Length > 3) float.TryParse(args[3], out defaultHealth);


            buildplayer.currentHealth = defaultHealth;
            buildplayer.currentGrade = GetGrade(defaultGrade);
            buildplayer.currentHeightAdjustment = heightAdjustment;
            SendReply(player, string.Format("Building Tool BuildUP: {0} - Height: {1} - Grade: {2} - Health: {3}", args[0], buildplayer.currentHeightAdjustment.ToString(), buildplayer.currentGrade.ToString(), buildplayer.currentHealth.ToString()));
        }
        [ChatCommand("buildgrade")]
        void cmdChatBuilGrade(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (hasNoArguments(player, args)) return;
            BuildPlayer buildplayer = GetBuildPlayer(player);
            defaultGrade = 0;
            buildplayer.selection = "select";
            buildplayer.currentType = "grade";
            int.TryParse(args[0], out defaultGrade);
            if (args.Length > 1)
                if (args[1] == "all")
                    buildplayer.selection = "all";
            buildplayer.currentGrade = GetGrade(defaultGrade);

            SendReply(player, string.Format("Building Tool SetGrade: {0} - for {1}", buildplayer.currentGrade.ToString(), buildplayer.selection));
        }
        [ChatCommand("buildheal")]
        void cmdChatBuilHeal(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (hasNoArguments(player, args)) return;
            BuildPlayer buildplayer = GetBuildPlayer(player);
            
            buildplayer.currentType = "heal";
            buildplayer.selection = "select";
            if (args.Length > 0)
                if (args[0] == "all")
                    buildplayer.selection = "all";

            SendReply(player, string.Format("Building Tool Heal for: {0}", buildplayer.selection));
        }
        [ChatCommand("buildrotate")]
        void cmdChatBuilRotate(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            BuildPlayer buildplayer = GetBuildPlayer(player);

            buildplayer.currentType = "rotate";
            buildplayer.selection = "select";
            float rotate = 0f;
            
            if (args.Length > 0) float.TryParse(args[0], out rotate);
            if (args.Length > 1)
                if (args[1] == "all")
                    buildplayer.selection = "all";
            buildplayer.currentRotate = Quaternion.Euler(0f, rotate, 0f);
            SendReply(player, string.Format("Building Tool Rotation for: {0}", buildplayer.selection));
        }
        [ChatCommand("buildhelp")]
        void cmdChatBuildhelp(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (args == null || args.Length == 0)
            {
                SendReply(player, "======== Buildings ========");
                SendReply(player, "/buildhelp buildings");
                SendReply(player, "/buildhelp grades");
                SendReply(player, "/buildhelp heal");
                SendReply(player, "======== Deployables ========");
                SendReply(player, "/buildhelp deployables");
                SendReply(player, "======== Resources (Trees, Ores, Barrels) ========");
                SendReply(player, "/buildhelp resources");
                SendReply(player, "======== Animals ========");
                SendReply(player, "/buildhelp animals");
                SendReply(player, "======== Erase ========");
                SendReply(player, "/buildhelp erase");
                return;
            }
            if (args[0].ToLower() == "buildings")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/build StructureName Optional:HeightAdjust(can be negative, 0 default) Optional:Grade Optional:Health");
                SendReply(player, "/buildup StructureName Optional:HeightAdjust(can be negative, 3 default) Optional:Grave Optional:Health");
                SendReply(player, "/buildrotate");
                SendReply(player, "======== Usage ========");
                SendReply(player, "/build foundation => build a Twigs Foundation");
                SendReply(player, "/build foundation 0 2 => build a Stone Foundation");
                SendReply(player, "/build wall 0 3 1 => build a Metal Wall with 1 health");
                SendReply(player, "======== List ========");
                SendReply(player, "/build foundation - /build foundation.triangle - /build foundation.steps(not avaible)");
                SendReply(player, "/build block.halfheight - /build block.halfheight.slanted (stairs)");
                SendReply(player, "/build wall - /build wall.low - /build wall.doorway - /build wall.window");
                SendReply(player, "/build floor - /build floor.triangle - /build roof");
            }
            else if (args[0].ToLower() == "grades")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/buildgrade GradeLevel Optional:all => default is only the selected block");
                SendReply(player, "======== Usage ========");
                SendReply(player, "/buildgrade 0 => set grade 0 for the select block");
                SendReply(player, "/buildgrade 2 all => set grade 2 (Stone) for the entire building");
            }
            else if (args[0].ToLower() == "heal")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/buildheal Optional:all => default is only the selected block");
                SendReply(player, "======== Usage ========");
                SendReply(player, "/buildheal all => will heal your entire structure");
            }
            else if (args[0].ToLower() == "deployables")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/deploy \"Deployable Name\" Optional:HeightAdjust(can be negative, 0 default)");
                SendReply(player, "======== Usage ========");
                SendReply(player, "/deploy \"Tool Cupboard\" => build a Tool Cupboard");
            }
            else if (args[0].ToLower() == "resources")
            {
                int i = 0;
                SendReply(player, "======== Commands ========");
                SendReply(player, "/plant \"Resource ID\"");
                SendReply(player, "======== List ========");
                foreach (string resource in resourcesList)
                {
                    SendReply(player, string.Format("{0} - {1}", i.ToString(), resource.Substring(19)));
                    i++;
                }
            }
            else if (args[0].ToLower() == "animals")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/animal \"Name\"");
                SendReply(player, "======== List ========");
                foreach (KeyValuePair<string, string> pair in animalList)
                {
                    SendReply(player, string.Format("{0}", pair.Key));
                }
            }
            else if (args[0].ToLower() == "erase")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/erase => Erase where you are looking at, there is NO all option here to prevent fails :p");
            }
        }
    }
}
