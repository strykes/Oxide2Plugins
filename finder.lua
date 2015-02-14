PLUGIN.Name = "finder"
PLUGIN.Title = "Finder"
PLUGIN.Version = V(1, 4, 2)
PLUGIN.Description = "Find objects that belongs to players. sleepingbags, players, sleepers, doors for the moment."
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true
PLUGIN.ResourceId = 692
local deadPlayerList = false
function PLUGIN:Init()
	self.Find = {}
	command.AddChatCommand( "findsleepingbags", self.Plugin, "cmdFindSleepingBag" )
	command.AddChatCommand( "findsleepingbag", self.Plugin, "cmdFindSleepingBag" )
	command.AddChatCommand( "findplayers", self.Plugin, "cmdFindPlayer" )
	command.AddChatCommand( "findplayer", self.Plugin, "cmdFindPlayer" )
	command.AddChatCommand( "finddoor", self.Plugin, "cmdFindDoor" )
	command.AddChatCommand( "finddoors", self.Plugin, "cmdFindDoor" )
	command.AddChatCommand( "findtp", self.Plugin, "cmdFindTp" )
	command.AddChatCommand( "finditem", self.Plugin, "cmdFindItem" )
	command.AddChatCommand( "findprivilege", self.Plugin, "cmdFindBuildingPrivilege" )
	command.AddChatCommand( "findprivileges", self.Plugin, "cmdFindBuildingPrivilege" )
	local pluginList = plugins.GetAll()
    for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "deadPlayerList" then
            deadPlayerList = pluginList[i].Object
            break
        end
    end
	local pluginList = plugins.GetAll()
    for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "Building Owners" then
            buildingowners = pluginList[i].Object
            break
        end
    end
	if(not deadPlayerList) then
		print("To increase your chance in finding players, use the deadPlayerList plugin")
	end
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. msg .. "\"" );
end
function PLUGIN:LoadDefaultConfig()
	self.Config.authLevel = 1
