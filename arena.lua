PLUGIN.Name = "Arena"
PLUGIN.Title = "Arena"
PLUGIN.Version = V(1, 0, 0)
PLUGIN.Description = "Arena converted from Oxide 1"
PLUGIN.Author = "Reneb - Oxide 1 version by eDeloa"
PLUGIN.HasConfig = true

local DataFile = "arena"
local Data = {}

function PLUGIN:Init()
  
  self:LoadDataFile()
  
  self.ArenaData = {}
  self.ArenaData.Games = {}
  self.ArenaData.CurrentGame = nil
  self.ArenaData.Users = {}
  self.ArenaData.UserCount = 0
  self.ArenaData.IsOpen = false
  self.ArenaData.HasStarted = false
  self.ArenaData.HasEnded = false
  self.ArenaData.AutoArena = false
  self.ArenaTimers = {}
  command.AddChatCommand( "arena", self.Object, "cmdArena")
end

function PLUGIN:OnServerInitialized()
	local pluginList = plugins.GetAll()
    for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "Spawns Database" then
            spawns_plugin = pluginList[i].Object
            break
        end
    end
    arena_loaded = false
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
	
	command.AddChatCommand( "arena_launch", self.Object, "cmdArenaLaunch")
	command.AddChatCommand( "arena_stop", self.Object, "cmdArenaStop")
	
	command.AddChatCommand( "arena_reward", self.Object, "cmdArenaReward")
	command.AddChatCommand( "arena_givereward", self.Object, "cmdArenaGiveReward")
	
	timer.Once(0.1, function()
		if(self.Config.Default.SpawnFileName  ~= "") then	
			self:LoadArenaSpawnFile(self.Config.Default.SpawnFileName)
		end
		if(self.Config.Default.GameName ~= "") then	
			for i=1, #self.ArenaData.Games do
				if(self.ArenaData.Games[i].GameName == self.Config.Default.GameName) then
					success, err = self:SelectArenaGame(i)
					if(not success) then
						print(tostring(err))
					end
					break
				end
			end
		end
	end)
end

-- *******************************************
-- PLUGIN:Unload()
-- Called when the plugin is reloaded
-- *******************************************
function PLUGIN:Unload()
	self:resetTimers()
end

function PLUGIN:resetTimers()
	for k,v in pairs (self.ArenaTimers) do
		self.ArenaTimers[k]:Destroy()
	end
end

-- *******************************************
-- PLUGIN:LoadDataFile()
-- Load data files from oxide/data/DATAFILE.json
-- *******************************************
function PLUGIN:LoadDataFile()
    local data = datafile.GetDataTable(DataFile)
    Data = data or {}
    Data.Rewards = Data.Rewards or {}
end
function PLUGIN:SaveData()
    datafile.SaveDataTable(DataFile)
end

-- *******************************************
-- CHAT COMMANDS
-- *******************************************

function PLUGIN:cmdArena(player, cmd, args)
	if(not arena_loaded) then
		rust.SendChatMessage(player, self.Config.ChatName, "The Arena plugin was not successfully loaded, you probably forgot the Spawns Plugin")
		return
	end
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
			if(self.ArenaData.AutoArena and self.ArenaTimers["NextArena"]) then
				rust.SendChatMessage(player, self.Config.ChatName, "Next Arena is in: " .. self.ArenaTimers["NextArena"].Delay)
			else
				rust.SendChatMessage(player, self.Config.ChatName, "There is no Arenas at the moment" )
			end
			return
		end
	end
	return
end

function PLUGIN:cmdArenaList(player, cmd, args)
	rust.SendChatMessage(player, self.Config.ChatName, "Game#         Arena Game")
	rust.SendChatMessage(player, self.Config.ChatName, "---------------------------")
	for i = 1, #self.ArenaData.Games do
		rust.SendChatMessage(player, self.Config.ChatName, "#" .. i .. "                  " .. self.ArenaData.Games[i].GameName)
	end
end

function PLUGIN:cmdArenaGame(player, cmd, args)
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
		rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
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
		rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
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
		rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
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
		rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
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
		rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
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
		rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
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
function PLUGIN:cmdArenaLaunch(player,cmd,args)
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
		rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
		return
	end
	if (not self.ArenaData.CurrentGame) then
		rust.SendChatMessage(player, self.Config.ChatName, "An Arena game must first be chosen.")
		return
	end
	allowed = true
	if(tonumber(self:pluginsCall("AutoArenaConfig",{"MinimumPlayers"})) == nil) then allowed = false end
	if(tonumber(self:pluginsCall("AutoArenaConfig",{"MaximumPlayers"})) == nil) then allowed = false end
	if(tonumber(self:pluginsCall("AutoArenaConfig",{"CancelArenaTime"})) == nil) then allowed = false end
	if(tonumber(self:pluginsCall("AutoArenaConfig",{"WaitToStartTime"})) == nil) then allowed = false end
	if(tostring(self:pluginsCall("AutoArenaConfig",{"CloseOnStart"})) == "nil") then allowed = false end
	if(tonumber(self:pluginsCall("AutoArenaConfig",{"ArenasInterval"})) == nil) then allowed = false end
	if(tonumber(self:pluginsCall("AutoArenaConfig",{"ArenaLimitTime"})) == nil) then allowed = false end
	if(not allowed) then
		rust.SendChatMessage(player, self.Config.ChatName, "The Mod that you tried to start was not properly made as an Auto Arena, configs are missing.")
		return
	end
	self.ArenaData.AutoArena = true
	success, err = self:OpenArena()
	if (not success) then
		self.ArenaData.AutoArena = false
		rust.SendChatMessage(player, self.Config.ChatName, err)
		return
	end
	rust.SendChatMessage(player, self.Config.ChatName, "The Arena was successfully launched, and will work on its own.")
