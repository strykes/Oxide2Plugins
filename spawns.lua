PLUGIN.Name = "Spawns Database"
PLUGIN.Title = "Spawns Database"
PLUGIN.Version = V(1, 0, 3)
PLUGIN.Description = "Set Custom Spawns"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true
---------- TO DO LIST ----------
-- /spawns_list --
-- /spawns_tp NUMBER - to teleport to the spawns number (good to see which one to remove or not)
--------------------------------
function PLUGIN:Init()
	self:InitializeCommands()
	self.Data = {}
	LoadedSpawns = {}
end
function PLUGIN:InitializeCommands()
	command.AddChatCommand( "spawns_new",  self.Object, "cmdSpawnsNew" )
	command.AddChatCommand( "spawns_add",  self.Object, "cmdSpawnsAdd" )
	command.AddChatCommand( "spawns_remove",  self.Object, "cmdSpawnsRemove" )
	command.AddChatCommand( "spawns_save",  self.Object, "cmdSpawnsSave" )
	command.AddChatCommand( "spawns_open",  self.Object, "cmdSpawnsOpen" )
	command.AddChatCommand( "spawns_close",  self.Object, "cmdSpawnsClose" )
	command.AddChatCommand( "spawns_help",  self.Object, "cmdSpawnsHelp" )
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. msg .. "\"" );
end
function PLUGIN:LoadDefaultConfig()
	self.Config.Settings = {}
	self.Config.Settings.authLevel = 1
	self.Config.Messages = {}
	self.Config.Messages.NotAllowed = "You are not allowed to use this command"
	self.Config.Messages.AlreadyCreatingSpawnFile = "You are already creating a spawn file"
	self.Config.Messages.CreatingANewSpawnFile = "You are now creating a new spawn file"
	self.Config.Messages.NotMakingASpawnFile = "You need to start a new spawnfile first: /spawns_new"
	self.Config.Messages.SuccessfullyAddASpawn = "You have successfully added a spawn point"
	self.Config.Messages.ErrorTryAgain = "An Error occured, please try again."
	self.Config.Messages.YouNeedToSetANumber = "You need to set a number"
	self.Config.Messages.NumberOutOfRange = "This number is out of range"
	self.Config.Messages.SuccessfullyRemovedASpawn = "You have successfully removed a spawn point"
	self.Config.Messages.SuccessfullyClosed = "You have successfully closed the spawnfile without saving"
	self.Config.Messages.YouNeedToSetAFileName = "You need to set a filename"
	self.Config.Messages.NoSpawnsSet = "No Spawns were set"
	self.Config.Messages.SuccessfullySavedFile = "You have successfully saved the file"
	self.Config.Messages.SpawnFileIsEmpty = "Spawnfile is Empty"
	self.Config.Messages.SuccessfullyOpenedFile = "You have successfully opened the file"
	self.Config.Messages.Help = {}
	self.Config.Messages.Help[1] = "Start by making a new data with: /spawns_new"
	self.Config.Messages.Help[2] = "Add new spawn points where you are standing with /spawns_add"
	self.Config.Messages.Help[3] = "Remove a spawn point that you didn't like with /spawns_remove NUMBER"
	self.Config.Messages.Help[4] = "Save the spawn points into a file with: /spawns_save FILENAME"
	self.Config.Messages.Help[5] = "Use /spawns_open later on to open it back and edit it"
	self.Config.Messages.Help[6] = "Use /spawns_close to stop setting points without saving"
end

