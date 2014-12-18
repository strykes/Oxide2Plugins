PLUGIN.Name = "Arena"
PLUGIN.Title = "Arena"
PLUGIN.Version = V(0, 0, 2)
PLUGIN.Description = "Arena converted from Oxide 1"
PLUGIN.Author = "Reneb - Oxide 1 version by eDeloa"
PLUGIN.HasConfig = true


function PLUGIN:Init()
  -- Load the config file
  --self.Config = {}
  --self:LoadDefaultConfig()
  
  
  self.ArenaData = {}
  self.ArenaData.Games = {}
  self.ArenaData.CurrentGame = nil
  self.ArenaData.Users = {}
  self.ArenaData.UserCount = 0
  self.ArenaData.IsOpen = false
  self.ArenaData.HasStarted = false
  self.ArenaData.HasEnded = false

	local pluginList = plugins.GetAll()
    for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "Spawns Database" then
            spawns_plugin = pluginList[i].Object
            break
        end
    end
    if(not spawns_plugin) then
   	 print("You must have the Spawns Database @ http://forum.rustoxide.com/plugins/spawns-database.720/")
   	 return false
   	end
   	arena_loaded = true
	command.AddChatCommand( "arena_game", self.Object, "cmdArenaGame")
	command.AddChatCommand( "arena_open", self.Object, "cmdArenaOpen")
	command.AddChatCommand( "arena_close", self.Object, "cmdArenaClose")
	command.AddChatCommand( "arena_start", self.Object, "cmdArenaStart")
	command.AddChatCommand( "arena_end", self.Object, "cmdArenaEnd")
	command.AddChatCommand( "arena_spawnfile", self.Object, "cmdArenaSpawnFile")

	command.AddChatCommand( "arena_list", self.Object, "cmdArenaList")
	command.AddChatCommand( "arena_join", self.Object, "cmdArenaJoin")
	command.AddChatCommand( "arena_leave", self.Object, "cmdArenaLeave")
	
	command.AddChatCommand( "arena", self.Object, "cmdArena")
	--command.AddChatCommand( "arena_launch", self.Object, "cmdArenaLaunch")
	--command.AddChatCommand( "arena_stop", self.Object, "cmdArenaStop")
		
	self:LoadArenaSpawnFile(self.Config.SpawnFileName)
end
function PLUGIN:BroadcastChat(msg)
  local netusers = self:GetAllPlayers()
  for k,player in pairs(netusers) do
  	rust.SendChatMessage(player,msg)
  end
end

-- *******************************************
-- CHAT COMMANDS
-- *******************************************
function PLUGIN:cmdArenaList(player, cmd, args)
  rust.SendChatMessage(player, self.Config.ChatName, "Game#         Arena Game")
  rust.SendChatMessage(player, self.Config.ChatName, "---------------------------")
  for i = 1, #self.ArenaData.Games do
    rust.SendChatMessage(player, self.Config.ChatName, "#" .. i .. "                  " .. self.ArenaData.Games[i].GameName)
  end
end

function PLUGIN:cmdArenaGame(player, cmd, args)
  if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
  	rust.SendChatMessage(player, self.Config.ChatName, "This command is restricted")
  	return
  end
  if (args.Length == 0) then
    rust.SendChatMessage(player, self.Config.ChatName, "Syntax: /arena_game {gameID}")
    return
  end
  if (tonumber(args[0])==nil) then
    rust.SendChatMessage(player, self.Config.ChatName, "Syntax: /arena_game {gameID}")
    return
  end

  local success, err = self:SelectArenaGame(tonumber(args[0]))
  if (not success) then
    rust.SendChatMessage(player, self.Config.ChatName, err)
    return
  end
  rust.SendChatMessage(player, self.Config.ChatName, self.ArenaData.Games[self.ArenaData.CurrentGame].GameName .. " is now the next Arena game.")
end

function PLUGIN:cmdArenaOpen(player, cmd, args)
  if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
  	rust.SendChatMessage(player, self.Config.ChatName, "This command is restricted")
  	return
  end
  local success, err = self:OpenArena()
  if (not success) then
    rust.SendChatMessage(player, self.Config.ChatName, err)
    return
  end
end

