// Reference: Oxide.Ext.Rust
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("CleanUp", "Reneb", "0.0.1")]
    public class CleanUp : RustPlugin
    {
		void Loaded()
		{
			if (!permission.PermissionExists("canclean")) permission.RegisterPermission("canclean", this);
		}
		void OnServerInitialized()
		{
			InitializeTable();
		}
		
		Dictionary<string, string> displaynameToShortname = new Dictionary<string, string>();
		
		private void InitializeTable()
        {
            displaynameToShortname.Clear();
            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                displaynameToShortname.Add(itemdef.displayName.english.ToString().ToLower(), itemdef.shortname.ToString());
            }
        }
		
		
		bool hasAccess(BasePlayer player)
        {
            if (player == null) return false;
            if (player.net.connection.authLevel > 0) return true;
            return permission.UserHasPermission(player.userID.ToString(), "canclean");
        }
        
		[ChatCommand("clean")]
        void cmdChatClean(BasePlayer player, string command, string[] args)
        {
            if (!hasAccess(player)) { SendReply(player, "You dont have access to this command"); return; }
            if(args.Length < 2) { SendReply(player, "/clean \"Deployable Item Name\" world/all");  SendReply(player, "world: only items that are not on a building"); SendReply(player, "all: all items"); return; }
            if(args[1] != "world" && args[1] != "all") { SendReply(player, "/clean \"Deployable Item Name\" world/all");  SendReply(player, "world: only items that are not on a building"); SendReply(player, "all: all items"); return; }
            switch(args[0].ToLower())
            {
            	default:
            		string shortname = args[0].ToLower();
            		if(displaynameToShortname.ContainsKey(shortname))
            			shortname = displaynameToShortname[shortname];
            		else if(!displaynameToShortname.ContainsValue(shortname))
            		{
            			SendReply(player, string.Format("{0} is not a valid item name", args[0]));
            			return;
            		}
            		
            	break;
            }
        }
    }
}
