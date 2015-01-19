PLUGIN.Name = "give"
PLUGIN.Title = "Give Plugin"
PLUGIN.Version = V(1, 1, 0)
PLUGIN.Description = "Allow admins to give to players"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true
	

function PLUGIN:Init()
	command.AddChatCommand( "give", self.Object, "cmdGive" )
	command.AddConsoleCommand("inv.give", self.Object, "ccmdGive")
	command.AddConsoleCommand("inv.giveplayer", self.Object, "ccmdGive")
	command.AddConsoleCommand("inventory.giveplayer", self.Object, "ccmdGive")
	command.AddConsoleCommand("inv.giveall", self.Object, "ccmdGive")

end

local function getItem(iname)
	if(string.sub(string.lower(iname),-3) == " bp") then
		return string.sub(string.lower(iname),0,-4), true
	end
	return iname, false
end

function PLUGIN:OnServerInitialized()
	self:InitializeTable()
end

function PLUGIN:InitializeTable()
	self.Table = {}
	local itemlist = global.ItemManager.GetItemDefinitions();
	local it = itemlist:GetEnumerator()
	while (it:MoveNext()) do
		local correctname = string.lower(it.Current.displayname,"%%","t")
		self.Table[correctname] = tostring(it.Current.shortname)
	end
end
function PLUGIN:LoadDefaultConfig()
	self.Config.authLevel = 1
	self.Config.Messages = {}
	self.Config.Messages.NotAllowed = self.Config.Messages.NotAllowed or "You are not allowed to use this command on someone else"
	self.Config.Messages.PlayerDoesntExist = self.Config.Messages.PlayerDoesntExist or "{name} doesn't exist"
	self.Config.Messages.MultiplePlayersFound =  self.Config.Messages.MultiplePlayersFound or "Multiple Players Found"
	self.Config.Messages.WrongArguments = self.Config.Messages.WrongArguments or "Wrong arguments: /give \"player(optional)\" \"item\" \"amount\""
	self.Config.Messages.InvalidItem = self.Config.Messages.InvalidItem or "{name} is an invalid item"
	self.Config.Messages.HelpText = self.Config.Messages.HelpText or "/give \"Player\" \"Item Name\" \"Amount\" - to give an item to a player"
end


function PLUGIN:ccmdGive(arg)
	if(not self.Table) then self:InitializeTable() end
    local player = nil
    local command = arg.cmd.namefull

    if arg.connection then
        player = arg.connection.player
    end

    if player and (player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel)  then
        return true
    end
    
    if command == "inv.give" then
        if not arg.Args or arg.Args.Length < 2 then
            arg:ReplyWith("Wrong argument : inv.give \"ITEM\" \"AMOUNT\"")
        else
			if(not player) then
				arg:ReplyWith("You may only use inv.give via the ingame console, use inv.giveplayer.")
				return
			end
            if tonumber(arg.Args[1]) == nil then
                arg:ReplyWith("Wrong Number amount, needs to be a number: inv.giveplayer \"ITEM\" \"AMOUNT\"")
                return
            end
            itemname, isBlueprint = getItem(arg.Args[0])
			descname, err = self:GiveItemToInv(player,itemname,tonumber(arg.Args[1]),player.inventory.containerMain,isBlueprint)
			if(not descname) then arg:ReplyWith(err) return end
			arg:ReplyWith(tostring(arg.Args[1]) .. "x " .. descname .. " was given to " ..  tostring(player.displayName))
			rust.SendChatMessage(player,"You have received " .. tostring(arg.Args[1]) .. "x " .. descname)
			return true
		end
    elseif(command == "inv.giveplayer" or command == "inventory.giveplayer") then
        if not arg.Args or arg.Args.Length < 3 then
            arg:ReplyWith("Wrong argument : inv.giveplayer \"PLAYERNAME/STEAMID\" \"ITEM\" \"AMOUNT\"")
        else
			local targetplayer, err = self:FindPlayer(tostring(arg.Args[0]))
			if(not targetplayer) then
				arg:ReplyWith(err)
				return
			end
            if tonumber(arg.Args[2]) == nil then
                arg:ReplyWith("Wrong Number amount, needs to be a number: inv.giveplayer \"ITEM\" \"AMOUNT\"")
                return
            end
            itemname, isBlueprint = getItem(arg.Args[1])
			descname, err = self:GiveItemToInv(targetplayer,itemname,tonumber(arg.Args[2]),targetplayer.inventory.containerMain,isBlueprint)
			if(not descname) then arg:ReplyWith(err) return end
			rust.SendChatMessage(targetplayer,"You have received " .. tostring(arg.Args[2]) .. " x " .. descname)
			arg:ReplyWith(tostring(arg.Args[2]) .. " x " .. descname .. " was given to " ..  tostring(targetplayer.displayName));
			return true
		end
    elseif(command == "inv.giveall") then
        if not arg.Args or arg.Args.Length < 2 then
            arg:ReplyWith("Wrong argument : inv.giveall \"ITEM\" \"AMOUNT\"")
        else
            if tonumber(arg.Args[1]) == nil then
                arg:ReplyWith("Wrong Number amount, needs to be a number: inv.giveplayer \"ITEM\" \"AMOUNT\"")
                return
            end
			allBasePlayer = UnityEngine.Object.FindObjectsOfTypeAll(global.BasePlayer._type)
			count = 0
			itemname, isBlueprint = getItem(arg.Args[0])
			for i = 0, tonumber(allBasePlayer.Length - 1) do
				if(allBasePlayer[ i ]:IsConnected()) then
					descname, err = self:GiveItemToInv(allBasePlayer[ i ],itemname,tonumber(arg.Args[1]),allBasePlayer[ i ].inventory.containerMain,isBlueprint)
					if(not descname) then arg:ReplyWith(err) return end
					rust.SendChatMessage(allBasePlayer[ i ],"You have received " .. tostring(arg.Args[1]) .. "x " .. descname)
					count = count + 1
				end
			end
			arg:ReplyWith(tostring(arg.Args[1]) .. "x " .. descname .. " was given to " ..  count .. " inventories");
			return true
		end
    end
    return
