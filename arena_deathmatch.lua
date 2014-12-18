PLUGIN.Name = "Arena: Deathmatch"
PLUGIN.Title = "Nonstop Arena deathmatch"
PLUGIN.Version = V(0, 0, 1)
PLUGIN.Description = "Arena Deathmatch converted from Oxide 1"
PLUGIN.Author = "Reneb - Oxide 1 version by eDeloa"
PLUGIN.HasConfig = false

function PLUGIN:Init()

	local pluginList = plugins.GetAll()
    for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "Spawns Database" then
            spawns_plugin = pluginList[i].Object
        elseif pluginTitle == "Arena" then
        	arena_plugin = pluginList[i].Object
        end
    end
    self.DeathmatchData = {}
	self.DeathmatchData.Users = {}
	self.DeathmatchData.IsChosen = false
	self.DeathmatchData.HasStarted = false
	self.DeathmatchData.CustomPack = 0
	   
    if(not spawns_plugin or not arena_plugin) then
   	  print("You must have the Spawns Database @ http://forum.rustoxide.com/plugins/spawns-database.720/")
   	  print("You must have the Arena Plugin")
	  arena_loaded = false
	  return
   	else
   		arena_loaded = true
   	end
   	
    self.Config = {}
    self:LoadDefaultConfig()
    
    command.AddChatCommand( "deathmatch_pack", self.Object, "cmdDeathmatchPack")

	self.DeathmatchData.GameID = arena_plugin:RegisterArenaGame("Deathmatch")
  
  

end
function PLUGIN:BroadcastChat(msg)
  local netusers = arena_plugin:GetAllPlayers()
  for k,player in pairs(netusers) do
  	rust.SendChatMessage(player,msg)
  end
