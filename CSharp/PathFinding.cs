// Reference: Oxide.Ext.Rust
// Reference: RustBuild

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;

namespace Oxide.Plugins
{
    [Info("PathFinding", "Reneb", "0.0.7", ResourceId = 868)]
    class PathFinding : RustPlugin
    {
        [PluginReference]
        Plugin Draw;
        public class Pathfinder
        {
            public SortedList<float, List<PathfindNode>> SortNode;
            public Hash<Vector3, PathfindNode> NodeList;
            public Hash<Vector3, bool> ClosedList;
            public List<PathfindNode> RunDetection;

            public PathfindNode targetNode;

            public float currentPriority;

            public BasePlayer player;

            public int Loops;

            public bool shouldBreak;

            public Pathfinder()
            {
                SortNode = new SortedList<float, List<PathfindNode>>();
                NodeList = new Hash<Vector3, PathfindNode>();
                RunDetection = new List<PathfindNode>();
                ClosedList = new Hash<Vector3, bool>();
            }
            public List<Vector3> FindPath(Vector3 sourcePos, Vector3 targetPos)
            {
                Reset();
                this.targetNode = new PathfindNode(this);
                PathfindGoal(this, this.targetNode, targetPos);
                PathfindFirst(this, new PathfindNode(this), sourcePos);

                while (true)
                {
                    currentPriority = SortNode.Keys[0];
                    foreach (PathfindNode pathnode in (SortNode[currentPriority]))
                        RunDetection.Add(pathnode);
                    SortNode.Remove(currentPriority);
                    foreach (PathfindNode pathnode in RunDetection)
                    {
                        pathnode.DetectAdjacentNodes();
                        if (pathnode.isGoal) { targetNode.parentNode = pathnode; shouldBreak = true; }
                    }
                    RunDetection.Clear();
                    Loops++;
                    if (Loops > MaxLoops) { Reset(); return null; }
                    if (shouldBreak) break;
                }

                PathfindNode parentnode = targetNode.parentNode;
                List<Vector3> PlayerPath = new List<Vector3>();
                while (true)
                {
                    PlayerPath.Add(parentnode.position);
                    parentnode = parentnode.parentNode;
                    if (parentnode == null) break;
                }
                PlayerPath.Reverse();
                PlayerPath.RemoveAt(0);
                Reset();
                return PlayerPath;
            }
            public void Reset()
            {
                currentPriority = 0f;
                SortNode.Clear();
                RunDetection.Clear();
                NodeList.Clear();
                Loops = 0;
                shouldBreak = false;
            }

            public void AddToPriorityList(PathfindNode currentNode)
            {
                if (!SortNode.ContainsKey(currentNode.F)) SortNode.Add(currentNode.F, new List<PathfindNode>());
                ((List<PathfindNode>)SortNode[currentNode.F]).Add(currentNode);
            }
        }

        public class PathfindNode
        {
            // FIELDS
            public float H = 0f;
            public float G = 0f;
            public float F = 0f;
            public PathfindNode parentNode = null;
            public PathfindNode north = null;
            public PathfindNode northeast = null;
            public PathfindNode northwest = null;
            public PathfindNode east = null;
            public PathfindNode south = null;
            public PathfindNode southeast = null;
            public PathfindNode southwest = null;
            public PathfindNode west = null;
            public Vector3 position;
            public Vector3 positionEyes;
            public bool isGoal = false;
            public Pathfinder pathfinder;

            ///
            /// PathfindNode()
            /// Raw pathfind creation
            public PathfindNode(Pathfinder pathfinder)
            {
                this.pathfinder = pathfinder;
            }

            ///
            /// PathfindNode(PathfindNode parentnode, Vector3 position, bool diagonal)
            /// This is called by all new pathnodes
            public PathfindNode(Pathfinder pathfinder, PathfindNode parentnode, Vector3 position, bool diagonal)
            {
                this.pathfinder = pathfinder;
                this.position = position;
                this.positionEyes = new Vector3(position.x, Mathf.Ceil(position.y) + 0.5f, position.z);
                this.parentNode = parentnode;
                CalculateManhattanDistance(this, pathfinder.targetNode);
                CalculateMovementCost(this, diagonal);
                this.F = this.H + this.G;
                pathfinder.AddToPriorityList(this);
                if (this.pathfinder.player != null)
                    this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.green, position, 0.5f);
            }

            // METHODS

