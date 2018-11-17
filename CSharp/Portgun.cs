#if REIGNOFKINGS
using CodeHatch;
using CodeHatch.Engine.Networking;
#endif
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
#if HURTWORLD || REIGNOFKINGS || RUST || RUSTLEGACY || THEFOREST
using UnityEngine;
#endif
#if HURTWORLD
using uLink;
#endif

// TODO: Protect players from damage (in all games) until second after teleporting

namespace Oxide.Plugins
{
    [Info("Portgun", "Wulf/lukespragg", "3.3.1")]
    [Description("Teleports players with permission to object or terrain they are looking at")]
    public class Portgun : CovalencePlugin
    {
        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandPort"] = "port",
                ["NoDestination"] = "Could not find a valid destination to port to",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["OutOfBounds"] = "The location you're aiming at is outside the map's boundaries",
                ["PlayersOnly"] = "Command '{0}' can only be used by a player"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private readonly HashSet<string> protection = new HashSet<string>();

        private const string permUse = "portgun.use";

        private float mapSize; 
        private int layers;

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);

            AddCovalenceCommand("p", "PortCommand");
            AddLocalizedCommand("CommandPort", "PortCommand");

#if HURTWORLD
            layers = LayerMaskManager.TerrainConstructionsMachines;
#elif REIGNOFKINGS
            layers = LayerMask.GetMask("Cubes", "Environment", "Terrain");
#elif RUST
            mapSize = TerrainMeta.Size.x / 2;
            layers = LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World");
#elif RUSTLEGACY
            //layers = LayerMask.GetMask();
#elif THEFOREST
            layers = 67108864;
#endif
        }

        #endregion Initialization

        #region Port Command

        private void PortCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                player.Reply(Lang("PlayersOnly", player.Id, command));
                return;
            }

            if (!player.HasPermission(permUse))
            {
                player.Reply(Lang("NotAllowed", player.Id, command));
                return;
            }

#if HURTWORLD || REIGNOFKINGS || RUST || RUSTLEGACY || THEFOREST
            RaycastHit hit = new RaycastHit();
#endif
#if HURTWORLDITEMV2
            EntityReferenceCache entity = (player.Object as PlayerSession).WorldPlayerEntity;
            CamData simData = entity.GetComponent<PlayerStatManager>().RefCache.PlayerCamera.SimData;
            CharacterController controller = entity.GetComponent<CharacterController>();
            Vector3 point1 = simData.FirePositionWorldSpace + controller.center + Vector3.up * -controller.height * 0.5f;
            Vector3 point2 = point1 + Vector3.up * controller.height;
            Vector3 direction = simData.FireRotationWorldSpace * Vector3.forward;
            if (!Physics.CapsuleCast(point1, point2, controller.radius, direction, out hit, float.MaxValue, layers))
#elif HURTWORLD
            GameObject entity = (player.Object as PlayerSession).WorldPlayerEntity;
            Transform transform = entity.GetComponentInChildren<Assets.Scripts.Core.CamPosition>().transform;
            Vector3 direction = transform.rotation * Vector3.forward;
            if (!Physics.Raycast(entity.transform.position + new Vector3(0f, 1.5f, 0f), direction, out hit, float.MaxValue, layers))
#elif REIGNOFKINGS
            CodeHatch.Engine.Core.Cache.Entity entity = (player.Object as Player).Entity;
            if (!Physics.Raycast(entity.Position, entity.GetOrCreate<LookBridge>().Forward, out hit, float.MaxValue, layers))
#elif RUST
            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer != null && !Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, float.MaxValue, layers))
#elif RUSTLEGACY
            Character character = (player.Object as NetUser).playerClient.controllable.idMain;
            if (!Physics.Raycast(character.eyesRay, out hit, float.MaxValue, 67108864))
#elif THEFOREST
            BoltEntity entity = player.Object as BoltEntity;
            Physics.Raycast(entity.transform.position, entity.transform.rotation * Vector3.forward, out hit, float.MaxValue, layers);
            if (hit.collider != null && !hit.collider.CompareTag("TerrainMain") && !hit.collider.CompareTag("structure"))
#endif
            {
                player.Reply(Lang("NoDestination", player.Id));
                return;
            }

#if HURTWORLDITEMV2
            Vector3 safePos = simData.FirePositionWorldSpace + direction * hit.distance;
            GenericPosition destination = new GenericPosition(safePos.x, safePos.y, safePos.z);
#elif HURTWORLD || REIGNOFKINGS || RUST || RUSTLEGACY || THEFOREST
            GenericPosition destination = new GenericPosition(hit.point.x, hit.point.y, hit.point.z);
#else
            GenericPosition destination = new GenericPosition();
#endif

            if (!IsValidMapCoordinates(destination.X, destination.Y, destination.Z))
            {
                player.Reply(Lang("OutOfBounds", player.Id));
                return;
            }

#if DEBUG
            player.Reply($"Current position: {player.Position()}");
            player.Reply($"Destination: {destination}");
#endif
            protection.Add(player.Id); // TODO: Remove to reset before adding if using timer?
            player.Teleport(destination.X, destination.Y, destination.Z);
        }

        #endregion Port Command

        #region Damage Protection

#if HURTWORLD
        private void OnPlayerTakeDamage(PlayerSession session, EntityEffectSourceData source)
        {
            string id = session.SteamId.ToString();
            if (protection.Contains(id))
            {
                source.Value = 0f;
                timer.Once(10f, () => protection.Remove(id)); // TODO: Detect ground instead of timer
            }
        }
#elif RUST
        private void OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            BasePlayer basePlayer = entity as BasePlayer;
            if (basePlayer != null && protection.Contains(basePlayer.UserIDString))
            {
                info.damageTypes = new Rust.DamageTypeList();
                info.HitMaterial = 0;
                info.PointStart = Vector3.zero;
                timer.Once(10f, () => protection.Remove(basePlayer.UserIDString)); // TODO: Detect ground instead of timer
            }
        }
#endif

        #endregion Damage Protection

        #region Helpers

        private bool IsValidMapCoordinates(float x, float y, float z)
        {
#if RUST
            return x <= mapSize && x >= -mapSize && y < 2000 && y >= -100 && z <= mapSize && z >= -mapSize;
#else
            return true; // Return true for unsupported games
#endif
        }

        private void AddLocalizedCommand(string key, string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages.Where(m => m.Key.Equals(key)))
                {
                    if (!string.IsNullOrEmpty(message.Value))
                    {
                        AddCovalenceCommand(message.Value, command);
                    }
                }
            }
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion Helpers
    }
}
