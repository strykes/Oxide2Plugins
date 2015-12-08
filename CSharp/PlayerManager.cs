using System.Collections.Generic;
using System;
using System.Reflection;
using System.Data;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Logging;
using Oxide.Core.Plugins;
using Rust; 

namespace Oxide.Plugins
{
    [Info("PlayerManager", "Reneb", "1.0.0")]
    class PlayerManager : RustPlugin
    {
        [PluginReference]
        Plugin PlayerDatabase;
		
		Hash<ulong, PlayerMGUI> playerGUI = new Hash<ulong, PlayerMGUI>();

        class PlayerMGUI
        {
            public string guiid;
            public int page;
        }
		
		void OpenGUI(BasePlayer player)
		{
			DestroyGUI(player, "PlayerManagerOverlay");
			AddUI(player, parentoverlay);
			playerGUI[player.userID] = new PlayerMGUI();
			RefreshUI(player);
		}
		
		void RefreshUI(BasePlayer player)
		{
			// Players List Button
			
		
		}
		
		void AddUI(BasePlayer player, string json)
		{
			CommunityEntity.ServerInstance.ClientRPCEx(new Network.SendInfo { connection = player.net.connection }, null, "AddUI", new Facepunch.ObjectList(json, null, null, null, null));
		}
		
		private const string parentoverlay = @"[
			{
				""name"": ""PlayerManagerOverlay"",
				""parent"": ""HUD/Overlay"",
				""components"":
				[
					{
						 ""type"":""UnityEngine.UI.Image"",
						 ""color"":""0.1 0.1 0.1 0.8"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0 0"",
						""anchormax"": ""1 1""
					},
					{
						""type"":""NeedsCursor"",
					}
				]
			},
			{
				""parent"": ""PlayerManagerOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""Close"",
						""fontSize"":20,
						""align"": ""MiddleCenter"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0 0.01"",
						""anchormax"": ""1 0.05""
					},
				]
			},
			{
				""parent"": ""PlayerManagerOverlay"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Button"",
						""command"":""playermanager.close"",
						""color"": ""0.5 0.5 0.5 0.2"",
						""imagetype"": ""Tiled""
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""0 0.01"",
						""anchormax"": ""1 0.05""
					}
				]
			}
		]
		";
		
		
		private const string jsonbutton = @"[
			{
				""parent"": ""{0}"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Button"",
						""command"":""{1}"",
						""color"": ""{2}"",
						""imagetype"": ""Tiled""
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""{3} {5}"",
						""anchormax"": ""{4} {6}""
					}
				]
			}
		]
		";
		private const string jsontext = @"[
			{
				""parent"": ""{0}"",
				""components"":
				[
					{
						""type"":""UnityEngine.UI.Text"",
						""text"":""{1}"",
						""fontSize"":{2},
						""align"": ""MiddleCenter"",
					},
					{
						""type"":""RectTransform"",
						""anchormin"": ""{3} {5}"",
						""anchormax"": ""{4} {6}""
					},
				]
			}
		]
		";
		
		string GenerateButton(string overlay, string command, string color, string xmin, string xmax, string ymin, string ymax)
		{
			return string.Format(jsonbutton, overlay, command, color, xmin, xmax, ymin, ymax);
		}
		string GenerateText(string overlay, string text, string textsize, string xmin, string xmax, string ymin, string ymax)
		{
			return string.Format(jsontext, overlay, text, textsize, xmin, xmax, ymin, ymax);
		}
		
		
    }
}
