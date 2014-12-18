PLUGIN.Name = "portgun"
PLUGIN.Title       = "Portgun"
PLUGIN.Description = "Teleport to where you are looking at"
PLUGIN.Version     = V(1, 2, 3)
PLUGIN.HasConfig   = true
PLUGIN.Author      = "Reneb"

function PLUGIN:Init()
    command.AddChatCommand( "p",  self.Object, "cmdTeleport" )
	command.AddChatCommand( "pg",  self.Object, "cmdTeleport" )
	command.AddChatCommand( "forward",  self.Object, "cmdForward" )
	command.AddChatCommand( "fw",  self.Object, "cmdForward" )
	command.AddChatCommand( "up",  self.Object, "cmdUp" )
	command.AddChatCommand( "down",  self.Object, "cmdDown" )
	TeleportVectors = {}
end
function PLUGIN:LoadDefaultConfig()
	self.Config.PortgunForModerators = true
end
local function makeTeleportVectors()
	if #TeleportVectors == 0 then
        local coordsArray = util.TableToArray( { 0, 0, 0 } )
        local tempValues = { 
            { x = 2000, y = 0, z = 2000 },
            { x = 2000, y = 0, z = -2000 },
            { x = -2000, y = 0, z = -2000 },
            { x = -2000, y = 0, z = 2000 }
        }

        for k, v in pairs( tempValues ) do
        	util.ConvertAndSetOnArray( coordsArray, 0, v.x, System.Single._type )
        	util.ConvertAndSetOnArray( coordsArray, 1, v.y, System.Single._type )
        	util.ConvertAndSetOnArray( coordsArray, 2, v.z, System.Single._type )
            vector3 = new( UnityEngine.Vector3._type, coordsArray )
            table.insert( TeleportVectors, vector3 )
        end
    end
end

function PLUGIN:Teleport( player, destination, rot )
	if(not preTeleportLocation) then preTeleportLocation = new( UnityEngine.Vector3._type, nil ) end
    if #TeleportVectors == 0 then makeTeleportVectors() end

    for _,vector3 in pairs( TeleportVectors ) do
        if UnityEngine.Vector3.Distance( player.transform.position, vector3 ) > 1000 and UnityEngine.Vector3.Distance( destination, vector3 ) > 1000 then
            preTeleportLocation = vector3
            break
        end
    end
    player.transform.position = preTeleportLocation
    player.transform.rotation = rot;
    player:UpdateNetworkGroup()
    player:UpdatePlayerCollider(true, false)
    destination.y = destination.y + 0.5
    player.transform.position = destination
    player:UpdateNetworkGroup()
    player:UpdatePlayerCollider(true, false)  
    player:StartSleeping()
    player.metabolism:NetworkUpdate()
    player:SendFullSnapshot()
    timer.Once(0.1, function() player:EndSleeping() player.inventory:SendSnapshot() end )
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
		if(enumhit.Current.distance < closestdist) then
			closestdist = enumhit.Current.distance
			closestpoint = enumhit.Current.point
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
