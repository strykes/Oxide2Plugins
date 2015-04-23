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
    [Info("Waypoints", "Reneb", "1.0.1")]
    class Waypoints : RustPlugin
    {

        void Loaded()
        {
            LoadData();
        }

        StoredData storedData;
        static Hash<string, Waypoint> waypoints = new Hash<string, Waypoint>();
        class StoredData
        {
            public HashSet<Waypoint> WayPoints = new HashSet<Waypoint>();

            public StoredData()
            {
            }
        }
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("Waypoints", storedData);
        }
        void LoadData()
        {
            waypoints.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("Waypoints");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var thewaypoint in storedData.WayPoints)
                waypoints[thewaypoint.Name] = thewaypoint;
        }

        ////////////////////////////////////////////////////// 
        ///  class WaypointInfo
        ///  Waypoint information, position & speed
        ///  public => will be saved in the data file
        ///  non public => won't be saved in the data file
        class WaypointInfo
        {
            public string x;
            public string y;
            public string z;
            public string s;
            Vector3 position;
            float speed;

            public WaypointInfo(Vector3 position, float speed)
            {
                x = position.x.ToString();
                y = position.y.ToString();
                z = position.z.ToString();
                s = speed.ToString();

                this.position = position;
                this.speed = speed;
            }

            public Vector3 GetPosition()
            {
                if (position == Vector3.zero)
                    position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                return position;
            }
            public float GetSpeed()
            {
                speed = Convert.ToSingle(s);
                return speed;
            }
        }
        ////////////////////////////////////////////////////// 
        ///  class Waypoint
        ///  Waypoint List information
        //////////////////////////////////////////////////////
        class Waypoint
        {
            public string Name;
            public List<WaypointInfo> Waypoints;

            public Waypoint()
            {
                Waypoints = new List<WaypointInfo>();
            }
            public void AddWaypoint(Vector3 position, float speed)
            {
                Waypoints.Add(new WaypointInfo(position, speed));
            }
        }


        class WaypointEditor : MonoBehaviour
        {
            public Waypoint targetWaypoint;

            void Awake()
            {
            }
        } 
        [HookMethod("GetWaypointsList")]
        object GetWaypointsList(string name)
        {
            if (waypoints[name] == null) return null;
            var returndic = new List<object>();
            
            foreach(WaypointInfo wp in waypoints[name].Waypoints)
            {
                var newdic = new Dictionary<Vector3, float>();
                newdic.Add(wp.GetPosition(), wp.GetSpeed());
                returndic.Add(newdic);
            }
            return returndic;
        }

        bool hasAccess(BasePlayer player)
        {
            if (player.net.connection.authLevel < 1) { SendReply(player, "You don't have access to this command"); return false; }
            return true;
        }
         bool isEditingWP(BasePlayer player, int ttype)
        {
            if (player.GetComponent<WaypointEditor>() != null)
            {
                if (ttype == 0) SendReply(player, string.Format("You are already editing {0}", player.GetComponent<WaypointEditor>().targetWaypoint.Name.ToString()));
                return true;
            }
            else
            {
                if (ttype == 1) SendReply(player, string.Format("You are not editing any waypoints, say /waypoints_new or /waypoints_edit NAME"));
                return false;
            }
        }
        ////////////////////////////////////////////////////// 
        // Waypoints manager
        ////////////////////////////////////////////////////// 

        [ChatCommand("waypoints_new")]
        void cmdWaypointsNew(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (isEditingWP(player, 0)) return;

            var newWaypoint = new Waypoint();
            if (newWaypoint == null)
            {
                SendReply(player, "Waypoints: Something went wrong while making a new waypoint");
                return;
            }
            var newWaypointEditor = player.gameObject.AddComponent<WaypointEditor>();
            newWaypointEditor.targetWaypoint = newWaypoint;
            SendReply(player, "Waypoints: New WaypointList created, you may now add waypoints.");
        }
        [ChatCommand("waypoints_add")]
        void cmdWaypointsAdd(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (!isEditingWP(player, 1)) return;
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if (WaypointEditor.targetWaypoint == null)
            {
                SendReply(player, "Waypoints: Something went wrong while getting your WaypointList");
                return;
            }
            float speed = 3f;
            if (args.Length > 0) float.TryParse(args[0], out speed);
            WaypointEditor.targetWaypoint.AddWaypoint(player.transform.position, speed);

            SendReply(player, string.Format("Waypoint Added: {0} {1} {2} - Speed: {3}", player.transform.position.x.ToString(), player.transform.position.y.ToString(), player.transform.position.z.ToString(), speed.ToString()));
        }
        [ChatCommand("waypoints_list")]
        void cmdWaypointsList(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (waypoints.Count == 0)
            {
                SendReply(player, "No waypoints created yet");
                return;
            }
            SendReply(player, "==== Waypoints ====");
            foreach (KeyValuePair<string, Waypoint> pair in waypoints)
            {
                SendReply(player, pair.Key);
            }
        }
        [ChatCommand("waypoints_remove")]
        void cmdWaypointsRemove(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (waypoints.Count == 0)
            {
                SendReply(player, "No waypoints created yet");
                return;
            }
            if(args.Length == 0)
            {
                SendReply(player, "/waypoints_list to get the list of waypoints");
                return;
            }
            if(waypoints[args[0]] == null)
            {
                SendReply(player, "Waypoint "+ args[0]+ " doesn't exist");
                return;
            }
            storedData.WayPoints.Remove(waypoints[args[0]]);
            waypoints[args[0]] = null;
            SaveData();
            SendReply(player, string.Format("Waypoints: {0} was removed",args[0]));
        }
        [ChatCommand("waypoints_save")]
        void cmdWaypointsSave(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (!isEditingWP(player, 1)) return;
            if (args.Length == 0)
            {
                SendReply(player, "Waypoints: /waypoints_save NAMEOFWAYPOINT");
                return;
            }
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if (WaypointEditor.targetWaypoint == null)
            {
                SendReply(player, "Waypoints: Something went wrong while getting your WaypointList");
                return;
            }

            WaypointEditor.targetWaypoint.Name = args[0];
            if (waypoints[args[0]] != null) storedData.WayPoints.Remove(waypoints[args[0]]);
            waypoints[args[0]] = WaypointEditor.targetWaypoint;
            Debug.Log(waypoints[args[0]].ToString());
            storedData.WayPoints.Add(waypoints[args[0]]);
            SendReply(player, string.Format("Waypoints: New waypoint saved with: {0} with {1} waypoints stored", WaypointEditor.targetWaypoint.Name, WaypointEditor.targetWaypoint.Waypoints.Count.ToString()));
            GameObject.Destroy(player.GetComponent<WaypointEditor>());
            SaveData();
        }
        [ChatCommand("waypoints_close")]
        void cmdWaypointsClose(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (!isEditingWP(player, 1)) return;
            SendReply(player, "Waypoints: Closed without saving");
            GameObject.Destroy(player.GetComponent<WaypointEditor>());
        }
    }
}