            // DetectAdjacentNodes()
            // This automatically creates the surrounding pathnodes
            public void DetectAdjacentNodes()
            {
                if (!pathfinder.ClosedList[this.positionEyes + VectorForward])
                    if (!Physics.Linecast(this.positionEyes, this.positionEyes + VectorForward, blockLayer))
                    {
                        north = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorForward, false);
                    }
                    else
                    {
                        if(this.pathfinder.player != null)
                            this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.red, this.positionEyes + VectorForward, 0.5f);
                    }
                if (!pathfinder.ClosedList[this.positionEyes + VectorRight])
                    if (!Physics.Linecast(this.positionEyes, this.positionEyes + VectorRight, blockLayer))
                    {
                        east = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorRight, false);
                    }
                    else
                    {
                        if (this.pathfinder.player != null)
                            this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.red, this.positionEyes + VectorRight, 0.5f);
                    }
                if (!pathfinder.ClosedList[this.positionEyes + VectorBack])
                    if (!Physics.Linecast(this.positionEyes, this.positionEyes + VectorBack, blockLayer))
                    {
                        south = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorBack, false);
                    }
                    else
                    {
                        if (this.pathfinder.player != null)
                            this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.red, this.positionEyes + VectorBack, 0.5f);
                    }
                if (!pathfinder.ClosedList[this.positionEyes + VectorLeft])
                    if (!Physics.Linecast(this.positionEyes, this.positionEyes + VectorLeft, blockLayer))
                    {
                        west = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorLeft, false);
                    }
                    else
                    {
                        if (this.pathfinder.player != null)
                            this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.red, this.positionEyes + VectorLeft, 0.5f);
                    }

                if (!pathfinder.ClosedList[this.positionEyes + VectorForwardRight])
                    if (!Physics.Linecast(this.positionEyes, this.positionEyes + VectorForwardRight, blockLayer))
                    {
                        northeast = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorForwardRight, true);
                    }
                    else
                    {
                        if (this.pathfinder.player != null)
                            this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.red, this.positionEyes + VectorForwardRight, 0.5f);
                    }
                if (!pathfinder.ClosedList[this.positionEyes + VectorForwardLeft])
                    if (!Physics.Linecast(this.positionEyes, this.positionEyes + VectorForwardLeft, blockLayer))
                    {
                        northwest = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorForwardLeft, true);
                    }
                    else
                    {
                        if (this.pathfinder.player != null)
                            this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.red, this.positionEyes + VectorForwardLeft, 0.5f);
                    }
                if (!pathfinder.ClosedList[this.positionEyes + VectorBackLeft])
                    if (!Physics.Linecast(this.positionEyes, this.positionEyes + VectorBackLeft, blockLayer))
                    {
                    southeast = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorBackLeft, true);
                    }
                    else
                    {
                        if (this.pathfinder.player != null)
                            this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.red, this.positionEyes + VectorBackLeft, 0.5f);
                    }
                if (!pathfinder.ClosedList[this.positionEyes + VectorBackRight])
                    if (!Physics.Linecast(this.positionEyes, this.positionEyes + VectorBackRight, blockLayer))
                    {
                     southwest = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorBackRight, true);
                    }
                    else
                    {
                        if (this.pathfinder.player != null)
                            this.pathfinder.player.SendConsoleCommand("ddraw.box", 5f, UnityEngine.Color.red, this.positionEyes + VectorBackRight, 0.5f);
                    }
            }
        }
        // Here we calculate the movement cost between 2 points.
        private static void CalculateMovementCost(PathfindNode currentNode, bool diagonal) { currentNode.G = currentNode.parentNode.G + (diagonal ? 14f : 10f); }

        // Here we calculate the distance between the current node and the target node
        private static void CalculateManhattanDistance(PathfindNode currentNode, PathfindNode targetNode) { currentNode.H = ((Mathf.Abs(currentNode.positionEyes.x - targetNode.positionEyes.x) + Mathf.Abs(currentNode.positionEyes.z - targetNode.positionEyes.z) + Mathf.Abs(currentNode.positionEyes.y - targetNode.positionEyes.y)) * 10); }

        // Create a new node or get the node information
        public static PathfindNode FindPathNodeOrCreate(Pathfinder pathfinder, PathfindNode parentnode, Vector3 position, bool diagonal)
        {
            pathfinder.ClosedList[position] = true;
            if (!FindGroundPosition(position, out GroundPosition, out FixedGroundPosition))
            {
                if(pathfinder.player != null)
                    pathfinder.player.SendConsoleCommand("ddraw.box", 20f, UnityEngine.Color.yellow, position, 0.5f);
                return null;
            }
            if (pathfinder.NodeList[FixedGroundPosition] == null) pathfinder.NodeList[FixedGroundPosition] = new PathfindNode(pathfinder, parentnode, GroundPosition, diagonal);
            else if (pathfinder.NodeList[FixedGroundPosition].isGoal) { pathfinder.targetNode.parentNode = parentnode; parentnode.isGoal = true; }
            return null;
        }

        public static bool FindGroundPosition(Vector3 sourcePos, out Vector3 groundPos, out Vector3 fixedPos)
        {
            groundPos = fixedPos = sourcePos;
            if (Physics.Raycast(sourcePos, Vector3Down, out hitinfo, groundLayer))
            {
                if (hitinfo.collider.gameObject.layer == 4) return false;
                groundPos.y = hitinfo.point.y;
                fixedPos.y = Mathf.Ceil(hitinfo.point.y);
                return true;
            }
            return false;
        }
        public static bool FindRawGroundPosition(Vector3 sourcePos, out Vector3 groundPos)
        {
            groundPos = sourcePos;
            if (Physics.Raycast(sourcePos, Vector3Down, out hitinfo, groundLayer))
            {
                groundPos.y = hitinfo.point.y;
                return true;
            }
            return false;
        }
        public static bool FindRawGroundPositionUP(Vector3 sourcePos, out Vector3 groundPos)
        {
            groundPos = sourcePos;
            if (Physics.Raycast(sourcePos, Vector3UP, out hitinfo, groundLayer))
            {
                groundPos.y = hitinfo.point.y;
                return true;
            }
            return false;
        }
        /// PathfindNode(Vector3 position, Quaternion rotation)
        /// This is called by the First Path

        public static void PathfindFirst(Pathfinder pathfinder, PathfindNode pathfindnode, Vector3 position)
        {
            pathfindnode.position = position;
            pathfindnode.positionEyes = new Vector3(Mathf.Floor(position.x), Mathf.Ceil(position.y) + 0.5f, Mathf.Floor(position.z));
            pathfindnode.H = 0;
            pathfindnode.G = 0;
            pathfindnode.F = 0;
            pathfinder.AddToPriorityList(pathfindnode);
        }

        ///
        /// PathfindNode(Vector3 position)
        /// This is called by the Goal
        public static void PathfindGoal(Pathfinder pathfinder, PathfindNode pathfindnode, Vector3 position)
        {
            pathfindnode.position = position;
            pathfindnode.positionEyes = new Vector3(Mathf.Floor(position.x), Mathf.Ceil(position.y) + 0.5f, Mathf.Floor(position.z));
            pathfindnode.isGoal = true;
            pathfinder.NodeList[pathfindnode.positionEyes - EyesPosition] = pathfindnode;
        }

        class PathFollower : MonoBehaviour
        {
            public List<Vector3> Paths = new List<Vector3>();
            public float secondsTaken;
            public float secondsToTake;
            public float waypointDone;
            public float speed;
            public Vector3 StartPos;
            public Vector3 EndPos;
            public Vector3 nextPos;
            public BaseEntity entity;
            public BasePlayer player;

            void Awake()
            {
                entity = GetComponent<BaseEntity>();
                if (GetComponent<BasePlayer>() != null) player = GetComponent<BasePlayer>();
                speed = 10f;
            }
            void Move() {
                if (secondsTaken == 0f) FindNextWaypoint();
                Execute_Move();
                if (waypointDone >= 1f) secondsTaken = 0f;
            }
            void Execute_Move()
            {
                if (StartPos != EndPos) {
                    secondsTaken += Time.deltaTime;
                    waypointDone = Mathf.InverseLerp(0f, secondsToTake, secondsTaken);
                    nextPos = Vector3.Lerp(StartPos, EndPos, waypointDone);
                    entity.transform.position = nextPos;
                    if (player != null) player.ClientRPCPlayer(null, player, "ForcePositionTo", nextPos);
                    entity.TransformChanged();
                }
            }
            void FindNextWaypoint()
            {
                if (Paths.Count == 0) { StartPos = EndPos = Vector3.zero; enabled = false; return; }
                SetMovementPoint(Paths[0], speed);
            }

            public void SetMovementPoint(Vector3 endpos, float s)
            {

                StartPos = entity.transform.position;
                if (endpos != StartPos) {
                    EndPos = endpos;
                    secondsToTake = Vector3.Distance(EndPos, StartPos) / s;
                    entity.transform.rotation = Quaternion.LookRotation(EndPos - StartPos);
                    if (player != null) SetViewAngle(player, entity.transform.rotation);
                    secondsTaken = 0f;
                    waypointDone = 0f;
                }
                Paths.RemoveAt(0);
            }
            void FixedUpdate() { Move(); }
        }
        static void SetViewAngle(BasePlayer player, Quaternion ViewAngles)
        {
            viewangles.SetValue(player, ViewAngles);
            player.SendNetworkUpdate(BasePlayer.NetworkQueue.Positional);
        }


        public static RaycastHit hitinfo;

        public static Vector3 jumpPosition = new Vector3(0f, 1f, 0f);
        public static Vector3 GroundPosition = new Vector3(0f, 0f, 0f);
        public static Vector3 FixedGroundPosition = new Vector3(0f, 0f, 0f);
        public static Vector3 EyesPosition = new Vector3(0f, 0.5f, 0f);
        public static Vector3 Vector3Down = new Vector3(0f, -1f, 0f);
        public static Vector3 Vector3UP = new Vector3(0f, 1f, 0f);
        public static Vector3 VectorForward = new Vector3(0f, 0f, 1f);
        public static Vector3 VectorBack = new Vector3(0f, 0f, -1f);
        public static Vector3 VectorRight = new Vector3(1f, 0f, 0f);
        public static Vector3 VectorLeft = new Vector3(-1f, 0f, 0f);
        public static Vector3 VectorForwardRight = VectorForward + VectorRight;
        public static Vector3 VectorForwardLeft = VectorForward + VectorLeft;
        public static Vector3 VectorBackRight = VectorBack + VectorRight;
        public static Vector3 VectorBackLeft = VectorBack + VectorLeft;

        public static int groundLayer;
        public static int blockLayer;


        private Quaternion currentRot;
        public Quaternion viewAngle;
        private static FieldInfo serverinput;
        private static FieldInfo viewangles;
        public object closestEnt;
        public Vector3 closestHitpoint;

        private Oxide.Plugins.Timer PathfindingTimer;



        private static int MaxLoops = 500;

        void LoadDefaultConfig() { }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }

        void Init()
        {
            CheckCfg<int>("Max Loops", ref MaxLoops);
            SaveConfig();
        }


        /////////////////////////////////////////////
        /// OXIDE HOOKS
        /////////////////////////////////////////////

        void OnServerInitialized()
        {
            groundLayer = LayerMask.GetMask(new string[] { "Terrain", "World", "Construction" });
            blockLayer = LayerMask.GetMask(new string[] { "World", "Construction", "Tree" });

            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            viewangles = typeof(BasePlayer).GetField("viewAngles", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));

            PathfindingTimer = timer.Once(30f, () => ResetPathFollowers());
        }
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(PathFollower));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }

        /////////////////////////////////////////////
        /// Outside Plugin Calls
        /////////////////////////////////////////////
        bool FindAndShowPath(BasePlayer player, Vector3 sourcePosition, Vector3 targetPosition)
        {
            var curtime = Time.realtimeSinceStartup;
            var bestPath = FindBestPathShow(player, sourcePosition, targetPosition);
            SendReply(player, "Took: "+(Time.realtimeSinceStartup - curtime).ToString()+"seconds");
            if (bestPath == null) return false;
            foreach(Vector3 pos in (List<Vector3>)bestPath)
            {
                player.SendConsoleCommand("ddraw.sphere", 20f, UnityEngine.Color.blue, pos+EyesPosition, 1f);
            }
            return true;
        }
        bool FindAndFollowPath(BaseEntity entity, Vector3 sourcePosition, Vector3 targetPosition)
        {
            //var curtime = Time.realtimeSinceStartup;
            var bestPath = FindBestPath(sourcePosition, targetPosition);
            //Debug.Log((Time.realtimeSinceStartup - curtime).ToString());
            if (bestPath == null) return false;
            FollowPath(entity, bestPath);
            return true;
        }

        void FollowPath(BaseEntity entity, List<Vector3> pathpoints)
        {
            PathFollower pathfollower;
            if (entity.GetComponent<PathFollower>() != null) pathfollower = entity.GetComponent<PathFollower>();
            else pathfollower = entity.gameObject.AddComponent<PathFollower>();
            pathfollower.Paths = pathpoints;
            pathfollower.enabled = true;
        }
        List<Vector3> FindBestPathShow(BasePlayer player, Vector3 sourcePosition, Vector3 targetPosition)
        {
            List<Vector3> bestPath = FindLinePath(sourcePosition, targetPosition, player);
            if (bestPath == null) bestPath = FindPath(sourcePosition, targetPosition, player);
            return bestPath;
        }
        List<Vector3> FindBestPath(Vector3 sourcePosition, Vector3 targetPosition)
        {
            List<Vector3> bestPath = FindLinePath(sourcePosition, targetPosition);
            if(bestPath == null) bestPath = FindPath(sourcePosition, targetPosition);
            return bestPath;
        }

        List<Vector3> FindPath(Vector3 sourcePosition, Vector3 targetPosition, BasePlayer player = null)
        {
            var path = new Pathfinder();
            if (player != null)
                path.player = player;
            var FoundPath = path.FindPath(sourcePosition, targetPosition);
            path = null;
            return FoundPath;
        }

        List<Vector3> FindLinePath(Vector3 sourcePosition, Vector3 targetPosition, BasePlayer player = null)
        {
            float distance = (int)Mathf.Ceil(Vector3.Distance(sourcePosition, targetPosition));
            Hash<float,Vector3> StraightPath = new Hash<float, Vector3>();
            StraightPath[0f] = sourcePosition;
            Vector3 currentPos;
            for(float i = 1f; i < distance; i++)
            {
                currentPos = Vector3.Lerp(sourcePosition, targetPosition, i/ distance);
                if (!FindRawGroundPosition(currentPos, out GroundPosition))
                    if (!FindRawGroundPositionUP(currentPos, out GroundPosition))
                        return null;
                if (Vector3.Distance(GroundPosition, StraightPath[i - 1f]) > 2) return null;
                if (Physics.Linecast(StraightPath[i - 1f] + jumpPosition, GroundPosition + jumpPosition, blockLayer)) return null;
                if (player != null)
                {
                    Draw.Call("Sphere",player, StraightPath[i], 0.5f, UnityEngine.Color.white, 20f);
                }
                StraightPath[i] = GroundPosition;
            }
            if (Physics.Linecast(StraightPath[distance - 1f] + jumpPosition, targetPosition + jumpPosition, blockLayer)) return null;
            StraightPath[distance] = targetPosition;
            StraightPath.Remove(0f);

            List<Vector3> straightPath = new List<Vector3>();
            foreach (KeyValuePair<float, Vector3> pair in StraightPath) { straightPath.Add(pair.Value); }
            StraightPath.Clear();
            return straightPath;
        }

        /////////////////////////////////////////////
        /// Reset part of the plugin
        /////////////////////////////////////////////
        void ResetPathFollowers()
        {
            var objects = GameObject.FindObjectsOfType(typeof(PathFollower));
            if (objects != null)
                foreach (PathFollower gameObj in objects)
                    if(gameObj.Paths.Count == 0)
                        GameObject.Destroy(gameObj);
        }

        /////////////////////////////////////////////
        /// Debug Command
        /////////////////////////////////////////////
        [ChatCommand("path")]
        void cmdChatPath(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1) return;
            if (!TryGetPlayerView(player, out currentRot)) return;
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint)) return;

            FindAndShowPath(player, player.transform.position, closestHitpoint);
        }

        bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
        {
            viewAngle = new Quaternion(0f, 0f, 0f, 0f);
            var input = serverinput.GetValue(player) as InputState;
            if (input == null) return false;
            if (input.current == null) return false;
            viewAngle = Quaternion.Euler(input.current.aimAngles);
            return true;
        }
        bool TryGetClosestRayPoint(Vector3 sourcePos, Quaternion sourceDir, out object closestEnt, out Vector3 closestHitpoint)
        {
            Vector3 sourceEye = sourcePos + new Vector3(0f, 1.5f, 0f);
            UnityEngine.Ray ray = new UnityEngine.Ray(sourceEye, sourceDir * Vector3.forward);

            var hits = UnityEngine.Physics.RaycastAll(ray);
            float closestdist = 999999f;
            closestHitpoint = sourcePos;
            closestEnt = false;
            foreach (var hit in hits)
                if (hit.collider.GetComponentInParent<TriggerBase>() == null)
                    if (hit.distance < closestdist)
                    {
                        closestdist = hit.distance;
                        closestEnt = hit.collider;
                        closestHitpoint = hit.point;
                    }

            if (closestEnt is bool) return false;
            return true;
        }
    }
}