function PLUGIN:cmdArenaClose(player, cmd, args)
  if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
  	rust.SendChatMessage(player, self.Config.ChatName, "This command is restricted")
  	return
  end
  local success, err = self:CloseArena()
  if (not success) then
    rust.SendChatMessage(player, self.Config.ChatName, err)
    return
  end
end

function PLUGIN:cmdArenaStart(player, cmd, args)
  if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
  	rust.SendChatMessage(player, self.Config.ChatName, "This command is restricted")
  	return
  end
  local success, err = self:StartArena()
  if (not success) then
    rust.SendChatMessage(player, self.Config.ChatName, err)
    return
  end
end

function PLUGIN:cmdArenaEnd(player, cmd, args)
  if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
  	rust.SendChatMessage(player, self.Config.ChatName, "This command is restricted")
  	return
  end
  local success, err = self:EndArena()
  if (not success) then
    rust.SendChatMessage(player, self.Config.ChatName, err)
    return
  end
end

function PLUGIN:cmdArenaSpawnFile(player, cmd, args)
  if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
  	rust.SendChatMessage(player, self.Config.ChatName, "This command is restricted")
  	return
  end
  if (args.Length == 0) then
    rust.SendChatMessage(player, self.Config.ChatName, "Syntax: /arena_spawnfile {filename}")
    return
  end
  
  local success, err = self:LoadArenaSpawnFile(args[0])
  if (not success) then
    rust.SendChatMessage(player, self.Config.ChatName, err)
    return
  end
  
  rust.SendChatMessage(player, self.Config.ChatName, "Successfully loaded the spawn file.")
end

function PLUGIN:cmdArenaJoin(player, cmd, args)
  local success, err = self:JoinArena(player)
  if (not success) then
    rust.SendChatMessage(player, self.Config.ChatName, err)
    return
  end

  rust.SendChatMessage(player, self.Config.ChatName, "Successfully joined the Arena.")
end

function PLUGIN:cmdArenaLeave(player, cmd, args)
  local success, err = self:LeaveArena(player)
  if (not success) then
    rust.SendChatMessage(player, self.Config.ChatName, err)
    return
  end

  rust.SendChatMessage(player, self.Config.ChatName, "Successfully left the Arena.")
end

-- *******************************************
-- API COMMANDS
-- *******************************************
function PLUGIN:RegisterArenaGame(gamename)
  table.insert(self.ArenaData.Games, {GameName = gamename})
  return #(self.ArenaData.Games)
end

-- *******************************************
-- HOOK FUNCTIONS
-- *******************************************

function PLUGIN:pluginsCall(hookCall,args)
	local arr = util.TableToArray( args )
	for i,k in pairs(args) do
		util.ConvertAndSetOnArray(arr, i, k, UnityEngine.Object._type)
	end
	return plugins.CallHook(hookCall, arr )
end

function PLUGIN:OnPlayerSpawn( baseplayer )
	if(not arena_loaded) then return end
	if (self.ArenaData.HasStarted and self:IsPlaying(baseplayer)) then
		timer.Once(0.2, function()
			self:TeleportPlayerToArena(baseplayer) 
			self:pluginsCall("OnArenaSpawnPost",{ baseplayer })
		end)
    end
end
--[[
function PLUGIN:OnRunCommand(arg, wantsfeedback)
	if(not arena_loaded) then return end
    if (not arg) then return end
    if (not arg.connection) then return end
    if (not arg.connection.player) then return end
    if (not arg.cmd) then return end
    if (not arg.cmd.name) then return end
    if(arg.cmd.name ~= "wakeup") then return end
    if(arg.connection.player == nil) then return end
	if(not arg.connection.player:IsSleeping()) then return end
	if(arg.connection.player:IsSpectating()) then return end
	if (self.ArenaData.HasStarted and self:IsPlaying(arg.connection.player)) then
		timer.Once(0.2, function() 
			self:TeleportPlayerToArena(arg.connection.player) 
			self:pluginsCall("OnArenaSpawnPost",{ arg.connection.player })
		end)
    end
	return
end
]]
function PLUGIN:OnPlayerDisconnected(player,connection)
  if (not arena_loaded) then
    return
  end
  if (self:IsPlaying(player)) then
    player:Die()
    self:LeaveArena(player)
  end
end