end
function PLUGIN:cmdArenaStop(player,cmd,args)
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
		rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
		return
	end
	if(not self.ArenaData.AutoArena) then
		rust.SendChatMessage(player, self.Config.ChatName, "The AutoArena wasn't launched.")
		return
	end
	self.ArenaData.AutoArena = false
	self:resetTimers()
	rust.SendChatMessage(player, self.Config.ChatName, "The AutoArena was deactivated.")
end
function PLUGIN:cmdArenaReward(player,cmd,args)
	cuserid = rust.UserIDFromPlayer(player)
	if(not Data.Rewards[cuserid]) then 
		rust.SendChatMessage(player, self.Config.ChatName, "You have 0 rewards waiting")
		return
	end
	count = 0
	for i=1, #Data.Rewards[cuserid] do
		count = count + 1
	end
	rust.SendChatMessage(player, self.Config.ChatName, "You have " .. count .. " rewards waiting")
	todel = {}
	for i=1, #Data.Rewards[cuserid] do
		if(self:pluginsCall("isRewardRandom",{ Data.Rewards[cuserid][i] })) then
			self:pluginsCall("giveRandomReward",{ player, Data.Rewards[cuserid][i] })
			table.insert(todel,i)
			rust.SendChatMessage(player, self.Config.ChatName, "You've received a random reward from the \"" .. Data.Rewards[cuserid][i] .. "\" game.")
		else
			if(args.Length == 0) then
				rust.SendChatMessage(player, self.Config.ChatName, Data.Rewards[cuserid][i] .. ": Choose a Reward between: " .. tostring(self:pluginsCall("OnRewardGetList",{ Data.Rewards[cuserid][i] })))
			else
				trygiveReward = self:pluginsCall("giveSpecificReward",{ player, Data.Rewards[cuserid][i], args[0] })
				if(trygiveReward == "true") then
					rust.SendChatMessage(player, self.Config.ChatName, "You've received your reward from the \"" .. Data.Rewards[cuserid][i] .. "\" game.")
					table.insert(todel,i)
					break
				end
			end
		end
	end
	for o = #todel,1, -1 do
		table.remove(Data.Rewards[cuserid],todel[o])
	end
	self:SaveData()
