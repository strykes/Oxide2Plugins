using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Oxide.Core;
using Newtonsoft.Json;
using System.ComponentModel;

namespace Oxide.Plugins
{
    [Info("Parachute", "Reneb", "1.0.0")]
    class Parachute : RustPlugin
    {
        static int parachuteLayer = 1 << (int)Rust.Layer.Water | 1 << (int)Rust.Layer.World | 1 << (int)Rust.Layer.Construction | 1 << (int) Rust.Layer.Debris | 1 << (int) Rust.Layer.Default | 1 << (int)Rust.Layer.Terrain | 1 << (int)Rust.Layer.Tree | 1 << (int)Rust.Layer.Vehicle_Large | 1 << (int)Rust.Layer.Vehicle_Movement | 1 << (int)Rust.Layer.Deployed;

        Dictionary<ulong, float> cooldownTimers = new Dictionary<ulong, float>();

        void OnServerInitialized()
        {
            permission.RegisterPermission("parachute.allowed", this);
            LoadVariables();
        }

        void Unload()
        {
            var objects = GameObject.FindObjectsOfType(typeof(ParaMovement));

            if (objects != null)
            {
                foreach (var gameObj in objects)
                {
                    GameObject.Destroy(gameObj);
                }
            }
        }

        private ConfigData config;

        public class ConfigData
        {
            [JsonProperty(PropertyName = "Cooldown Options")]
            public CooldownOptions Cooldown { get; set; }

            [JsonProperty(PropertyName = "Parachute Options (for advanced users only)")]
            public ParachuteOptions Parachute { get; set; }

            public class CooldownOptions
            {
                [JsonProperty(PropertyName = "Use Cooldown (true/false)")]
                [DefaultValue(true)]
                public bool useCooldown { get; set; } = true;

                [JsonProperty(PropertyName = "Timer (number)")]
                [DefaultValue(10f)]
                public float timerCooldown { get; set; } = 10f;
            }

            public class ParachuteOptions
            {
                [JsonProperty(PropertyName = "Up force, to counter the gravity fall")]
                [DefaultValue(7f)]
                public float upForce { get; set; } = 7f;

                [JsonProperty(PropertyName = "Max drop speed, before the plugin starts to give an extra break from gravity fall")]
                [DefaultValue(-10f)]
                public float maxDropSpeed { get; set; } = -10f;

                [JsonProperty(PropertyName = "Forward acceleration strength")]
                [DefaultValue(6f)]
                public float forwardStrength { get; set; } = 6f;

                [JsonProperty(PropertyName = "Backward acceleration strength")]
                [DefaultValue(4f)]
                public float backwardStrength { get; set; } = 4f;

                [JsonProperty(PropertyName = "Rotation acceleration strength")]
                [DefaultValue(0.4f)]
                public float rotationStrength { get; set; } = 0.4f;

                [JsonProperty(PropertyName = "Forward resistance (will slow down constantly the parachute)")]
                [DefaultValue(0.3f)]
                public float forwardResistance { get; set; } = 0.3f;

                [JsonProperty(PropertyName = "Rotation resistance (will reduce the rotation if the player stops pressing rotation)")]
                [DefaultValue(0.5f)]
                public float rotationResistance { get; set; } = 0.5f;

                [JsonProperty(PropertyName = "Auto release parachute height")]
                [DefaultValue(1.5f)]
                public float autoremoveParachuteHeight { get; set; } = 1.5f;

                [JsonProperty(PropertyName = "Auto release parachute proximity")]
                [DefaultValue(0.5f)]
                public float autoremoveParachuteProximity { get; set; } = 0.5f;

                [JsonProperty(PropertyName = "Angular modifier (how much your are on the side depending on your rotation speed)")]
                [DefaultValue(50f)]
                public float angularModifier { get; set; } = 50f;
            }
        }