function PLUGIN:SendHelpText(player)
  if (not arena_loaded) then
    return
  end
  rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_list to list all Arena games.")
  rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_join to join the Arena when it is open.")
  rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_leave to leave the Arena.")
  if (player:GetComponent("BaseNetworkable").net.connection.authLevel > 0) then
    rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_spawnfile {filename} to load a spawnfile.")
    rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_game {gameID} to select an Arena game.")
    rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_open to open the Arena.")
    rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_close to close the Arena entrance.")
    rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_start to start the Arena game.")
    rust.SendChatMessage(player, self.Config.ChatName, "Use /arena_end to end the Arena game.")
  end
end
-- *******************************************
-- MAIN FUNCTIONS
-- *******************************************
function PLUGIN:LoadArenaSpawnFile(filename)
  local spawnsCount, err = spawns_plugin:GetSpawnsCount(filename)
  if (not spawnsCount) then
    return false, err
  end

  self.ArenaData.SpawnsFile = filename
  self.ArenaData.SpawnCount = spawnsCount
  return true
end

function PLUGIN:SelectArenaGame(gameid)
  if (gameid < 1 or gameid > #(self.ArenaData.Games)) then
    return false, "Invalid gameID."
  end

  if (self.ArenaData.IsOpen or self.ArenaData.HasStarted) then
    return false, "The Arena needs to be closed and ended before selecting a new game."
  end

  local success = self:pluginsCall("CanSelectArenaGame",{ gameid })
  if (success ~= "true" and success ~= nil) then
    return false, success
  end
  self.ArenaData.CurrentGame = gameid
  self:pluginsCall("OnSelectArenaGamePost",{ gameid })
  return true
end

function PLUGIN:OpenArena()
  if (not self.ArenaData.CurrentGame) then
    return false, "An Arena game must first be chosen."
  elseif (not self.ArenaData.SpawnsFile) then
    return false, "A spawn file must first be loaded."
  elseif (self.ArenaData.IsOpen) then
    return false, "The Arena is already open."
  end
  local success = self:pluginsCall("CanArenaOpen", { } )
  if (success ~= "true" and success ~= nil) then
    return false, success
  end
  self.ArenaData.IsOpen = true
  self:BroadcastChat("The Arena is now open for: " .. self.ArenaData.Games[self.ArenaData.CurrentGame].GameName .. "!  Type /arena_join to join!")
  self:pluginsCall("OnArenaOpenPost", { } )
  return true
end

function PLUGIN:CloseArena()
  if (not self.ArenaData.IsOpen) then
    return false, "The Arena is already closed."
  end

  local success = self:pluginsCall("CanArenaClose", { } )
  if (success ~= "true" and success ~= nil) then
    return false, success
  end

  self.ArenaData.IsOpen = false
  self:BroadcastChat("The Arena entrance is now closed!")
  self:pluginsCall("OnArenaClosePost", { } )
  return true
end

function PLUGIN:StartArena()
  if (not self.ArenaData.CurrentGame) then
    return false, "An Arena game must first be chosen."
  elseif (not self.ArenaData.SpawnsFile) then
    return false, "A spawn file must first be loaded."
  elseif (self.ArenaData.HasStarted) then
    return false, "An Arena game has already started."
  end

  local success = self:pluginsCall("CanArenaStart", { } )
  if (success ~= "true" and success ~= nil) then
    return false, success
  end
  
  self:pluginsCall("OnArenaStartPre", { } )

  self:BroadcastChat("Arena: " .. self.ArenaData.Games[self.ArenaData.CurrentGame].GameName .. " is about to begin!")
  self.ArenaData.HasStarted = true
  self.ArenaData.HasEnded = false
	
  timer.Once(5, function() self:SaveAllHomeLocations() self:TeleportAllPlayersToArena() self:pluginsCall("OnArenaStartPost", { } ) end)
  return true
end

function PLUGIN:EndArena()
  if (self.ArenaData.HasEnded or ((not self.ArenaData.HasStarted) and (not self.ArenaData.IsOpen))) then
    return false, "An Arena game is not underway."
  end

  local success, err = self:pluginsCall("CanArenaEnd", { } )
  if (success ~= "true" and success ~= nil) then
    return false, success
  end

  self.ArenaData.IsOpen = false
  self.ArenaData.HasEnded = true

  self:pluginsCall("OnArenaEndPre", { } )

  local netusers = self:GetAllPlayers()
  for k,player in pairs(netusers) do
    if (self:IsPlaying(player)) then
      self:LeaveArena(player)
    end
  end

  self:BroadcastChat("Arena: " .. self.ArenaData.Games[self.ArenaData.CurrentGame].GameName .. " is now over!")
  self.ArenaData.HasStarted = false
  self:pluginsCall("OnArenaEndPost", { } )
  return true
end

function PLUGIN:JoinArena(player)
  if (not self.ArenaData.IsOpen) then
    return false, "The Arena is currently closed."
  elseif (self:IsPlaying(player)) then
    return false, "You are already in the Arena."
  end

  local success, err = self:pluginsCall("CanArenaJoin", { player } )
  if (success ~= "true" and success ~= nil) then
    return false, success
  end
  self.ArenaData.Users[rust.UserIDFromPlayer( player )] = {}
  self.ArenaData.Users[rust.UserIDFromPlayer( player )].HasJoined = true
  self.ArenaData.UserCount = self.ArenaData.UserCount + 1

  if (self.ArenaData.HasStarted) then
    self:SaveHomeLocation(player)
  end
  
  self:BroadcastChat(player.displayName .. " has joined the Arena!  (Total Players: " .. self.ArenaData.UserCount .. ")")
  self:pluginsCall("OnArenaJoinPost", { player } )
  return true
end

function PLUGIN:LeaveArena(player)
  if (not self:IsPlaying(player)) then
    return false, "You are not currently in the Arena."
  end

  self.ArenaData.UserCount = self.ArenaData.UserCount - 1

  if (not self.ArenaData.HasEnded) then
    self:BroadcastChat(player.displayName .. " has left the Arena!  (Total Players: " .. self.ArenaData.UserCount .. ")")
  end

  if (self.ArenaData.HasStarted) then
    self:TeleportPlayerHome(player)
    self.ArenaData.Users[rust.UserIDFromPlayer( player )] = nil
    self:pluginsCall("OnArenaLeavePost", { player } )
  else
    self.ArenaData.Users[rust.UserIDFromPlayer( player )] = nil
  end

  return true
end

-- *******************************************
-- HELPER FUNCTIONS
-- *******************************************
function PLUGIN:GetAllPlayers()
    itPlayerList = global.BasePlayer.activePlayerList:GetEnumerator()
    playerList = {}
    while itPlayerList:MoveNext() do
        table.insert(playerList,itPlayerList.Current)
    end
    return playerList
end


function PLUGIN:LoadDefaultConfig()
  -- Set default configuration settings
  self.Config.ChatName = "Arena"
  self.Config.SpawnFileName = ""
  self.Config.authLevel = 1
  self.Config.AutoArena_Settings = {}
  self.Config.AutoArena_Settings.IntervalTime = 1800
  self.Config.AutoArena_Settings.WaitTimeBeforeStart = 60
  self.Config.AutoArena_Settings.AutoDisable = 300
end

function PLUGIN:IsPlaying(player)
  local userID = rust.UserIDFromPlayer( player )
  return (self.ArenaData.Users[userID] and self.ArenaData.Users[userID].HasJoined)
end

function PLUGIN:SaveAllHomeLocations()
  local netusers =  self:GetAllPlayers()
  for k,player in pairs(netusers) do
    if (self:IsPlaying(player)) then
      self:SaveHomeLocation(player)
    end
  end
end

function PLUGIN:SaveHomeLocation(player)
  local userID = rust.UserIDFromPlayer( player )
  local homePos = player.transform.position
  self.ArenaData.Users[userID].HomeCoords = {}
  self.ArenaData.Users[userID].HomeCoords.x = homePos.x
  self.ArenaData.Users[userID].HomeCoords.y = homePos.y
  self.ArenaData.Users[userID].HomeCoords.z = homePos.z
end

function PLUGIN:KillAllPlayers()
  local netusers = self:GetAllPlayers()
  for k,player in pairs(netusers) do
    if (self:IsPlaying(player)) then
      self:KillPlayer(player)
    end
  end
end

function PLUGIN:KillPlayer(player)
  player:Die()
end

function PLUGIN:TeleportAllPlayersToArena()
  local netusers = self:GetAllPlayers()
  for k,player in pairs(netusers) do
    if (self:IsPlaying(player)) then
      self:TeleportPlayerToArena(player)
    end
  end
end


local function makeTeleportVectors()
	if (TeleportVectors == nil or (TeleportVectors and #TeleportVectors == 0)) then
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
function PLUGIN:TeleportPlayerToArena(player)
  if(not newVector3) then newVector3 = new( UnityEngine.Vector3._type , nil ) end
  
  local spawnPoint, err = spawns_plugin:GetRandomSpawn( self.ArenaData.SpawnsFile, self.ArenaData.SpawnCount )
  if(not spawnPoint) then self:BroadcastChat(err) end
  newVector3.x = spawnPoint.x
  newVector3.y = spawnPoint.y
  newVector3.z = spawnPoint.z
  self:TeleportPlayer(player, newVector3)
end

function PLUGIN:TeleportPlayerHome(player)
	if(not newVector3) then newVector3 = new( UnityEngine.Vector3._type , nil ) end
	
    local userID = rust.UserIDFromPlayer(player)
    if (self.ArenaData.Users[userID] and self.ArenaData.Users[userID].HomeCoords) then
    newVector3.x = self.ArenaData.Users[userID].HomeCoords.x
    newVector3.y = self.ArenaData.Users[userID].HomeCoords.y
    newVector3.z = self.ArenaData.Users[userID].HomeCoords.z
    self:TeleportPlayer(player, newVector3)
  end
end

function PLUGIN:TeleportPlayer( player, destination )
	if(not preTeleportLocation) then preTeleportLocation = new( UnityEngine.Vector3._type, nil ) end
    if (TeleportVectors == nil or (TeleportVectors and #TeleportVectors == 0)) then makeTeleportVectors() end
    for _,vector3 in pairs( TeleportVectors ) do
        if UnityEngine.Vector3.Distance( player.transform.position, vector3 ) > 1000 and UnityEngine.Vector3.Distance( destination, vector3 ) > 1000 then
            preTeleportLocation = vector3
            break
        end
    end
    player.transform.position = preTeleportLocation
    player:UpdateNetworkGroup()
    player:UpdatePlayerCollider(true, false)
    destination.y = destination.y + 0.1
    player.transform.position = destination
    player:UpdateNetworkGroup()
    player:UpdatePlayerCollider(true, false)  
    player:StartSleeping()
    player.metabolism:NetworkUpdate()
    player:SendFullSnapshot()
    timer.Once(0.1, function()
    	player:EndSleeping() 
    	player.inventory:SendSnapshot()
    end)
end

function PLUGIN:cmdArena(player, cmd, args)
	if(self.ArenaData.HasStarted) then
		if(self.ArenaData.IsOpen) then
			rust.SendChatMessage(player, self.Config.ChatName, "The Arena has already started but may still join. (/arena_join)" )
			rust.SendChatMessage(player, self.Config.ChatName, "Players ingame: " .. self.ArenaData.UserCount )
		else
			rust.SendChatMessage(player, self.Config.ChatName, "The Arena has already started and you may not join anymore." )
			rust.SendChatMessage(player, self.Config.ChatName, "Players ingame: " .. self.ArenaData.UserCount )
		end
		return
	else
		if(self.ArenaData.IsOpen) then
			rust.SendChatMessage(player, self.Config.ChatName, "The Arena is Opened, say /arena_join to join" )
			rust.SendChatMessage(player, self.Config.ChatName, "Currently " .. self.ArenaData.UserCount .. " players listed")
			return
		else
			rust.SendChatMessage(player, self.Config.ChatName, "There is no Arenas at the moment" )
			--rust.SendChatMessage(player, self.Config.ChatName, "Next Arena is in: " .. self.Config.Timer - self.ArenaTock .. "secs" )
			return
		end
	end
	return
end

function PLUGIN:BroadcastToPlayers(message)
  netusers = self:GetAllPlayers()
  if (netusers) then
    for k,player in pairs(netusers) do
      if (self:IsPlaying(player)) then
        rust.SendChatMessage(player, self.Config.ChatName, message)
      end
    end
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
					return false, "Multiple Players Found"
				end
				targetplayer = currentplayer
			end
		end
	end
	return targetplayer
end