end  
-- *******************************************
-- CHAT FUNCTIONS
-- *******************************************
function PLUGIN:cmdDeathmatchPack(player, cmd, args)
  if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel) then
  	rust.SendChatMessage(player, self.Config.ChatName, "This command is restricted")
  	return
  end
  pack = tonumber(args[1])
  if (not pack) then
    rust.SendChatMessage(player, "Deathmatch", "Syntax: /deathmatch_pack {packNumber (0 = default)}")
    return
  end

  if (pack == 0) then
    self.DeathmatchData.CustomPack = 0
    rust.SendChatMessage(player, "Deathmatch", "Default pack settings loaded.")
  elseif (pack > 0 and pack <= #self.Config.Packs) then
    self.DeathmatchData.CustomPack = pack
    rust.SendChatMessage(player, "Deathmatch", "Custom Deathmatch pack selected.")
  else
    rust.SendChatMessage(player, "Deathmatch", "Specified pack number out of bounds.")
  end
end

-- *******************************************
-- ARENA HOOK FUNCTIONS
-- *******************************************
function PLUGIN:CanSelectArenaGame(gameid)
  if (gameid == self.DeathmatchData.GameID) then
    return "true"
  end
end

function PLUGIN:OnSelectArenaGamePost(gameid)
  if (gameid == self.DeathmatchData.GameID) then
    self.DeathmatchData.IsChosen = true
  else
    self.DeathmatchData.IsChosen = false
  end
end

function PLUGIN:CanArenaOpen()
  if (self.DeathmatchData.IsChosen) then
    return "true"
  end
end

function PLUGIN:OnArenaOpenPost()
  if (self.DeathmatchData.IsChosen) then
    arena_plugin:BroadcastChat("In Deathmatch, your inventory WILL be lost!  Do not join until you have put away your items!")
  end
end

function PLUGIN:CanArenaClose()
  if (self.DeathmatchData.IsChosen) then
    return "true"
  end
end

function PLUGIN:OnArenaClosePost()
end

function PLUGIN:CanArenaStart()
  if (self.DeathmatchData.IsChosen) then
    return "true"
  end
end

function PLUGIN:ArenaHasMinimum(currentNumber)
  if (self.DeathmatchData.IsChosen) then
  	if(currentNumber >= self.Config.AutoArena_Settings.MinimumPlayers) then
    	return "true"
    end
  end
end

function PLUGIN:ArenaHasMaximum(currentNumber)
  if (self.DeathmatchData.IsChosen) then
  	if(currentNumber >= self.Config.AutoArena_Settings.MaximumPlayers) then
    	return "true"
    end
  end
end

-- *******************************************
-- Called after everyone has been teleported into the Arena
-- *******************************************
function PLUGIN:OnArenaStartPost()
  if (self.DeathmatchData.IsChosen) then
    self.DeathmatchData.HasStarted = true
    self:EquipAllPlayers()
  end
end

function PLUGIN:CanArenaEnd()
  if (self.DeathmatchData.IsChosen) then
    return "true"
  end
end

-- *******************************************
-- Called after everyone has already been kicked out of the Arena.
-- OnArenaLeavePost() is called for each user before OnArenaEndPost() is called
-- *******************************************
function PLUGIN:OnArenaEndPost()
  if (self.DeathmatchData.IsChosen) then
    self.DeathmatchData.HasStarted = false
  end
end

function PLUGIN:CanArenaJoin(player)
  if (self.DeathmatchData.IsChosen) then
    return "true"
  end
end

function PLUGIN:OnArenaJoinPost(player)
  if (self.DeathmatchData.IsChosen) then
    if (self.DeathmatchData.HasStarted) then
      arena_plugin:TeleportPlayerToArena(player)
      self:EquipPlayer(player)
    end

    local userID = rust.UserIDFromPlayer(player)
    self.DeathmatchData.Users[userID] = {}
    self.DeathmatchData.Users[userID].kills = 0
    self.DeathmatchData.Users[userID].spawnTime = -1
  end
end

-- *******************************************
-- Called after a player has left the Arena.
-- *******************************************
function PLUGIN:OnArenaLeavePost(player)
  if (self.DeathmatchData.IsChosen) then
    self:ClearInventory(player)
    self.DeathmatchData.Users[rust.UserIDFromPlayer(player)] = nil
  end
end

function PLUGIN:OnArenaSpawnPost(player)
  if (self.DeathmatchData.IsChosen) then
    self:EquipPlayer(player)
    self:GivePlayerImmunity(player)
  end
end

-- *******************************************
-- HOOK FUNCTIONS
-- *******************************************

function PLUGIN:OnEntityAttacked(entity,hitinfo)
  if (not arena_loaded) then
    return
  end
  if (self.DeathmatchData.IsChosen and self.DeathmatchData.HasStarted) then
    if (hitinfo and hitinfo.Initiator and hitinfo.Initiator:ToPlayer()) then
      if (entity and entity:ToPlayer()) then
          if (entity:ToPlayer() ~= hitinfo.Initiator:ToPlayer()) then
              -- If the victim is protected, deal no damage
              if (arena_plugin:IsPlaying(entity:ToPlayer()) and self:IsImmune(entity:ToPlayer())) then
                rust.SendChatMessage(hitinfo.Initiator:ToPlayer(), "Deathmatch", "New spawns have immunity!")
                return false
              end
          end
        end
    end
  end
end
function PLUGIN:OnEntityDeath(entity, hitinfo)
  if (not arena_loaded) then
    return
  end
  if (self.DeathmatchData.IsChosen and self.DeathmatchData.HasStarted) then
    if (entity:ToPlayer()) then
      if (hitinfo and hitinfo.Initiator and hitinfo.Initiator:ToPlayer()) then
        local attacker = hitinfo.Initiator:ToPlayer()
        local victim = entity:ToPlayer()

        if (attacker and victim and arena_plugin:IsPlaying(victim)) then
          if (attacker == victim) then
            -- Process suicide
          elseif (not arena_plugin:IsPlaying(attacker)) then
            -- Handle this
          else
            self:AwardKill(attacker)
          end
          --timer.Once(0, function() arena_plugin:RemoveBag(victim) end)
        end
      end
    end
  end
end

function PLUGIN:SendHelpText(player)
  if (not arena_loaded) then
    return
  end
  if (player:GetComponent("BaseNetworkable").net.connection.authLevel > 0) then
    rust.SendChatMessage(player, "Deathmatch", "Use /deathmatch_pack {packNumber} to select a custom pack for Deathamtch.")
  end
end

-- *******************************************
-- MAIN FUNCTIONS
-- *******************************************
function PLUGIN:InitializeTable()
	Table = {}
	local itemlist = global.ItemManager.GetItemDefinitions();
	local it = itemlist:GetEnumerator()
	while (it:MoveNext()) do
		local correctname = string.lower(it.Current.displayname)
		Table[correctname] = tostring(it.Current.shortname)
	end
end 
  
function PLUGIN:EquipAllPlayers()
  local netusers = arena_plugin:GetAllPlayers()
  for k,player in pairs(netusers) do
    if (arena_plugin:IsPlaying(player)) then
      self:EquipPlayer(player)
    end
  end
end
function PLUGIN:GiveItem(inv,name,amount,type)
	local itemname = false
	name = string.lower(name)
	if(Table[name]) then
		itemname = Table[name]
	else
		itemname = name
	end
	if(tonumber(amount) == nil) then
		return false, "amount is not valid"
	end
	local container
	if(type == "belt") then
		container = inv.containerBelt
	elseif(type == "main") then
		container = inv.containerMain
	elseif(type == "wear") then
		container = inv.containerWear
	else
		return false, "wrong type: belt, main or wear"
	end
	local giveitem = global.ItemManager.CreateByName(itemname,amount)
	if(not giveitem) then
		return false, itemname .. " is not a valid item name"
	end
	inv:GiveItem(giveitem,container);
	return giveitem
end

function PLUGIN:EquipPlayer(player)
  if(not Table) then self:InitializeTable() end
  self:ClearInventory(player)

  local packNum = self.Config.DefaultPack
  if (self.DeathmatchData.CustomPack > 0) then
    packNum = self.DeathmatchData.CustomPack
  elseif (self.Config.RandomPack) then
    packNum = math.random(#self.Config.Packs)
  end

  local packData = self.Config.Packs[packNum]
  
  -- Equip player with armor
  if (packData.armor) then
    for i = 1, #packData.armor do
      str = packData.armor[i]
      giveitem, err = self:GiveItem(player.inventory,str,1,"wear")
	  if(not giveitem) then print("Deathmatch: Error while giving " .. str .. ": " .. err) end
    end
  end

  -- Equip player with items in their backpack
  if (packData.backpack) then
    for i = 1, #packData.backpack do
      if (packData.backpack[i][2]) then
        giveitem, err = self:GiveItem(player.inventory,packData.backpack[i][1],packData.backpack[i][2],"main")
	    if(not giveitem) then print("Deathmatch: Error while giving " .. packData.backpack[i][1] .. ": " .. err) end
      else
        giveitem, err = self:GiveItem(player.inventory,packData.backpack[i][1],1,"main")
	    if(not giveitem) then print("Deathmatch: Error while giving " .. packData.backpack[i][1] .. ": " .. err) end
      end
    end
  end

  -- Equip player with items on their belt
  if (packData.belt) then
    for i = 1, #packData.belt do
      if (packData.belt[i][2]) then
        giveitem, err = self:GiveItem(player.inventory,packData.belt[i][1],packData.belt[i][2],"belt")
	    if(not giveitem) then print("Deathmatch: Error while giving " .. packData.belt[i][1] .. ": " .. err) end
      else
        giveitem, err = self:GiveItem(player.inventory,packData.belt[i][1],1,"belt")
	    if(not giveitem) then print("Deathmatch: Error while giving " .. packData.belt[i][1] .. ": " .. err) end
      end
    end
  end
end

function PLUGIN:AwardKill(player)
  local userData = self:GetUserData(player)
  userData.kills = userData.kills + 1
  self:DisplayKillMessage(player)
  self:ShowPlayerScore(player)

  if (self.Config.KillLimit > 0 and userData.kills >= self.Config.KillLimit) then
    self:GiveWin(player)
  end
end

function PLUGIN:GiveWin(player)
  -- Announce win
  local str = "DEATHMATCH IS OVER!  " .. string.upper(player.displayName) .. " WINS!"
  for i = 1, 10 do
    arena_plugin:BroadcastToPlayers(str)
  end

  -- Trigger the end of the arena
  timer.Once(5, function() arena_plugin:EndArena() end)
end

-- *******************************************
-- HELPER FUNCTIONS
-- *******************************************
function PLUGIN:ClearInventory(player)
  player.inventory:Strip()
end

-- *******************************************
-- PLUGIN:LoadDefaultConfig()
-- Loads the default configuration into the config table
-- *******************************************
function PLUGIN:LoadDefaultConfig()
  -- Set default configuration settings
  self.Config.AutoArena_Settings = {}
  self.Config.AutoArena_Settings.MinimumPlayers = 2
  self.Config.AutoArena_Settings.MaximumPlayers = 10
  
  self.Config.authLevel = 1
  self.Config.SpawnImmunity = 4
  self.Config.RandomPack = true
  self.Config.DefaultPack = 1
  self.Config.KillLimit = 5000
  self.Config.Packs =
  {
    {belt = {{"Thompson"},{"Medical Syringe", 1}}, armor = {"Hazmat Boots", "Hazmat Jacket", "Hazmat Gloves", "Hazmat Pants"}, backpack = {{"Pistol Bullet", 250}}},
    {belt = {{"Thompson"},{"Medical Syringe", 1}}, armor = {"Hazmat Boots", "Hazmat Jacket", "Hazmat Gloves", "Hazmat Pants"}, backpack = {{"Pistol Bullet", 250}}}
  }
  self.Config.KillMessages =
  {
    "Sweet Kill!",
    "+1 Kill!",
    "Nice Shot!",
    "Destruction!",
    "Like a boss!",
    "You Mex'd him!",
    "Boom!",
    "Ultrakill!",
    "Y0u 4R3 s0 1337!"
  }
end

function PLUGIN:GivePlayerImmunity(player)
  self.DeathmatchData.Users[rust.UserIDFromPlayer(player)].spawnTime = time.GetUnixTimestamp()
end

function PLUGIN:IsImmune(player)
  return (not ((time.GetUnixTimestamp() - self.DeathmatchData.Users[rust.UserIDFromPlayer(player)].spawnTime) > self.Config.SpawnImmunity))
end

local msgNumber = 1
function PLUGIN:DisplayKillMessage(player)
  rust.SendChatMessage(player, "Deathmatch", self.Config.KillMessages[msgNumber])
  msgNumber = (msgNumber % #self.Config.KillMessages) + 1
end

function PLUGIN:ShowPlayerScore(player)
  local userData = self:GetUserData(player)
  arena_plugin:BroadcastToPlayers(player.displayName .. " has a total of " .. userData.kills .. " kills!")
end

function PLUGIN:GetUserData(player)
  return self.DeathmatchData.Users[rust.UserIDFromPlayer(player)]
end
