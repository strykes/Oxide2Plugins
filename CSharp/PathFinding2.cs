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
    [Info("PathFinding", "Reneb", "0.0.1")]
    class PathFinding : RustPlugin
    {
        public class Pathfinder
        {
            public SortedList<float, List<PathfindNode>> SortNode;
            public SortedList<float, List<PathfindNode>> TempSortNode;
            public Hash<Vector3, PathfindNode> NodeList;
            public List<PathfindNode> CurrentDelete;

            public PathfindNode targetNode;

            public float currentPriority;

            public int Loops;

            public bool shouldBreak;

            public Pathfinder()
            {
                SortNode = new SortedList<float, List<PathfindNode>>();
                TempSortNode = new SortedList<float, List<PathfindNode>>();
                NodeList = new Hash<Vector3, PathfindNode>();
                CurrentDelete = new List<PathfindNode>();
            }
            public List<Vector3> FindPath(Vector3 sourcePos, Vector3 targetPos)
            {
                Reset();
                this.targetNode = new PathfindNode(this);
                PathfindGoal(this, this.targetNode, new Vector3(Mathf.Floor(targetPos.x), Mathf.Ceil(targetPos.y), Mathf.Floor(targetPos.z)));
                PathfindFirst(this, new PathfindNode(this), sourcePos);


                while (true)
                {
                    foreach (KeyValuePair<float, List<PathfindNode>> tempsort in TempSortNode)
                    {
                        if (!SortNode.ContainsKey(tempsort.Key)) SortNode.Add(tempsort.Key, new List<PathfindNode>());
                        foreach (PathfindNode pathnod in tempsort.Value)
                        {
                            SortNode[tempsort.Key].Add(pathnod);
                        }
                    }
                    TempSortNode.Clear();
                    CurrentDelete.Clear();
                    currentPriority = SortNode.Keys[0];
                    foreach (PathfindNode pathnode in (SortNode[currentPriority]))
                    {
                        CurrentDelete.Add(pathnode);
                        pathnode.DetectAdjacentNodes();
                        if (pathnode.isGoal) { targetNode.parentNode = pathnode; shouldBreak = true; }
                    }
                    SortNode.Remove(currentPriority);
                    Loops++;
                    if (Loops > 5000) { Debug.Log("FAIL"); NodeList.Clear(); return null; }
                    if (shouldBreak) break;
                }

                Debug.Log(string.Format("SUCCESS WITH {0} LOOPS", Loops.ToString()));
                PathfindNode parentnode = targetNode.parentNode;
                List<Vector3> PlayerPath = new List<Vector3>();
                while (true)
                {
                    PlayerPath.Add(parentnode.position);
                    parentnode = parentnode.parentNode;
                    if (parentnode == null) break;
                }
                PlayerPath.Reverse();
                Debug.Log(PlayerPath.Count.ToString());

                NodeList.Clear();
                CurrentDelete.Clear();
                TempSortNode.Clear();
                SortNode.Clear();
                Reset();
                return PlayerPath;

            }
            public void Reset()
            {
                currentPriority = 0f;
                SortNode.Clear();
                TempSortNode.Clear();
                NodeList.Clear();
                CurrentDelete.Clear();
                Loops = 0;
                shouldBreak = false;
            }

            public void AddToPriorityList(PathfindNode currentNode)
            {
                if (!TempSortNode.ContainsKey(currentNode.F)) TempSortNode.Add(currentNode.F, new List<PathfindNode>());
                ((List<PathfindNode>)TempSortNode[currentNode.F]).Add(currentNode);
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
                this.position = new Vector3(position.x, position.y, position.z);
                this.positionEyes = this.position + EyesPosition;
                this.parentNode = parentnode; 
                CalculateManhattanDistance(this, pathfinder.targetNode);
                CalculateMovementCost(this,diagonal);
                this.F = this.H + this.G;
                pathfinder.AddToPriorityList(this);
            }


            // METHODS

            // DetectAdjacentNodes()
            // This automatically creates the surrounding pathnodes
            public void DetectAdjacentNodes()
            {
                if (!Physics.Linecast(this.position, this.positionEyes + VectorForward, blockLayer))
                    north = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorForward, false);
                if (!Physics.Linecast(this.position, this.positionEyes + VectorRight, blockLayer))
                    east = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorRight, false);
                if (!Physics.Linecast(this.position, this.positionEyes + VectorBack, blockLayer))
                    south = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorBack, false);
                if (!Physics.Linecast(this.position, this.positionEyes + VectorLeft, blockLayer))
                    west = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorLeft, false);

                if (!Physics.Linecast(this.position, this.positionEyes + VectorForwardRight, blockLayer))
                    northeast = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorForwardRight, true);
                if (!Physics.Linecast(this.position, this.positionEyes + VectorForwardLeft, blockLayer))
                    northwest = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorForwardLeft, true);
                if (!Physics.Linecast(this.position, this.positionEyes + VectorBackLeft, blockLayer))
                    southeast = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorBackLeft, true);
                if (!Physics.Linecast(this.position, this.positionEyes + VectorBackRight, blockLayer))
                    southwest = FindPathNodeOrCreate(this.pathfinder, this, this.positionEyes + VectorBackRight, true);
            }
        }
        // Here we calculate the movement cost between 2 points.
        private static void CalculateMovementCost(PathfindNode currentNode, bool diagonal) { currentNode.G = currentNode.parentNode.G + (diagonal ? 14f : 10f); }
        // Here we calculate the distance between the current node and the target node
        private static void CalculateManhattanDistance(PathfindNode currentNode, PathfindNode targetNode) { currentNode.H = ((Mathf.Abs(currentNode.position.x - targetNode.position.x) + Mathf.Abs(currentNode.position.z - targetNode.position.z) + Mathf.Abs(currentNode.position.y - targetNode.position.y)) * 10); }

        // Create a new node or get the node information
        public static PathfindNode FindPathNodeOrCreate(Pathfinder pathfinder, PathfindNode parentnode, Vector3 position, bool diagonal)
        {
            if (!FindGroundPosition(position, out GroundPosition)) return null;
            if (pathfinder.NodeList[GroundPosition] == null) pathfinder.NodeList[GroundPosition] = new PathfindNode(pathfinder, parentnode, GroundPosition, diagonal);
            else if (pathfinder.NodeList[GroundPosition].isGoal) { pathfinder.targetNode.parentNode = parentnode; parentnode.isGoal = true; }

            return pathfinder.NodeList[GroundPosition];
        } 
        
        public static bool FindGroundPosition(Vector3 sourcePos, out Vector3 groundPos)
        {
            groundPos = sourcePos;
            if (Physics.Raycast(sourcePos, Vector3Down, out hitinfo, groundLayer)) {
                groundPos.y = Mathf.Ceil(hitinfo.point.y);
                return true;
            } 
            return false;
        }
        /// PathfindNode(Vector3 position, Quaternion rotation)
        /// This is called by the First Path

        public static void PathfindFirst(Pathfinder pathfinder, PathfindNode pathfindnode, Vector3 position)
        {
            pathfindnode.position = new Vector3(Mathf.Floor(position.x), Mathf.Ceil(position.y), Mathf.Floor(position.z));
            pathfindnode.positionEyes = pathfindnode.position + new Vector3(0f, 0.5f, 0f);
            pathfindnode.H = 0; 
            CalculateManhattanDistance(pathfindnode, pathfinder.targetNode);
            pathfindnode.F = pathfindnode.H + pathfindnode.G;
            pathfinder.AddToPriorityList(pathfindnode);
            Debug.Log("First Node is " + pathfindnode.position.ToString());
        }

        /// 
        /// PathfindNode(Vector3 position)
        /// This is called by the Goal
        public static void PathfindGoal(Pathfinder pathfinder, PathfindNode pathfindnode, Vector3 position)
        {
            pathfindnode.position = new Vector3(Mathf.Floor(position.x), Mathf.Ceil(position.y), Mathf.Floor(position.z));
            pathfindnode.isGoal = true;
            pathfinder.NodeList[pathfindnode.position] = pathfindnode;
            Debug.Log("Goal is " + pathfindnode.position.ToString());
        }



        
        

        public static RaycastHit hitinfo;

        public static Vector3 jumpPosition = new Vector3(0f, 1f, 0f);
        public static Vector3 GroundPosition = new Vector3(0f, 0f, 0f);
        public static Vector3 EyesPosition = new Vector3(0f, 0.5f, 0f);
        public static Vector3 Vector3Down = new Vector3(0f, -1f, 0f);
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


         

        void OnServerInitialized()
        {
            groundLayer = LayerMask.GetMask(new string[] { "Terrain", "World", "Construction" });
            blockLayer = LayerMask.GetMask(new string[] { "World", "Construction" });

            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            viewangles = typeof(BasePlayer).GetField("viewAngles", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
        }


        [ChatCommand("path")]
        void cmdChatPath(BasePlayer player, string command, string[] args)
        {
            if (!TryGetPlayerView(player, out currentRot)) return;
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint)) return;
            var path = new Pathfinder();
            var PlayerPath = path.FindPath(player.transform.position, closestHitpoint);
            if (PlayerPath == null) return;
            if (player.GetComponent<PathFollower>() != null) GameObject.Destroy(player.GetComponent<PathFollower>());
            Puts("ADD"); 
            var pathfollower = player.gameObject.AddComponent<PathFollower>();
            pathfollower.Paths = PlayerPath;
            
        }
        /// 

        static float GetGroundY(Vector3 position)
        {
            position = position + jumpPosition;
            if (Physics.Raycast(position, Vector3Down, out hitinfo, 1.5f, groundLayer))
            {
                return hitinfo.point.y;
            } 
            return position.y - 1.5f;
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
        class PathFollower : MonoBehaviour
        {
            public List<Vector3> Paths = new List<Vector3>();
            public float secondsTaken;
            public float secondsToTake;
            public float waypointDone;
            public Vector3 StartPos;
            public Vector3 EndPos;
            public Vector3 nextPos;
            public BasePlayer player;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                Debug.Log("AWAKE");
                enabled = true;
            }
            void Move()
            {
                if (secondsTaken == 0f) FindNextWaypoint();
                Execute_Move();
                if (waypointDone >= 1f)
                    secondsTaken = 0f;
            }
            void Execute_Move()
            {
                if (StartPos != EndPos)
                {
                    secondsTaken += Time.deltaTime;
                    waypointDone = Mathf.InverseLerp(0f, secondsToTake, secondsTaken);
                    nextPos = Vector3.Lerp(StartPos, EndPos, waypointDone);
                    nextPos.y = GetGroundY(nextPos);
                    player.transform.position = nextPos;
                    player.ClientRPC(null, player, "ForcePositionTo", nextPos);
                    player.SendNetworkUpdate(BasePlayer.NetworkQueue.Positional);
                    player.TransformChanged();
                }
            }
            void FindNextWaypoint()
            {
                if (Paths.Count == 0)
                {
                    StartPos = EndPos = Vector3.zero;
                    return;
                } 
                SetMovementPoint(Paths[0], 20f);
            }

            public void SetMovementPoint(Vector3 endpos, float s)
            {
                StartPos = player.transform.position;
                if (endpos != StartPos)
                {
                    EndPos = endpos;
                    secondsToTake = Vector3.Distance(EndPos, StartPos) / s;
                    secondsTaken = 0f;
                    waypointDone = 0f;
                }
                Paths.RemoveAt(0);
            }
            void FixedUpdate()
            {
                Move();
            }
        }

        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(PathFollower));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        } 
    }
}