function PLUGIN:cmdSpawnsNew( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then ChatMessage(player, self.Config.Messages.NotAllowed ) return end
	if(self.Data[player]) then ChatMessage(player, self.Config.Messages.AlreadyCreatingSpawnFile ) return end
	self.Data[player] = {}
	ChatMessage(player, self.Config.Messages.CreatingANewSpawnFile )
end
function PLUGIN:cmdSpawnsOpen( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then ChatMessage(player, self.Config.Messages.NotAllowed ) return end
	if(self.Data[player]) then ChatMessage(player, self.Config.Messages.AlreadyCreatingSpawnFile ) return end
	if(args.Length == 0) then ChatMessage(player, self.Config.Messages.YouNeedToSetAFileName ) return end
	local DataFile = datafile.GetDataTable( tostring(args[0]) )
	DataFile = DataFile or {}
	local empty = true
	self.Data[player] = {}
	for k,v in pairs( DataFile ) do
		if(k and v) then 
			self.Data[player][tonumber(k)] = v
			empty = false 
		end
	end
	if(empty) then ChatMessage(player, self.Config.Messages.SpawnFileIsEmpty .. " ("..args[0]..")" ) return end
	ChatMessage(player, self.Config.Messages.SuccessfullyOpenedFile .. " ("..args[0]..")" )
end

function PLUGIN:cmdSpawnsAdd( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then ChatMessage(player, self.Config.Messages.NotAllowed ) return end
	if(not self.Data[player]) then ChatMessage(player, self.Config.Messages.NotMakingASpawnFile ) return end
	if(not player.transform or (player.transform and not player.transform.position)) then ChatMessage(player, self.Config.Messages.ErrorTryAgain ) return end
	local coords = {}
	coords["x"] = math.ceil( (player.transform.position.x)*100)/100
	coords["y"] = math.ceil( player.transform.position.y )
	coords["z"] = math.ceil( (player.transform.position.z)*100)/100
	table.insert(self.Data[player],coords)
	ChatMessage(player, self.Config.Messages.SuccessfullyAddASpawn .. " (n°" .. #self.Data[player] .. ")")
end

function PLUGIN:cmdSpawnsRemove( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then ChatMessage(player, self.Config.Messages.NotAllowed ) return end
	if(not self.Data[player]) then ChatMessage(player, self.Config.Messages.NotMakingASpawnFile ) return end
	if(args.Length == 0) then ChatMessage(player, self.Config.Messages.YouNeedToSetANumber ) return end
	if(tonumber(args[0])==nil) then ChatMessage(player, self.Config.Messages.YouNeedToSetANumber ) return end
	if(tonumber(args[0]) > #self.Data[player]) then ChatMessage(player, self.Config.Messages.NumberOutOfRange ) return end
	table.remove(self.Data[player],tonumber(args[0]))
	ChatMessage(player, self.Config.Messages.SuccessfullyRemovedASpawn .. " (n°" .. args[0] .. ")")
end

function PLUGIN:cmdSpawnsSave( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then ChatMessage(player, self.Config.Messages.NotAllowed ) return end
	if(not self.Data[player]) then ChatMessage(player, self.Config.Messages.NotMakingASpawnFile ) return end
	if(#self.Data[player]==0) then ChatMessage(player, self.Config.Messages.NoSpawnsSet ) return end
	if(args.Length == 0) then ChatMessage(player, self.Config.Messages.YouNeedToSetAFileName ) return end
	local DataFile = datafile.GetDataTable( tostring(args[0]) )
	DataFile = DataFile or {}
	for i,d in pairs(DataFile) do
		DataFile[i] = nil
	end
	for i=1, #self.Data[player] do
		DataFile[tostring(i)] = self.Data[player][i]
	end 
	datafile.SaveDataTable( tostring(args[0]) )
	if(LoadedSpawns[tostring(args[0])]) then LoadedSpawns[tostring(args[0])] = false end
	self.Data[player] = nil
	ChatMessage(player, self.Config.Messages.SuccessfullySavedFile .. " (" .. args[0] .. ".json)")
end

function PLUGIN:cmdSpawnsClose( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then ChatMessage(player, self.Config.Messages.NotAllowed ) return end
	if(not self.Data[player]) then ChatMessage(player, self.Config.Messages.NotMakingASpawnFile ) return end
	self.Data[player] = nil
	ChatMessage(player, self.Config.Messages.SuccessfullyClosed )
end

function PLUGIN:GetSpawnsCount( filename )
	local count = 0
	local DataFile = datafile.GetDataTable( filename )
	DataFile = DataFile or {}
	for k,v in pairs( DataFile ) do
		if(k and v) then 
			count = count + 1
		end
	end
	if(count == 0) then return false, "This file doesn't exist or is empty" end
	return count
end
local function loadSpawnfile( filename )
	local DataFile = datafile.GetDataTable( filename )
	DataFile = DataFile or {}
	local empty = true
	for k,v in pairs( DataFile ) do
		if(k and v) then
			empty = false
			break
		end
	end
	if(empty) then return false, "This file doesn't exist or is empty" end
	LoadedSpawns[filename] = {}

	for k,v in pairs( DataFile ) do
		if(k and v) then
			LoadedSpawns[filename][tonumber(k)] = v
		end
	end
	return true
end
function PLUGIN:GetRandomSpawn( filename , max )
	if(not LoadedSpawns[filename]) then
		local success, err = loadSpawnfile(filename)
		if(not success) then return false, err end
	end
	if(not LoadedSpawns[filename][max]) then return false, "This spawn number is out of range" end
	return LoadedSpawns[filename][math.random(max)]
end
function PLUGIN:GetRandomSpawnVector3( filename , max )
	if(not LoadedSpawns[filename]) then
		local success, err = loadSpawnfile(filename)
		if(not success) then return false, err end
	end
	if(not LoadedSpawns[filename][max]) then return false, "This spawn number is out of range" end
	newPos = new( UnityEngine.Vector3._type, nil);
	spawndata = LoadedSpawns[filename][math.random(max)]
	newPos.x = spawndata.x
	newPos.y = spawndata.y
	newPos.z = spawndata.z
	return newPos
end
function PLUGIN:GetSpawn( filename , number )
	if(not LoadedSpawns[filename]) then
		local success, err = loadSpawnfile(filename)
		if(not success) then return false, err end
	end
	if(not LoadedSpawns[filename][number]) then return false, "This spawn number is out of range" end
	return LoadedSpawns[filename][number]
end

function PLUGIN:OnPlayerDisconnected(player,connection)
	if (self.Data[player]) then
		self.Data[player] = nil
	end
end
function PLUGIN:cmdSpawnsHelp( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then ChatMessage(player, self.Config.Messages.NotAllowed ) return end
	for i=1, #self.Config.Messages.Help do
		ChatMessage(player, self.Config.Messages.Help[i] )
	end
end
