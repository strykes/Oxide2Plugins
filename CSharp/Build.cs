using Facepunch;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Build", "Reneb & NoGrod", "2.0.82")]
    public class Build : RustPlugin
    {
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

        bool permBuildForAll = false;
        
        private static float mdist = 9999f;

        private static int generalColl = LayerMask.GetMask("Construction", "Deployable", "Default", "Prevent Building", "Deployed", "Resource", "Terrain", "Water", "World","Tree");
        private static int constructionColl = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Deployable", "Prevent Building", "Deployed" });

        private string green = "0 1 0 0.5"; 
        private string red = "1 0 0 0.5"; 
        private string gray = "0.5 0.5 0.5 0.5";
        private string permBuild = "build.perm";

        private static Vector3 newPos = new Vector3(0f, 0f, 0f);
        private static Quaternion newRot = new Quaternion(0f, 0f, 0f, 0f);
        private static Quaternion defaultQuaternion = Quaternion.Euler(0f, 45f, 0f);
        private static BuildingConstruction sourcebuild = null;
        private static BuildingConstruction targetbuild = null;
        private static Dictionary<string, BuildingConstruction> buildings = new Dictionary<string, BuildingConstruction>();
        private static Dictionary<Building, string> BuildingToPrefab = new Dictionary<Building, string>();
        private static Dictionary<string, uint> deployables = new Dictionary<string, uint>();
        private static List<string> resourcesList = new List<string>();
        private static List<object> houseList = new List<object>();
        private static List<Vector3> checkFrom = new List<Vector3>();

        private Dictionary<ulong, PlayerBuild> playerBuild = new Dictionary<ulong, PlayerBuild>();
        private Dictionary<ulong, bool> buildToggled = new Dictionary<ulong, bool>();

        private enum Placement
        {
            Auto,
            Up,
            Force
        }

        private enum PlayerBuildType
        {
            Build,
            Deploy,
            Spawn,
            Grade,
            Heal,
            Rotate,
            Erase,
            None
        }

        private enum Building
        {
            Foundation,
            FoundationTriangle,
            FoundationSteps,
            Floor,
            FloorTriangle,
            FloorFrame,
            Wall,
            WallDoorway,
            WallWindow,
            WallFrame,
            WallHalf,
            WallLow,
            StairsLShaped,
            StairsUShaped,
            Roof
        }

        private enum Selection
        {
            Select,
            All
        }

        private enum SocketType
        {
            Wall,
            Floor,
            Steps,
            FloorTriangle,
            Block,
            Roof
        }

        bool hasAuth(BasePlayer player)
        {
            return (permBuildForAll || (permission.UserHasPermission(player.userID.ToString(), permBuild)));
        }

        void OnServerInitialized()
        {
            permission.RegisterPermission(permBuild, this);

            BuildingToPrefab = new Dictionary<Building, string>
            {
                {Building.Foundation, "assets/prefabs/building core/foundation/foundation.prefab" },
                {Building.Floor, "assets/prefabs/building core/floor/floor.prefab" },
                {Building.FloorFrame, "assets/prefabs/building core/floor.frame/floor.frame.prefab" },
                {Building.FoundationSteps, "assets/prefabs/building core/foundation.steps/foundation.steps.prefab" },
                {Building.Wall,"assets/prefabs/building core/wall/wall.prefab" },
                {Building.WallDoorway,"assets/prefabs/building core/wall.doorway/wall.doorway.prefab" },
                {Building.WallFrame,"assets/prefabs/building core/wall.frame/wall.frame.prefab" },
                {Building.WallHalf,"assets/prefabs/building core/wall.half/wall.half.prefab" },
                {Building.WallLow,"assets/prefabs/building core/wall.low/wall.low.prefab" },
                {Building.WallWindow,"assets/prefabs/building core/wall.window/wall.window.prefab" },
                {Building.FoundationTriangle,"assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab"},
                {Building.FloorTriangle,"assets/prefabs/building core/floor.triangle/floor.triangle.prefab" },
                {Building.StairsLShaped,"assets/prefabs/building core/stairs.l/block.stair.lshape.prefab" },
                {Building.Roof,"assets/prefabs/building core/roof/roof.prefab" },
                {Building.StairsUShaped,"assets/prefabs/building core/stairs.u/block.stair.ushape.prefab" }
            };

            buildings.Clear();

            var foundationSockets = new BuildingSocket(SocketType.Floor);
            foundationSockets.AddTargetSock(SocketType.Floor, new Vector3(0, 0, -3f), new Quaternion(0, 1f, 0, 0));
            foundationSockets.AddTargetSock(SocketType.Floor, new Vector3(-3f, 0, 0), new Quaternion(0, 0f, 0, 1f));
            foundationSockets.AddTargetSock(SocketType.Floor, new Vector3(0f, 0f, 3f), new Quaternion(0f, 0f, 0f, 1f));
            foundationSockets.AddTargetSock(SocketType.Floor, new Vector3(3f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            foundationSockets.AddTargetSock(SocketType.FloorTriangle, new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 1f, 0f, 0f));
            foundationSockets.AddTargetSock(SocketType.FloorTriangle, new Vector3(-1.5f, 0f, 0f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            foundationSockets.AddTargetSock(SocketType.FloorTriangle, new Vector3(0f, 0f, 1.5f), new Quaternion(0f, 0f, 0f, 1f));
            foundationSockets.AddTargetSock(SocketType.FloorTriangle, new Vector3(1.5f, 0f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));

            foundationSockets.AddTargetSock(SocketType.Wall, new Vector3(0f, 0f, 1.5f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            foundationSockets.AddTargetSock(SocketType.Wall, new Vector3(-1.5f, 0f, 0f), new Quaternion(0f, 1f, 0f, 0f));
            foundationSockets.AddTargetSock(SocketType.Wall, new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            foundationSockets.AddTargetSock(SocketType.Wall, new Vector3(1.5f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            foundationSockets.AddTargetSock(SocketType.Roof, new Vector3(0f, 0f, 3f), new Quaternion(0f, 1f, 0f, 0f));
            foundationSockets.AddTargetSock(SocketType.Roof, new Vector3(-3f, 0f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            foundationSockets.AddTargetSock(SocketType.Roof, new Vector3(0f, 0f, -3f), new Quaternion(0f, 0f, 0f, 1f));
            foundationSockets.AddTargetSock(SocketType.Roof, new Vector3(3f, 0f, 0f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));

            foundationSockets.AddTargetSock(SocketType.Block, new Vector3(0f, 0.1f, 0f), new Quaternion(0f, 1f, 0f, 0f));
             
            foundationSockets.AddTargetSock(SocketType.Steps, new Vector3(0f, 0f, 1.5f), new Quaternion(0f, -0.7071068f, 0f, 0.7071068f));
            foundationSockets.AddTargetSock(SocketType.Steps, new Vector3(-1.5f, 0f, 0f), new Quaternion(0f, 1f, 0f, 0f));
            foundationSockets.AddTargetSock(SocketType.Steps, new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            foundationSockets.AddTargetSock(SocketType.Steps, new Vector3(1.5f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            buildings.Add("assets/prefabs/building core/foundation/foundation.prefab", new BuildingConstruction("assets/prefabs/building core/foundation/foundation.prefab", foundationSockets, Building.Foundation));
            buildings.Add("assets/prefabs/building core/floor/floor.prefab", new BuildingConstruction("assets/prefabs/building core/floor/floor.prefab", foundationSockets, Building.Floor));
            buildings.Add("assets/prefabs/building core/floor.frame/floor.frame.prefab", new BuildingConstruction("assets/prefabs/building core/floor.frame/floor.frame.prefab", foundationSockets, Building.FloorFrame));

            var wallSockets = new BuildingSocket(SocketType.Wall);
            wallSockets.AddTargetSock(SocketType.Wall, new Vector3(0f, 0f, -3f), new Quaternion(0f, 0f, 0f, 1f));
            wallSockets.AddTargetSock(SocketType.Wall, new Vector3(0f, 0f, 3f), new Quaternion(0f, 0f, 0f, 1f));

            wallSockets.AddTargetSock(SocketType.Floor, new Vector3(1.5f, 3f, 0f), new Quaternion(0f, 0.7071068f, 0f, -0.7071068f));
            wallSockets.AddTargetSock(SocketType.Floor, new Vector3(-1.5f, 3f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));

            wallSockets.AddTargetSock(SocketType.Roof, new Vector3(1.5f, 3f, 0f), new Quaternion(0f, 0.7071068f, 0f, -0.7071068f));
            wallSockets.AddTargetSock(SocketType.Roof, new Vector3(-1.5f, 3f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));


            buildings.Add("assets/prefabs/building core/wall/wall.prefab", new BuildingConstruction("assets/prefabs/building core/wall/wall.prefab", wallSockets, Building.Wall));
            buildings.Add("assets/prefabs/building core/wall.doorway/wall.doorway.prefab", new BuildingConstruction("assets/prefabs/building core/wall.doorway/wall.doorway.prefab", wallSockets, Building.Wall));
            buildings.Add("assets/prefabs/building core/wall.frame/wall.frame.prefab", new BuildingConstruction("assets/prefabs/building core/wall.frame/wall.frame.prefab", wallSockets, Building.WallFrame));
            buildings.Add("assets/prefabs/building core/wall.half/wall.half.prefab", new BuildingConstruction("assets/prefabs/building core/wall.half/wall.half.prefab", wallSockets, Building.WallHalf));
            buildings.Add("assets/prefabs/building core/wall.low/wall.low.prefab", new BuildingConstruction("assets/prefabs/building core/wall.low/wall.low.prefab", wallSockets, Building.WallLow));
            buildings.Add("assets/prefabs/building core/wall.window/wall.window.prefab", new BuildingConstruction("assets/prefabs/building core/wall.window/wall.window.prefab", wallSockets, Building.WallWindow));

            var stepsSockets = new BuildingSocket(SocketType.Steps);

            stepsSockets.AddTargetSock(SocketType.Wall, new Vector3(0f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));
            stepsSockets.AddTargetSock(SocketType.Wall, new Vector3(3f, 1.5f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            stepsSockets.AddTargetSock(SocketType.Floor, new Vector3(4.5f, 1.5f, 0f), new Quaternion(0f, 0.7071068f, 0f, -0.7071068f));
            stepsSockets.AddTargetSock(SocketType.Floor, new Vector3(-1.5f, 0f, 0f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            buildings.Add("assets/prefabs/building core/foundation.steps/foundation.steps.prefab", new BuildingConstruction("assets/prefabs/building core/foundation.steps/foundation.steps.prefab", stepsSockets, Building.FoundationSteps));

            var trianglesSockets = new BuildingSocket(SocketType.FloorTriangle);
            trianglesSockets.AddTargetSock(SocketType.FloorTriangle, new Vector3(0f, 0f, 0f), new Quaternion(0f, 1f, 0f, 0.0000001629207f));
            trianglesSockets.AddTargetSock(SocketType.FloorTriangle, new Vector3(-0.75f, 0f, 1.299038f), new Quaternion(0f, 0.4999998f, 0f, -0.8660255f));
            trianglesSockets.AddTargetSock(SocketType.FloorTriangle, new Vector3(0.75f, 0f, 1.299038f), new Quaternion(0f, 0.5000001f, 0f, 0.8660254f));

            trianglesSockets.AddTargetSock(SocketType.Wall, new Vector3(0f, 0f, 0f), new Quaternion(0f, 0.7f, 0f, 0.7000001629207f));
            trianglesSockets.AddTargetSock(SocketType.Wall, new Vector3(-0.75f, 0f, 1.299038f), new Quaternion(0f, 0.96593f, 0f, -0.25882f));
            trianglesSockets.AddTargetSock(SocketType.Wall, new Vector3(0.75f, 0f, 1.299038f), new Quaternion(0f, -0.25882f, 0f, 0.96593f));

            trianglesSockets.AddTargetSock(SocketType.Roof, new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 0f, 0f, 1f));
            trianglesSockets.AddTargetSock(SocketType.Roof, new Vector3(-2.0490381f, 0f, 2.0490381f), new Quaternion(0f, 0.8660254f, 0f, 0.5f));
            trianglesSockets.AddTargetSock(SocketType.Roof, new Vector3(2.0490381f, 0f, 2.0490381f), new Quaternion(0f, -0.8660254f, 0f, 0.5f));

            trianglesSockets.AddTargetSock(SocketType.Floor, new Vector3(0f, 0f, -1.5f), new Quaternion(0f, 1f, 0f, 0f));
            trianglesSockets.AddTargetSock(SocketType.Floor, new Vector3(-2.0490381f, 0f, 2.0490381f), new Quaternion(0f, 0.5f, 0f, -0.8660254f));
            trianglesSockets.AddTargetSock(SocketType.Floor, new Vector3(2.0490381f, 0f, 2.0490381f), new Quaternion(0f, 0.5f, 0f, 0.8660254f));

            buildings.Add("assets/prefabs/building core/floor.triangle/floor.triangle.prefab", new BuildingConstruction("assets/prefabs/building core/floor.triangle/floor.triangle.prefab", trianglesSockets, Building.FloorTriangle));
            buildings.Add("assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab", new BuildingConstruction("assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab", trianglesSockets, Building.FoundationTriangle));

            buildings.Add("assets/prefabs/building core/stairs.l/block.stair.lshape.prefab", new BuildingConstruction("assets/prefabs/building core/stairs.l/block.stair.lshape.prefab", new BuildingSocket(SocketType.Block), Building.StairsLShaped));
            buildings.Add("assets/prefabs/building core/stairs.u/block.stair.ushape.prefab", new BuildingConstruction("assets/prefabs/building core/stairs.u/block.stair.ushape.prefab", new BuildingSocket(SocketType.Block), Building.StairsUShaped));

            var roofSockets = new BuildingSocket(SocketType.Roof);
            roofSockets.AddTargetSock(SocketType.Roof, new Vector3(0f, 3f, -3f), new Quaternion(0f, 0f, 0f, 1f));
            roofSockets.AddTargetSock(SocketType.Roof, new Vector3(-3f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));
            roofSockets.AddTargetSock(SocketType.Roof, new Vector3(3f, 0f, 0f), new Quaternion(0f, 0f, 0f, 1f));

            roofSockets.AddTargetSock(SocketType.Wall, new Vector3(0f, 3f, -1.5f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));

            roofSockets.AddTargetSock(SocketType.Floor, new Vector3(0f, 3f, -3f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));
            roofSockets.AddTargetSock(SocketType.Floor, new Vector3(0f, 0f, 3f), new Quaternion(0f, 0.7071068f, 0f, 0.7071068f));

            buildings.Add("assets/prefabs/building core/roof/roof.prefab", new BuildingConstruction("assets/prefabs/building core/roof/roof.prefab", roofSockets, Building.Roof));

            foreach (var d in GetAllPrefabs<Construction>())
            {
                if (d.deployable != null && d.isServer)
                {
                    string name = d.hierachyName.Replace("_deployed", "").Replace(".deployed", "");
                    if(!deployables.ContainsKey(name))
                        deployables.Add(name, d.prefabID);
                }
            }
            deployables = deployables.OrderBy(w => w.Key).ToDictionary(t => t.Key, t => t.Value);

            resourcesList.Clear();

            var bundlesField = typeof(AssetBundleBackend).GetField("bundles", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var filesField = typeof(AssetBundleBackend).GetField("files", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var manifestField = typeof(AssetBundleBackend).GetField("manifest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            
            var manifest = manifestField.GetValue(FileSystem.Backend) as AssetBundleManifest;
            var bundles = bundlesField.GetValue(FileSystem.Backend) as Dictionary<string, AssetBundle>;
            
            var files = (Dictionary<string, AssetBundle>) filesField.GetValue(FileSystem.Backend);
            foreach (var str in files.Keys)
            {
                if (str.EndsWith(".prefab"))
                {
                    if (str.Contains(".worldmodel.")
                    || str.Contains("/fx/")
                    || str.Contains("/effects/")
                    || str.Contains("/build/skins/")
                    || str.Contains("/_unimplemented/")
                    || str.Contains("/ui/")
                    || str.Contains("/sound/")
                    || str.Contains("/footsteps/")
                    || str.Contains("/sounds/")
                    || str.Contains("/world/")
                    || str.Contains("/env/")
                    || str.Contains("/clothing/")
                    || str.Contains("/skins/")
                    || str.Contains("/decor/")
                    || str.Contains("/monument/")
                    || str.Contains("/projectiles/")
                    || str.Contains("/meat_")
                    || str.EndsWith(".skin.prefab")
                    || str.EndsWith(".viewmodel.prefab") 
                    || str.EndsWith("_test.prefab")
                    || str.EndsWith("_collision.prefab")
                    || str.EndsWith("_ragdoll.prefab")
                    || str.EndsWith("_skin.prefab")
                    || str.Contains("localization")
                    || str.Contains("/skin/")
                    || str.Contains("/materials/")
                    || str.Contains("/system/")
                    || str.Contains("/physicmaterials/")
                    || str.Contains("/image effects/")
                    || str.Contains("/icons/")
                    || str.Contains("/system/")
                    || str.Contains("/server/")
                    || str.Contains("/clutter/"))
                        continue;

                    var gmobj = GameManager.server.FindPrefab(str);

                    if (gmobj?.GetComponent<BaseEntity>() != null && !resourcesList.Contains(str))
                    {
                        resourcesList.Add(str);
                    }
                }
            }
        }

        void OnPlayerInput(BasePlayer player, InputState state)
        {
            if(state.WasJustPressed(BUTTON.FIRE_THIRD))
            {
                if (hasAuth(player))
                {
                    if (!buildToggled.ContainsKey(player.userID))
                    {
                        if (!playerBuild.ContainsKey(player.userID))
                        {
                            playerBuild.Add(player.userID, new PlayerBuild(player));
                        }
                        BuildMenu_Toggle(player);
                        buildToggled.Add(player.userID, true);
                    }
                    else
                    {
                        BuildMenu_UnToggle(player);
                        buildToggled.Remove(player.userID);
                    }
                }
            }
            else if(playerBuild.ContainsKey(player.userID) && state.IsDown(BUTTON.FIRE_SECONDARY))
            {
                PlayerBuild pB = playerBuild[player.userID];
                if (!state.WasJustPressed(BUTTON.FIRE_SECONDARY))
                {
                    if((Time.realtimeSinceStartup - pB.lastClick) < 0.5f)
                    {
                        return;
                    }
                }
                pB.lastClick = Time.realtimeSinceStartup;

                pB.Execute();
            }
        }

        #region BuildMenu
        void BuildMenu_Toggle(BasePlayer player)
        {
            PlayerBuild pB = playerBuild[player.userID];

            BuildMenu_UnToggle(player);

            CuiElementContainer page_container = UI.CreateElementContainer("Overlay", "BuildMenuContainer", "0.1 0.1 0.1 0.8", "0.1 0.3", "0.9 0.5", true);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.buildType == PlayerBuildType.Build ? green : gray, "Build", 16, "0.01 0.05", "0.11 0.5", "build.select build", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.buildType == PlayerBuildType.Deploy ? green : gray, "Deploy", 16, "0.12 0.05", "0.22 0.5", "build.select deploy", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.buildType == PlayerBuildType.Spawn ? green : gray, "Spawn", 16, "0.23 0.05", "0.33 0.5", "build.select spawn", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.buildType == PlayerBuildType.Grade ? green : gray, "Grade", 16, "0.34 0.05", "0.44 0.5", "build.select grade", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.buildType == PlayerBuildType.Rotate ? green : gray, "Rotate", 16, "0.45 0.05", "0.55 0.5", "build.select rotate", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.buildType == PlayerBuildType.Heal ? green : gray, "Heal", 16, "0.56 0.05", "0.66 0.5", "build.select heal", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.buildType == PlayerBuildType.Erase ? green : gray, "Erase", 16, "0.67 0.05", "0.77 0.5", "build.select erase", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.buildType == PlayerBuildType.None ? green : gray, "None", 16, "0.78 0.05", "0.88 0.5", "build.select none", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", red, "Undo", 16, "0.89 0.05", "0.99 0.5", "build.select undo", TextAnchor.MiddleCenter);

            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.crosshair ? green : gray, "Crosshair", 10, "0.78 0.80", "0.99 0.99", "build.select crosshair", TextAnchor.MiddleCenter);
            UI.CreateButton(ref page_container, "BuildMenuContainer", pB.player.modelState.flying ? green : gray, "Noclip", 10, "0.78 0.60", "0.99 0.79", "build.select noclip", TextAnchor.MiddleCenter);

            CuiHelper.AddUi(player, page_container);

            BuildMenu_Sub(player);
        }

        void BuildMenu_UnToggle(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BuildMenuContainer");
            CuiHelper.DestroyUi(player, "BuildMenuContainerSub");
        }

        void BuildMenu_Sub(BasePlayer player)
        {
            PlayerBuild pB = playerBuild[player.userID];

            if (pB.buildType == PlayerBuildType.None || pB.buildType == PlayerBuildType.Erase) return;

            string sizeFrom = (pB.buildType == PlayerBuildType.Grade) || (pB.buildType == PlayerBuildType.Heal) || (pB.buildType == PlayerBuildType.Rotate) ? "0.1 0.20" : "0.1 0.05";
            string sizeTo = "0.9 0.30";
            CuiElementContainer page_container = UI.CreateElementContainer("Overlay", "BuildMenuContainerSub", "0.1 0.1 0.1 0.8", sizeFrom, sizeTo, true);

            if ((pB.buildType == PlayerBuildType.Grade) || (pB.buildType == PlayerBuildType.Heal) || (pB.buildType == PlayerBuildType.Rotate))
            {
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.select == Selection.Select ? green : gray, "Select", 16, "0.01 0.50", "0.20 0.95", "build.select select select", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.select == Selection.All ? green : gray, "All", 16, "0.21 0.50", "0.40 0.95", "build.select select all", TextAnchor.MiddleCenter);
            }

            if ((pB.buildType == PlayerBuildType.Build) || (pB.buildType == PlayerBuildType.Spawn) || (pB.buildType == PlayerBuildType.Deploy))
            {
                UI.CreateLabel(ref page_container, "BuildMenuContainerSub", gray, "Height:", 12, "0.31 0.90", "0.40 0.99", TextAnchor.MiddleCenter);

                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "-3", 12, "0.41 0.90", "0.45 0.99", "build.select height minus 3", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "-1.5", 12, "0.45 0.90", "0.49 0.99", "build.select height minus 1.5", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "0", 12, "0.49 0.90", "0.53 0.99", "build.select height reset", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "+1.5", 12, "0.53 0.90", "0.57 0.99", "build.select height plus 1.5", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "+3", 12, "0.57 0.90", "0.61 0.99", "build.select height plus 3", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "----", 9, "0.62 0.92", "0.65 0.97", "build.select height minus 10", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "---", 9, "0.65 0.92", "0.68 0.97", "build.select height minus 1", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "--", 9, "0.68 0.92", "0.71 0.97", "build.select height minus 0.1", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "-", 9, "0.71 0.92", "0.74 0.97", "build.select height minus 0.01", TextAnchor.MiddleCenter);
                UI.CreateLabel(ref page_container, "BuildMenuContainerSub", gray, pB.height.ToString(), 12, "0.75 0.90", "0.80 0.99", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "+", 9, "0.81 0.92", "0.84 0.97", "build.select height plus 0.01", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "++", 9, "0.84 0.92", "0.87 0.97", "build.select height plus 0.1", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "+++", 9, "0.87 0.92", "0.90 0.97", "build.select height plus 1", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "++++", 9, "0.90 0.92", "0.93 0.97", "build.select height plus 10", TextAnchor.MiddleCenter);
            }

            if (pB.buildType == PlayerBuildType.Build)
            {
                UI.CreateLabel(ref page_container, "BuildMenuContainerSub", gray, "Placement:", 16, "0.01 0.80", "0.10 0.99", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.placement == Placement.Auto ? green : gray, "Auto", 16, "0.11 0.80", "0.15 0.99", "build.select placement auto", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.placement == Placement.Force ? green : gray, "Force", 16, "0.16 0.80", "0.20 0.99", "build.select placement force", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.placement == Placement.Up ? green : gray, "Up", 16, "0.21 0.80", "0.25 0.99", "build.select placement up", TextAnchor.MiddleCenter);

                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.Twigs ? green : gray, "Twig", 16, "0.01 0.60", "0.20 0.79", "build.select grade 0", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.Wood ? green : gray, "Wood", 16, "0.21 0.60", "0.40 0.79", "build.select grade 1", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.Stone ? green : gray, "Stone", 16, "0.41 0.60", "0.60 0.79", "build.select grade 2", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.Metal ? green : gray, "Metal", 16, "0.61 0.60", "0.80 0.79", "build.select grade 3", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.TopTier ? green : gray, "TopTier", 16, "0.81 0.60", "0.99 0.79", "build.select grade 4", TextAnchor.MiddleCenter);

                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.Foundation ? green : gray, "Foundation", 12, "0.01 0.05", "0.0753 0.5", "build.select build foundation", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.FoundationTriangle ? green : gray, "Foundation Triangle", 12, "0.0753 0.05", "0.1406 0.5", "build.select build foundation.triangle", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.FoundationSteps ? green : gray, "Foundation Steps", 12, "0.1406 0.05", "0.206 0.5", "build.select build foundation.steps", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.Floor ? green : gray, "Floor", 12, "0.206 0.05", "0.271 0.5", "build.select build floor", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.FloorTriangle ? green : gray, "Floor Triangle", 12, "0.271 0.05", "0.337 0.5", "build.select build floor.triangle", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.FloorFrame ? green : gray, "Floor Frame", 12, "0.337 0.05", "0.402 0.5", "build.select build floor.frame", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.Wall ? green : gray, "Wall", 12, "0.402 0.05", "0.467 0.5", "build.select build wall", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.WallDoorway ? green : gray, "Wall Doorway", 12, "0.467 0.05", "0.533 0.5", "build.select build wall.doorway", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.WallWindow ? green : gray, "Wall Window", 12, "0.533 0.05", "0.598 0.5", "build.select build wall.window", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.WallFrame ? green : gray, "Wall Frame", 12, "0.598 0.05", "0.663 0.5", "build.select build wall.frame", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.WallHalf ? green : gray, "Wall Half", 12, "0.663 0.05", "0.729 0.5", "build.select build wall.half", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.WallLow ? green : gray, "Wall Low", 12, "0.729 0.05", "0.794 0.5", "build.select build wall.low", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.StairsLShaped ? green : gray, "Stairs L Shapred", 12, "0.794 0.05", "0.859 0.5", "build.select build stairs.l.shaped", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.StairsUShaped ? green : gray, "Stairs U Shapred", 12, "0.859 0.05", "0.925 0.5", "build.select build stairs.u.shaped", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.constructionBuild == Building.Roof ? green : gray, "Roof", 12, "0.925 0.05", "0.99 0.5", "build.select build roof", TextAnchor.MiddleCenter);

            }
            else if (pB.buildType == PlayerBuildType.Deploy)
            {
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "<<", 12, "0.01 0.01", "0.50 0.10", "build.select deploy page previous", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, ">>", 12, "0.5 0.01", "0.99 0.10", "build.select deploy page next", TextAnchor.MiddleCenter);

                int startid = pB.deploypage * 80;

                float px = 0f;
                float py = 0.9f;
                int i = 0;
                foreach (KeyValuePair<string, uint> deployable in deployables)
                {
                    if (i >= startid)
                    {
                        UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.currentConstruction != null ? pB.currentConstruction.prefabID == deployable.Value ? green : gray : gray, deployable.Key, 12, string.Format("{0} {1}", px.ToString(), (py - 0.09f).ToString()), string.Format("{0} {1}", (px + 0.1f).ToString(), py.ToString()), "build.select deploy select " + deployable.Value.ToString(), TextAnchor.MiddleCenter);
                        px += 0.1f;
                        if (px > 0.91f)
                        {
                            py -= 0.1f;
                            px = 0f;
                            if (py < 0.19f)
                            {
                                break;
                            }
                        }
                    }

                    i++;
                }
            }
            else if (pB.buildType == PlayerBuildType.Grade)
            {
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.Twigs ? green : gray, "Twig", 16, "0.01 0.05", "0.20 0.49", "build.select grade 0", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.Wood ? green : gray, "Wood", 16, "0.21 0.05", "0.40 0.49", "build.select grade 1", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.Stone ? green : gray, "Stone", 16, "0.41 0.05", "0.60 0.49", "build.select grade 2", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.Metal ? green : gray, "Metal", 16, "0.61 0.05", "0.80 0.49", "build.select grade 3", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.grade == BuildingGrade.Enum.TopTier ? green : gray, "TopTier", 16, "0.81 0.05", "0.99 0.49", "build.select grade 4", TextAnchor.MiddleCenter);
            }
            else if (pB.buildType == PlayerBuildType.Spawn)
            {
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, "<<", 12, "0.01 0.01", "0.50 0.10", "build.select spawn page previous", TextAnchor.MiddleCenter);
                UI.CreateButton(ref page_container, "BuildMenuContainerSub", gray, ">>", 12, "0.5 0.01", "0.99 0.10", "build.select spawn page next", TextAnchor.MiddleCenter);

                int startid = pB.spawnpage * 40;

                float px = 0f;
                float py = 0.9f;
                int i = 0;
                foreach (string r in resourcesList)
                {
                    if (i >= startid)
                    {
                        UI.CreateButton(ref page_container, "BuildMenuContainerSub", pB.currentSpawn != string.Empty ? pB.currentSpawn == r ? green : gray : gray,  r.Replace("assets/", "").Replace("prefabs/", "").Replace("bundled/", "").Replace("content/", ""), 9, string.Format("{0} {1}", px.ToString(), (py - 0.09f).ToString()), string.Format("{0} {1}", (px + 0.2f).ToString(), py.ToString()), "build.select spawn select " + r.ToString(), TextAnchor.MiddleCenter);
                        px += 0.2f;
                        if (px > 0.91f)
                        {
                            py -= 0.1f;
                            px = 0f;
                            if (py < 0.19f)
                            {
                                break;
                            }
                        }
                    }

                    i++;
                }
            }
            CuiHelper.AddUi(player, page_container);
        }

        void BuildGUI_Refresh(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BuildTop");

            PlayerBuild pB = playerBuild[player.userID];

            if (pB.buildType == PlayerBuildType.None) return;

            CuiElementContainer page_container = UI.CreateElementContainer("Overlay", "BuildTop", "0.1 0.1 0.1 0.8", "0.2 0.95", "0.8 1", false);
            string text = string.Empty;

            text = string.Format("{0}", pB.buildType == PlayerBuildType.Rotate ? "Rotate: " + pB.select.ToString() : pB.buildType == PlayerBuildType.Heal ? "Heal: " + pB.select.ToString() : pB.buildType == PlayerBuildType.Grade ? string.Format("Grade: {0} - {1}", pB.select.ToString(), pB.grade.ToString()) : pB.buildType == PlayerBuildType.Spawn ? string.Format("Spawn: {0} - Height: {1}", pB.currentSpawn, pB.height.ToString()) : pB.buildType == PlayerBuildType.Build ? string.Format("Build: {0} - {1} - Height: {2}", pB.placement.ToString(), pB.constructionBuild, pB.height.ToString()) : pB.buildType == PlayerBuildType.Deploy ? string.Format("Deploy: {0} - Height: {1}", pB.currentConstruction == null ? string.Empty : pB.currentConstruction.ToString(), pB.height.ToString()) : pB.buildType == PlayerBuildType.Erase ? "Erasing" : "Error");

            UI.CreateLabel(ref page_container, "BuildTop", "1 1 1 1", text, 16, "0 0", "1 1", TextAnchor.MiddleCenter);
            CuiHelper.AddUi(player, page_container);
        }
        #endregion

        class BuildLog
        {
            public GameObject go;
            public PlayerBuildType e;
            public string svalue1;
            public float fvalue1;
            public int ivalue1;
            public Vector3 vvalue1;
            public Quaternion qvalue1;

            public BuildLog(GameObject go, PlayerBuildType e) { this.go = go; this.e = e; }
            public BuildLog(GameObject go, PlayerBuildType e, float value1) { this.go = go; this.e = e; this.fvalue1 = value1; }
            public BuildLog(GameObject go, PlayerBuildType e, int value1) { this.go = go; this.e = e; this.ivalue1 = value1; }
            public BuildLog(GameObject go, PlayerBuildType e, Vector3 value1) { this.go = go; this.e = e; this.vvalue1 = value1; }
            public BuildLog(GameObject go, PlayerBuildType e, Quaternion value1) { this.go = go; this.e = e; this.qvalue1 = value1; }
            public BuildLog(string value1, PlayerBuildType e, Vector3 value2, Quaternion value3, int value4 = 0) { this.svalue1 = value1; this.e = e;  this.vvalue1 = value2; this.qvalue1 = value3; this.ivalue1 = value4; }
        }

        class PlayerBuild
        {
            public bool crosshair = false;
            public int spawnpage = 0;
            public int deploypage = 0;
            public float height = 0f;
            public float lastClick = Time.realtimeSinceStartup;
            public string currentSpawn = string.Empty;
            public object closestEnt = null;
            public Vector3 closestHitpoint = new Vector3();
            public Quaternion currentRot = new Quaternion();
            public Quaternion currentRotate = Quaternion.Euler(0f, 45f, 0f);
            public Quaternion rotate = Quaternion.Euler(0f, 0f, 0f);

            public BasePlayer player;
            public BaseNetworkable currentBaseNet = null;
            public Collider currentCollider = null;
            public Construction currentConstruction;

            public Building constructionBuild = Building.Foundation;
            public BuildingGrade.Enum grade = BuildingGrade.Enum.Twigs;
            public Selection select = Selection.Select;
            public Placement placement = Placement.Auto;
            public PlayerBuildType buildType = PlayerBuildType.None;

            public List<List<BuildLog>> logs = new List<List<BuildLog>>();

            public PlayerBuild(BasePlayer player)
            {
                this.player = player;
            }

            public void Execute()
            {
                if (!TryGetPlayerView(player, out currentRot))
                    return;

                if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint))
                    return;

                currentCollider = closestEnt as Collider;

                if (buildType == PlayerBuildType.Erase)
                {
                    logs.Add(new List<BuildLog>() { new BuildLog(currentCollider.GetComponentInParent<BaseNetworkable>().PrefabName, PlayerBuildType.Erase, currentCollider.transform.position, currentCollider.transform.rotation, currentCollider.GetComponentInParent<BuildingBlock>() != null ? (int)(currentCollider.GetComponentInParent<BuildingBlock>().grade) : 0 ) });
                    currentBaseNet = currentCollider.GetComponentInParent<BaseNetworkable>();
                    currentBaseNet?.Kill(BaseNetworkable.DestroyMode.Gib);
                }
                else if(buildType == PlayerBuildType.Build)
                {
                    DoBuild(this, player, currentCollider, placement);
                }
                else if (buildType == PlayerBuildType.Deploy)
                {
                    DoDeploy(this, player, currentCollider);
                }
                else if (buildType == PlayerBuildType.Grade)
                {
                    DoGrade(this, player, currentCollider);
                }
                else if (buildType == PlayerBuildType.Heal)
                {
                    DoHeal(this, player, currentCollider);
                }
                else if (buildType == PlayerBuildType.Rotate)
                {
                    DoRotation(this, player, currentCollider);
                }
                else if (buildType == PlayerBuildType.Spawn)
                {
                    DoSpawn(this, player, currentCollider);
                }
            }
        }

        class VectorQuaternion
        {
            public Vector3 vector3;
            public Quaternion quaternion;

            public VectorQuaternion(Vector3 vector3, Quaternion quaternion)
            {
                this.vector3 = vector3;
                this.quaternion = quaternion;
            }
        }

        class BuildingSocket
        {
            public SocketType sock;

            public Dictionary<SocketType, List<VectorQuaternion>> sockets = new Dictionary<SocketType, List<VectorQuaternion>>();

            public BuildingSocket(SocketType sock)
            {
                this.sock = sock;
            }

            public void AddTargetSock(SocketType s, Vector3 v, Quaternion q)
            {
                if (!sockets.ContainsKey(s))
                {
                    sockets.Add(s, new List<VectorQuaternion>());
                }
                sockets[s].Add(new VectorQuaternion(v, q));
            }
        }

        class BuildingConstruction
        {
            public string prefab;
            public BuildingSocket socket;
            public Building build;
            public BuildingConstruction(string prefab, BuildingSocket socket, Building build)
            {
                this.prefab = prefab;
                this.socket = socket;
                this.build = build;
            }
        }

        private static bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = Quaternion.identity;

            if (player.serverInput.current == null)
                return false;

            viewAngle = Quaternion.Euler(player.serverInput.current.aimAngles);

            return true;
        }
        private static bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            float closestdist = 999999f;

            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            Ray ray = new Ray(sourceEye, sourceDir * Vector3.forward);
            
            closestHitpoint = sourcePos;
            closestEnt = false;

            foreach (var hit in Physics.RaycastAll(ray, mdist, generalColl))
            {
                if (hit.collider.GetComponentInParent<TriggerBase>() == null)
                {
                    if (hit.distance < closestdist)
                    {
                        closestdist = hit.distance;
                        closestEnt = hit.GetCollider();
                        closestHitpoint = hit.point;
                    }
                }
            }

            if (closestEnt is bool)
                return false;

            return true;
        }

        private static void DoHeal(PlayerBuild buildplayer, BasePlayer player, Collider baseentity)
        {
            BaseCombatEntity ent = baseentity.GetComponentInParent<BaseCombatEntity>();

            if (ent == null)
                return;

            List<BuildLog> bl = new List<BuildLog>();
            bl.Add(new BuildLog(baseentity.gameObject, PlayerBuildType.Heal,ent.health));

            ent.health = ent.MaxHealth();

            if (buildplayer.select == Selection.All)
            {
                if (GetAllBaseEntities<BaseCombatEntity>(baseentity.GetComponentInParent<BaseEntity>()))
                {
                    foreach (BaseCombatEntity fent in houseList)
                    {
                        bl.Add(new BuildLog(fent.gameObject, PlayerBuildType.Heal,fent.health));
                        fent.health = fent.MaxHealth();
                    }
                }
            }

            buildplayer.logs.Add(bl);
        }

        static bool GetAllBaseEntities<T>(BaseEntity initialEntity)
        {
            try
            {
                houseList = new List<object>();
                checkFrom = new List<Vector3>();

                houseList.Add(initialEntity);
                checkFrom.Add(initialEntity.transform.position);

                int current = 0;


                while (true)
                {
                    current++;

                    if (current > checkFrom.Count)
                        break;

                    List<BaseEntity> list = Pool.GetList<BaseEntity>();

                    Vis.Entities<BaseEntity>(checkFrom[current - 1], 3f, list, constructionColl);

                    for (int i = 0; i < list.Count; i++)
                    {
                        BaseEntity hit = list[i];

                        var fent = hit.GetComponentInParent<T>();

                        if (fent != null && !(houseList.Contains(hit)))
                        {
                            houseList.Add(hit);
                            checkFrom.Add(hit.transform.position);
                        }
                    }
                }
                checkFrom.Clear();
                return true;
            }
            catch(Exception e)
            {
                Interface.Oxide.LogError(e.Message);
                Interface.Oxide.LogError(e.StackTrace);
                return false;
            }
        }

        private static void DoSpawn(PlayerBuild buildplayer, BasePlayer player, Collider baseentity)
        {
            newPos = buildplayer.closestHitpoint + (Vector3.up * buildplayer.height);
            newRot = buildplayer.currentRot;
            newRot.x = 0f;
            newRot.z = 0f;
            newRot = newRot * new Quaternion(0f, 0.7071068f, 0f, 0.7071068f);
            SpawnEntity(buildplayer, buildplayer.currentSpawn, newPos, newRot);
        }
        
        private static void DoBuild(PlayerBuild buildplayer, BasePlayer player, Collider baseentity, Placement placement)
        {
            uint bid = 0u;
            if(placement == Placement.Force)
            {
                newPos = buildplayer.closestHitpoint + (Vector3.up * buildplayer.height);
                newRot = buildplayer.currentRot;
                newRot.x = 0f;
                newRot.z = 0f;
            }
            else
            {
                var fbuildingblock = baseentity.GetComponentInParent<BuildingBlock>();

                if (fbuildingblock == null)
                {
                    return;
                }

                if(placement == Placement.Up)
                {
                    newPos = fbuildingblock.transform.position + (Vector3.up * buildplayer.height);
                    newRot = fbuildingblock.transform.rotation;
                }
                else
                {
                    float distance = 999999f;
                    newPos = new Vector3(0f, 0f, 0f);
                    newRot = new Quaternion(0f, 0f, 0f, 0f);
                    if (buildings.ContainsKey(fbuildingblock.blockDefinition.fullName))
                    {
                        sourcebuild = buildings[fbuildingblock.blockDefinition.fullName];
                        targetbuild = buildings[BuildingToPrefab[buildplayer.constructionBuild]];
                        if (sourcebuild.socket.sockets.ContainsKey(targetbuild.socket.sock))
                        {
                            foreach (VectorQuaternion vq in sourcebuild.socket.sockets[targetbuild.socket.sock])
                            {
                                var currentrelativepos = (fbuildingblock.transform.rotation * vq.vector3) + fbuildingblock.transform.position;
                                if (Vector3.Distance(currentrelativepos, buildplayer.closestHitpoint) < distance)
                                {
                                    distance = Vector3.Distance(currentrelativepos, buildplayer.closestHitpoint);
                                    newPos = currentrelativepos + (Vector3.up * buildplayer.height);
                                    newRot = (fbuildingblock.transform.rotation * vq.quaternion);
                                }
                            }
                        }
                    }
                    if (newPos.x == 0f)
                        return;

                }

                if (IsColliding(BuildingToPrefab[buildplayer.constructionBuild], newPos, 1f))
                    return;

                bid = fbuildingblock.buildingID;
            }
            
            SpawnStructure(buildplayer, BuildingToPrefab[buildplayer.constructionBuild], newPos, newRot, buildplayer.grade, 0f, bid);
        }

        private static bool IsColliding(string prefabname, Vector3 position, float radius)
        {
            List<BaseEntity> ents = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(position, radius, ents);

            foreach (BaseEntity ent in ents)
            {
                if (ent.PrefabName == prefabname && ent.transform.position == position)
                    return true;
            }
            return false;
        }

        private static GameObject SpawnPrefab(string prefabname, Vector3 pos, Quaternion angles, bool active)
        {
            GameObject prefab = GameManager.server.CreatePrefab(prefabname, pos, angles, active);

            if (prefab == null)
                return null;

            prefab.transform.position = pos;
            prefab.transform.rotation = angles;
            prefab.gameObject.SetActive(active);

            return prefab;
        }

        private static void SpawnStructure(PlayerBuild bp, string prefabname, Vector3 pos, Quaternion angles, BuildingGrade.Enum grade, float health, uint buildingID = 0)
        {
            GameObject prefab = SpawnPrefab(prefabname, pos, angles, true);

            if (prefab == null)
                return;

            BuildingBlock block = prefab.GetComponent<BuildingBlock>();

            if (block == null)
                return;

            block.blockDefinition = PrefabAttribute.server.Find<Construction>(block.prefabID);

            if (buildingID > 0) 
                block.AttachToBuilding(buildingID);

            block.SetGrade(grade);
            block.Spawn();

            if (health <= 0f)
                block.health = block.MaxHealth();
            else
                block.health = health;

            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            bp.logs.Add(new List<BuildLog>() { new BuildLog(prefab, PlayerBuildType.Build) });
        }

        private static void SpawnEntity(PlayerBuild bp, string prefabname, Vector3 pos, Quaternion angles)
        {
            GameObject prefab = SpawnPrefab(prefabname, pos, angles, true);

            if (prefab == null)
                return;

            BaseEntity entity = prefab?.GetComponent<BaseEntity>();
            entity?.Spawn();

            bp.logs.Add(new List<BuildLog>() { new BuildLog(prefab, PlayerBuildType.Spawn) });
        }

        private static void DoDeploy(PlayerBuild buildplayer, BasePlayer player, Collider baseentity)
        {
            Quaternion rotationOffSet = buildplayer.currentRot;
            rotationOffSet.x = 0f;
            rotationOffSet.z = 0f;
            rotationOffSet = rotationOffSet * new Quaternion(0f, 1f, 0f, 0f);

            GameObject go = SpawnPrefab(buildplayer.currentConstruction.fullName, buildplayer.currentConstruction.socketHandle ? Vector3.zero : (buildplayer.closestHitpoint + (Vector3.up * buildplayer.height)), buildplayer.currentConstruction.socketHandle ? Quaternion.identity : rotationOffSet, false);
            
            BaseEntity ent = go.ToBaseEntity();
            BaseEntity sent = baseentity.GetComponentInParent<BaseEntity>();

            if (buildplayer.currentConstruction.socketHandle)
                ent.SetParent(sent, (uint)buildplayer.currentConstruction.deployable.slot);

            if ((buildplayer.currentConstruction.deployable.setSocketParent && (sent != null)) && (ent != null))
            {
                ent.SetParent(sent, (uint)0);
                ent.transform.position = sent.transform.InverseTransformPoint(ent.transform.position);
            }

            DecayEntity entity2 = ent as DecayEntity;
            if (entity2 != null)
            {
                entity2.AttachToBuilding(sent as DecayEntity);
            }
            ent.Spawn(); 
            go.AwakeFromInstantiate();

            buildplayer.logs.Add(new List<BuildLog>() { new BuildLog(go, PlayerBuildType.Deploy) });
        }

        private static void DoGrade(PlayerBuild buildplayer, BasePlayer player, Collider baseentity)
        {
            var fbuildingblock = baseentity.GetComponentInParent<BuildingBlock>();

            if (fbuildingblock == null)
                return;

            List<BuildLog> bl = new List<BuildLog>();
            bl.Add(new BuildLog(baseentity.gameObject, PlayerBuildType.Grade, (int)fbuildingblock.grade));

            SetGrade(fbuildingblock, buildplayer.grade);

            if (buildplayer.select == Selection.All)
            {
                if (GetAllBaseEntities<BuildingBlock>(baseentity.GetComponentInParent<BaseEntity>()))
                {
                    foreach (BuildingBlock fent in houseList)
                    {
                        bl.Add(new BuildLog(fent.gameObject, PlayerBuildType.Grade, (int)fent.grade));
                        SetGrade(fent, buildplayer.grade);
                    }
                }
            }

            buildplayer.logs.Add(bl);
        }
        private static void SetGrade(BuildingBlock block, BuildingGrade.Enum level)
        {
            block.SetGrade(level);
            block.health = block.MaxHealth();
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }
        
        private static void DoRotation(PlayerBuild buildplayer, BasePlayer player, Collider baseentity)
        {
            BuildingBlock buildingblock = baseentity.GetComponentInParent<BuildingBlock>();
            buildplayer.logs.Add(new List<BuildLog>() { new BuildLog(baseentity.gameObject, PlayerBuildType.Rotate, baseentity.GetComponentInParent<BaseEntity>().transform.localRotation) });

            SetRotation(baseentity.GetComponentInParent<BaseEntity>(), baseentity.GetComponentInParent<BaseEntity>().transform.localRotation * ( buildplayer.currentRotate != defaultQuaternion ? buildplayer.currentRotate : buildingblock != null ? buildingblock.blockDefinition.rotationAmount != Vector3.zero ? Quaternion.Euler(buildingblock.blockDefinition.rotationAmount) : buildplayer.currentRotate : buildplayer.currentRotate));
        }

        private static void SetRotation(BaseEntity baseentity, Quaternion rotation)
        {
            baseentity.transform.localRotation = rotation;

            BuildingBlock buildingblock = baseentity.GetComponent<BuildingBlock>();
            if (buildingblock != null && buildingblock.blockDefinition != null)
            {
                buildingblock.RefreshEntityLinks();
                buildingblock.UpdateSurroundingEntities();
                buildingblock.SendNetworkUpdateImmediate(false);
                buildingblock.ClientRPC(null, "RefreshSkin");
            }
            else
            {
                baseentity.SendNetworkUpdateImmediate(false);
                baseentity.ClientRPC(null, "RefreshSkin");
            }
        }


        [ConsoleCommand("build.select")]
        private void cmdBuildSelect(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null)
            {
                arg.ReplyWith("Only a player can use this command.");
                return;
            }
             
            if (!hasAuth(player))
            {
                arg.ReplyWith("You are not allowed to use this command.");
                return;
            }

            PlayerBuild pB = playerBuild[player.userID];

            switch (arg.Args[0])
            {
                case "build":
                    if (arg.Args.Length > 1)
                    {
                        Building targetBuilding = Building.Foundation;
                        switch(arg.Args[1])
                        {
                            case "foundation":
                                targetBuilding = Building.Foundation;
                                break;
                            case "foundation.triangle":
                                targetBuilding = Building.FoundationTriangle;
                                break;
                            case "foundation.steps":
                                targetBuilding = Building.FoundationSteps;
                                break;
                            case "floor":
                                targetBuilding = Building.Floor;
                                break;
                            case "floor.triangle":
                                targetBuilding = Building.FloorTriangle;
                                break;
                            case "floor.frame":
                                targetBuilding = Building.FloorFrame;
                                break;
                            case "wall":
                                targetBuilding = Building.Wall;
                                break;
                            case "wall.doorway":
                                targetBuilding = Building.WallDoorway;
                                break;
                            case "wall.window":
                                targetBuilding = Building.WallWindow;
                                break;
                            case "wall.frame":
                                targetBuilding = Building.WallFrame;
                                break;
                            case "wall.half":
                                targetBuilding = Building.WallHalf;
                                break;
                            case "wall.low":
                                targetBuilding = Building.WallLow;
                                break;
                            case "stairs.l.shaped":
                                targetBuilding = Building.StairsLShaped;
                                break;
                            case "stairs.u.shaped":
                                targetBuilding = Building.StairsUShaped;
                                break;
                            case "roof":
                                targetBuilding = Building.Roof;
                                break;
                        }
                        pB.constructionBuild = targetBuilding;
                    }
                    else
                    {
                        pB.buildType = PlayerBuildType.Build;
                    }
                    break;
                case "deploy":
                    if(arg.Args.Length > 1)
                    {
                        if(arg.Args[1] == "page")
                        {
                            if(arg.Args[2] == "previous")
                            {
                                pB.deploypage -= 1;
                                if (pB.deploypage < 0) pB.deploypage = 0;
                            }
                            else
                            {
                                pB.deploypage += 1;
                                if(pB.deploypage*80 > deployables.Count)
                                {
                                    pB.deploypage -= 1;
                                }
                            }
                        }
                        else if (arg.Args[1] == "select")
                        {
                            pB.currentConstruction = PrefabAttribute.server.Find<Construction>(uint.Parse(arg.Args[2]));
                        }
                    }
                    else
                        pB.buildType = PlayerBuildType.Deploy;
                    break;
                case "spawn":
                    if (arg.Args.Length > 1)
                    {
                        if (arg.Args[1] == "page")
                        {
                            if (arg.Args[2] == "previous")
                            {
                                pB.spawnpage -= 1;
                                if (pB.spawnpage < 0) pB.spawnpage = 0;
                            }
                            else
                            {
                                pB.spawnpage += 1;
                                if (pB.spawnpage * 40 > resourcesList.Count)
                                {
                                    pB.spawnpage -= 1;
                                }
                            }
                        }
                        else if (arg.Args[1] == "select")
                        {
                            pB.currentSpawn = String.Join(" ", arg.Args.Skip(2));
                        }
                    }
                    else
                        pB.buildType = PlayerBuildType.Spawn;
                    break;
                case "grade":
                    if (arg.Args.Length > 1)
                    {
                        pB.grade = (BuildingGrade.Enum)int.Parse(arg.Args[1]);
                    }
                    else
                    {
                        pB.buildType = PlayerBuildType.Grade;
                    }
                    break;
                case "heal":
                    pB.buildType = PlayerBuildType.Heal;
                    break;
                case "select":
                    if (arg.Args[1] == "all")
                    {
                        pB.select = Selection.All;
                    }
                    else
                    {
                        pB.select = Selection.Select;
                    }
                    break;
                case "height":
                    if (arg.Args[1] == "reset")
                    {
                        pB.height = 0f;
                    }
                    else
                    {
                        float dif = float.Parse(arg.Args[2]);
                        if (arg.Args[1] == "plus")
                        {
                            pB.height += dif;
                        }
                        else if (arg.Args[1] == "minus")
                        {
                            pB.height -= dif;
                        }
                    }

                    break;
                case "noclip":
                    pB.player.SendConsoleCommand("noclip", new object[] { });
                    break;
                case "crosshair":
                    pB.crosshair = !pB.crosshair;
                    if(pB.crosshair)
                    {
                        CuiElementContainer crosshair_container = UI.CreateElementContainer("Overlay", "BuildCrosshair", "0.1 0.1 0.1 1", "0.499 0.499", "0.501 0.501", false);
                        CuiHelper.AddUi(player, crosshair_container);
                    }
                    else
                    {
                        CuiHelper.DestroyUi(player, "BuildCrosshair");
                    }

                    break;
                case "rotate":
                    pB.buildType = PlayerBuildType.Rotate;
                    break;
                case "erase":
                    pB.buildType = PlayerBuildType.Erase;
                    break;
                case "placement":
                    pB.placement = arg.Args[1] == "force" ? Placement.Force : arg.Args[1] == "up" ? Placement.Up : Placement.Auto;
                    break;
                case "undo":
                    if(pB.logs.Count == 0)
                    {
                        return;
                    }
                    List<BuildLog> logs = pB.logs[pB.logs.Count - 1];
                    for(int i = 0; i < logs.Count; i++)
                    {
                        try
                        {
                            BuildLog log = logs[i];
                            if (log.e == PlayerBuildType.Build || log.e == PlayerBuildType.Deploy || log.e == PlayerBuildType.Spawn)
                            {
                                log.go?.GetComponentInParent<BaseNetworkable>()?.Kill(BaseNetworkable.DestroyMode.Gib);
                            }
                            else if (log.e == PlayerBuildType.Rotate)
                            {
                                if (log.go?.ToBaseEntity() != null)
                                    SetRotation(log.go?.ToBaseEntity(), log.qvalue1);
                            }
                            else if (log.e == PlayerBuildType.Grade)
                            {
                                BuildingBlock bblock = log.go?.ToBaseEntity()?.GetComponent<BuildingBlock>();
                                if (bblock != null)
                                    SetGrade(bblock, (BuildingGrade.Enum)log.ivalue1);
                            }
                            else if (log.e == PlayerBuildType.Heal)
                            {
                                BaseCombatEntity bcentity = log.go?.ToBaseEntity()?.GetComponent<BaseCombatEntity>();
                                if (bcentity != null)
                                    bcentity.health = log.fvalue1;
                            }
                            else if(log.e == PlayerBuildType.Erase)
                            {
                                GameObject prefab = SpawnPrefab(log.svalue1, log.vvalue1, log.qvalue1, true);
                                BaseEntity ent = prefab?.GetComponent<BaseEntity>();
                                ent.Spawn();

                                BuildingBlock block = ent?.GetComponent<BuildingBlock>();
                                if (block != null)
                                {
                                    block.SetGrade((BuildingGrade.Enum)log.ivalue1);
                                }

                                BaseCombatEntity bcentity = ent?.GetComponent<BaseCombatEntity>();
                                if(bcentity != null)
                                {
                                    bcentity.health = bcentity.MaxHealth();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Interface.Oxide.LogWarning(e.Message);
                            Interface.Oxide.LogWarning(e.StackTrace);
                        }
                    }
                    pB.logs.RemoveAt(pB.logs.Count - 1);
                    break;
                case "none":
                    pB.buildType = PlayerBuildType.None;
                    break;
            }
            BuildMenu_Toggle(player);
            BuildGUI_Refresh(player);
        }

        private T[] GetAllPrefabs<T>()
        {
            var prefabs = PrefabAttribute.server.prefabs;
            if (prefabs == null || prefabs.Any() == false)
            {
                return new T[0];
            }

            var results = new List<T>();
            foreach (var prefab in prefabs.Values)
            {
                var arrayCache = prefab.Find<T>();
                if (arrayCache == null || !arrayCache.Any())
                {
                    continue;
                }

                results.AddRange(arrayCache);
            }

            return results.ToArray();
        }
    }
}
