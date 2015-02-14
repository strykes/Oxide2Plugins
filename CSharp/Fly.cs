// Reference: Newtonsoft.Json
// Reference: Oxide.Ext.Rust

using System.Collections.Generic;
using System.Reflection;
using System;
using System.Data;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Fly", "Reneb", 1.4)]
    class Fly : RustPlugin
    {

        private Dictionary<BasePlayer, float> fly;
        private Dictionary<BasePlayer, Vector3> oldPos;
        private FieldInfo serverinput;
        void Loaded()
        {
            serverinput = typeof(BasePlayer).GetField("serverInput", (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic));
            fly = new Dictionary<BasePlayer, float>();
            oldPos = new Dictionary<BasePlayer, Vector3>();
        }
        void ForcePlayerPosition(BasePlayer player, Vector3 destination)
        {
            player.transform.position = destination;
            player.ClientRPC(null, player, "ForcePositionTo", new object[] { destination });
            player.TransformChanged();
        }
        void OnTick()
        {
            if (fly.Count > 0)
            {
                var enumerator = fly.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var player = enumerator.Current.Key as BasePlayer;
                    if (player.net == null)
                        DeactivateFly(player);
                    else
                    {
                        var input = serverinput.GetValue(player) as InputState;
                        var newPos = oldPos[player];
                        var currentRot = Quaternion.Euler(input.current.aimAngles);
                        var speedMult = 0.5f;
                        if (input.IsDown(BUTTON.SPRINT))
                            speedMult = enumerator.Current.Value;
                        if (!(player.IsSpectating()))
                            player.ChangePlayerState(PlayerState.Type.Spectating, false);
                        if (player.parentEntity.Get(true) != null)
                        {
                            newPos = player.parentEntity.Get(true).transform.position;
                            player.parentEntity.Set(null);
                            player.parentBone = 0;
                            player.UpdateNetworkGroup();
                            player.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                        }
                        else if (input.IsDown(BUTTON.FORWARD))
                        {
                            newPos = newPos + (currentRot * Vector3.forward * speedMult);
                        }
                        else if (input.IsDown(BUTTON.RIGHT))
                        {
                            newPos = newPos + (currentRot * Vector3.right * speedMult);
                        }
                        else if (input.IsDown(BUTTON.LEFT))
                        {
                            newPos = newPos + (currentRot * Vector3.left * speedMult);
                        }
                        else if (input.IsDown(BUTTON.BACKWARD))
                        {
                            newPos = newPos + (currentRot * Vector3.back * speedMult);
                        }
                        else
                            newPos = player.transform.position;
                        if (newPos != oldPos[player])
                        {
                            ForcePlayerPosition(player, newPos);
                            oldPos[player] = newPos;
                        }
                    }
                }
            }
        }
        void DeactivateFly(BasePlayer player)
        {
            fly.Remove(player);
            oldPos.Remove(player);
            if (player.net != null)
            {
                player.ChangePlayerState(PlayerState.Type.Normal, false);
                SendReply(player, "FlyMode: Deactivated");
            }
        }
        void ActivateFly(BasePlayer player, float speed)
        {
            fly.Add(player, speed);
            oldPos.Add(player, player.transform.position);
            player.ChangePlayerState(PlayerState.Type.Spectating, false);
            SendReply(player, "FlyMode: Activated, press any key to start flying");
        }
        void ModifyFly(BasePlayer player, float speed)
        {
            fly[player] = speed;
            SendReply(player, "FlyMode: Speed changed");
        }
        void ToggleFly(BasePlayer player, float speed)
        {
            if (fly.ContainsKey(player))
            {
                if (speed == 1f)
                    DeactivateFly(player);
                else
                    ModifyFly(player, speed);
            }
            else
                ActivateFly(player, speed);
        }
        [ChatCommand("fly")]
        void cmdChatFly(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            var speed = 1f;
            if (args.Length > 0)
                speed = Convert.ToSingle(args[0]);
            ToggleFly(player, speed);
        }
        [ChatCommand("land")]
        void cmdChatLand(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < 1)
            {
                SendReply(player, "You are not allowed to use this command");
                return;
            }
            if (!fly.ContainsKey(player))
            {
                SendReply(player, "You can't land if you aren't flying");
                return;
            }
            LandPlayer(player);
        }
        void LandPlayer(BasePlayer player)
        {
            var hits = UnityEngine.Physics.RaycastAll(player.transform.position, Vector3.down);
            var dist = 5000000f;
            var newpos = player.transform.position;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<TriggerBase>() == null)
                {
                    if (hit.distance < dist)
                    {
                        newpos = hit.point;
                        dist = hit.distance;
                    }
                }
            }
            if (newpos == player.transform.position)
            {
                SendReply(player, "Couldn't find somewhere to land");
            }
            else
            {
                ForcePlayerPosition(player, newpos);
                oldPos[player] = newpos;
                SendReply(player, "You have landed, you may safely use /fly to stop flying.");
            }
        }
    }
}