end
function PLUGIN:cmdArenaGiveReward(player,cmd,args)
  if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
  	rust.SendChatMessage(player, self.Config.ChatName, "You are not allowed to use this command")
  	return
  end
  if(args.Length == 0) then
  	rust.SendChatMessage(player, self.Config.ChatName, "/arena_givereward \"PLAYER\" \"ARENAGAME\"")
  	return
  end
  if(args.Length == 1) then
	rust.SendChatMessage(player, self.Config.ChatName, "/arena_givereward \"PLAYER\" \"ARENAGAME\"")
	for i = 1, #self.ArenaData.Games do
      rust.SendChatMessage(player, self.Config.ChatName, "/arena_givereward \"PLAYER\" \"" .. self.ArenaData.Games[i].GameName .. "\"")
  	end
	return
  end
  targetPlayer, err = self:FindPlayer(args[0])
  if(not targetPlayer) then rust.SendChatMessage(player,self.Config.ChatName,err) return end
  targetGame = false
  for i = 1, #self.ArenaData.Games do
  	if(self.ArenaData.Games[i].GameName == args[1]) then
  	  targetGame = true
  	  break
  	end
  end
  if(not targetGame) then rust.SendChatMessage(player,self.Config.ChatName,"This GameName doesn't exist") return end
  self:GiveReward(targetPlayer,args[1])
  rust.SendChatMessage(player,self.Config.ChatName,"Reward from " .. args[1] .. " was successfully given to " .. targetPlayer.displayName)
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
  rust.BroadcastChat(self.Config.ChatName,"The Arena is now open for: " .. self.ArenaData.Games[self.ArenaData.CurrentGame].GameName .. "!  Type /arena_join to join!")
  self:pluginsCall("OnArenaOpenPost", { } )
  
  if(self.ArenaData.AutoArena) then
  	self:resetTimers()
  	self.ArenaTimers["CancelArena"] = timer.Once(tonumber(self:pluginsCall("AutoArenaConfig",{"CancelArenaTime"})), function() self:CloseArena() end)
  end
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
  self:pluginsCall("OnArenaClosePost", { } )
  if(self.ArenaData.HasStarted) then
 	 rust.BroadcastChat(self.Config.ChatName,"The Arena entrance is now closed!")
  else
     rust.BroadcastChat(self.Config.ChatName,"The Arena was cancelled!")
     if(self.ArenaData.AutoArena) then
     	self:resetTimers()
     	self.ArenaTimers["NextArena"] = timer.Once(tonumber(self:pluginsCall("AutoArenaConfig",{"ArenasInterval"})), function() self:OpenArena() end)
     	rust.BroadcastChat(self.Config.ChatName,"Next Arena will be in " .. tonumber(self:pluginsCall("AutoArenaConfig",{"ArenasInterval"})) .. " seconds")
     end
  end
  
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

  rust.BroadcastChat(self.Config.ChatName,"Arena: " .. self.ArenaData.Games[self.ArenaData.CurrentGame].GameName .. " is about to begin!")
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

  rust.BroadcastChat(self.Config.ChatName,"Arena: " .. self.ArenaData.Games[self.ArenaData.CurrentGame].GameName .. " is now over!")
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

  local success = self:pluginsCall("CanArenaJoin", { player } )
  if (success ~= "true" and success ~= nil) then
    return false, success
  end
  self.ArenaData.Users[rust.UserIDFromPlayer( player )] = {}
  self.ArenaData.Users[rust.UserIDFromPlayer( player )].HasJoined = true
  self.ArenaData.UserCount = self.ArenaData.UserCount + 1

  if (self.ArenaData.HasStarted) then
    self:SaveHomeLocation(player)
  end
  
  rust.BroadcastChat(self.Config.ChatName,player.displayName .. " has joined the Arena!  (Total Players: " .. self.ArenaData.UserCount .. ")")
  self:pluginsCall("OnArenaJoinPost", { player } )
  return true
end

function PLUGIN:LeaveArena(player)
  if (not self:IsPlaying(player)) then
    return false, "You are not currently in the Arena."
  end

  self.ArenaData.UserCount = self.ArenaData.UserCount - 1

  if (not self.ArenaData.HasEnded) then
    rust.BroadcastChat(self.Config.ChatName,player.displayName .. " has left the Arena!  (Total Players: " .. self.ArenaData.UserCount .. ")")
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

function PLUGIN:CanArenaJoin(player)
	if(self.ArenaData.AutoArena) then
		if(self.ArenaData.UserCount >= tonumber(self:pluginsCall("AutoArenaConfig",{"MaximumPlayers"}))) then
			return "Max players reached"
		end
	end
end
function PLUGIN:OnArenaJoinPost(player)
	if(self.ArenaData.AutoArena) then
		if(self.ArenaData.UserCount >= tonumber(self:pluginsCall("AutoArenaConfig",{"MinimumPlayers"}))) then
			self:resetTimers()
			self.ArenaTimers["StartArena"] = timer.Once(tonumber(self:pluginsCall("AutoArenaConfig",{"WaitToStartTime"})), function()
				success, err = self:StartArena() 
				if(not success) then
					rust.BroadcastChat(self.Config.ChatName,err)
				end
			end)
		end
	end
end
function PLUGIN:OnArenaStartPost()
	if(self.ArenaData.AutoArena) then
		self:resetTimers()
		self.ArenaTimers["EndArena"] = timer.Once(tonumber(self:pluginsCall("AutoArenaConfig",{"ArenaLimitTime"})), function() self:EndArena() end)
	end
end
function PLUGIN:OnArenaEndPost()
	if(self.ArenaData.AutoArena) then
		self:resetTimers()
		self.ArenaTimers["NextArena"] = timer.Once(tonumber(self:pluginsCall("AutoArenaConfig",{"ArenasInterval"})), function() self:OpenArena() end)
	end
end

function PLUGIN:GiveReward(player,cgame)
	cuserid = rust.UserIDFromPlayer(player)
	if(not Data.Rewards[cuserid]) then Data.Rewards[cuserid] = {} end
	table.insert(Data.Rewards[cuserid],cgame)
	rust.SendChatMessage(player,self.Config.ChatName,"You have won a reward, say /arena_reward to get more informations")
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
  self.Config.Default = {}
  self.Config.Default.SpawnFileName = ""
  self.Config.Default.GameName = "Deathmatch"
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
		TeleportVectors = {}
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
  if(not spawnPoint) then rust.BroadcastChat(self.Config.ChatName,err) end
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
    	player.inventory:SendSnapshot()
    end)
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
	if(not targetplayer) then return false, "No players found" end
	return targetplayer
end


