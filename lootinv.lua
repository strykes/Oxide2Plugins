PLUGIN.Name = "lootinv"
PLUGIN.Title = "Inventory Viewer"
PLUGIN.Version = V(1, 1, 1)
PLUGIN.Description = "View players inventory"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true


function PLUGIN:Init()
	command.AddChatCommand( "view", self.Object, "cmdView" )
	command.AddChatCommand( "viewsleeper", self.Object, "cmdViewSleeper" )
	command.AddChatCommand( "viewall", self.Object, "cmdViewAll" )
	self.ViewAll = {}
end
function PLUGIN:Unload()
end
function PLUGIN:LoadDefaultConfig()
	self.Config.authLevel = 1
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. msg .. "\"" );
end
function PLUGIN:cmdView( player, com, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel
	if(authlevel >= neededlevel) then
		if(args.Length >= 1) then
			local loot = player.inventory.loot
			if(not loot:IsLooting()) then
				ChatMessage(player,"You must be looting a box to start viewing players inventories")
				return
			end
			local target = ""
			for i=0, args.Length-1 do
				if(i == 0) then
					target = args[i]
				else
					target = target .. " " .. args[i]
				end
			end
			local targetplayer = global.BasePlayer.Find(target)
			if(not targetplayer) then
				local plistenum = player.activePlayerList
				local it = plistenum:GetEnumerator()
				while (it:MoveNext()) do
					if(targetplayer and string.find(it.Current.displayName,target)) then
						ChatMessage(player,"Multiple Players found")
						return
					end
					if(string.find(it.Current.displayName,target)) then
						targetplayer = it.Current
					end
				end
				if(not targetplayer) then
					ChatMessage(player,"No players found")
					return
				end
			end
			ChatMessage(player,"You are starting to loot: " .. targetplayer.displayName .. " - " .. rust.UserIDFromPlayer(targetplayer))
			loot:StartLootingPlayer(targetplayer)
			loot.PositionChecks = false
		end
	end
end
function PLUGIN:cmdViewSleeper( player, com, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel
	if(authlevel >= neededlevel) then
		if(args.Length >= 1) then
			local select = false
			if(args.Length == 2) then
				if(tonumber(args[1]) ~= nil) then
					select = tonumber(args[1])
				end
			end
			local loot = player.inventory.loot
			if(not loot:IsLooting()) then
				ChatMessage(player,"You must be looting a box to start viewing players inventories")
				return
			end
			local target = args[0]
			local steamid = false
			if(tonumber(target)	~= nil) then
				if(string.len(target)==17) then
					steamid = true
				end
			end
			local targetplayer = false
			local plistenum = player.sleepingPlayerList
			local it = plistenum:GetEnumerator()
			while (it:MoveNext()) do
				if(not steamid) then
					if(targetplayer and string.find(it.Current.displayName,target) and not select) then
						ChatMessage(player,"Multiple sleepers with that name found")
						return
					end
					if(string.find(it.Current.displayName,target)) then
						if(select and select > 1) then
							select = select - 1
						elseif(select and select == 1) then
							targetplayer = it.Current
							break
						else
							targetplayer = it.Current
						end
					end
				else
					if(rust.UserIDFromPlayer(it.Current) == target) then
						targetplayer = it.Current
						break
					end
				end
			end
			if(not targetplayer) then
				ChatMessage(player,"No sleepers with that name found")
				return
			end
			ChatMessage(player,"You are starting to loot: " .. targetplayer.displayName .. " - " .. rust.UserIDFromPlayer(targetplayer))
			loot:StartLootingPlayer(targetplayer)
			loot.PositionChecks = false
		end
	end
end

function PLUGIN:cmdViewAll( player, com, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel
	if(authlevel >= neededlevel) then
		local thetimer = 3
		if(args.Length >= 1) then
			if(tonumber(args[0]) ~= nil) then
				thetimer = tonumber(args[0])
			end
		end
		local loot = player.inventory.loot
		if(not loot:IsLooting()) then
			ChatMessage(player,"You must be looting a box to start viewing players inventories")
			return
		end
		local plistenum = player.activePlayerList
		local it = plistenum:GetEnumerator()
		self.ViewAll[player] = {}
		while (it:MoveNext()) do
			self.ViewAll[player][#self.ViewAll[player] + 1] = it.Current
		end
		self:ViewInv(player,1,thetimer)
	end
end
function PLUGIN:ViewInv(player,curr,thetimer)
	if(not player:IsConnected()) then self:EndView(player) return end
	if(not self.ViewAll[player][curr]) then
		ChatMessage(player,"No more inventories to look at")
		self:EndView(player)
		return
	end
	local loot = player.inventory.loot
	if(not loot:IsLooting()) then ChatMessage(player,"You have stoped looting") self:EndView(player) return end
	loot:StartLootingPlayer(self.ViewAll[player][curr])
	ChatMessage(player,"Inventory of: " .. self.ViewAll[player][curr].displayName .. " - " .. rust.UserIDFromPlayer(self.ViewAll[player][curr]))
	loot.PositionChecks = false
	timer.Once(thetimer, function() self:ViewInv(player,curr+1,thetimer) end)
end
function PLUGIN:EndView(player)
	self.ViewAll[player] = nil
end
