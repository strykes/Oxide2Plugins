// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Rust;

namespace Oxide.Plugins
{
    [Info("HumanNPC", "Reneb", "1.0")]
    class HumanNPC : RustPlugin
    {

        private static FieldInfo serverinput;
        private static FieldInfo viewangles;
        private DamageTypeList emptyDamage;
        private List<Oxide.Plugins.Timer> TimersList;

        // Cached
        private Quaternion currentRot;

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
				if (Single.IsNaN(speed))
					speed = float.Parse(s);
				return speed;
			}
		}
        class SpawnInfo
		{
			public string x;
			public string y;
			public string z;
			public string rx;
            public string ry;
            public string rz;
            public string rw;
			Vector3 position;
            Quaternion rotation;

			public SpawnInfo(Vector3 position, Quaternion rotation)
			{
				x = position.x.ToString();
				y = position.y.ToString();
				z = position.z.ToString();
				
                rx = rotation.x.ToString();
				ry = rotation.y.ToString();
				rz = rotation.z.ToString();
			    rw = rotation.w.ToString();

				this.position = position;
				this.rotation = rotation;
			}

			public Vector3 GetPosition()
			{
				if (position == Vector3.zero)
					position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
				return position;
			}
			public Quaternion GetRotation()
			{
				if (rotation == new Quaternion(0f,0f,0f,0f))
					rotation = new Quaternion(float.Parse(rx), float.Parse(ry), float.Parse(rz), float.Parse(rw));
				return rotation;
			}
		}
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
        		Waypoints.Add(new WaypointInfo( position, speed ));
        	}
        }


        class HumanPlayer : MonoBehaviour
        {
            public HumanNPCInfo info;
            public BasePlayer player;

            public bool invulnerability;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
            public void SetInfo(HumanNPCInfo info)
            {
                this.info = info;
                enabled = true;
            } 
            void Update()
            {
                if(info == null) enabled = false;

            }
        }

        class HumanNPCInfo
        {
            public string userid;
            public string displayName;
            public string invulnerability;
            public string initialHealth;
            public string maxHealth;
            public string respawn;
            public string respawnSeconds;
            public SpawnInfo spawnInfo;

            public HumanNPCInfo(ulong userid, Vector3 position, Quaternion rotation)
			{
				this.userid = userid.ToString();
                displayName = "NPC";
                invulnerability = "true";
                initialHealth = "50";
                maxHealth = "100";
                respawn = "true";
                respawnSeconds = "60";
                spawnInfo = new SpawnInfo(position, rotation);
			}
        }
    	
    	class WaypointEditor : MonoBehaviour
        {
            public Waypoint targetWaypoint;

            void Awake()
            {
            }
        }
    	class NPCEditor : MonoBehaviour
        {
            public BasePlayer player;
            public HumanPlayer targetNPC;
            void Awake()
            {
                player = GetComponent<BasePlayer>();
            }
        }

    	class StoredData
        {
            public HashSet<Waypoint> WayPoints = new HashSet<Waypoint>();
            public HashSet<HumanNPCInfo> HumanNPCs = new HashSet<HumanNPCInfo>();

            public StoredData()
            {
            }
        }
    	
    	StoredData storedData;
        Hash<string, Waypoint> waypoints = new Hash<string, Waypoint>();
        Hash<string, HumanNPCInfo> humannpcs = new Hash<string, HumanNPCInfo>();
        bool dataChanged = false;
    	
    	void Loaded()
        {
            LoadData();
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            viewangles = typeof(BasePlayer).GetField("viewAngles", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            TimersList = new List<Oxide.Plugins.Timer>();
        }
        void Unloaded()
        {
            SaveData();
        }
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(WaypointEditor));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        } 
        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("HumanNPC", storedData);
        }
		void LoadData()
        {
            waypoints.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("HumanNPC");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var thewaypoint in storedData.WayPoints)
                waypoints[thewaypoint.Name] = thewaypoint;
            foreach (var thenpc in storedData.HumanNPCs)
                humannpcs[thenpc.userid] = thenpc;
        }
    	void OnServerSave()
        {
            if (!dataChanged) return;
            SaveData();
            dataChanged = false;
        }
    	bool hasAccess(BasePlayer player)
        {
			if(player.net.connection.authLevel < 1)
			{
				SendReply(player, "You don't have access to this command");
				return false;
			}
			return true;
        }
        bool isEditingWP(BasePlayer player, int ttype)
        {
        	if(player.GetComponent<WaypointEditor>() != null)
        	{
        		if(ttype == 0)
        			SendReply(player, string.Format("You are already editing {0}",player.GetComponent<WaypointEditor>().targetWaypoint.Name.ToString()));

        		return true;
        	}
        	else
        	{
        		if(ttype == 1)
        			SendReply(player, string.Format("You are not editing any waypoints, say /waypoints_new or /waypoints_edit NAME"));
        			
        		return false;
        	}
        }
        bool hasNoArguments(BasePlayer player, string[] args, int Number)
        {
        	if(args.Length < Number)
        	{
        		SendReply(player, "Not enough Arguments, say /waypoints_help for more informations");
        		return true;
        	}
        	return false;
        }
        bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
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


        [ChatCommand("npc_add")]
        void cmdChatNPCAdd(BasePlayer player, string command, string[] args)
        {
            if (player.GetComponent<NPCEditor>() != null)
            {
                SendReply(player, "NPC Editor: Already editing an NPC, say /npc_end first");
                return;
            }
            if (!TryGetPlayerView(player, out currentRot))
            {
                return;
            }

            var newplayer = GameManager.server.CreateEntity("player/player", player.transform.position, currentRot).ToPlayer();
            newplayer.Spawn(true);
            var humannpcinfo = new HumanNPCInfo(newplayer.userID, player.transform.position, currentRot);
            var humanplayer = newplayer.gameObject.AddComponent<HumanPlayer>();
            humanplayer.SetInfo(humannpcinfo);

            var npceditor = player.gameObject.AddComponent<NPCEditor>();
            npceditor.targetNPC = humanplayer;
            
            if(humannpcs[newplayer.userID.ToString()] != null) storedData.HumanNPCs.Remove(humannpcs[newplayer.userID.ToString()]);
            humannpcs[newplayer.userID.ToString()] = humannpcinfo;
            storedData.HumanNPCs.Add(humannpcs[newplayer.userID.ToString()]);
            SaveData();
        }














        List<WaypointInfo> GetWayPoints(string name) => waypoints[name]?.Waypoints;
        
    	[ChatCommand("waypoints_new")]
        void cmdWaypointsNew(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(isEditingWP(player,0)) return;
            
            var newWaypoint = new Waypoint();
            if(newWaypoint == null)
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
            if(!hasAccess(player)) return;
            if(!isEditingWP(player,1)) return;
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if(WaypointEditor.targetWaypoint == null)
            {
            	SendReply(player, "Waypoints: Something went wrong while getting your WaypointList");
            	return;
            }
            float speed = 3f;
            if(args.Length > 0) float.TryParse(args[0], out speed);
            WaypointEditor.targetWaypoint.AddWaypoint(player.transform.position,speed);
            
            SendReply(player, string.Format("Waypoint Added: {0} {1} {2} - Speed: {3}",player.transform.position.x.ToString(),player.transform.position.y.ToString(),player.transform.position.z.ToString(),speed.ToString()));
        }
        [ChatCommand("waypoints_save")]
        void cmdWaypointsSave(BasePlayer player, string command, string[] args)
        {
            if(!hasAccess(player)) return;
            if(!isEditingWP(player,1)) return;
            if(args.Length == 0)
            {
                SendReply(player, "Waypoints: /waypoints_save NAMEOFWAYPOINT");
                return;
            }
            var WaypointEditor = player.GetComponent<WaypointEditor>();
            if(WaypointEditor.targetWaypoint == null)
            {
            	SendReply(player, "Waypoints: Something went wrong while getting your WaypointList");
            	return;
            }
            
            WaypointEditor.targetWaypoint.Name = args[0];
            
            if(waypoints[args[0]] != null) storedData.WayPoints.Remove(waypoints[args[0]]);
            waypoints[args[0]] = WaypointEditor.targetWaypoint;
            storedData.WayPoints.Add(waypoints[args[0]]);
            SendReply(player, string.Format("Waypoints: New waypoint saved with: {0} with {1} waypoints stored",WaypointEditor.targetWaypoint.Name, WaypointEditor.targetWaypoint.Waypoints.Count.ToString()));
        	GameObject.Destroy(player.GetComponent<WaypointEditor>());
            SaveData();
        }
    }
}
