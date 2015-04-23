using System.Collections.Generic;
using System;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
 
namespace Oxide.Plugins
{
    [Info("AntiCheat", "Reneb", "2.1.7", ResourceId = 730)]
    class AntiCheat : RustPlugin
    {
        static RaycastHit cachedRaycasthit;
        static int constructionColl;
        float lastTime;
        bool serverInitialized = false;
        Oxide.Plugins.Timer activateTimer;
        static Vector3 VectorDown = new Vector3(0f, -1f, 0f);
        GameObject originalWallhack;
        List<GameObject> ListGameObjects = new List<GameObject>();

        Hash<TriggerBase, Hash<BaseEntity, Vector3>> TriggerData = new Hash<TriggerBase, Hash<BaseEntity, Vector3>>();
        Hash<TriggerBase, BuildingBlock> TriggerToBlock = new Hash<TriggerBase, BuildingBlock>();
        Hash<BaseEntity, float> lastDetections = new Hash<BaseEntity, float>();

        static int authIgnore = 1; 


        static bool speedhack = true;
        static bool speedhackPunish = true;
        static int speedhackDetections = 3;
        static float minSpeedPerSecond = 10f;


        static bool flyhack = true;
        static bool flyhackPunish = true;
        static int flyhackDetections = 3;

        static bool wallhack = true;
         
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
            CheckCfg<int>("Settings: Ignore Hacks for authLevel", ref authIgnore);
            CheckCfg<bool>("SpeedHack: activated", ref speedhack);
            CheckCfg<bool>("SpeedHack: Punish", ref speedhackPunish);
            CheckCfg<int>("SpeedHack: Punish Detections", ref speedhackDetections);
            CheckCfg<float>("SpeedHack: Speed Detection", ref minSpeedPerSecond);
            CheckCfg<bool>("Flyhack: activated", ref flyhack);
            CheckCfg<bool>("Flyhack: Punish", ref flyhackPunish);
            CheckCfg<int>("Flyhack: Punish Detections", ref flyhackDetections);
            CheckCfg<bool>("Wallhack: activated", ref wallhack);
            SaveConfig();

        } 


        public class PlayerHack : MonoBehaviour
        {
            public BasePlayer player;
            public Vector3 lastPosition;
            public float Distance3D;
            public float VerticalDistance;
            public bool isonGround;
            
            public float currentTick;
            public float lastTick;

            public float speedHackDetections = 0f;
            public float lastTickSpeed;

            public float flyHackDetections = 0f;
            public float lastTickFly;

