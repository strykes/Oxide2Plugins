using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using RustProto;

namespace Oxide.Plugins
{
    [Info("GlobalBroadcast", "Reneb", "1.0.0")]
    class GlobalBroadcast : RustLegacyPlugin
    {
        /*
        public static FieldInfo playerList;
        void Loaded()
        {
            playerList = typeof(VoiceCom).GetField("playerList", BindingFlags.NonPublic | BindingFlags.Static);
        }
        object OnPlayerVoice(NetUser netuser, List<uLink.NetworkPlayer> list)
        {
            Debug.Log(netuser.playerClient.userName + " is speaking");
            list.Clear();
            foreach(PlayerClient player in PlayerClient.All)
            {
                list.Add(player.netPlayer);
            }
            return 10000000;
        }
        private object OnClientSpeak(IDLocal local, PlayerClient client, int total, int setupData, byte[] data)
        {
            List<uLink.NetworkPlayer> players = (List<uLink.NetworkPlayer>)playerList.GetValue(null);
            object num = Interface.CallHook("OnPlayerVoice", client.netUser, players);
            playerList.SetValue(null, players);
            Debug.Log("yes");
            if (num == null) return total; 
            Debug.Log(local.ToString());
            total = 1;
            Debug.Log(players.Count.ToString());
            foreach (PlayerClient player in PlayerClient.All)
            { 
                player.GetComponent<VoiceCom>().networkView.RPC("VoiceCom:voiceplay", players, new object[] { 9999999999f, setupData, data });
            }
            return true;
        }*/
    }
}
