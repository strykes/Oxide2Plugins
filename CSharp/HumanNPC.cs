using System;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("HumanNPC", "Reneb", "0.1.16", ResourceId = 856)]
    class HumanNPC : RustPlugin
    {
        //////////////////////////////////////////////////////
        ///  Fields
        //////////////////////////////////////////////////////
        private static FieldInfo serverinput;
        private static FieldInfo viewangles;
        public static FieldInfo lastPositionValue;
        private static int playerLayer;
        private DamageTypeList emptyDamage;
        private List<Oxide.Plugins.Timer> TimersList;
        private static Vector3 Vector3Down;
        private static Vector3 Vector3Forward;
        private static Vector3 jumpPosition;
        private static int groundLayer;
        private static int blockshootLayer;
       

        StoredData storedData;
        Hash<string, HumanNPCInfo> humannpcs = new Hash<string, HumanNPCInfo>();

        [PluginReference]
        Plugin Kits;
        [PluginReference]
        Plugin Waypoints;
        [PluginReference]
        public static Plugin PathFinding;

        //////////////////////////////////////////////////////
        ///  Cached Fields
        //////////////////////////////////////////////////////
        private Quaternion currentRot;
        private Vector3 closestHitpoint;
        private static Vector3 eyesPosition;
        private static RaycastHit hitinfo;
        private object closestEnt;

        //////////////////////////////////////////////////////
        ///  class WaypointInfo
        ///  Waypoint information, position & speed
        ///  public => will be saved in the data file
        ///  non public => won't be saved in the data file
        //////////////////////////////////////////////////////
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
        ///  class SpawnInfo
        ///  Spawn information, position & rotation
        ///  public => will be saved in the data file
        ///  non public => won't be saved in the data file
        //////////////////////////////////////////////////////
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
                if (rotation.x == 0f)
                    rotation = new Quaternion(Convert.ToSingle(rx), Convert.ToSingle(ry), Convert.ToSingle(rz), Convert.ToSingle(rw));
                return rotation;
            }
            public string String()
            {
                return string.Format("Pos({0},{1},{2}) - Rot({3},{4},{5},{6})", x, y, z, rx, ry, rz, rw);
            }
            public string ShortString()
            {
                return string.Format("Pos({0},{1},{2})", Math.Ceiling(position.x).ToString(), Math.Ceiling(position.y).ToString(), Math.Ceiling(position.z).ToString());
            }
            public SpawnInfo Clone()
            {
                var clone = (SpawnInfo)MemberwiseClone();
                return clone;
            }
        }

        //////////////////////////////////////////////////////
        ///  class HumanTrigger
        /// MonoBehaviour: managed by UnityEngine
        ///  This takes care of all collisions and area management of humanNPCs
        //////////////////////////////////////////////////////
        class HumanTrigger : MonoBehaviour
        {
            HumanPlayer npc;

            Collider[] colliderArray;

            List<BasePlayer> collidePlayers = new List<BasePlayer>();
            List<BasePlayer> triggerPlayers = new List<BasePlayer>();
            List<BasePlayer> removePlayers = new List<BasePlayer>();

            BasePlayer cachedPlayer;
            public float collisionRadius;

            void Awake()
            {
                npc = GetComponent<HumanPlayer>();
                collisionRadius = float.Parse(npc.info.collisionRadius);
                InvokeRepeating("UpdateTriggerArea", 2, 2);

            }
            void UpdateTriggerArea()
            {
                colliderArray = Physics.OverlapSphere(npc.player.transform.position, collisionRadius, playerLayer);
                foreach (Collider collider in colliderArray)
                {
                    cachedPlayer = collider.GetComponentInParent<BasePlayer>();
                    if (cachedPlayer == null) continue;
                    if (cachedPlayer == npc.player) continue;
                    collidePlayers.Add(cachedPlayer);
                    if (!triggerPlayers.Contains(cachedPlayer)) OnEnterCollision(cachedPlayer);
                }

                foreach (BasePlayer player in triggerPlayers) { if (!collidePlayers.Contains(player)) removePlayers.Add(player); }
                foreach (BasePlayer player in removePlayers) { OnLeaveCollision(player); }

                collidePlayers.Clear();
                removePlayers.Clear();
            }
            void OnEnterCollision(BasePlayer targetplayer)
            {
                triggerPlayers.Add(targetplayer);
                Interface.CallHook("OnEnterNPC", npc.player, targetplayer);
            }
            void OnLeaveCollision(BasePlayer targetplayer)
            {
                triggerPlayers.Remove(targetplayer);
                Interface.CallHook("OnLeaveNPC", npc.player, targetplayer);
            }
        }

        //////////////////////////////////////////////////////
        ///  class HumanLocomotion
        /// MonoBehaviour: managed by UnityEngine
        ///  This takes care of all movements and attacks of HumanNPCs
        //////////////////////////////////////////////////////
        class HumanLocomotion : MonoBehaviour
        {
            public HumanPlayer npc;
            public Vector3 StartPos = new Vector3(0f, 0f, 0f);
            public Vector3 EndPos = new Vector3(0f, 0f, 0f);
            public Vector3 LastPos = new Vector3(0f, 0f, 0f);
            public Vector3 nextPos = new Vector3(0f, 0f, 0f);
            public float waypointDone = 0f;
            public float secondsTaken = 0f;
            public float secondsToTake = 0f;

            public List<WaypointInfo> cachedWaypoints;
            public int currentWaypoint = -1;

            public float c_attackDistance = 0f;
            public float attackDistance = 0f;
            public float maxDistance = 0f;
            public float damageDistance = 0f;
            public float damageInterval = 0f;
            public float damageAmount = 0f;
            public float lastHit = 0f;
            public float speed = 4f;

            public int noPath = 0;
            public bool shouldMove = true;

            public BaseEntity attackEntity = null;

            public List<Vector3> pathFinding;
            public List<Vector3> temppathFinding;

            void Awake()
            {
                npc = GetComponent<HumanPlayer>();
                var cwaypoints = Interface.CallHook("GetWaypointsList", npc.info.waypoint);
                if (cwaypoints == null)
                    cachedWaypoints = null;
                else
                {
                    cachedWaypoints = new List<WaypointInfo>();
                    foreach (var cwaypoint in (List<object>)cwaypoints)
                    {
                        foreach (KeyValuePair<Vector3, float> pair in (Dictionary<Vector3, float>)cwaypoint)
                        {
                            cachedWaypoints.Add(new WaypointInfo(pair.Key, pair.Value));
                        }
                    }
                }
                attackDistance = float.Parse(npc.info.attackDistance);
                maxDistance = float.Parse(npc.info.maxDistance);
                damageDistance = float.Parse(npc.info.damageDistance);
                damageInterval = float.Parse(npc.info.damageInterval);
                damageAmount = float.Parse(npc.info.damageAmount);
                speed = float.Parse(npc.info.speed);
            }
            void FixedUpdate()
            {
                TryToMove();
            }
            void TryToMove()
            {
                if (npc.player.IsWounded()) return;
                if (attackEntity != null) MoveOrAttack(attackEntity);
                else if (secondsTaken == 0f) GetNextPath();
                if (StartPos != EndPos) Execute_Move();
                if (waypointDone >= 1f) secondsTaken = 0f;
            }
            void Execute_Move()
            {
                if (!shouldMove) return;
                secondsTaken += Time.deltaTime;
                waypointDone = Mathf.InverseLerp(0f, secondsToTake, secondsTaken);
                nextPos = Vector3.Lerp(StartPos, EndPos, waypointDone);
                npc.player.transform.position = nextPos;
                
                npc.player.TransformChanged();
            }
            void GetNextPath()
            {
                if (npc == null) npc = GetComponent<HumanPlayer>();
                LastPos = Vector3.zero;
                shouldMove = true;
                if (cachedWaypoints == null) { shouldMove = false; return; }
                Interface.CallHook("OnNPCPosition", npc.player, npc.player.transform.position);
                if (currentWaypoint + 1 >= cachedWaypoints.Count)
                    currentWaypoint = -1;
                currentWaypoint++;
                SetMovementPoint(npc.player.transform.position, cachedWaypoints[currentWaypoint].GetPosition(), cachedWaypoints[currentWaypoint].GetSpeed());
                if (npc.player.transform.position == cachedWaypoints[currentWaypoint].GetPosition()) { npc.DisableMove(); npc.Invoke("AllowMove", cachedWaypoints[currentWaypoint].GetSpeed()); return; }
            }
            public void SetMovementPoint(Vector3 startpos, Vector3 endpos, float s)
            {
                StartPos = startpos;
                EndPos = endpos;
                if (StartPos != EndPos)
                    secondsToTake = Vector3.Distance(EndPos, StartPos) / s;
                LookTowards(npc.player, EndPos);
                secondsTaken = 0f;
                waypointDone = 0f;
            }
            void MoveOrAttack(BaseEntity entity)
            {

                c_attackDistance = Vector3.Distance(entity.transform.position, npc.player.transform.position);
                shouldMove = false;
                if (((BaseCombatEntity)entity).IsAlive() && c_attackDistance < attackDistance && Vector3.Distance(LastPos, npc.player.transform.position) < maxDistance && noPath < 5)
                {
                    if (waypointDone >= 1f)
                    {
                        if (pathFinding != null && pathFinding.Count > 0) pathFinding.RemoveAt(0);
                        waypointDone = 0f;
                    }
                    if (c_attackDistance < damageDistance && CanSee(npc.player, entity))
                    {
                        if (Time.realtimeSinceStartup > lastHit + damageInterval)
                            DoHit(this, (BaseCombatEntity)entity);
                        return;
                    }
                    if (pathFinding == null || pathFinding.Count < 1) return;
                    shouldMove = true;
                    if (waypointDone == 0f) SetMovementPoint(npc.player.transform.position, pathFinding[0], speed * 2);
                }
                else
                    npc.EndAttackingEntity();
            }
            public void PathFinding()
            {
                if (IsInvoking("PathFinding")) { CancelInvoke("PathFinding"); }

                temppathFinding = (List<Vector3>)Interface.CallHook("FindBestPath", npc.player.transform.position, attackEntity.transform.position);

                if (temppathFinding == null)
                {
                    if (pathFinding == null || pathFinding.Count == 0)
                        noPath++;
                    else noPath = 0;
                    Invoke("PathFinding", 2);
                }
                else
                {
                    noPath = 0;
                    pathFinding = temppathFinding;
                    waypointDone = 0f;
                    Invoke("PathFinding", pathFinding.Count / speed);
                }
            }

            public void GetBackToLastPos()
            {
                if (npc.player.transform.position != LastPos)
                {
                    SetMovementPoint(npc.player.transform.position, LastPos, 7f);
                    secondsTaken = 0.01f;
                }
            }
            public void Enable() { this.enabled = true; }
            public void Disable() { this.enabled = false; }
        }

        //////////////////////////////////////////////////////
        ///  class HumanPlayer : MonoBehaviour
        ///  MonoBehaviour: managed by UnityEngine
        /// Takes care of all the sub categories of the HumanNPCs
        //////////////////////////////////////////////////////
        class HumanPlayer : MonoBehaviour
        {
            public HumanNPCInfo info;
            public HumanLocomotion locomotion;
            public HumanTrigger trigger;

            public BasePlayer player;

            public bool hostile;
            public bool invulnerability;

            public bool stopandtalk;
            public float stopandtalkSeconds;

            public float lastMessage;

            public List<TuneNote> tunetoplay = new List<TuneNote>();
            public int currentnote = 0;
            Effect effectP = new Effect("fx/gestures/guitarpluck", new Vector3(0, 0, 0), Vector3.forward);
            Effect effectS = new Effect("fx/gestures/guitarstrum", new Vector3(0, 0, 0), Vector3.forward);

            void Awake()
            {
                player = GetComponent<BasePlayer>();
            }

            public void SetInfo(HumanNPCInfo info)
            {
                this.info = info;
                if (info == null) return;
                player.userID = ulong.Parse(info.userid);
                player.displayName = info.displayName;
                invulnerability = bool.Parse(info.invulnerability);
                stopandtalk = bool.Parse(info.stopandtalk);
                hostile = bool.Parse(info.hostile);
                stopandtalkSeconds = float.Parse(info.stopandtalkSeconds);
                player.InitializeHealth(float.Parse(info.health), float.Parse(info.health));
                player.syncPosition = true;
                player.transform.position = info.spawnInfo.GetPosition();
                lastPositionValue.SetValue(player, player.transform.position);
                player.TransformChanged();
                SetViewAngle(player, info.spawnInfo.GetRotation());
                player.EndSleeping();
                player.UpdateNetworkGroup();
                Interface.CallHook("OnNPCRespawn", player);
                if (info.minstrel != null) PlayTune();
                locomotion = player.gameObject.AddComponent<HumanLocomotion>();
                trigger = player.gameObject.AddComponent<HumanTrigger>();
                enabled = true;
                lastMessage = Time.realtimeSinceStartup;
            }
            public void AllowMove() { locomotion.Enable(); }
            public void DisableMove() { locomotion.Disable(); }
            public void TemporaryDisableMove(float thetime = -1f)
            {
                if (thetime == -1f) thetime = stopandtalkSeconds;
                DisableMove();
                if (IsInvoking("AllowMove")) CancelInvoke("AllowMove");
                Invoke("AllowMove", thetime);
            }
            public void EndAttackingEntity()
            {
                if (locomotion.IsInvoking("PathFinding")) locomotion.CancelInvoke("PathFinding");
                locomotion.noPath = 0;
                locomotion.shouldMove = true;
                Debug.Log("end");
                Interface.CallHook("OnNPCStopTarget", player, locomotion.attackEntity);
                locomotion.attackEntity = null;
                player.health = float.Parse(info.health);
                locomotion.GetBackToLastPos();
            }
            public void PlayTune()
            {
                if (info.minstrel == null) return;
                if (tunetoplay.Count == 0) GetTune(this);
                if (tunetoplay.Count == 0) return;
                Invoke("PlayNote", 1);
            }
            public void PlayNote()
            {
                if (tunetoplay[currentnote].Pluck)
                {
                    effectP.worldPos = player.transform.position;
                    effectP.origin = player.transform.position;
                    effectP.scale = tunetoplay[currentnote].NoteScale;
                    EffectNetwork.Send(effectP);
                }
                else
                {
                    effectS.worldPos = player.transform.position;
                    effectS.origin = player.transform.position;
                    effectS.scale = tunetoplay[currentnote].NoteScale;
                    EffectNetwork.Send(effectS);
                }
                currentnote++;
                if (currentnote >= tunetoplay.Count)
                    currentnote = 0;
                Invoke("PlayNote", tunetoplay[currentnote].Delay);
            }
            public void StartAttackingEntity(BaseEntity entity)
            {

                if (Interface.CallHook("OnNPCStartTarget", player, entity) == null)
                {

                    locomotion.attackEntity = entity;
                    locomotion.pathFinding = null;
                    locomotion.temppathFinding = null;

                    if (locomotion.LastPos == Vector3.zero) locomotion.LastPos = player.transform.position;
                    if (IsInvoking("AllowMove")) { CancelInvoke("AllowMove"); AllowMove(); }
                    locomotion.Invoke("PathFinding", 0);
                }
            }

            void OnDestroy()
            {
                GameObject.Destroy(locomotion);
                GameObject.Destroy(trigger);
            }
        }

        //////////////////////////////////////////////////////
        ///  class HumanNPCInfo
        ///  NPC information that will be saved inside the datafile
        ///  public => will be saved in the data file
        ///  non public => won't be saved in the data file
        //////////////////////////////////////////////////////
        class HumanNPCInfo
        {
            public string userid;
            public string displayName;
            public string invulnerability;
            public string health;
            public string respawn;
            public string respawnSeconds;
            public SpawnInfo spawnInfo;
            public string waypoint;
            public string collisionRadius;
            public string spawnkit;
            public string damageAmount;
            public string damageDistance;
            public string damageInterval;
            public string attackDistance;
            public string maxDistance;
            public string minstrel;
            public string hostile;
            public string speed;
            public string stopandtalk;
            public string stopandtalkSeconds;
            public string enable;
            public string lootable;
            public List<string> message_hello;
            public List<string> message_bye;
            public List<string> message_use;
            public List<string> message_hurt;
            public List<string> message_kill;

            public HumanNPCInfo(ulong userid, Vector3 position, Quaternion rotation)
            {
                this.userid = userid.ToString();
                displayName = "NPC";
                invulnerability = "true";
                health = "50";
                hostile = "false";
                respawn = "true";
                respawnSeconds = "60";
                spawnInfo = new SpawnInfo(position, rotation);
                collisionRadius = "10";
                damageDistance = "3";
                damageAmount = "10";
                attackDistance = "100";
                maxDistance = "200";
                speed = "3";
                stopandtalk = "true";
                stopandtalkSeconds = "3";
                enable = "true";
                lootable = "true";
                damageInterval = "2";
            }

            public HumanNPCInfo Clone(ulong userid)
            {
                var clone = new HumanNPCInfo(userid, this.spawnInfo.GetPosition(), this.spawnInfo.GetRotation());
                clone.userid = userid.ToString();
                clone.displayName = this.displayName;
                clone.invulnerability = this.invulnerability;
                clone.health = this.health;
                clone.respawn = this.respawn;
                clone.respawnSeconds = this.respawnSeconds;
                clone.waypoint = this.waypoint;
                clone.collisionRadius = this.collisionRadius;
                clone.spawnkit = this.spawnkit;
                clone.damageAmount = this.damageAmount;
                clone.damageDistance = this.damageDistance;
                clone.attackDistance = this.attackDistance;
                clone.maxDistance = this.maxDistance;
                clone.hostile = this.hostile;
                clone.speed = this.speed;
                clone.stopandtalk = this.stopandtalk;
                clone.stopandtalkSeconds = this.stopandtalkSeconds;
                clone.lootable = this.lootable;
                clone.damageInterval = this.damageInterval;
                clone.minstrel = this.minstrel;
                clone.message_hello = this.message_hello;
                clone.message_bye = this.message_bye;
                clone.message_use = this.message_use;
                clone.message_hurt = this.message_hurt;
                clone.message_kill = this.message_kill;
                return clone;
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
            public HashSet<HumanNPCInfo> HumanNPCs = new HashSet<HumanNPCInfo>();

            public StoredData()
            {
            }
        }

        private static Dictionary<string, object> weaponToFX = DefaultWeaponToFx();

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
            CheckCfg<Dictionary<string, object>>("Weapon To FX", ref weaponToFX);
            SaveConfig();
        }

        static Dictionary<string, object> DefaultWeaponToFx()
        {
            var defaultfx = new Dictionary<string, object>();
            defaultfx.Add("shotgun_waterpipe", "assets/blunded/prefabs/fx/weapons/vm_waterpipe_shotgun/attack.prefab");
            defaultfx.Add("shotgun_pump", "assets/blunded/prefabs/fx/weapons/vm_waterpipe_shotgun/attack.prefab");
            defaultfx.Add("smg_thompson", "assets/blunded/prefabs/fx/weapons/vm_thompson/attack.prefab");
            defaultfx.Add("rifle_ak", "assets/blunded/prefabs/fx/weapons/vm_ak47u/attack.prefab");
            defaultfx.Add("rifle_bolt", "assets/blunded/prefabs/fx/weapons/vm_bolt_rifle/attack.prefab");
            defaultfx.Add("pistol_revolver", "assets/blunded/prefabs/fx/weapons/vm_revolver/attack.prefab");
            defaultfx.Add("pistol_eoka", "assets/blunded/prefabs/fx/weapons/vm_eoka_pistol/attack.prefab");
            defaultfx.Add("unarmed", "assets/blunded/prefabs/fx/weapons/vm_unarmed/uppercut.prefab");
            defaultfx.Add("torch", "assets/blunded/prefabs/fx/weapons/vm_torch/attack.prefab");
            return defaultfx; 
        }

        static float GetGroundY(Vector3 position)
        {
            position = position + jumpPosition;
            if (Physics.Raycast(position, Vector3Down, out hitinfo, 1.5f, groundLayer))
            {
                return hitinfo.point.y;
            }
            return position.y - 1.5f;
        }

        void Loaded()
        {
            LoadData();
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            viewangles = typeof(BasePlayer).GetField("viewAngles", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            lastPositionValue = typeof(BasePlayer).GetField("lastPositionValue", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            TimersList = new List<Oxide.Plugins.Timer>();
            eyesPosition = new Vector3(0f, 0.5f, 0f);
            jumpPosition = new Vector3(0f, 1f, 0f);
            Vector3Down = new Vector3(0f, -1f, 0f);
            Vector3Forward = new Vector3(0f, 0f, 1f);
               
            foreach(BasePlayer player in BasePlayer.activePlayerList)
            {
                Network.SendInfo sendInfo = new Network.SendInfo(player.net.group) { method = Network.SendMethod.Unreliable };
                player.ClientRPCEx(sendInfo, null, "SignalFromServer", new object[] { (int)BaseEntity.Signal.Attack, string.Empty });
            }
        }

        void Unloaded()
        {
            SaveData();
        }

        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(HumanPlayer));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            objects = GameObject.FindObjectsOfType(typeof(NPCEditor));
            if (objects != null)
                foreach (var gameObj in objects)
                    GameObject.Destroy(gameObj);
            foreach (Oxide.Plugins.Timer timers in TimersList)
                timers.Destroy();
            TimersList.Clear();
        }

        void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject("HumanNPC", storedData);
        }

        void LoadData()
        {
            humannpcs.Clear();
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>("HumanNPC");
            }
            catch
            {
                storedData = new StoredData();
            }
            foreach (var thenpc in storedData.HumanNPCs)
                humannpcs[thenpc.userid] = thenpc;
        }

        public class TuneNote
        {
            public float NoteScale, Delay;
            public bool Pluck;
            public TuneNote()
            {
            }
        }

        static void GetTune(HumanPlayer hp)
        {
            var tune = Interface.CallHook("getTune", hp.info.minstrel);
            if (tune == null)
            {
                hp.CancelInvoke("PlayTune");
                return;
            }
            var newtune = new List<TuneNote>();
            foreach (var note in (List<object>)tune)
            {
                var newnote = new TuneNote();
                foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)note)
                {
                    if (pair.Key == "NoteScale") newnote.NoteScale = Convert.ToSingle(pair.Value);
                    if (pair.Key == "Delay") newnote.Delay = Convert.ToSingle(pair.Value);
                    if (pair.Key == "Pluck") newnote.Pluck = Convert.ToBoolean(pair.Value);
                }
                newtune.Add(newnote);
            }
            hp.tunetoplay = newtune;
        }

        //////////////////////////////////////////////////////
        ///  Oxide Hooks
        //////////////////////////////////////////////////////

        //////////////////////////////////////////////////////
        ///  OnServerInitialized()
        ///  called when the server is done being initialized
        //////////////////////////////////////////////////////
        void OnServerInitialized()
        {
            playerLayer = UnityEngine.LayerMask.GetMask(new string[] { "Player (Server)" });
            groundLayer = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Terrain", "World" });
            blockshootLayer = UnityEngine.LayerMask.GetMask(new string[] { "Construction", "Terrain", "World" });
            RefreshAllNPC();
            emptyDamage = new DamageTypeList();
        }

        //////////////////////////////////////////////////////
        ///  OnServerSave()
        ///  called when a server performs a save
        //////////////////////////////////////////////////////
        void OnServerSave()
        {
            SaveData();
        }

        //////////////////////////////////////////////////////
        /// OnPlayerInput(BasePlayer player, InputState input)
        /// Called when a plugin presses a button
        //////////////////////////////////////////////////////
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.USE))
            {
                TryGetPlayerView(player, out currentRot);
                if (Physics.Raycast(new Ray(player.transform.position + eyesPosition, currentRot * Vector3.forward), out hitinfo, 5f, playerLayer))
                    if (hitinfo.collider.GetComponentInParent<HumanPlayer>() != null)
                        Interface.CallHook("OnUseNPC", hitinfo.collider.GetComponentInParent<BasePlayer>(), player);
            }
        }

        //////////////////////////////////////////////////////
        /// OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        /// Called when an entity gets attacked (can be anything, building, animal, player ..)
        //////////////////////////////////////////////////////
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (entity.GetComponent<HumanPlayer>() != null)
            {

                Interface.CallHook("OnHitNPC", entity.GetComponent<BaseCombatEntity>(), hitinfo);
                if (entity.GetComponent<HumanPlayer>().invulnerability)
                {
                    hitinfo.damageTypes = emptyDamage;
                    hitinfo.HitMaterial = 0;
                }
            }
        }

        //////////////////////////////////////////////////////
        /// OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        /// Called when an entity gets killed (can be anything, building, animal, player ..)
        //////////////////////////////////////////////////////
        void OnEntityDeath(BaseEntity entity, HitInfo hitinfo)
        {
            if (entity.GetComponent<HumanPlayer>() != null)
            {
                Interface.CallHook("OnKillNPC", entity.GetComponent<BasePlayer>(), hitinfo);
                if (entity.GetComponent<HumanPlayer>().info.respawn == "true")
                {
                    var userid = entity.GetComponent<HumanPlayer>().info.userid;
                    TimersList.Add(timer.Once(float.Parse(entity.GetComponent<HumanPlayer>().info.respawnSeconds), () => SpawnOrRefresh(userid, false)));
                }
            }
        }

        void OnPlayerLoot(PlayerLoot loot, BaseEntity target)
        {
            var userid = GetIDFromLoot(target);
            if (userid != 0L && humannpcs[userid.ToString()] != null)
            {
                Interface.CallHook("OnLootNPC", loot, target, userid.ToString());
            }
        }

        //////////////////////////////////////////////////////
        /// End of Oxide Hooks
        //////////////////////////////////////////////////////

        ulong GetIDFromLoot(BaseEntity target)
        {
            if (target is PlayerCorpse) return (target as PlayerCorpse).playerSteamID;
            if (target is BasePlayer) return (target as BasePlayer).userID;
            return 0L;
        }

        HumanPlayer FindHumanPlayerByID(ulong userid)
        {
            var allBasePlayer = UnityEngine.Resources.FindObjectsOfTypeAll<HumanPlayer>();
            foreach (HumanPlayer humanplayer in allBasePlayer)
            {
                if (humanplayer.player.userID == userid) return humanplayer;
            }
            return null;
        }

        BasePlayer FindPlayerByID(ulong userid)
        {
            var allBasePlayer = UnityEngine.Resources.FindObjectsOfTypeAll<BasePlayer>();
            foreach (BasePlayer player in allBasePlayer)
            {
                if (player.userID == userid) return player;
            }
            return null;
        }
         
        static void DoHit(HumanLocomotion loc, BaseCombatEntity target)
        {
            loc.lastHit = Time.realtimeSinceStartup;
            HitInfo info = new HitInfo(loc.npc.player, DamageType.Stab, loc.damageAmount, target.transform.position)
            {
                PointStart = loc.npc.player.transform.position,
                PointEnd = target.transform.position
            };
            target.SendMessage("OnAttacked", info, SendMessageOptions.DontRequireReceiver);
            ForceSignalBroadcast(loc.npc.GetComponent<BasePlayer>());
            
            var activeitem = loc.npc.player.inventory.containerBelt.GetSlot(0);
            //PlayAttack(activeitem, loc.npc.player.transform.position, (target.transform.position - loc.npc.player.transform.position));
        }
        static void ForceSignalBroadcast(BasePlayer entity)
        {
            entity.SignalBroadcast(BaseEntity.Signal.Gesture, "pickup_item", null);
            //Network.SendInfo sendInfo = new Network.SendInfo(entity.net.group) { method = Network.SendMethod.Unreliable };
            //entity.ClientRPCEx(sendInfo, null, "SignalFromServer", new object[] { (int)BaseEntity.Signal.Gesture, "item_drop" });
        }
        /*
        void OnClientRPCEx(BaseEntity entity, Network.SendInfo sendinfo, Network.Connection source, string funcname, object[] args)
        {
            Debug.Log(string.Format("{0} : {1} - {2}", entity.ToString(), source == null ? "null" : source.ToString(), funcname));
            foreach(object arg in args)
            {
                Debug.Log(arg == string.Empty ? "EMPTY":arg.ToString());
            }
        }*/
        static void PlayAttack(Item attackitem, Vector3 source, Vector3 dir)
        {
            if (attackitem != null)
            {
                
                if (weaponToFX.ContainsKey(attackitem.info.shortname))
                {
                    Effect effect = new Effect();
                    effect.Init(Effect.Type.Projectile, source, dir.normalized, null);
                    effect.scale = dir.magnitude;
                    effect.pooledString = (string)weaponToFX[attackitem.info.shortname];
                    effect.number = 0;
                    EffectNetwork.Send(effect);
                }
            }
        }

        static void SetViewAngle(BasePlayer player, Quaternion ViewAngles)
        {
            viewangles.SetValue(player, ViewAngles);
            player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
        }

        void RefreshAllNPC()
        {
            List<string> npcspawned = new List<string>();
            foreach (KeyValuePair<string, HumanNPCInfo> pair in humannpcs)
            {
                if (pair.Value.enable == "true")
                {
                    npcspawned.Add(pair.Key);
                    SpawnOrRefresh(pair.Key, false);
                }
            }
            foreach (BasePlayer player in UnityEngine.Resources.FindObjectsOfTypeAll<BasePlayer>())
            {
                if (player.userID < 76560000000000000L && player.userID > 0L)
                    if (!npcspawned.Contains(player.userID.ToString())) { player.KillMessage(); Puts(string.Format("Detected a HumanNPC with no data or disabled, deleting him: {0} {1}", player.userID.ToString(), player.displayName)); }
            }
        }

        void SpawnOrRefresh( string userid, bool isediting )
        {
            BasePlayer findplayer = FindPlayerByID(Convert.ToUInt64(userid));

            if (findplayer == null) SpawnNPC(userid, false);
            else RefreshNPC(findplayer, false);
        }

        void SpawnNPC(string userid, bool isediting)
        {
            if (humannpcs[userid] == null) return;
            if (!isediting && humannpcs[userid].enable == "false") return;
            var newplayer = GameManager.server.CreateEntity("assets/bundled/prefabs/player/player.prefab", humannpcs[userid].spawnInfo.GetPosition(), humannpcs[userid].spawnInfo.GetRotation()).ToPlayer();
            newplayer.userID = Convert.ToUInt64(userid);
            var humanplayer = newplayer.gameObject.AddComponent<HumanPlayer>();
            newplayer.displayName = (humannpcs[userid]).displayName;
            newplayer.Spawn(true);
            humanplayer.SetInfo(humannpcs[userid]);
            Puts("Spawned NPC: " + userid);
        }

        void RefreshNPC(BasePlayer player, bool isediting)
        {
            if (player.GetComponent<HumanPlayer>() != null) GameObject.Destroy(player.GetComponent<HumanPlayer>());
            var humanplayer = player.gameObject.AddComponent<HumanPlayer>();
            humanplayer.SetInfo(humannpcs[player.userID.ToString()]);
            if (humannpcs[player.userID.ToString()].enable == "false" && !isediting) { player.KillMessage(); Puts("NPC was refreshed and Killed because he is disabled: " + player.userID.ToString()); return; }
            Puts("Refreshed NPC: " + player.userID.ToString());
        }

        bool hasAccess(BasePlayer player)
        {
            if (player.net.connection.authLevel < 1) { SendReply(player, "You don't have access to this command"); return false; }
            return true;
        }

        bool hasNoArguments(BasePlayer player, string[] args, int Number)
        {
            if (args.Length < Number) { SendReply(player, "Not enough Arguments"); return true; }
            return false;
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

        static bool CanSee(BasePlayer source, BaseEntity target)
        {
            if (UnityEngine.Physics.Linecast(source.transform.position + jumpPosition, target.transform.position + jumpPosition, blockshootLayer))
                return false;
            return true;
        }
        string GetRandomMessage(List<string> messagelist) { return messagelist[GetRandom(0, messagelist.Count)]; }
        int GetRandom(int min, int max) { return UnityEngine.Random.Range(min, max); }

        List<string> ListFromArgs(string[] args, int from)
        {
            var newlist = new List<string>();
            for (var i = from; i < args.Length; i++) { newlist.Add(args[i]); }
            return newlist;
        }

        public static void LookTowards(BasePlayer player, Vector3 pos)
        {
            if (pos != player.transform.position)
                SetViewAngle(player, Quaternion.LookRotation(pos - player.transform.position));
        }

        //////////////////////////////////////////////////////////////////////////////
        /// Chat Commands
        //////////////////////////////////////////////////////////////////////////////
        [ChatCommand("npc_add")]
        void cmdChatNPCAdd(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (player.GetComponent<NPCEditor>() != null) { SendReply(player, "NPC Editor: Already editing an NPC, say /npc_end first"); return; }
            if (!TryGetPlayerView(player, out currentRot)) return;

            var newplayer = GameManager.server.CreateEntity("assets/bundled/prefabs/player/player.prefab", player.transform.position, currentRot).ToPlayer();
            newplayer.displayName = "NPC";
            newplayer.Spawn(true);
            var humannpcinfo = new HumanNPCInfo(newplayer.userID, player.transform.position, currentRot);
            var humanplayer = newplayer.gameObject.AddComponent<HumanPlayer>();

            if (args.Length > 0)
            {
                if (humannpcs[args[0]] != null)
                {
                    humannpcinfo = humannpcs[args[0]].Clone(newplayer.userID);
                    humannpcinfo.userid = newplayer.userID.ToString();
                    humannpcinfo.spawnInfo = new SpawnInfo(player.transform.position, currentRot);
                    humanplayer.SetInfo(humannpcinfo);
                }
            }
            else
            {
                humanplayer.SetInfo(humannpcinfo);
            }

            var npceditor = player.gameObject.AddComponent<NPCEditor>();
            npceditor.targetNPC = humanplayer;

            if (humannpcs[newplayer.userID.ToString()] != null) storedData.HumanNPCs.Remove(humannpcs[newplayer.userID.ToString()]);

            humannpcs[newplayer.userID.ToString()] = humannpcinfo;
            storedData.HumanNPCs.Add(humannpcs[newplayer.userID.ToString()]);
            SaveData();
        }

        [ChatCommand("npc_edit")]
        void cmdChatNPCEdit(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (player.GetComponent<NPCEditor>() != null) { SendReply(player, "NPC Editor: Already editing an NPC, say /npc_end first"); return; }

            HumanPlayer targetnpc;
            ulong userid;
            if (args.Length == 0)
            {
                if (!TryGetPlayerView(player, out currentRot)) return;
                if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint)) return;
                if (((Collider)closestEnt).GetComponentInParent<HumanPlayer>() == null) { SendReply(player, "This is not an NPC"); return; }
                targetnpc = ((Collider)closestEnt).GetComponentInParent<HumanPlayer>();
            }
            else if (humannpcs[args[0]] != null)
            {
                if (!ulong.TryParse(args[0], out userid)) { SendReply(player, "/npc_edit TARGETID"); return; }
                targetnpc = FindHumanPlayerByID(userid);
                if (targetnpc == null) { SpawnNPC(args[0], true); }
                targetnpc = FindHumanPlayerByID(userid);
                if (targetnpc == null) { SendReply(player, "Couldn't Spawn the NPC"); return; }
            }
            else { SendReply(player, "You are not looking at an NPC or this userid doesn't exist"); return; }

            var npceditor = player.gameObject.AddComponent<NPCEditor>();
            npceditor.targetNPC = targetnpc;
            SendReply(player, string.Format("NPC Editor: Start Editing {0} - {1}", npceditor.targetNPC.player.displayName, npceditor.targetNPC.player.userID.ToString()));
        }

        [ChatCommand("npc_list")]
        void cmdChatNPCList(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (humannpcs.Count == 0) { SendReply(player, "No NPC created yet"); return; }

            SendReply(player, "==== NPCs ====");
            foreach (KeyValuePair<string, HumanNPCInfo> pair in humannpcs) SendReply(player, string.Format("{0} - {1} - {2} {3}", pair.Key, pair.Value.displayName, pair.Value.spawnInfo.ShortString(), pair.Value.enable == "true" ? "" : "- Disabled"));
        }

        [ChatCommand("npc")]
        void cmdChatNPC(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (player.GetComponent<NPCEditor>() == null) { SendReply(player, "NPC Editor: You need to be editing an NPC, say /npc_add or /npc_edit"); return; }
            var npceditor = player.GetComponent<NPCEditor>();
            if (args.Length == 0)
            {
                SendReply(player, "<color=#81F781>/npc attackdistance</color><color=#F2F5A9> XXX </color>=> <color=#D8D8D8>Distance between him and the target needed for the NPC to ignore the target and go back to spawn</color>");
                SendReply(player, "<color=#81F781>/npc bye</color> reset/<color=#F2F5A9>\"TEXT\" \"TEXT2\" \"TEXT3\" </color>=><color=#D8D8D8> Dont forgot the \", this is what NPC with say when a player gets away, multiple texts are possible</color>");
                SendReply(player, "<color=#81F781>/npc damageamount</color> <color=#F2F5A9>XXX </color>=> <color=#D8D8D8>Damage done by that NPC when he hits a player</color>");
                SendReply(player, "<color=#81F781>/npc damagedistance</color> <color=#F2F5A9>XXX </color>=> <color=#D8D8D8>Min distance for the NPC to hit a player (3 is default, maybe 20-30 needed for snipers?)</color>");
                SendReply(player, "<color=#81F781>/npc damageinterval</color> <color=#F2F5A9>XXX </color>=> <color=#D8D8D8>Time to wait before attacking again (2 seconds is default)</color>");
                SendReply(player, "<color=#81F781>/npc enable</color> <color=#F2F5A9>true</color>/<color=#F6CECE>false</color><color=#D8D8D8>Enable/Disable the NPC, maybe save it for later?</color>");
                SendReply(player, "<color=#81F781>/npc health</color> <color=#F2F5A9>XXX </color>=> <color=#D8D8D8>To set the Health of the NPC</color>");
                SendReply(player, "<color=#81F781>/npc hello</color> <color=#F6CECE>reset</color>/<color=#F2F5A9>\"TEXT\" \"TEXT2\" \"TEXT3\" </color>=> <color=#D8D8D8>Dont forgot the \", this what will be said when the player gets close to the NPC</color>");
                SendReply(player, "<color=#81F781>/npc hostile</color> <color=#F2F5A9>true</color>/<color=#F6CECE>false</color> <color=#F2F5A9>XX </color>=> <color=#D8D8D8>To set it if the NPC is Hostile</color>");
                SendReply(player, "<color=#81F781>/npc hurt</color> <color=#F6CECE>reset</color>/<color=#F2F5A9>\"TEXT\" \"TEXT2\" \"TEXT3\"</color> => <color=#D8D8D8>Dont forgot the \", set a message to tell the player when he hurts the NPC</color>");
                SendReply(player, "<color=#81F781>/npc invulnerable</color> <color=#F2F5A9>true</color>/<color=#F6CECE>false </color>=> <color=#D8D8D8>To set the NPC invulnerable or not</color>");
                SendReply(player, "<color=#81F781>/npc kill</color> <color=#F6CECE>reset</color>/<color=#F2F5A9>\"TEXT\" \"TEXT2\" \"TEXT3\" </color>=> <color=#D8D8D8>Dont forgot the \", set a message to tell the player when he kills the NPC</color>");
                SendReply(player, "<color=#81F781>/npc kit</color> <color=#F6CECE>reset</color>/<color=#F2F5A9>\"KitName\" </color>=> <color=#D8D8D8>To set the kit of this NPC, requires the Kit plugin</color>");
                SendReply(player, "<color=#81F781>/npc lootable</color> <color=#F2F5A9>true</color>/<color=#F6CECE>false</color> <color=#F2F5A9>XX </color>=> <color=#D8D8D8>To set it if the NPC corpse is lootable or not</color>");
                SendReply(player, "<color=#81F781>/npc maxdistance</color> <color=#F2F5A9>XXX </color>=><color=#D8D8D8> Max distance from the spawn point that the NPC can run from (while attacking a player)</color>");
                SendReply(player, "<color=#81F781>/npc minstrel</color> <color=#F6CECE>reset</color>/<color=#F2F5A9>\"TuneName\" </color>=> <color=#D8D8D8>To set tunes to play by the NPC.</color>");
                SendReply(player, "<color=#81F781>/npc name</color> <color=#F2F5A9>\"THE NAME\"</color> =><color=#D8D8D8> To set a name to the NPC</color>");
                SendReply(player, "<color=#81F781>/npc radius</color> <color=#F2F5A9>XXX</color> =><color=#D8D8D8> Radius of which the NPC will detect the player</color>");
                SendReply(player, "<color=#81F781>/npc respawn</color> <color=#F2F5A9>true</color>/<color=#F6CECE>false</color> <color=#F2F5A9>XX </color>=> <color=#D8D8D8>To set it to respawn on death after XX seconds, default is instant respawn</color>");
                SendReply(player, "<color=#81F781>/npc spawn</color> <color=#F2F5A9>\"new\" </color>=> <color=#D8D8D8>To set the new spawn location</color>");
                SendReply(player, "<color=#81F781>/npc speed</color><color=#F2F5A9> XXX </color>=> <color=#D8D8D8>To set the NPC running speed (while chasing a player)</color>");
                SendReply(player, "<color=#81F781>/npc stopandtalk</color> <color=#F2F5A9>true</color>/<color=#F6CECE>false</color> XX <color=#F2F5A9>XX </color>=> <color=#D8D8D8>To choose if the NPC should stop & look at the player that is talking to him</color>");
                SendReply(player, "<color=#81F781>/npc use</color> <color=#F6CECE>reset</color>/<color=#F2F5A9>\"TEXT\" \"TEXT2\" \"TEXT3\"</color> => <color=#D8D8D8>Dont forgot the \", this what will be said when the player presses USE on the NPC</color>");
                SendReply(player, "<color=#81F781>/npc waypoints</color> <color=#F6CECE>reset</color>/<color=#F2F5A9>\"Waypoint list Name\" </color>=> <color=#D8D8D8>To set waypoints of an NPC, /npc_help for more informations</color>");
                return;
            }
            switch (args[0].ToLower())
            {
                case "name":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC name is: {0}", npceditor.targetNPC.info.displayName)); return; }
                    npceditor.targetNPC.info.displayName = args[1];
                    break;
                case "enable":
                case "enabled":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC Respawn enabled: {0}", npceditor.targetNPC.info.enable)); return; }
                    if (args[1] == "true" || args[1] == "1")
                        npceditor.targetNPC.info.enable = "true";
                    else
                        npceditor.targetNPC.info.enable = "false";
                    break;
                case "invulnerable":
                case "invulnerability":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC invulnerability is set to: {0}", npceditor.targetNPC.info.invulnerability)); return; }
                    if (args[1] == "true" || args[1] == "1")
                        npceditor.targetNPC.info.invulnerability = "true";
                    else
                        npceditor.targetNPC.info.invulnerability = "false";
                    break;
                case "lootable":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC lootable is set to: {0}", npceditor.targetNPC.info.lootable)); return; }
                    if (args[1] == "true" || args[1] == "1")
                        npceditor.targetNPC.info.lootable = "true";
                    else
                        npceditor.targetNPC.info.lootable = "false";
                    break;
                case "hostile":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC hostility is set to: {0}", npceditor.targetNPC.info.hostile)); return; }
                    if (args[1] == "true" || args[1] == "1")
                        npceditor.targetNPC.info.hostile = "true";
                    else
                        npceditor.targetNPC.info.hostile = "false";
                    break;
                case "health":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC Initial health is set to: {0}", npceditor.targetNPC.info.health)); return; }
                    npceditor.targetNPC.info.health = args[1];
                    break;
                case "attackdistance":
                    if (args.Length == 1) { SendReply(player, string.Format("This Max Attack Distance is: {0}", npceditor.targetNPC.info.attackDistance)); return; }
                    npceditor.targetNPC.info.attackDistance = args[1];
                    break;
                case "damageamount":
                    if (args.Length == 1) { SendReply(player, string.Format("This Damage amount is: {0}", npceditor.targetNPC.info.damageAmount)); return; }
                    npceditor.targetNPC.info.damageAmount = args[1];
                    break;
                case "damageinterval":
                    if (args.Length == 1) { SendReply(player, string.Format("This Damage interval is: {0} seconds", npceditor.targetNPC.info.damageInterval)); return; }
                    npceditor.targetNPC.info.damageInterval = args[1];
                    break;
                case "maxdistance":
                    if (args.Length == 1) { SendReply(player, string.Format("The Max Distance from spawn is: {0}", npceditor.targetNPC.info.maxDistance)); return; }
                    npceditor.targetNPC.info.maxDistance = args[1];
                    break;
                case "damagedistance":
                    if (args.Length == 1) { SendReply(player, string.Format("This Damage distance is: {0}", npceditor.targetNPC.info.damageDistance)); return; }
                    npceditor.targetNPC.info.damageDistance = args[1];
                    break;
                case "radius":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC Collision radius is set to: {0}", npceditor.targetNPC.info.collisionRadius)); return; }
                    npceditor.targetNPC.info.collisionRadius = args[1];
                    break;
                case "respawn":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC Respawn is set to: {0} after {1} seconds", npceditor.targetNPC.info.respawn, npceditor.targetNPC.info.respawnSeconds)); return; }
                    if (args[1] == "true" || args[1] == "1")
                        npceditor.targetNPC.info.respawn = "true";
                    else
                        npceditor.targetNPC.info.respawn = "false";

                    npceditor.targetNPC.info.respawnSeconds = "60";
                    if (args.Length > 2)
                        npceditor.targetNPC.info.respawnSeconds = args[2];
                    break;
                case "spawn":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC Spawn is set to: {0}", npceditor.targetNPC.info.spawnInfo.String())); return; }
                    TryGetPlayerView(player, out currentRot);
                    var newSpawn = new SpawnInfo(player.transform.position, currentRot);
                    npceditor.targetNPC.info.spawnInfo = newSpawn;
                    SendReply(player, string.Format("This NPC Spawn now is set to: {0}", newSpawn.String()));
                    break;
                case "speed":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC Chasing speed is: {0}", npceditor.targetNPC.info.speed)); return; }
                    npceditor.targetNPC.info.speed = args[1];
                    break;
                case "stopandtalk":
                    if (args.Length == 1) { SendReply(player, string.Format("This NPC stop to talk is set to: {0} for {1} seconds", npceditor.targetNPC.info.stopandtalk, npceditor.targetNPC.info.stopandtalkSeconds)); return; }
                    if (args[1] == "true" || args[1] == "1")
                        npceditor.targetNPC.info.stopandtalk = "true";
                    else
                        npceditor.targetNPC.info.stopandtalk = "false";

                    npceditor.targetNPC.info.stopandtalkSeconds = "3";
                    if (args.Length > 2)
                        npceditor.targetNPC.info.stopandtalkSeconds = args[2];
                    break;
                case "waypoints":
                case "waypoint":
                    if (args.Length == 1)
                    {
                        if (npceditor.targetNPC.info.waypoint == null || npceditor.targetNPC.info.waypoint == "") SendReply(player, "No waypoints set for this NPC yet");
                        else SendReply(player, string.Format("This NPC waypoints are: {0}", npceditor.targetNPC.info.waypoint));
                        return;
                    }
                    if (args[1] == "reset") npceditor.targetNPC.info.waypoint = "";
                    else if (Interface.CallHook("GetWaypointsList", args[1]) == null) { SendReply(player, "This waypoint doesn't exist"); return; }
                    else npceditor.targetNPC.info.waypoint = args[1];
                    break;
                case "minstrel":
                    if (args.Length == 1)
                    {
                        if (npceditor.targetNPC.info.minstrel == null || npceditor.targetNPC.info.minstrel == "") SendReply(player, "No tune set for this NPC yet");
                        else SendReply(player, string.Format("This NPC Tune is: {0}", npceditor.targetNPC.info.minstrel));
                        return;
                    }
                    npceditor.targetNPC.info.minstrel = args[1];
                    break;
                case "kit":
                case "kits":
                    if (args.Length == 1)
                    {
                        if (npceditor.targetNPC.info.spawnkit == null || npceditor.targetNPC.info.spawnkit == "") SendReply(player, "No spawn kits set for this NPC yet");
                        else SendReply(player, string.Format("This NPC spawn kit is: {0}", npceditor.targetNPC.info.spawnkit));
                        return;
                    }
                    npceditor.targetNPC.info.spawnkit = args[1];
                    break;
                case "hello":
                    if (args.Length == 1)
                    {
                        if (npceditor.targetNPC.info.message_hello == null || (npceditor.targetNPC.info.message_hello.Count == 0)) SendReply(player, "No hello message set yet");
                        else SendReply(player, string.Format("This NPC will say hi: {0} different messages", npceditor.targetNPC.info.message_hello.Count.ToString()));
                        return;
                    }
                    if (args[1] == "reset") npceditor.targetNPC.info.message_hello = new List<string>();
                    else npceditor.targetNPC.info.message_hello = ListFromArgs(args, 1);
                    break;
                case "bye":
                    if (args.Length == 1)
                    {
                        if (npceditor.targetNPC.info.message_bye == null || npceditor.targetNPC.info.message_bye.Count == 0) SendReply(player, "No bye message set yet");
                        else SendReply(player, string.Format("This NPC will say bye: {0} difference messages ", npceditor.targetNPC.info.message_bye.Count.ToString()));
                        return;
                    }
                    if (args[1] == "reset") npceditor.targetNPC.info.message_bye = new List<string>();
                    else npceditor.targetNPC.info.message_bye = ListFromArgs(args, 1);
                    break;
                case "use":
                    if (args.Length == 1)
                    {
                        if (npceditor.targetNPC.info.message_use == null || npceditor.targetNPC.info.message_use.Count == 0) SendReply(player, "No bye message set yet");
                        else SendReply(player, string.Format("This NPC will say bye: {0} different messages", npceditor.targetNPC.info.message_use.Count.ToString()));
                        return;
                    }
                    if (args[1] == "reset") npceditor.targetNPC.info.message_use = new List<string>();
                    else npceditor.targetNPC.info.message_use = ListFromArgs(args, 1);
                    break;
                case "hurt":
                    if (args.Length == 1)
                    {
                        if (npceditor.targetNPC.info.message_hurt == null || npceditor.targetNPC.info.message_hurt.Count == 0) SendReply(player, "No hurt message set yet");
                        else SendReply(player, string.Format("This NPC will say ouch: {0} different messages", npceditor.targetNPC.info.message_hurt.Count.ToString()));
                        return;
                    }
                    if (args[1] == "reset") npceditor.targetNPC.info.message_hurt = new List<string>();
                    else npceditor.targetNPC.info.message_hurt = ListFromArgs(args, 1);
                    break;
                case "kill":
                    if (args.Length == 1)
                    {
                        if (npceditor.targetNPC.info.message_kill == null || npceditor.targetNPC.info.message_kill.Count == 0) SendReply(player, "No kill message set yet");
                        else SendReply(player, string.Format("This NPC will say a death message: {0} different messages", npceditor.targetNPC.info.message_kill.Count.ToString()));
                        return;
                    }
                    if (args[1] == "reset") npceditor.targetNPC.info.message_kill = new List<string>();
                    else npceditor.targetNPC.info.message_kill = ListFromArgs(args, 1);
                    break;
                default:
                    SendReply(player, "Wrong Argument, /npc for more informations");
                    return;
                    break;
            }

            if (args.Length > 1)
            {
                SendReply(player, string.Format("NPC Editor: Set {0} to {1}", args[0], args[1]));
                SaveData();
                RefreshNPC(npceditor.targetNPC.player, true);
            }
        }

        [ChatCommand("npc_end")]
        void cmdChatNPCEnd(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (player.GetComponent<NPCEditor>() == null) { SendReply(player, "NPC Editor: You are not editing any NPC"); return; }
            var npceditor = player.GetComponent<NPCEditor>();
            if (npceditor.targetNPC.info.enable == "false")
            {
                npceditor.targetNPC.player.KillMessage();
                SendReply(player, "NPC Editor: The NPC you edited is disabled, killing him");
            }
            GameObject.Destroy(player.GetComponent<NPCEditor>());
            SendReply(player, "NPC Editor: Ended");
        }

        [ChatCommand("npc_pathtest")]
        void cmdChatNPCPathTest(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (player.GetComponent<NPCEditor>() == null) { SendReply(player, "NPC Editor: You are not editing any NPC"); return; }
            if (!TryGetPlayerView(player, out currentRot)) return;
            if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint)) return;
            var npceditor = player.GetComponent<NPCEditor>();
            var curtime = Time.realtimeSinceStartup;
            //List<Vector3> vector3list = (List<Vector3>)Interface.CallHook("FindBestPath", npceditor.targetNPC.player.transform.position, closestHitpoint);
            Interface.CallHook("FindAndFollowPath", npceditor.targetNPC.player, npceditor.targetNPC.player.transform.position, closestHitpoint);
            Debug.Log((Time.realtimeSinceStartup - curtime).ToString());
        }

        [ChatCommand("npc_remove")]
        void cmdChatNPCRemove(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;

            HumanPlayer targetnpc;
            ulong userid;
            if (args.Length == 0)
            {
                if (!TryGetPlayerView(player, out currentRot)) return;
                if (!TryGetClosestRayPoint(player.transform.position, currentRot, out closestEnt, out closestHitpoint)) return;
                if (((Collider)closestEnt).GetComponentInParent<HumanPlayer>() == null) { SendReply(player, "This is not an NPC"); return; }

                targetnpc = ((Collider)closestEnt).GetComponentInParent<HumanPlayer>();
            }
            else if (humannpcs[args[0]] != null)
            {
                if (!ulong.TryParse(args[0], out userid)) { SendReply(player, "/npc_remove TARGETID"); return; }
                targetnpc = FindHumanPlayerByID(userid);
                if (targetnpc == null) { SendReply(player, "This NPC doesn't exist"); return; }
            }
            else { SendReply(player, "You are not looking at an NPC or this userid doesn't exist"); return; }

            var targetid = targetnpc.player.userID.ToString();
            storedData.HumanNPCs.Remove(humannpcs[targetid]);
            humannpcs[targetid] = null;
            RefreshAllNPC();
            SendReply(player, string.Format("NPC {0} Removed", targetid));
        }

        [ChatCommand("npc_reset")]
        void cmdChatNPCReset(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) return;
            if (player.GetComponent<NPCEditor>() != null) GameObject.Destroy(player.GetComponent<NPCEditor>());
            humannpcs.Clear();
            storedData.HumanNPCs.Clear();
            SaveData();
            SendReply(player, "All NPCs were removed");
            OnServerInitialized();
        }

        void SendMessage(HumanPlayer npc, BasePlayer target, string message)
        {
            if (Time.realtimeSinceStartup > npc.lastMessage + 0.1f)
            {
                target.SendConsoleCommand("chat.add", new object[] { "0", string.Format("<color=#FA58AC>{0}:</color> {1}", npc.player.displayName, message), 1.0 });
                npc.lastMessage = Time.realtimeSinceStartup;
            }
        }

        //////////////////////////////////////////////////////
        // NPC HOOKS:
        // will call ALL plugins
        //////////////////////////////////////////////////////

        //////////////////////////////////////////////////////
        /// OnHitNPC(BasePlayer npc, HitInfo hinfo)
        /// called when an NPC gets hit
        //////////////////////////////////////////////////////
        void OnHitNPC(BasePlayer npc, HitInfo hinfo)
        {

            npc.GetComponent<HumanPlayer>().StartAttackingEntity(hinfo.Initiator);
            if (npc.GetComponent<HumanPlayer>().info.message_hurt != null && npc.GetComponent<HumanPlayer>().info.message_hurt.Count != 0)
                if (hinfo.Initiator != null)
                    if (hinfo.Initiator.ToPlayer() != null)
                        SendMessage(npc.GetComponent<HumanPlayer>(), hinfo.Initiator.ToPlayer(), GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_hurt));
        }

        //////////////////////////////////////////////////////
        ///  OnUseNPC(BasePlayer npc, BasePlayer player)
        ///  called when a player press USE while looking at the NPC (5m max)
        //////////////////////////////////////////////////////
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            var usenpc = npc.GetComponent<HumanPlayer>();
            if (usenpc.stopandtalk) { LookTowards(npc, player.transform.position); usenpc.TemporaryDisableMove(); }
            if (usenpc.info.message_use != null && usenpc.info.message_use.Count != 0)
                SendMessage(npc.GetComponent<HumanPlayer>(), player, GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_use));
        }

        //////////////////////////////////////////////////////
        ///  OnEnterNPC(BasePlayer npc, BasePlayer player)
        ///  called when a player gets close to an NPC (default is in 10m radius)
        //////////////////////////////////////////////////////
        void OnEnterNPC(BasePlayer npc, BasePlayer player)
        {
            if (npc.GetComponent<HumanPlayer>().hostile)
                if (npc.GetComponent<HumanPlayer>().locomotion.attackEntity == null)
                    if (player.net.connection != null && player.net.connection.authLevel < 1)
                        npc.GetComponent<HumanPlayer>().StartAttackingEntity(player);
            if (npc.GetComponent<HumanPlayer>().info.message_hello != null && npc.GetComponent<HumanPlayer>().info.message_hello.Count != 0)
                SendMessage(npc.GetComponent<HumanPlayer>(), player, GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_hello));
        }

        //////////////////////////////////////////////////////
        ///  OnLeaveNPC(BasePlayer npc, BasePlayer player)
        ///  called when a player gets away from an NPC
        //////////////////////////////////////////////////////
        void OnLeaveNPC(BasePlayer npc, BasePlayer player)
        {
            if (npc.GetComponent<HumanPlayer>().info.message_bye != null && npc.GetComponent<HumanPlayer>().info.message_bye.Count != 0)
                SendMessage(npc.GetComponent<HumanPlayer>(), player, GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_bye));
        }

        //////////////////////////////////////////////////////
        ///  OnKillNPC(BasePlayer npc, HitInfo hinfo)
        ///  called when an NPC gets killed
        //////////////////////////////////////////////////////
        void OnKillNPC(BasePlayer npc, HitInfo hinfo)
        {
            if (npc.GetComponent<HumanPlayer>().info.message_kill != null && npc.GetComponent<HumanPlayer>().info.message_kill.Count != 0)
                if (hinfo.Initiator != null)
                    if (hinfo.Initiator.ToPlayer() != null)
                        SendMessage(npc.GetComponent<HumanPlayer>(), hinfo.Initiator.ToPlayer(), GetRandomMessage(npc.GetComponent<HumanPlayer>().info.message_kill));
        }

        //////////////////////////////////////////////////////
        ///  OnNPCPosition(BasePlayer npc, Vector3 pos)
        ///  Called when an npc reachs a position
        //////////////////////////////////////////////////////
        void OnNPCPosition(BasePlayer npc, Vector3 pos)
        {
            return;
        }

        //////////////////////////////////////////////////////
        ///  OnNPCRespawn(BasePlayer npc)
        ///  Called when an NPC respawns
        ///  here it will give an NPC a kit and set the first tool in the belt as the active weapon
        //////////////////////////////////////////////////////
        void OnNPCRespawn(BasePlayer npc)
        {
            if (npc.GetComponent<HumanPlayer>().info.spawnkit != null && npc.GetComponent<HumanPlayer>().info.spawnkit != "")
            {
                npc.inventory.Strip();
                Kits.Call("GiveKit", npc, npc.GetComponent<HumanPlayer>().info.spawnkit);
                if (npc.inventory.containerBelt.GetSlot(0) != null)
                {
                    HeldEntity entity2 = npc.inventory.containerBelt.GetSlot(0).GetHeldEntity() as HeldEntity;
                    entity2.SetHeld(true);
                }
                npc.SV_ClothingChanged();
                npc.inventory.ServerUpdate(0f);
            }
        }

        //////////////////////////////////////////////////////
        ///  OnNPCStartAttacking(BasePlayer npc, BaseEntity target)
        ///  Called when an NPC start to target someone to attack
        ///  return anything will block the attack
        //////////////////////////////////////////////////////
        object OnNPCStartTarget(BasePlayer npc, BaseEntity target)
        {
            return null;
        }
        //////////////////////////////////////////////////////
        ///  OnNPCStopTarget(BasePlayer npc, BaseEntity target)
        ///  Called when an NPC stops targetting
        ///  no return;
        //////////////////////////////////////////////////////
        void OnNPCStopTarget(BasePlayer npc, BaseEntity target)
        {
            return;
        }

        //////////////////////////////////////////////////////
        ///  OnLootNPC(PlayerLoot loot, BaseEntity target, string npcuserID)
        ///  Called when an NPC gets looted
        ///  no return;
        //////////////////////////////////////////////////////
        void OnLootNPC(PlayerLoot loot, BaseEntity target, string npcuserID)
        {
            if (humannpcs[npcuserID].lootable == "false")
                timer.Once(0.01f, () => loot.Clear());
        }
    }
}