            void Awake()
            {
                player = GetComponent<BasePlayer>();
                InvokeRepeating("CheckPlayer", 1f, 1f);
                lastPosition = player.transform.position;
            }
            void CheckPlayer()
            {
                if (!player.IsConnected()) GameObject.Destroy(this);
                currentTick = Time.realtimeSinceStartup;
                Distance3D = Vector3.Distance(player.transform.position, lastPosition);
                VerticalDistance = player.transform.position.y - lastPosition.y;
                isonGround = player.IsOnGround();

                if(!player.IsWounded() && !player.IsDead() && !player.IsSleeping())
                    CheckForHacks(this);

                lastPosition = player.transform.position;
                lastTick = currentTick;
            }
        }
        static void CheckForHacks(PlayerHack hack)
        {
            if (speedhack)
                CheckForSpeedHack(hack);
            if (flyhack)
                CheckForFlyhack(hack);
          
        } 
        static void CheckForSpeedHack(PlayerHack hack)
        {
            if (hack.Distance3D < minSpeedPerSecond) return;
            if (hack.VerticalDistance < -10f) return;
            if(hack.lastTickSpeed == hack.lastTick)
            {
                hack.speedHackDetections++; 
                SendDetection(string.Format("{0} - {1} is being detected with: Speedhack ({2}m/s)", hack.player.userID.ToString(), hack.player.displayName, hack.Distance3D.ToString()));
                if(hack.speedHackDetections >= speedhackDetections)
                {
                    if(speedhackPunish)
                        Punish(hack.player, string.Format("rSpeedhack ({0}m/s)", hack.Distance3D.ToString()));
                }
            }
            else
            {
                hack.speedHackDetections = 0f;
            }
            hack.lastTickSpeed = hack.currentTick;
        }
        static void CheckForFlyhack(PlayerHack hack)
        { 
            if (hack.isonGround) return;
            if (hack.player.transform.position.y < 5f) return;
            if (hack.VerticalDistance < -10f) return; 
            if (UnityEngine.Physics.Raycast(hack.player.transform.position, VectorDown, 5f)) return;
            if (hack.lastTickFly == hack.lastTick) 
            {
                hack.flyHackDetections++;
                SendDetection(string.Format("{0} - {1} is being detected with: Flyhack ({2}m/s)", hack.player.userID.ToString(), hack.player.displayName, hack.Distance3D.ToString()));
                if (hack.flyHackDetections >= flyhackDetections)
                {
                    if (flyhackPunish)
                        Punish(hack.player, string.Format("rFlyhack ({0}m/s)", hack.Distance3D.ToString()));
                }
            } 
            else
            {
                hack.flyHackDetections = 0f;
            }
            hack.lastTickFly = hack.currentTick;
        }
        static void SendDetection(string msg)
        {
            foreach (var cplayer in BasePlayer.activePlayerList)
            {
                if (cplayer.net.connection.authLevel >= authIgnore)
                {
                    cplayer.SendConsoleCommand("chat.add", new object[] { 0, msg.QuoteSafe() });
                }
            }
            Debug.Log(msg);
        }
        static void Punish(BasePlayer player, string msg)
        {
            if (player.net.connection.authLevel < authIgnore)
                Interface.GetMod().CallHook("Ban", null, player, msg, false);
            else
            {
                GameObject.Destroy(player.GetComponent<PlayerHack>());
            }
        }
       bool isOpen(BuildingBlock block)
        {
            if (block.IsOpen()) return false;
            BaseLock slot = block.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
            if (slot == null) return false;
            return !slot.IsLocked();
        }
        void OnEntityEnter(TriggerBase triggerbase, BaseEntity entity)
        {
            if (triggerbase.gameObject.name != "Anti Wallhack(Clone)") return;
            if (entity.GetComponent<BasePlayer>() == null) return;
            if (TriggerData[triggerbase] == null)
                TriggerData[triggerbase] = new Hash<BaseEntity,Vector3>();
            (TriggerData[triggerbase])[entity] = entity.transform.position;
        }
        void OnEntityLeave(TriggerBase triggerbase, BaseEntity entity)
        {
            if (entity == null || triggerbase == null || triggerbase.gameObject.name != "Anti Wallhack(Clone)") return;
            if (entity.GetComponent<BasePlayer>() == null) return;
            if(TriggerToBlock[triggerbase] == null)
            {
                GameObject.Destroy(triggerbase.gameObject);
                return;
            }
           
            if(!isOpen(TriggerToBlock[triggerbase]))
            {
                if (entity.GetComponent<BasePlayer>().net.connection.authLevel < authIgnore)
                    if (CheckWallhack(TriggerToBlock[triggerbase], entity, (TriggerData[triggerbase])[entity]))
                    {
                    if (Time.realtimeSinceStartup - lastDetections[entity] < 2)
                    {
                        SendMsgAdmin(string.Format("{0} was detected wallhacking from {1} to {2}", entity.GetComponent<BasePlayer>().displayName, (TriggerData[triggerbase])[entity].ToString(), entity.transform.position.ToString()));
                        Debug.Log(string.Format("{0} was detected wallhacking from {1} to {2}", entity.GetComponent<BasePlayer>().displayName, (TriggerData[triggerbase])[entity].ToString(), entity.transform.position.ToString()));
                    }

                    lastDetections[entity] = Time.realtimeSinceStartup;
                    ForcePlayerBack(entity.GetComponent<BasePlayer>(), (TriggerData[triggerbase])[entity], entity.transform.position);
                }
            } 
            (TriggerData[triggerbase]).Remove(entity);
        }
        static void SendMsgAdmin(string msg)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.net != null)
                {
                    if (player.net.connection.authLevel > 0)
                    {
                        player.SendConsoleCommand("chat.add", new object[] { 0, msg.QuoteSafe() });
                    }
                }
            }
        }
        bool CheckWallhack(BuildingBlock buildingblock, BaseEntity col, Vector3 initialPos)
        {
            Vector3 cachedDiff = col.transform.position - initialPos;
            if (initialPos.y - buildingblock.transform.position.y > 2) return false;

            if (UnityEngine.Physics.Linecast(initialPos, col.transform.position, out cachedRaycasthit, constructionColl))
            {
                if (cachedRaycasthit.collider.GetComponentInParent<BuildingBlock>() == buildingblock)
                {
                    return true;
                }
            }
            return false;
        }
        void Loaded()
        {
            constructionColl = LayerMask.GetMask( new string[] { "Construction" });
        }
        void ForcePlayerBack(BasePlayer player, Vector3 entryposition, Vector3 exitposition)
        {
            var distance = Vector3.Distance(exitposition, entryposition) + 0.5f;
            var direction = (entryposition - exitposition).normalized;
            ForcePlayerPosition(player, exitposition + (direction * distance));
        }
        void OnServerInitialized()
        { 
            serverInitialized = true;
            originalWallhack = new UnityEngine.GameObject("Anti Wallhack");
            originalWallhack.AddComponent<MeshCollider>();
            originalWallhack.AddComponent<TriggerBase>();
            originalWallhack.gameObject.layer = UnityEngine.LayerMask.NameToLayer("Trigger");
            var newlayermask = new UnityEngine.LayerMask();
            newlayermask.value = 133120;
            originalWallhack.GetComponent<TriggerBase>().interestLayers = newlayermask;
            RefreshAllWalls(); 
            RefreshPlayers();
        }  
        void Unload()
        {
            foreach(GameObject gameObj in ListGameObjects)
            { 
                GameObject.Destroy(gameObj);
            } 
            var objects = GameObject.FindObjectsOfType(typeof(PlayerHack));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            if (activateTimer != null)
                activateTimer.Destroy();
        } 
        void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.transform.position = destination;
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();
        }
        void RefreshPlayers()
        {
            if (!speedhack && !flyhack) return;
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.GetComponent<PlayerHack>() != null) GameObject.Destroy(player.GetComponent<PlayerHack>());
                if (player.net.connection.authLevel > authIgnore) continue;
                player.gameObject.AddComponent<PlayerHack>();
            }
        } 
        void RefreshAllWalls()
        {
            if (!wallhack) return;
            var allbuildings = UnityEngine.Resources.FindObjectsOfTypeAll<BuildingBlock>();
            var currenttime = Time.realtimeSinceStartup;
            foreach (BuildingBlock block in allbuildings)
            {
                if (block.blockDefinition != null && (block.blockDefinition.hierachyName == "wall" || block.blockDefinition.hierachyName == "door.hinged"))
                {
                    CreateNewProtectionFromBlock(block, true);
                }
            } 
            Debug.Log(string.Format("AntiCheat: Took {0} seconds to protect all walls & doors", (Time.realtimeSinceStartup - currenttime).ToString()));
        }
        void CreateNewProtectionFromBlock(BuildingBlock buildingblock, bool Immediate)
        {
            var newgameobject = UnityEngine.Object.Instantiate(originalWallhack);
            ListGameObjects.Add(newgameobject);
            var mesh = newgameobject.GetComponent<MeshCollider>();
            newgameobject.transform.position = buildingblock.transform.position;
            newgameobject.transform.rotation = buildingblock.transform.rotation;
            mesh.convex = false;
            mesh.sharedMesh = buildingblock.GetComponentInChildren<UnityEngine.MeshCollider>().sharedMesh;
            mesh.enabled = true;
            if (Immediate) newgameobject.SetActive(true);
            else
            {
                newgameobject.SetActive(false);
                timer.Once(20f, () => ActivateGameObject(newgameobject));
            }
            TriggerToBlock[newgameobject.GetComponent<TriggerBase>()] = buildingblock;
        }
        void ActivateGameObject(UnityEngine.GameObject gameObj)
        {
            if (gameObj == null) return;
            gameObj.SetActive(true);
        }
        void OnPlayerRespawned(BasePlayer  player)
        {
            if (player.net.connection.authLevel >= authIgnore) return;
            if (player.GetComponent<PlayerHack>() == null)
            {
                player.gameObject.AddComponent<PlayerHack>();
            } 
        }
        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (!serverInitialized) return;
            if (!wallhack) return;
            var buildingblock = gameobject.GetComponentInParent<BuildingBlock>();
            if (buildingblock == null) return;
            if (buildingblock.blockDefinition.hierachyName == "wall" || buildingblock.blockDefinition.hierachyName == "door.hinged")
            {
                CreateNewProtectionFromBlock(buildingblock, false);
            }
        }
    }
}
