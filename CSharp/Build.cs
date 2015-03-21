// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Build", "Reneb", "1.0")]
    class Build : RustPlugin
    {
        class BuildPlayer : MonoBehaviour
        {
            
            public BasePlayer player;
            public InputState input;
            public string currentPrefab;
            public string currentType;
            public float currentHealth;
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
                    DoAction();
                }
                else if (input.IsDown(BUTTON.FIRE_SECONDARY))
                {
                    if((Time.realtimeSinceStartup - lastTickPress) > 1)
                    {
                        DoAction();
                    }
                }
                else
                {
                    ispressed = false;
                }
            }
            void DoAction()
            {
                if (currentType == "building")
                {
                    DoBuild(this);
                }
                else if (currentType == "buildup")
                {
                    DoBuildUp(this);
                }
                else if (currentType == "deploy")
                {
                    DoDeploy(this);
                }
                else if (currentType == "plant")
                {
                    DoPlant(this);
                }
                else if (currentType == "animal")
                {
                    DoAnimal(this);
                }
                else if (currentType == "grade")
                {
                    DoGrade(this);
                }
                else if (currentType == "heal")
                {
                    DoHeal(this);
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
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(BuildPlayer));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }
        void OnServerInitialized()
        {
            InitializeBlocks();
            InitializeSockets();
            InitializeDeployables();
            InitializeResources();
            InitializeAnimals();
        }
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
        void InitializeSockets()
        {
            nameToSockets = new Dictionary<string, SocketType>();
            TypeToType = new Dictionary<SocketType, object>();
            var FloorType = new Dictionary<SocketType, object>();
            var FloortoFloor = new Dictionary<Vector3, Quaternion>();
            FloortoFloor.Add(new Vector3(0f, 0f, -3f), new Quaternion(0f, 1f, 0f, 0f));
            FloortoFloor.Add(new Vector3(-3f, 0f, 0f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            FloortoFloor.Add(new Vector3(0f, 0f, 3f), new Quaternion(0f, 0f, 0f, 1f));
            FloortoFloor.Add(new Vector3(3f, 0f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));

            var FloortoWall = new Dictionary<Vector3, Quaternion>();
            FloortoWall.Add(new Vector3(0f, 0f, 1.5f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            FloortoWall.Add(new Vector3(-1.5f, 0f, 0f), new Quaternion(0f,1f, 0f, 0f));
            FloortoWall.Add(new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            FloortoWall.Add(new Vector3(1.5f, 0f, 0f), new Quaternion(0f, 0f, 0f,1f));

            var FloortoSupport = new Dictionary<Vector3, Quaternion>();
            FloortoSupport.Add(new Vector3(1.5f, 0f, 1.5f), new Quaternion(0f, 0f,0f, 1f));
            FloortoSupport.Add(new Vector3(-1.5f, 0f, 1.5f), new Quaternion(0f, 0f, 0f, 1f));
            FloortoSupport.Add(new Vector3(1.5f, 0f, -1.5f), new Quaternion(0f, 0.0f, 0f, 1f));
            FloortoSupport.Add(new Vector3(-1.5f, 0f, -1.5f), new Quaternion(0f, 0f, 0f, 1f));

            var FloorToBlock = new Dictionary<Vector3, Quaternion>();
            FloorToBlock.Add(new Vector3(0f, 0.1f, 0f), new Quaternion(0f, 1f, 0f, 0f));
            
            

            FloorType.Add(SocketType.Block, FloorToBlock);
            FloorType.Add(SocketType.Support, FloortoSupport);
            FloorType.Add(SocketType.Wall, FloortoWall);
            FloorType.Add(SocketType.Floor, FloortoFloor);
            TypeToType.Add(SocketType.Floor, FloorType);
            
            var WallType = new Dictionary<SocketType, object>();
            var WallToWall = new Dictionary<Vector3, Quaternion>();
            WallToWall.Add(new Vector3(0f, 0f, -3f), new Quaternion(0f, 1f, 0f, 0f));
            WallToWall.Add(new Vector3(0f, 0f, 3f), new Quaternion(0f, 0f, 0f, 1f));

            var WallToFloor = new Dictionary<Vector3, Quaternion>();
            WallToFloor.Add(new Vector3(1.5f, 3f, 0f), new Quaternion(0f, 1f, 0f, 0f));
            WallToFloor.Add(new Vector3(-1.5f, 3f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            WallType.Add(SocketType.Floor, WallToFloor);
            WallType.Add(SocketType.Wall, WallToWall);
            TypeToType.Add(SocketType.Wall, WallType);

            var BlockType = new Dictionary<SocketType, object>();
            var BlockToBlock = new Dictionary<Vector3, Quaternion>();
            BlockToBlock.Add(new Vector3(0f, 1.5f, 0f), new Quaternion(0f, 0f, 0f, 1f));
            BlockType.Add(SocketType.Block, BlockToBlock);
            TypeToType.Add(SocketType.Block, BlockType);


            var FloorTriangleType = new Dictionary<SocketType, object>();
            var FTtoFT = new Dictionary<Vector3, Quaternion>();
            FTtoFT.Add(new Vector3(0f, 0f, 0f), new Quaternion(0f,1f, 0f, 0.0000001629207f));
            FTtoFT.Add(new Vector3(-0.8f, 0f, 1.3f), new Quaternion(0f, 0.4999998f, 0f, -0.8660255f));
            FTtoFT.Add(new Vector3(0.8f, 0f, 1.3f), new Quaternion(0f, 0.5000001f, 0f, 0.8660254f));
            FloorTriangleType.Add(SocketType.FloorTriangle, FTtoFT);

            /*var FTtoWall = new Dictionary<Vector3, Quaternion>();
            FTtoWall.Add(new Vector3(0f, 0f, 0f), new Quaternion(0f, 0.7f, 0f, 0.7000001629207f)); // THIS ONE IS FINE ... BUT THE OTHER NO XD
            FTtoWall.Add(new Vector3(-0.8f, 0f, 1.3f), new Quaternion(0f, 0.4999998f, 0f, -0.8660255f));
            FTtoWall.Add(new Vector3(0.8f, 0f, 1.3f), new Quaternion(0f, 0.5000001f, 0f, 0.8660254f));

            FloorTriangleType.Add(SocketType.Wall, FTtoWall);*/

            TypeToType.Add(SocketType.FloorTriangle, FloorTriangleType);

            nameToSockets.Add("build/foundation", SocketType.Floor);
            nameToSockets.Add("build/foundation.triangle", SocketType.FloorTriangle);
            nameToSockets.Add("build/floor.triangle", SocketType.FloorTriangle);
            //nameToSockets.Add("build/foundation.steps", SocketType.Floor);
            nameToSockets.Add("build/roof", SocketType.Floor);
            nameToSockets.Add("build/floor", SocketType.Floor);
            nameToSockets.Add("build/wall", SocketType.Wall);
            nameToSockets.Add("build/wall.doorway", SocketType.Wall);
            nameToSockets.Add("build/wall.window", SocketType.Wall);
            nameToSockets.Add("build/wall.low", SocketType.Wall);
            nameToSockets.Add("build/pillar", SocketType.Support);
            nameToSockets.Add("build/block.halfheight", SocketType.Block);
            nameToSockets.Add("build/block.halfheight.slanted", SocketType.Block);
        }
        void InitializeBlocks()
        {
            ConstructionSkin[] allSkins = UnityEngine.Resources.FindObjectsOfTypeAll<ConstructionSkin>();
            foreach (Construction construction in UnityEngine.Resources.FindObjectsOfTypeAll<Construction>())
            {
               
                    Construction.Common item = new Construction.Common(construction, allSkins);
                    /*if (construction.name == "foundation")
                    {
                        Construction.Socket[] socketArray = item.sockets;

                        foreach (Construction.Socket socket in socketArray) 
                        {
                            Puts(string.Format("{0} {1} {2} {3}", socket.name, socket.type.ToString(), socket.position.ToString(), socket.rotation.w.ToString()));
                        }
                        Puts("================");
                    }*/
                    nameToBlockPrefab.Add(item.name, item.fullname);
            }
        }
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
        private static void SpawnDeployable(Item newitem, Vector3 pos, Quaternion angles, BasePlayer player)
        {
            if (newitem.info.GetComponent<ItemModDeployable>() == null)
            {
                return;
            }
            var deployable = newitem.info.GetComponent<ItemModDeployable>().entityPrefab.targetObject.GetComponent<Deployable>();
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
            newBaseEntity.SendMessage("InitializeItem", newitem, UnityEngine.SendMessageOptions.DontRequireReceiver);
            newBaseEntity.Spawn(true);
        }
        private static void SpawnStructure(UnityEngine.GameObject prefab, Vector3 pos, Quaternion angles, BuildingGrade.Enum grade, float health)
        {
            UnityEngine.GameObject build = UnityEngine.Object.Instantiate(prefab);
            if (build == null) return;
            BuildingBlock block = build.GetComponent<BuildingBlock>();
            if (block == null) return;
            block.transform.position = pos;
            block.transform.rotation = angles;
            block.gameObject.SetActive(true);
            block.blockDefinition = Construction.Library.FindByPrefabID(block.prefabID);
            block.Spawn(true);
            block.SetGrade(grade);
            if(health < 0f)
                block.health = block.MaxHealth();
            else
                block.health = health;
            
        }
        private static void SpawnResource(UnityEngine.GameObject prefab, Vector3 pos, Quaternion angles)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, angles);
            if (entity == null) return;
            entity.Spawn(true);
        }
        private static bool isColliding(string name, Vector3 position, float radius)
        {
            UnityEngine.Collider[] colliders = UnityEngine.Physics.OverlapSphere(position, radius);
            foreach (UnityEngine.Collider collider in colliders)
            {
                if (collider.GetComponentInParent<BuildingBlock>())
                {
                        if(collider.GetComponentInParent<BuildingBlock>().blockDefinition.fullname == name)
                            if (Vector3.Distance(collider.transform.position, position) < 0.6f)
                                return true;
                }
            }
            return false;
        }
        private static void DoAnimal(BuildPlayer buildplayer)
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
            newPos = closestHitpoint;
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab(buildplayer.currentPrefab);
            if (newPrefab == null)
            {
                return;
            }
            SpawnResource(newPrefab, newPos, newRot);
        }
        private static void DoPlant(BuildPlayer buildplayer)
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
            newPos = closestHitpoint + (VectorUP * buildplayer.currentHeightAdjustment);
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab(buildplayer.currentPrefab);
            if (newPrefab == null)
            {
                return;
            }
            SpawnResource(newPrefab, newPos, newRot);
        }
        private static void SetGrade(BuildingBlock block, BuildingGrade.Enum level)
        {
            block.SetGrade(level);
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }
        private static void SetHealth(BuildingBlock block)
        {
            block.health = block.MaxHealth();
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }
        private static void DoGrade(BuildPlayer buildplayer)
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
            var buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null)
            {
                return;
            }
            
            SetGrade(buildingblock, buildplayer.currentGrade);
            if (buildplayer.selection == "select")
                {
                return;
            }

            List<object> houseList = new List<object>();
            List<Vector3> checkFrom = new List<Vector3>();
            BuildingBlock fbuildingblock;

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
                            SetGrade(fbuildingblock, buildplayer.currentGrade);
                        }
                    }
                }
            }
        }
        private static void DoHeal(BuildPlayer buildplayer)
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

            List<object> houseList = new List<object>();
            List<Vector3> checkFrom = new List<Vector3>();
            BuildingBlock fbuildingblock;

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
        private static void DoDeploy(BuildPlayer buildplayer)
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
            newPos = closestHitpoint + (VectorUP * buildplayer.currentHeightAdjustment);
            newRot = currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            Item newItem = ItemManager.CreateByName(buildplayer.currentPrefab, 1);
            if (newItem != null)
            {
                SpawnDeployable(newItem, newPos, newRot, player);
            }
        }
        private static void DoBuildUp(BuildPlayer buildplayer)
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
            newPos = buildingblock.transform.position + (VectorUP * buildplayer.currentHeightAdjustment);
            newRot = buildingblock.transform.rotation;
            if (isColliding(buildplayer.currentPrefab, newPos, 1f))
            {
                return;
            }
            UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab(buildplayer.currentPrefab);
            if (newPrefab == null)
            {
                return;
            }
            SpawnStructure(newPrefab, newPos, newRot, buildplayer.currentGrade, buildplayer.currentHealth);
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
            distance = 999999f;
            Vector3 newPos = new Vector3(0f, 0f, 0f);
            newRot = new Quaternion(0f, 0f, 0f, 0f);
            if (nameToSockets.ContainsKey(buildingblock.blockDefinition.fullname))
            {
                sourcesocket = (SocketType)nameToSockets[buildingblock.blockDefinition.fullname];
                if (TypeToType.ContainsKey(sourcesocket))
                {
                    sourceSockets = TypeToType[sourcesocket] as Dictionary<SocketType, object>;
                    targetsocket = (SocketType)nameToSockets[buildplayer.currentPrefab];
                    if (sourceSockets.ContainsKey(targetsocket))
                    {
                        newsockets = sourceSockets[targetsocket] as Dictionary<Vector3, Quaternion>;
                        foreach (KeyValuePair<Vector3, Quaternion> pair in newsockets)
                        {
                            var currentrelativepos = (buildingblock.transform.rotation * pair.Key) + buildingblock.transform.position;
                            if (Vector3.Distance(currentrelativepos, closestHitpoint) < distance)
                            {
                                distance = Vector3.Distance(currentrelativepos, closestHitpoint);
                                newPos = currentrelativepos + (VectorUP * buildplayer.currentHeightAdjustment);
                                newRot = (buildingblock.transform.rotation * pair.Value);
                            }
                        }
                    }
                }
            }
            if (newPos.x == 0f)
            {
                return;
            }
            if (isColliding(buildplayer.currentPrefab,newPos, 1f))
            {
                return;
            }

            UnityEngine.GameObject newPrefab = GameManager.server.FindPrefab(buildplayer.currentPrefab);
            if (newPrefab == null)
            {
                return;
            }
            SpawnStructure(newPrefab, newPos, newRot, buildplayer.currentGrade, buildplayer.currentHealth);
        }

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
                    SendReply(player, "For more informations say: /buildhelp buildings");
                return;
            }
            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            BuildPlayer buildplayer;
            if (player.GetComponent<BuildPlayer>() == null)
                buildplayer = player.gameObject.AddComponent<BuildPlayer>();
            else
                buildplayer = player.GetComponent<BuildPlayer>();

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;
            defaultGrade = 0;
            defaultHealth = -1f;
            heightAdjustment = 0f;

            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);
            if (args.Length > 2) int.TryParse(args[2], out defaultGrade);
            if (args.Length > 3) float.TryParse(args[3], out defaultHealth);

            buildplayer.currentHealth = defaultHealth;
            buildplayer.currentGrade = GetGrade(defaultGrade);
            buildplayer.currentHeightAdjustment = heightAdjustment;
            SendReply(player, string.Format("Building Activated: {0}",args[0]));
        }
        [ChatCommand("deploy")]
        void cmdChatDeploy(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            if (args == null || args.Length == 0)
            {
                if (player.GetComponent<BuildPlayer>())
                {
                    UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                    SendReply(player, "Deploy Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp deployables");
                return;
            }
            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp deployables");
                return;
            }
            BuildPlayer buildplayer;
            if (player.GetComponent<BuildPlayer>() == null)
                buildplayer = player.gameObject.AddComponent<BuildPlayer>();
            else
                buildplayer = player.GetComponent<BuildPlayer>();

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;

            heightAdjustment = 0f;

            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);
            buildplayer.currentHeightAdjustment = heightAdjustment;
            SendReply(player, string.Format("Deploy Activated: {0}", args[0]));
        }
        [ChatCommand("plant")]
        void cmdChatPlant(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            if (args == null || args.Length == 0)
            {
                if (player.GetComponent<BuildPlayer>())
                {
                    UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                    SendReply(player, "Planting Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp resources");
                return;
            }
            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp resources");
                return;
            }
            BuildPlayer buildplayer;
            if (player.GetComponent<BuildPlayer>() == null)
                buildplayer = player.gameObject.AddComponent<BuildPlayer>();
            else
                buildplayer = player.GetComponent<BuildPlayer>();

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;

            heightAdjustment = 0f;

            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);
            buildplayer.currentHeightAdjustment = heightAdjustment;
            SendReply(player, string.Format("Planting Activated: {0}", prefabName));
        }
        [ChatCommand("animal")]
        void cmdChatAnimal(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            if (args == null || args.Length == 0)
            {
                if (player.GetComponent<BuildPlayer>())
                {
                    UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                    SendReply(player, "Spawning Animals Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp animals");
                return;
            }
            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp animals");
                return;
            }
            BuildPlayer buildplayer;
            if (player.GetComponent<BuildPlayer>() == null)
                buildplayer = player.gameObject.AddComponent<BuildPlayer>();
            else
                buildplayer = player.GetComponent<BuildPlayer>();

            buildplayer.currentPrefab = prefabName;
            buildplayer.currentType = buildType;

            heightAdjustment = 0f;

            if (args.Length > 1) float.TryParse(args[1], out heightAdjustment);
            buildplayer.currentHeightAdjustment = heightAdjustment;
            SendReply(player, string.Format("Planting Activated: {0}", prefabName));
        }
        [ChatCommand("buildup")]
        void cmdChatBuildup(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            if (args == null || args.Length == 0)
            {
                if (player.GetComponent<BuildPlayer>())
                {
                    UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                    SendReply(player, "BuildUp Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp buildings");
                return;
            }
            if (!TryGetBuildingPlans(args[0], out buildType, out prefabName))
            {
                SendReply(player, "Invalid Argument 1: For more informations say: /buildhelp buildings");
                return;
            }
            BuildPlayer buildplayer;
            if (player.GetComponent<BuildPlayer>() == null)
                buildplayer = player.gameObject.AddComponent<BuildPlayer>();
            else
                buildplayer = player.GetComponent<BuildPlayer>();
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
            SendReply(player, string.Format("BuildUp Activated: {0}", args[0]));
        }
        [ChatCommand("buildgrade")]
        void cmdChatBuilGrade(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            if (args == null || args.Length == 0)
            {
                if (player.GetComponent<BuildPlayer>())
                {
                    UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                    SendReply(player, "SetGrade Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp grades");
                return;
            }
            BuildPlayer buildplayer;
            if (player.GetComponent<BuildPlayer>() == null)
                buildplayer = player.gameObject.AddComponent<BuildPlayer>();
            else
                buildplayer = player.GetComponent<BuildPlayer>();
            buildType = "buildup";
            buildplayer.currentType = "grade";
            buildplayer.selection = "select";
            defaultGrade = 0;
            int.TryParse(args[0], out defaultGrade);
            if (args.Length > 1)
            {
                if (args[1] == "all")
                {
                    buildplayer.selection = "all";
                }
            }
            buildplayer.currentGrade = GetGrade(defaultGrade);
            SendReply(player, string.Format("SetGrave to level {0} Activated for {1}", args[0], buildplayer.selection));
        }
        [ChatCommand("buildheal")]
        void cmdChatBuilHeal(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            if (args == null || args.Length == 0)
            {
                if (player.GetComponent<BuildPlayer>())
                {
                    UnityEngine.GameObject.Destroy(player.GetComponent<BuildPlayer>());
                    SendReply(player, "Heal Deactivated");
                }
                else
                    SendReply(player, "For more informations say: /buildhelp heal");
                return;
            }
            BuildPlayer buildplayer;
            if (player.GetComponent<BuildPlayer>() == null)
                buildplayer = player.gameObject.AddComponent<BuildPlayer>();
            else
                buildplayer = player.GetComponent<BuildPlayer>();
            buildType = "buildup";
            buildplayer.currentType = "heal";
            buildplayer.selection = "select";
            defaultGrade = 0;
            int.TryParse(args[0], out defaultGrade);
            if (args.Length > 0)
            { 
                if (args[0] == "all")
                {
                    buildplayer.selection = "all";
                }
            }
            SendReply(player, string.Format("Heal Activated for {0}", buildplayer.selection));
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
                return;
            }
            if (args[0].ToLower() == "buildings")
            {
                SendReply(player, "======== Commands ========");
                SendReply(player, "/build StructureName Optional:HeightAdjust(can be negative, 0 default) Optional:Grade Optional:Health");
                SendReply(player, "/buildup StructureName Optional:HeightAdjust(can be negative, 3 default) Optional:Grave Optional:Health");
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
        }
    }
}