        private void LoadVariables()
        {
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;

            config = Config.ReadObject<ConfigData>();

            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultConfig()
        {
            var configData = new ConfigData
            {
                Cooldown = new ConfigData.CooldownOptions(),
                Parachute = new ConfigData.ParachuteOptions()
            };

            Config.WriteObject(configData, true);
        }
         
        public class ParaMovement : MonoBehaviour
        {
            Rigidbody myRigidbody;

            BaseEntity worldItem;
            BaseEntity chair;
            BaseEntity parachute;
            public TriggerParent triggerParent;
            BasePlayer player;

            ConfigData.ParachuteOptions config;

            public ParaMovement()
            {
                worldItem = GetComponent<BaseEntity>();

                parachute = GameManager.server.CreateEntity("assets/prefabs/misc/parachute/parachute.prefab", new Vector3(), new Quaternion(), true);
                parachute.enableSaving = false;
                parachute.transform.localPosition = new Vector3(0f, -7f, 0f);
                parachute?.Spawn();

                string chairprefab = "assets/prefabs/deployable/chair/chair.deployed.prefab";
                chair = GameManager.server.CreateEntity(chairprefab, new Vector3(), new Quaternion(), true);
                chair.skinID = 1169930802;
                chair.enableSaving = false;
                chair.Spawn();
                chair.GetComponent<MeshCollider>().convex = true;
                chair.SetParent(parachute, 0, false, false);
                chair.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                chair.UpdateNetworkGroup();

                parachute.SetParent(worldItem, 0, false, false);
                
                myRigidbody = worldItem.GetComponent<Rigidbody>();
                myRigidbody.isKinematic = false;
                enabled = false;
            }
            public void SetConfig(ConfigData.ParachuteOptions config)
            {
                this.config = config;
            }

            public void SetPlayer(BasePlayer player)
            { 
                this.player = player;
                chair.GetComponent<BaseMountable>().MountPlayer(player);
                enabled = true;
            }

            public void OnDestroy()
            {
                Release();
            }

            public void Release()
            {
                enabled = false;

                if(chair != null && chair.GetComponent<BaseMountable>().IsMounted())
                    chair.GetComponent<BaseMountable>().DismountPlayer(player, false);
                if (player != null && player.isMounted)
                    player.DismountObject();

                if(!chair.IsDestroyed) chair.Kill();
                if(!parachute.IsDestroyed) parachute.Kill();
                if(!worldItem.IsDestroyed) worldItem.Kill();

                UnityEngine.GameObject.Destroy(this.gameObject);
            }

            public void FixedUpdate()
            {
                if(chair == null) { OnDestroy(); return; }
                if(Physics.Raycast(new Ray(chair.transform.position, Vector3.down), config.autoremoveParachuteHeight, parachuteLayer))
                {
                    OnDestroy();
                    return; 
                }
                
                foreach (Collider col in Physics.OverlapSphere(chair.transform.position, config.autoremoveParachuteProximity, parachuteLayer))
                {
                    BaseEntity baseEntity = col.gameObject.ToBaseEntity();
                   
                    if (baseEntity != null && (baseEntity == chair || baseEntity == player.GetComponent<BaseEntity>()))
                    {
                        continue; 
                    } 
                    else
                    { 
                        OnDestroy(); 
                        return;
                    }
                } 
                
                
                if (TerrainMeta.HeightMap.GetHeight(chair.transform.position) >= chair.transform.position.y)
                {
                    Vector3 newPos = chair.transform.position; newPos.y = TerrainMeta.HeightMap.GetHeight(chair.transform.position);
                    OnDestroy();
                    player.Teleport(newPos);
                    return;
                }
                if(player.serverInput.IsDown(BUTTON.JUMP))
                {
                    OnDestroy();
                    return;
                }

                if(myRigidbody.velocity.y < config.maxDropSpeed)
                {
                    myRigidbody.AddForce(Vector3.up * (config.maxDropSpeed - myRigidbody.velocity.y), ForceMode.Impulse);
                }
                myRigidbody.AddForce(Vector3.up * config.upForce, ForceMode.Acceleration);
                

                if (myRigidbody.velocity.x < 0f || myRigidbody.velocity.x > 0f || myRigidbody.velocity.z < 0f || myRigidbody.velocity.z > 0f)
                {
                    myRigidbody.AddForce(new Vector3(-myRigidbody.velocity.x, 0f, -myRigidbody.velocity.z) * config.forwardResistance, ForceMode.Acceleration);
                }
                if (myRigidbody.angularVelocity.y > 0f || myRigidbody.angularVelocity.y > 0f)
                {
                    myRigidbody.AddTorque(new Vector3(0f, -myRigidbody.angularVelocity.y, 0f) * config.rotationResistance, ForceMode.Acceleration);
                }
               
                if (player.serverInput.IsDown(BUTTON.FORWARD))
                {
                    myRigidbody.AddForce(myRigidbody.transform.forward * config.forwardStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.BACKWARD))
                {
                    myRigidbody.AddForce(-myRigidbody.transform.forward * config.backwardStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.RIGHT))
                {
                    myRigidbody.AddTorque(Vector3.up * config.rotationStrength, ForceMode.Acceleration);
                }
                if (player.serverInput.IsDown(BUTTON.LEFT))
                {
                    myRigidbody.AddTorque(Vector3.up * -config.rotationStrength, ForceMode.Acceleration);
                }
                if(myRigidbody.angularVelocity.y > 0f || myRigidbody.angularVelocity.y < 0f)
                {
                    worldItem.transform.rotation = Quaternion.Euler(worldItem.transform.rotation.eulerAngles.x, worldItem.transform.rotation.eulerAngles.y, -myRigidbody.angularVelocity.y * config.angularModifier);
                }
            } 
        }

        private bool ExternalAddPlayerChute(BasePlayer player)
        {
            return DeployParachuteOnPlayer(player);
        }

        private bool TryDeployParachuteOnPlayer(BasePlayer player)
        {
            if(player.isMounted)
            {
                player.ChatMessage("You can't deploy when mounted.");
                return false;
            }
            if(player.IsOnGround())
            {
                player.ChatMessage("You can't deploy a parachute on the ground.");
                return false;
            }
            if (config.Cooldown.useCooldown)
            {
                if (!cooldownTimers.ContainsKey(player.userID)) cooldownTimers.Add(player.userID, 0f);
                if (Time.realtimeSinceStartup < cooldownTimers[player.userID] + config.Cooldown.timerCooldown)
                {
                    player.ChatMessage(string.Format("Your parachute is on cooldown: {0} seconds left.", Mathf.Ceil(cooldownTimers[player.userID] + config.Cooldown.timerCooldown - Time.realtimeSinceStartup).ToString()));
                    return false;
                }
                cooldownTimers[player.userID] = Time.realtimeSinceStartup;
            }
            return DeployParachuteOnPositionAndRotation(player, player.transform.position + new Vector3(0f, 7f, 0f), new Vector3(0f, player.GetNetworkRotation().eulerAngles.y, 0f));
        }
        private bool DeployParachuteOnPlayer(BasePlayer player)
        {
            return DeployParachuteOnPositionAndRotation(player, player.transform.position + new Vector3(0f,7f,0f), new Vector3(0f, player.GetNetworkRotation().eulerAngles.y, 0f));
        }

        private bool DeployParachuteOnPosition(BasePlayer player, Vector3 position)
        {
            return DeployParachuteOnPositionAndRotation(player, position, new Vector3(0f, 0f, 0f));
        }

        private bool DeployParachuteOnPositionAndRotation(BasePlayer player, Vector3 position, Vector3 rotation)
        {
            try
            {
                BaseEntity worldItem = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", position, Quaternion.Euler(rotation), true);
                worldItem.enableSaving = false;
                worldItem.Spawn();

                var sedanRigid = worldItem.gameObject.AddComponent<ParaMovement>();
                sedanRigid.SetConfig(config.Parachute);
                sedanRigid.SetPlayer(player);
            }
            catch 
            {
                return false;
            }
            return true;
        }

        [ChatCommand("parachute")]
        void chatParachute(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "parachute.allowed"))
            {
                TryDeployParachuteOnPlayer(player);
            }
        }
        [ConsoleCommand("parachute")]
        private void cmdParachute(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            string SteamID = player.userID.ToString();
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, "parachute.allowed"))
            {
                TryDeployParachuteOnPlayer(player);
            }
        }
    }
}
 