end
 
function PLUGIN:cmdGive( player, com, args )
	if(not self.Table) then self:InitializeTable() end
	if (player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.authLevel) then
		if(args.Length == 3) then
			local targetplayer, err = self:FindPlayer(tostring(args[0]))
			if(not targetplayer) then
				rust.SendChatMessage(player,err)
				return
			end
			if(tonumber(args[2]) == nil) then
				rust.SendChatMessage(player,self.Config.Messages.WrongArguments)
				return
			end
			itemname, isBlueprint = getItem(args[1])
			descname, err = self:GiveItemToInv(targetplayer,itemname,tonumber(args[2]),targetplayer.inventory.containerMainm,isBlueprint)
			if(not descname) then rust.SendChatMessage(player,err) return end
			
			rust.SendChatMessage(player,tostring(args[2]) .. "x " .. descname .. " was given to " ..  tostring(targetplayer.displayName))
			rust.SendChatMessage(targetplayer,"You have received " .. tostring(args[2]) .. "x " .. descname)
		elseif(args.Length == 2) then
			if(tonumber(args[1]) == nil) then
				rust.SendChatMessage(player,self.Config.Messages.WrongArguments)
				return
			end
			itemname, isBlueprint = getItem(args[0])
			descname, err = self:GiveItemToInv(player,itemname,tonumber(args[1]),player.inventory.containerMain,isBlueprint)
			if(not descname) then rust.SendChatMessage(player,err) return end
			rust.SendChatMessage(player,"You have received " .. tostring(args[1]) .. "x " .. descname)
		else
			rust.SendChatMessage(player,self.Config.Messages.WrongArguments)
		end
	else
		rust.SendChatMessage(player,self.Config.Messages.NotAllowed)
	end
end
function PLUGIN:GiveItemToInv(player,itemname,amount,pref,isBP)
	if(self.Table[string.lower(itemname)]) then
		itemname = self.Table[string.lower(itemname)]
	end
	arr = util.TableToArray( { itemname } )
	definition = global.ItemManager.FindItemDefinition.methodarray[1]:Invoke(nil, arr )
	if(not definition) then
		return false, self:BuildMSG(self.Config.Messages.InvalidItem,tostring(itemname))
	end
	if(isBP or (definition.stackable <= 1 and amount > 1)) then
		for i=1, amount do
			player.inventory:GiveItem(global.ItemManager.CreateByItemID(definition.itemid,1,isBP),pref);
		end
	else
		player.inventory:GiveItem(global.ItemManager.CreateByItemID(definition.itemid,amount,isBP),pref)
	end
	return definition.displayname
end
function PLUGIN:BuildMSG(msg,name)
	return tostring(string.gsub(msg, "{name}", name))
end
function PLUGIN:SendHelpText(player)
    if player:IsAdmin() then
        rust.SendChatMessage(player,self.Config.Messages.HelpText)
    end
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
					return false, self.Config.Messages.MultiplePlayersFound
				end
				targetplayer = currentplayer
			end
		end
	end
	if(not targetplayer) then return false, self.Config.Messages.PlayerDoesntExist end
	return targetplayer
end