end
function PLUGIN:cmdFindBuildingPrivilege(player,cmd,args)
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.authLevel ) then
		if(args.Length >= 1) then
			local targetplayer, err = self:FindPlayer(args[0])
			if(not targetplayer) then
				ChatMessage(player,err)
				return
			end
			ChatMessage(player,"Looking for building privileges of " .. targetplayer.displayName)
			local buildpriv, err = self:FindBuildingPrivilegeByPlayer(targetplayer)
			if(not buildpriv) then
				ChatMessage(player,err)
				return
			end
			self.Find[player] = buildpriv
			ChatMessage(player,"Found " .. #buildpriv .. " Building privileges for " .. targetplayer.displayName .. " use \"/findtp ID\" to teleport to it/them")
			for i = 1, #buildpriv do
				local pos = buildpriv[i].transform.position
				ChatMessage(player,i .. " - " .. math.ceil(pos.x) .. " " .. math.ceil(pos.y) .. " " .. math.ceil(pos.z))
			end
		end
	end
end
function PLUGIN:FindBuildingPrivilegeByPlayer(player)
	local userID = player.userID
	if(type(userID) ~= "string") then
		userID = rust.UserIDFromPlayer(player)
	end
	local buildingPriviliges = {}
	local allTriggerBase = UnityEngine.Object.FindObjectsOfTypeAll(global.TriggerBase._type)
	if(allTriggerBase.Length == 0) then return false, "No Tool Cupboard were found on your server" end
	for i = 0, allTriggerBase.Length - 1 do
		if(allTriggerBase[i]:GetComponent("BuildPrivilegeTrigger")) then
			local bpriv = rust.UserIDsFromBuildingPrivilege( allTriggerBase[i].privlidgeEntity )
			for o = 0, bpriv.Length - 1 do
				if(bpriv[o] == userID) then
					buildingPriviliges[#buildingPriviliges + 1] = allTriggerBase[i].privlidgeEntity:GetComponent("BaseEntity")
				end
			end
		end
	end
	if(#buildingPriviliges == 0) then return false, "No Building Privileges Found for " .. player.displayName end
	return buildingPriviliges
end
function PLUGIN:cmdFindItem(player,com,args)
	self.Find[player] = {}
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel 
	if(authlevel >= neededlevel) then
		if(args.Length >= 1) then
			local itemdefinition, err = self:GetItemDefinition(string.lower(args[0]))
			if(not itemdefinition) then
				ChatMessage(player,err)
				return
			end
			local containers,err = self:FindItemContainers()
			if(not containers) then
				ChatMessage(player,err)
				return
			end
			local minamount = 1
			if(args.Length >= 2) then
				if(tonumber(args[1]) ~= nil) then
					minamount = tonumber(args[1])
				end
			end
			local founditems, err = self:FindItemInContainers(containers,itemdefinition,minamount)
		 	if(not founditems) then
				ChatMessage(player,err)
				return
			end
			self.Find[player] = founditems
			ChatMessage(player,"Found " .. #founditems .. " Containers that have at least " .. tostring(minamount) .. " of ".. tostring(itemdefinition.displayname))
			for i = 1, #founditems do
				local pos = founditems[i].transform.position
				ChatMessage(player,i .. " - " .. tostring(err[i].type) .. " - " .. tostring(err[i].num) .. " " .. tostring(itemdefinition.displayname) .. " - " .. math.ceil(pos.x) .. " " .. math.ceil(pos.y) .. " " .. math.ceil(pos.z))
			end
		end
	end
end
function PLUGIN:FindItemInContainers(containers,itemdef,min)
	local found = {}
	local description = {}
	for i=1, #containers do
		local content = containers[i].item.contents
		local amount = content:GetAmount(itemdef.itemid,false)
		if(amount >= min) then
			found[#found + 1] = containers[i]
			description[#description+1] = {type=containers[i].item.info.displayname,num=amount}
		end
	end
	
	local allBasePlayer = UnityEngine.Object.FindObjectsOfTypeAll(global.BasePlayer._type)
	for i = 0, tonumber(allBasePlayer.Length - 1) do
		if(allBasePlayer[ i ].inventory) then
			local amount = allBasePlayer[ i ].inventory:GetAmount(itemdef.itemid)
			if(amount >= min) then
				found[#found + 1] = allBasePlayer[ i ]
				description[#description+1] = {type=allBasePlayer[ i ].displayName .. "(Player)",num=amount}
			end
		end
	end
	if(#found == 0) then
		return false, "No items found with those criterias"
	end
	return found, description
end
function PLUGIN:GetItemDefinition(itemname)
	local itemlist = global.ItemManager.GetItemDefinitions();
	local it = itemlist:GetEnumerator()
	local itemdefinition = false
	while (it:MoveNext()) do
		if(itemname == string.lower(it.Current.displayname)) then
			itemdefinition = it.Current
			break
		elseif(itemname == it.Current.shortname) then
			itemdefinition = it.Current
			break
		end
	end
	if(not itemdefinition) then
		return false, "Wrong item name"
	end
	return itemdefinition
end
function PLUGIN:cmdFindPlayer(player,com,args)
	self.Find[player] = {}
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel 
	if(authlevel >= neededlevel) then
		if(args.Length >= 1) then
			local targetplayers, err = self:FindPlayers(args[0])
			if(not targetplayers) then
				ChatMessage(player,err)
				return
			end
			self.Find[player] = targetplayers
			ChatMessage(player,"Found " .. #targetplayers .. " players that match " .. args[0] .. " use \"/findtp ID\" to teleport")
			for i = 1, #targetplayers do
				if(targetplayers[i].transform) then
					local pos = targetplayers[i].transform.position
					local state = "Alive"
					if(targetplayers[i]:IsDead()) then state = "Dead" end
					if(targetplayers[i]:IsSleeping()) then state = "Sleeping" end
					if(targetplayers[i]:IsSpectating()) then state = "Spectating" end
					local status = "Disconnected"
					if(targetplayers[i]:IsConnected()) then status = "Connected" end
					ChatMessage(player,i .. " - " ..  targetplayers[i].displayName .. " - ".. rust.UserIDFromPlayer(targetplayers[i]) .. " - " .. status .. " - " .. state .. " - " .. math.ceil(pos.x) .. " " .. math.ceil(pos.y) .. " " .. math.ceil(pos.z))
				else
					ChatMessage(player,i .. " - " ..  targetplayers[i].displayName .. " - ".. targetplayers[i].userID .. " - " .. "Disconnected" .. " - " .. "Dead" .. " - " .. math.ceil(targetplayers[i].pos.x) .. " " .. math.ceil(targetplayers[i].pos.y) .. " " .. math.ceil(targetplayers[i].pos.z))
				end
			end
		end
	end
end
function PLUGIN:cmdFindDoor(player,com,args)
	self.Find[player] = {}
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = 1
	if(authlevel >= neededlevel) then
		if(not buildingowners) then
			ChatMessage(player,"This command is not activated. You must have Building Owners plugin installed.")
			return
		end
		if(args.Length >= 1) then
			local targetplayer, err = self:FindPlayer(args[0])
			if(not targetplayer) then
				ChatMessage(player,err)
				return
			end
			ChatMessage(player,"Looking for Doors owned by " .. targetplayer.displayName)
			local doors, err = self:FindDoorsByPlayer(targetplayer)
			if(not doors) then
				ChatMessage(player,err)
				return
			end
			self.Find[player] = doors
			ChatMessage(player,"Found " .. #doors .. " Doors owned by " .. targetplayer.displayName .. " use \"/findtp ID\" to teleport to it/them")
			for i = 1, #doors do
				local pos = doors[i].transform.position
				ChatMessage(player,i .. " - Level " .. tostring(doors[i]:GetComponent("BuildingBlock").grade) .. " - " .. math.ceil(pos.x) .. " " .. math.ceil(pos.y) .. " " .. math.ceil(pos.z))
			end
		end
	end
end
function PLUGIN:cmdFindTp( player, com, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel 
	if(authlevel >= neededlevel) then
		if(not self.Find[player]) then
			ChatMessage(player,"You need to find stuff first. Use /findhelp to get the full list of what you can find")
			return
		end
		
		if(args.Length >= 1) then
			local target = false
			if(tonumber(args[0]) == nil) then
				ChatMessage(player,"Wrong argument 1: needs to be a number")
				return
			end
			target = tonumber(args[0])
			if(not self.Find[player][target]) then
				ChatMessage(player,"Wrong argument 1: This ID doesn't exist.")
				return
			end
			if(self.Find[player][target].transform) then
				self:Teleport( player, self.Find[player][target].transform.position )
			else
				local newpos = player.transform.position
				newpos.x = self.Find[player][target].pos.x
				newpos.y = self.Find[player][target].pos.y
				newpos.z = self.Find[player][target].pos.z
				self:Teleport( player, newpos )
			end
		else
			ChatMessage(player,"You must specify an ID of which you want to teleport to.")
		end
	end
end
function PLUGIN:cmdFindSleepingBag( player, com, args )
	self.Find[player] = {}
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel 
	if(authlevel >= neededlevel) then
		if(args.Length >= 1) then
			local targetplayer, err = self:FindPlayer(args[0])
			if(not targetplayer) then
				ChatMessage(player,err)
				return
			end
			ChatMessage(player,"Looking for sleeping bags owned by " .. targetplayer.displayName)
			local bags, err = self:FindSleepingBagByPlayer(targetplayer)
			if(not bags) then
				ChatMessage(player,err)
				return
			end
			self.Find[player] = bags
			ChatMessage(player,"Found " .. #bags .. " Sleeping Bag for " .. targetplayer.displayName .. " use \"/findtp ID\" to teleport to it/them")
			for i = 1, #bags do
				local pos = bags[i].transform.position
				ChatMessage(player,i .. " - " .. math.ceil(pos.x) .. " " .. math.ceil(pos.y) .. " " .. math.ceil(pos.z))
			end
		end
	end
end

function PLUGIN:FindDoorsByPlayer(player)
	local userID = player.userID
	if(type(userID) ~= "string") then
		userID = rust.UserIDFromPlayer(player)
	end
	local doors = {}
	local allDoors = UnityEngine.Object.FindObjectsOfTypeAll(global.Door._type)
	if(allDoors.Length == 0) then return false, "No Doors were found on your server" end
	for i = 0, tonumber(allDoors.Length - 1) do
		local currdoor = allDoors[i];
		local steamid = buildingowners:FindBlockData(currdoor:GetComponent("BuildingBlock"))
		if(steamid and userID == steamid) then
			doors[#doors + 1] = currdoor:GetComponent("BuildingBlock")
		end
	end
	if(#doors == 0) then return false, "No Doors Found for " .. player.displayName end
	return doors
end
function PLUGIN:FindItemContainers()
	local containers = {}
	local allItems = UnityEngine.Object.FindObjectsOfTypeAll(global.WorldItem._type)
	if(allItems.Length == 0) then return false, "No Items were found on your server" end
	for i = 0, tonumber(allItems.Length - 1) do
		local curritem = allItems[i];
		if(curritem.item and curritem.item.contents and curritem.item.contents.capacity) then
			containers[#containers + 1] = curritem
		end
	end
	if(#containers == 0) then return false, "No Item containers were found" end
	return containers
end
function PLUGIN:FindSleepingBagByPlayer(player)
	local userID = player.userID
	local bags = {}
	local sleepingbags = UnityEngine.Object.FindObjectsOfTypeAll(global.SleepingBag._type)
	if(sleepingbags.Length == 0) then return false, "No Sleeping Bags Found on the server" end
	for i = 0, tonumber(sleepingbags.Length - 1) do
		local sleepingbag = sleepingbags[i];
		if(sleepingbag.deployerUserID and sleepingbag.deployerUserID == tonumber(userID)) then
			bags[#bags + 1] = sleepingbag
		end
	end
	if(#bags == 0) then return false, "No Sleeping Bags Found for " .. player.displayName end
	return bags
end
function PLUGIN:FindPlayers(target)
	local found = {}
	local steamid = false
	if(tonumber(target) ~= nil and string.len(target) == 17) then
		steamid = target
	end
	local targetplayer = false
	local allBasePlayer = UnityEngine.Object.FindObjectsOfTypeAll(global.BasePlayer._type)
	for i = 0, tonumber(allBasePlayer.Length - 1) do
		local currentplayer = allBasePlayer[ i ];
		if(steamid) then
			if(steamid == rust.UserIDFromPlayer(currentplayer)) then
				found[1] = currentplayer
				return found
			end
		else
			if(string.find(currentplayer.displayName,target)) then
				found[#found + 1] = currentplayer
			elseif(string.find(rust.UserIDFromPlayer(currentplayer),target)) then
				found[#found + 1] = currentplayer
			end
		end
	end
	if(#found == 0) then 
		if deadPlayerList then
			local deadplayers, err = deadPlayerList:FindDeadPlayers(target)
			if(not deadplayers) then return false, err end
			for k,v in pairs(deadplayers) do
				v.userID = k
				found[# found +1 ] = v
			end
			return found
		end
		return false, "No players found" 
	end
	return found
end
function PLUGIN:FindPlayer( target )
	local steamid = false
	if(tonumber(target) ~= nil and string.len(target) == 17) then
		steamid = target
	end
	local targetplayer = false
	local allBasePlayer = UnityEngine.Object.FindObjectsOfTypeAll(global.BasePlayer._type)
	for i = 0, tonumber(allBasePlayer.Length - 1) do
		local currentplayer = allBasePlayer[ i ];
		if(steamid) then
			if(steamid == rust.UserIDFromPlayer(currentplayer)) then
				return currentplayer
			end
		else
			if(currentplayer.displayName == target) then
				return currentplayer
			elseif(string.find(currentplayer.displayName,target)) then
				if(targetplayer) then
					return false, "Multiple Players Found"
				end
				targetplayer = currentplayer
			end
		end
	end
	if(not targetplayer) then 
		if deadPlayerList then
			targetsteamid,targetplayer = deadPlayerList:FindDeadPlayer(target)
		end
		if(not targetsteamid) then
			return false, "No players found" 
		end
		targetplayer.userID = targetsteamid
	end
	return targetplayer
end

function PLUGIN:Teleport( player, destination )
    local preTeleportLocation = player.transform.position
    preTeleportLocation.x = preTeleportLocation.x + 1000
    preTeleportLocation.z = preTeleportLocation.z + 1000
    
    player.transform.position = preTeleportLocation
    player:UpdateNetworkGroup()
    player.transform.position = destination
    player:UpdateNetworkGroup()
    player:UpdatePlayerCollider(true, false)
    player:SendFullSnapshot()
	player:StartSleeping()
	timer.Once(0.1, function() player.inventory:SendSnapshot() end)
end
