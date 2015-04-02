// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("ZoneManager", "Reneb", "2.0.0")]
    class ZoneManager : RustPlugin
    {

        StoredData storedData;

        static Hash<string, ZoneDefinition> zonedefinitions = new Hash<string, ZoneDefinition>();
        public Hash<BasePlayer, string> LastZone = new Hash<BasePlayer, string>();
        public static Hash<BasePlayer, List<Zone>> playerZones = new Hash<BasePlayer, List<Zone>>();

        public static int triggerLayer;
        public static int playersMask;

        public FieldInfo[] allZoneFields;
        public FieldInfo cachedField;
        public static FieldInfo fieldInfo;

        public static Vector3 cachedDirection;



        public class ZoneLocation
        {
            public string x;
			public string y;
			public string z;
			public string r;
			Vector3 position;
            float radius;

            public ZoneLocation(Vector3 position, string radius)
			{
				x = position.x.ToString();
				y = position.y.ToString();
				z = position.z.ToString();

                r = radius.ToString();

				this.position = position;
				this.radius = float.Parse(radius);
			}

			public Vector3 GetPosition()
			{
				if (position == Vector3.zero)
					position = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
				return position;
			}
			public float GetRadius()
			{
                if(radius == 0f)
				    radius = float.Parse(r);
				return radius;
			}
            public string String()
            {
                return string.Format("Pos({0},{1},{2}) - Rad({3})",x,y,z,r);
            }
        }
        public class Zone : MonoBehaviour
        {
            public ZoneDefinition info;
            public List<BasePlayer> inTrigger = new List<BasePlayer>();

            void Awake()
            {
                gameObject.layer = triggerLayer;
                gameObject.name = "Zone Manager";
                gameObject.AddComponent<UnityEngine.SphereCollider>();
                gameObject.SetActive(true);
            }
            public void SetInfo(ZoneDefinition info)
            {
                this.info = info;
                GetComponent<UnityEngine.Transform>().position = info.Location.GetPosition();
                GetComponent<UnityEngine.SphereCollider>().radius = info.Location.GetRadius();
            }
            void OnDestroy()
            {
            }
            void OnTriggerEnter(Collider col)
            {
                if (col.GetComponentInParent<BasePlayer>())
                {
                    inTrigger.Add(col.GetComponentInParent<BasePlayer>());
                    OnEnterZone(this, col.GetComponentInParent<BasePlayer>());
                }
            }
            void OnTriggerExit(Collider col)
            {
                if (col.GetComponentInParent<BasePlayer>())
                {
                    inTrigger.Remove(col.GetComponentInParent<BasePlayer>());
                    OnExitZone(this, col.GetComponentInParent<BasePlayer>());
                }
            }
        }


        public class ZoneDefinition
        {

            public string name;
            public string radius;
            public ZoneLocation Location;
            public string ID;
            public string eject;
            public string pvpgod;
            public string pvegod;
            public string sleepgod;
            public string undestr;
            public string nobuild;
            public string notp;
            public string nochat;
            public string nodeploy;
            public string nokits;
            public string nosuicide;
            public string killsleepers;
            public string radiation;
            public string enter_message;
            public string leave_message;

            public ZoneDefinition(Vector3 position)
            {
                radius = "20";
                Location = new ZoneLocation(position, radius);
            }
            
        } 
        class StoredData
        {
            public HashSet<ZoneDefinition> ZoneDefinitions = new HashSet<ZoneDefinition>();
            public StoredData()
            {
            }
        }
        void SaveData() {
            Interface.GetMod().DataFileSystem.WriteObject("ZoneManager", storedData);
        }
        void LoadData()
        {
            zonedefinitions.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("ZoneManager");
            } 
            catch 
            {
                storedData = new StoredData();
            }
            foreach (var zonedef in storedData.ZoneDefinitions)
                zonedefinitions[zonedef.ID] = zonedef;
             
        }
        void NewZone(ZoneDefinition zonedef)
        {
            var newgameObject = new UnityEngine.GameObject();
            var newZone = newgameObject.AddComponent<Zone>();
            newZone.SetInfo(zonedef);
        }
        void RefreshZone(string zoneID)
        {
            var objects = GameObject.FindObjectsOfType(typeof(Zone));
            if (objects != null)
                foreach (Zone gameObj in objects)
                {
                    if (gameObj.info.ID == zoneID)
                    {
                        foreach(KeyValuePair<BasePlayer, List<Zone>> pair in playerZones)
                        {
                            if (pair.Value.Contains(gameObj)) playerZones[pair.Key].Remove(gameObj);
                        }
                        GameObject.Destroy(gameObj);
                        if(zonedefinitions[zoneID] != null)
                        {
                            NewZone(zonedefinitions[zoneID]);
                        }
                        break;
                    }
                }
        }

        void Loaded()
        {
            triggerLayer = UnityEngine.LayerMask.NameToLayer("Trigger");
            playersMask = LayerMask.GetMask(new string[] { "Player (Server)", "AI" });
            LoadData();
        }
        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(Zone));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
        }
        void Unloaded()
        {
            SaveData();
        } 
        void OnServerInitialized()
        {
            allZoneFields = typeof(ZoneDefinition).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (KeyValuePair<string, ZoneDefinition> pair in zonedefinitions)
            {
                NewZone(pair.Value); 
            } 
        }
        int GetRandom(int min, int max) { return UnityEngine.Random.Range(min, max); }

        FieldInfo GetZoneField(string name)
        {
            name = name.ToLower();
            foreach (FieldInfo fieldinfo in allZoneFields) { if(fieldinfo.Name == name) return fieldinfo; }
            return null;
        }
        static bool hasTag(BasePlayer player, string tagname)
        {
            if (playerZones[player] == null) { return false; }
            if (playerZones[player].Count == 0) { return false; }
            fieldInfo = typeof(ZoneDefinition).GetField(tagname, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (Zone zone in playerZones[player])
            {
                if (fieldInfo.GetValue(zone.info) != null)
                    return true;
            }
            return false;
        }
        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner.ownerPlayer == null) return;
            if (hasTag(planner.ownerPlayer,"nobuild"))
            {
                gameobject.GetComponentInParent<BuildingBlock>().Kill(BaseNetworkable.DestroyMode.Gib);
                SendMessage(planner.ownerPlayer, "You are not allowed to build here");
            }
        }
        void OnItemDeployed(Deployer deployer, BaseEntity deployedEntity)
        {
            if (deployer.ownerPlayer == null) return;
            if (hasTag(deployer.ownerPlayer, "nodeploy"))
            {
                deployedEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                SendMessage(deployer.ownerPlayer, "You are not allowed to deploy here");
            }
        }
        object canRedeemKit(BasePlayer player)
        {
            if (hasTag(player, "nokits")) { return "You may not redeem a kit inside this area"; }
            return null;
        }
        object canTeleport(BasePlayer player)
        {
            if (hasTag(player, "notp")) { return "You may not teleport in this area"; }
            return null;
        }
        static void OnEnterZone(Zone zone, BasePlayer player)
        {
            if (playerZones[player] == null) playerZones[player] = new List<Zone>();
            if (!playerZones[player].Contains(zone)) playerZones[player].Add(zone);
            //RefreshBuildPermission(player);
            if (zone.info.enter_message != null) SendMessage(player, zone.info.enter_message);
            if (zone.info.eject != null) EjectPlayer(zone, player);
        }
        static void OnExitZone(Zone zone, BasePlayer player)
        {
            if(playerZones[player].Contains(zone)) playerZones[player].Remove(zone);
            //RefreshBuildPermission(player);
            if (zone.info.leave_message != null) SendMessage(player, zone.info.leave_message);
        }
        static void EjectPlayer(Zone zone, BasePlayer player)
        {
            cachedDirection = player.transform.position - zone.transform.position;
            player.transform.position = zone.transform.position + ( cachedDirection / cachedDirection.magnitude * (zone.GetComponent<UnityEngine.SphereCollider>().radius + 1f));
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { player.transform.position });
            player.TransformChanged(); 
        }
        
        //////////////////////////////////////////////////////////////////////////////
        /// Chat Commands
        //////////////////////////////////////////////////////////////////////////////
        [ChatCommand("zone_add")]
        void cmdChatZoneAdd(BasePlayer player, string command, string[] args)
        {
            var newzoneinfo = new ZoneDefinition(player.transform.position);
            newzoneinfo.ID = GetRandom(1, 99999999).ToString();
            NewZone(newzoneinfo);
            if (zonedefinitions[newzoneinfo.ID] != null) storedData.ZoneDefinitions.Remove(zonedefinitions[newzoneinfo.ID]);
            zonedefinitions[newzoneinfo.ID] = newzoneinfo;
            LastZone[player] = newzoneinfo.ID;
            storedData.ZoneDefinitions.Add(zonedefinitions[newzoneinfo.ID]);
            SaveData();
            SendMessage(player, "New Zone created, you may now edit it: " + newzoneinfo.Location.String());
        }
        [ChatCommand("zone_reset")]
        void cmdChatZoneReset(BasePlayer player, string command, string[] args)
        { 
            zonedefinitions.Clear();
            storedData.ZoneDefinitions.Clear();
            SaveData();
            Unload();
            SendMessage(player, "All Zones were removed");
        }
        [ChatCommand("zone_remove")]
        void cmdChatZoneRemove(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0) return;
            if (zonedefinitions[args[0]] == null) { SendMessage(player, "This zone doesn't exist"); return; }
            zonedefinitions[args[0]] = null;
            storedData.ZoneDefinitions.Remove(zonedefinitions[args[0]]);
            SaveData();
            SendMessage(player, "All Zones were removed");
        }
        [ChatCommand("zone_edit")]
        void cmdChatZoneEdit(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0) return;
            if (zonedefinitions[args[0]] == null) { SendMessage(player, "This zone doesn't exist"); return; }
            LastZone[player] = args[0];
            SendMessage(player, "Editing zone ID: " + args[0]);
        }
        [ChatCommand("zone_list")]
        void cmdChatZoneList(BasePlayer player, string command, string[] args)
        {
            SendMessage(player, "========== Zone list ==========");
            if(zonedefinitions.Count == 0) { SendMessage(player, "empty"); return; }
            foreach (KeyValuePair<string, ZoneDefinition> pair in zonedefinitions)
            {
                SendMessage(player, string.Format("{0} => {1} - {2}",pair.Key,pair.Value.name,pair.Value.Location.String()));
            }
        }
        [ChatCommand("zone")]
        void cmdChatZone(BasePlayer player, string command, string[] args)
        {
            if (LastZone[player] == null) return;
            object value;
            
            if (args.Length < 2)
            {
                SendMessage(player, "/zone option value/reset");
                foreach (FieldInfo fieldinfo in allZoneFields)
                {
                    value = fieldinfo.GetValue(zonedefinitions[LastZone[player]]);
                    switch (fieldinfo.Name)
                    {
                        case "Location":
                            value = ((ZoneLocation)value).String();
                            break;
                        default:
                            if (value == null) value = "false";
                            break;
                    }
                    SendMessage(player, string.Format("{0} => {1}", fieldinfo.Name, value.ToString()));
                }
                return;
            }
            string editvalue;
            for (int i = 0; i < args.Length; i = i + 2)
            {
                cachedField =  GetZoneField(args[i]);
                if (cachedField == null) continue;
                switch(args[i+1])
                {
                    case "true":
                    case "1":
                        editvalue = "true";
                        break;
                    case "null":
                    case "0":
                    case "false":
                    case "reset":
                        editvalue = null;
                        break;
                    default:
                        editvalue = args[i + 1];
                        break;
                }
                cachedField.SetValue(zonedefinitions[LastZone[player]], editvalue);
                SendMessage(player, string.Format("{0} set to {1}", cachedField.Name, editvalue));
            }
            RefreshZone(LastZone[player]);
            SaveData();
        }
        static void SendMessage(BasePlayer player, string message) { player.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", "ZoneManager", message), 1.0 }); }
    }
}
