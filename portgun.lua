PLUGIN.Name = "portgun"
PLUGIN.Title       = "Portgun"
PLUGIN.Description = "Teleport to where you are looking at"
PLUGIN.Version     = V(1, 3, 2)
PLUGIN.HasConfig   = true
PLUGIN.Author      = "Reneb"

function PLUGIN:Init()
    command.AddChatCommand( "p",  self.Plugin, "cmdTeleport" )
	command.AddChatCommand( "pg",  self.Plugin, "cmdTeleport" )
	command.AddChatCommand( "forward",  self.Plugin, "cmdForward" )
	command.AddChatCommand( "fw",  self.Plugin, "cmdForward" )
	command.AddChatCommand( "up",  self.Plugin, "cmdUp" )
	command.AddChatCommand( "down",  self.Plugin, "cmdDown" )
	TeleportVectors = {}
end
function PLUGIN:LoadDefaultConfig()
	self.Config.PortgunForModerators = true
end

function PLUGIN:Teleport( player, destination, rot )
	player.transform.position = destination
	newobj = util.TableToArray( { destination } )
	util.ConvertAndSetOnArray( newobj, 0, destination, UnityEngine.Object._type )
	player:ClientRPC(nil,player,"ForcePositionTo",newobj)
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. msg .. "\"" );
end
function PLUGIN:cmdTeleport( player, cmd, args )
    local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = 2
	if(self.Config.PortgunForModerators) then
		neededlevel = 1
	end
	if(authlevel and authlevel >= neededlevel) then
		self:TeleportRay( player )
	else
		player:ChatMessage("You are not allowed to use this command")
	end
end

function PLUGIN:cmdForward( player, cmd, args )
    local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = 2
	if(self.Config.PortgunForModerators) then
		neededlevel = 1
	end
	if(authlevel and authlevel >= neededlevel) then
		local dist = 4
		if(args.Length > 0) then
			if(tonumber(args[0]) ~= nil) then
				dist = tonumber(args[0])
			end
		end
		self:TeleportForward( player, dist )
	else
		player:ChatMessage("You are not allowed to use this command")
	end
end


function PLUGIN:cmdUp( player, cmd, args )
    local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = 2
	if(self.Config.PortgunForModerators) then
		neededlevel = 1
	end
	if(authlevel and authlevel >= neededlevel) then
		local dist = 4
		if(args.Length > 0) then
			if(tonumber(args[0]) ~= nil) then
				dist = tonumber(args[0])
			end
		end
		self:TeleportUp( player, dist )
	else
		player:ChatMessage("You are not allowed to use this command")
	end
end
function PLUGIN:cmdDown( player, cmd, args )
    local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = 2
	if(self.Config.PortgunForModerators) then
		neededlevel = 1
	end
	if(authlevel and authlevel >= neededlevel) then
		local dist = 4
		if(args.Length > 0) then
			if(tonumber(args[0]) ~= nil) then
				dist = tonumber(args[0])
			end
		end
		self:TeleportDown( player, dist )
	else
		player:ChatMessage("You are not allowed to use this command")
	end
end

function PLUGIN:GetPoint(ray)
	local hits = UnityEngine.Physics.RaycastAll["methodarray"][1]:Invoke(nil, util.TableToArray({ ray }))
	local closestdist = 9999
	local closestpoint = false
	local enumhit = hits:GetEnumerator()
	while (enumhit:MoveNext()) do
		if(not enumhit.Current.collider:GetComponentInParent(global.TriggerBase._type)) then
			if(enumhit.Current.distance < closestdist) then
				closestdist = enumhit.Current.distance
				closestpoint = enumhit.Current.point
			end
		end
	end
	if(closestpoint) then
		closestpoint.y = closestpoint.y + 4
	end
	return closestpoint
end

function PLUGIN:TeleportRay( player )
	local ray = player.eyes:Ray()
	local rotation = player.transform.rotation
	if(not ray) then
		player:ChatMessage("Try again, i couldn't get your eyes!")
		return
	end
	local target = self:GetPoint(ray)
	if(not target) then
		player:ChatMessage("Try again, i couldn't see where you are looking at!")
		return
	end
	self:Teleport( player, target, rotation )
end

function PLUGIN:TeleportForward( player, dist )
	local ray = player.eyes:Ray()
	local rotation = player.transform.rotation
	if(not ray) then
		player:ChatMessage("Try again, i couldn't get your eyes!")
		return
	end
	local target = ray:GetPoint(dist)
	if(not target) then
		player:ChatMessage("Try again, i couldn't see where you are looking at!")
		return
	end
	self:Teleport( player, target, rotation )	
end

function PLUGIN:TeleportUp( player , dist )
	local pos = player:GetComponent("BaseEntity").transform.position
	pos.y = pos.y + dist
	self:Teleport( player, pos, player.transform.rotation )	
end
function PLUGIN:TeleportDown( player , dist )
	local pos = player:GetComponent("BaseEntity").transform.position
	pos.y = pos.y - dist
    self:Teleport( player, pos, player.transform.rotation )
end

function PLUGIN:SendHelpText(player)
    if player:IsAdmin() then
        player:ChatMessage("/pg - To teleport to where you are looking at")
    end
end
