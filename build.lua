PLUGIN.Name = "Build"
PLUGIN.Title = "Build"
PLUGIN.Version = V(0, 5, 5)
PLUGIN.Description = "Manage building owners"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true


function PLUGIN:Init()

------------------------------------------------------------------------
-- Initialize all the ingame commands ----------------------------------
------------------------------------------------------------------------
    command.AddChatCommand( "bld",  self.Plugin, "cmdBuild" )
    command.AddChatCommand( "bldup",  self.Plugin, "cmdBuildUp" )
    command.AddChatCommand( "blddown",  self.Plugin, "cmdBuildDown" )
    command.AddChatCommand( "bldlvl",  self.Plugin, "cmdBuildLevel" )
    command.AddChatCommand( "bldheal",  self.Plugin, "cmdBuildHeal" )
    command.AddChatCommand( "bldhelp",  self.Plugin, "cmdBuildHelp" )
    command.AddChatCommand( "spawn",  self.Plugin, "cmdSpawn" )
    command.AddChatCommand( "deploy",  self.Plugin, "cmdDeploy" )
    command.AddChatCommand( "plant",  self.Plugin, "cmdPlant" )
    command.AddChatCommand( "animal",  self.Plugin, "cmdAnimal" )

------------------------------------------------------------------------
-- Initialize the Default tables with the prefabs ----------------------
------------------------------------------------------------------------
    self:LoadDefaultTables()
------------------------------------------------------------------------
-- Initialize the buildingowners plugin  -------------------------------
------------------------------------------------------------------------
    buildingowners = plugins.Find("Building Owners")

    --self.Config = {}
    --self:LoadDefaultConfig()
    
    
    nilarray = util.TableToArray( { } )
end
function PLUGIN:LoadDefaultTables()
------------------------------------------------------------------------
-- Initialize the static Config table  ---------------------------------
------------------------------------------------------------------------
	BuildingConfig = {}
	BuildingConfig["foundation"] = {}
	BuildingConfig["foundation"]["isFloor"] = true
	BuildingConfig["foundation"]["prefab"] = "build/foundation"
	
	BuildingConfig["foundation.steps"] = {}
	BuildingConfig["foundation.steps"]["isFloor"] = true
	BuildingConfig["foundation.steps"]["prefab"] = "build/foundation.steps"
	
	BuildingConfig["wall"] = {}
	BuildingConfig["wall"]["isFloor"] = false
	BuildingConfig["wall"]["prefab"] = "build/wall"
	
	BuildingConfig["floor"] = {}
	BuildingConfig["floor"]["isFloor"] = true
	BuildingConfig["floor"]["prefab"] = "build/floor"
		
	BuildingConfig["window"] = {}
	BuildingConfig["window"]["isFloor"] = false
	BuildingConfig["window"]["prefab"] = "build/wall.window"
	
	BuildingConfig["railing"] = {}
	BuildingConfig["railing"]["isFloor"] = false
	BuildingConfig["railing"]["prefab"] = "build/wall.low"
	
	BuildingConfig["stairs"] = {}
	BuildingConfig["stairs"]["isFloor"] = false
	BuildingConfig["stairs"]["prefab"] = "build/stairs"
	
	BuildingConfig["block"] = {}
	BuildingConfig["block"]["isFloor"] = true
	BuildingConfig["block"]["isBlock"] = true
	BuildingConfig["block"]["prefab"] = "build/block.halfheight"
	
	--BuildingConfig["pillar"] = {}
	--BuildingConfig["pillar"]["isFloor"] = true
	--BuildingConfig["pillar"]["isPillar"] = true
	--BuildingConfig["pillar"]["prefab"] = "build/pillar"
	
	BuildingConfig["doorway"] = {}
	BuildingConfig["doorway"]["isFloor"] = false
	BuildingConfig["doorway"]["prefab"] = "build/wall.doorway"
------------------------------------------------------------------------
-- Initialize the static table that will us get configs of an ingame building block
------------------------------------------------------------------------
	BlockToBuilding = {}
	BlockToBuilding["build/foundation"] = "foundation"
	BlockToBuilding["build/foundation.steps"] = "foundation.steps"
	BlockToBuilding["build/wall"] = "wall"
	BlockToBuilding["build/floor"] = "floor"
	BlockToBuilding["build/wall.window"] = "window"
	BlockToBuilding["build/stairs"] = "stairs"
	BlockToBuilding["build/wall.doorway"] = "doorway"
	BlockToBuilding["build/wall.low"] = "railing"
	BlockToBuilding["build/block.halfheight"] = "block"
	BlockToBuilding["build/pillar"] = "pillar"
end
function PLUGIN:LoadDefaultConfig()
	self.Config["foundation"] = {}
    self.Config["foundation"]["health"] = "800"
    self.Config["foundation"]["authlevel"] = "0"
    self.Config["foundation"]["foundationtofoundationonly"] = "true"
    self.Config["foundation"]["build"] = "true"
    self.Config["foundation"]["spawn"] = "true"
    self.Config["foundation"]["grade"] = "5"
    
     self.Config["foundation.steps"]= {}
    self.Config["foundation.steps"]["health"] = "800"
    self.Config["foundation.steps"]["authlevel"] = "0"
    self.Config["foundation.steps"]["foundationtofoundationonly"] = "true"
    self.Config["foundation.steps"]["build"] = "true"
    self.Config["foundation.steps"]["spawn"] = "true"
    self.Config["foundation.steps"]["grade"] = "5"
    
    self.Config["wall"] = {}
    self.Config["wall"]["health"] = "100"
    self.Config["wall"]["grade"] = "5"
    self.Config["wall"]["authlevel"] = "0"
    self.Config["wall"]["build"] = "true"
    self.Config["wall"]["spawn"] = "true"
    
    self.Config["railing"] = {}
    self.Config["railing"]["health"] = "100"
    self.Config["railing"]["authlevel"] = "0"
    self.Config["railing"]["grade"] = "5"
    self.Config["railing"]["build"] = "true"
    self.Config["railing"]["spawn"] = "true"
    
    self.Config["window"] = {}
    self.Config["window"]["health"] = "100"
    self.Config["window"]["authlevel"] = "0"
    self.Config["window"]["build"] = "true"
    self.Config["window"]["spawn"] = "true"
    self.Config["window"]["grade"] = "5"
    
    self.Config["floor"] = {}
    self.Config["floor"]["health"] = "100"
    self.Config["floor"]["authlevel"] = "0"
    self.Config["floor"]["build"] = "true"
    self.Config["floor"]["spawn"] = "true"
    self.Config["floor"]["grade"] = "5"
    
    self.Config["stairs"] = {}
    self.Config["stairs"]["health"] = "100"
    self.Config["stairs"]["authlevel"] = "0"
    self.Config["stairs"]["build"] = "true"
    self.Config["stairs"]["spawn"] = "true"
    self.Config["stairs"]["grade"] = "5"
    
    self.Config["doorway"] = {}
    self.Config["doorway"]["health"] = "100"
    self.Config["doorway"]["authlevel"] = "0"
    self.Config["doorway"]["build"] = "true"
    self.Config["doorway"]["spawn"] = "true"
    self.Config["doorway"]["grade"] = "5"
    
    self.Config["block"] = {}
    self.Config["block"]["health"] = "100"
    self.Config["block"]["authlevel"] = "0"
    self.Config["block"]["build"] = "true"
    self.Config["block"]["spawn"] = "true"
    self.Config["block"]["grade"] = "5"
    
    --self.Config["pillar"] = {}
    --self.Config["pillar"]["health"] = "100"
    --self.Config["pillar"]["authlevel"] = "0"
    --self.Config["pillar"]["build"] = "true"
    --self.Config["pillar"]["spawn"] = "true"
    --self.Config["pillar"]["grade"] = "5"
    
    self.Config.Settings = {}
    self.Config.Settings.authLevel = 0
    self.Config.Settings.debug = true
    self.Config.deploy = {}
    self.Config.deploy.allow = true
    self.Config.deploy.authLevel = 1 
    self.Config.plant = {}
    self.Config.plant.allow = true
    self.Config.plant.authLevel = 1 
    self.Config.animals = {}
    self.Config.animals.allow = true
    self.Config.animals.authLevel = 1
    
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. tostring(msg) .. "\"" );
end
local function Distance2D(p1, p2)
    return math.sqrt(math.pow(p1.x - p2.x,2) + math.pow(p1.z - p2.z,2)) 
end
local function InitializePlantList()
	PlantList = {}
	local gamemanifest = global.GameManifest.Get()
    local it = gamemanifest.resourceFiles
    for i=0, it.Length-1 do 
    	if(string.find(it[i],"autospawn/resource") and string.find(it[i],"tree")) then
    		table.insert(PlantList,string.sub(it[i],9))
    	end
    end
end
local function ChatPlayer( player, txt )
	global.ConsoleSystem.SendClientCommand( connection, "chat.add \"SERVER\" \"MESSAGE\" \"DISTANCE\"" )
end
local function InitializeAnimalList()
	AnimalList = {}
	local gamemanifest = global.GameManifest.Get()
    local it = gamemanifest.resourceFiles
    for i=0, it.Length-1 do 
    	if(string.find(it[i],"autospawn/animals")) then
    		table.insert(AnimalList,string.sub(it[i],9))
    	end
    end
end
local function DoRaycastPlayer( player )
	local hits = UnityEngine.Physics.RaycastAll["methodarray"][1]:Invoke(nil, util.TableToArray({ player.eyes:Ray() }))
	local closestdist = 9999
	local closestent = false
	local closesthitpoint = false
	local enumhit = hits:GetEnumerator()
	while (enumhit:MoveNext()) do
		if(enumhit.Current.distance < closestdist) then
			closestdist = enumhit.Current.distance
			closestent = enumhit.Current.collider
			closesthitpoint = enumhit.Current.point
		end
	end
 	return closestent, closesthitpoint 
end
local function GetBuilding( originblock )
	local house = {}
	local radius = 3
	local checkfrom = {}
	table.insert(checkfrom,originblock.transform.position)
	house[originblock] = true
	local current = 0
	while(true) do
		current = current + 1
		if(not checkfrom[current]) then
			break
		end
		local arr = util.TableToArray( { checkfrom[current] , radius } )
		util.ConvertAndSetOnArray(arr, 1, radius, System.Single._type)
		local hits = UnityEngine.Physics.OverlapSphere["methodarray"][1]:Invoke(nil,arr)
		local it = hits:GetEnumerator()
		while (it:MoveNext()) do
			if(it.Current:GetComponentInParent(global.BuildingBlock._type)) then
				local fbuildingblock = it.Current:GetComponentInParent(global.BuildingBlock._type)
				if(not house[fbuildingblock]) then
					house[fbuildingblock] = true
					table.insert(checkfrom,fbuildingblock.transform.position)
				end
			end
		end
	end
	checkfrom = {}
	return house
end
local function DoHeal( blocks )
	count = 0
	for buildingblock,k in pairs(blocks) do
		buildingblock:GetComponent("BaseCombatEntity").health = buildingblock:MaxHealth()
		--buildingblock:DoHealthCreep()
		count = count + 1
	end
	return true, count
end
local function DoLevel( blocks, lvl )
	count = 0
	for buildingblock,k in pairs(blocks) do
		if(buildingblock.blockDefinition and buildingblock.blockDefinition.grades) then
			if( lvl > buildingblock.blockDefinition.grades.Length-1) then
				buildingblock:SetGrade(buildingblock.blockDefinition.grades.Length-1)
			else
				buildingblock:SetGrade(lvl)
			end
			count = count + 1
			buildingblock:GetComponent("BaseCombatEntity").health = buildingblock:MaxHealth()+1
		end
	end
	return true, count
end
local function DoCreateBuildingBlock( prefabname )
	prefabfound  = global.GameManager.server:FindPrefab(prefabname)
	local newobj = UnityEngine.Object.Instantiate.methodarray[1]:Invoke(nil, util.TableToArray( { prefabfound } ))
	local newblock = newobj:GetComponent("BuildingBlock")
	return newblock
end
local function doSetGrade( newblock, newgrade )
	if(newgrade > newblock.blockDefinition.grades.Length-1) then
		newblock:SetGrade(newblock.blockDefinition.grades.Length-1)
	else
		newblock:SetGrade(newgrade)
	end
end

function PLUGIN:CreateBuildingBlock( structure, pos, rot, player )
	local newblock = DoCreateBuildingBlock( BuildingConfig[structure]["prefab"] )
	newblock.transform.position = pos
	newblock.transform.rotation = rot
	newblock.gameObject:SetActive(true)
	newblock2 = newblock.gameObject:GetComponent("BuildingBlock")
	newblock2.blockDefinition = global["Construction+Library"].FindByPrefabID(newblock.prefabID)	
	doSetGrade(newblock2, tonumber(self.Config[structure]["grade"]))
	newblock2:GetComponent("BaseCombatEntity").health = tonumber(self.Config[structure]["health"])+1
	newblock2:Spawn(true)
	
	if(buildingowners) then
		local ownerid = buildingowners:CallHook("FindBlockData", util.TableToArray( { block } ));
		if(not ownerid) then
			buildingowners:CallHook("AddBlockData",util.TableToArray( { newblock,player } ))
		end
	end
	return newblock
end

local function GetDistanceAdjustment( buildingblock, structure )
	if(BlockToBuilding[buildingblock.blockDefinition.fullname] and BuildingConfig[BlockToBuilding[buildingblock.blockDefinition.fullname]] and BuildingConfig[BlockToBuilding[buildingblock.blockDefinition.fullname]]["isFloor"]) then
		if(BuildingConfig[structure] and not BuildingConfig[structure]["isFloor"]) then
			return 1.5
		end
	end
	return 3
end
local function GetBestPositionFromPlayerView( buildingblock, closesthitpoint, structure )
	local closestdist = 999999
	local temppos = false
	local newpos = false
	local distAdjustment = GetDistanceAdjustment( buildingblock , structure )
	for i=1, 4 do
		local arr = false
		if(not BuildingConfig[structure]["isPillar"]) then
			if(i==1) then
				arr = util.TableToArray( { buildingblock.transform.rotation, UnityEngine.Vector3.get_left() } )
			elseif(i==2) then
				arr = util.TableToArray( { buildingblock.transform.rotation, UnityEngine.Vector3.get_forward() } )
			elseif(i==3) then
				arr = util.TableToArray( { buildingblock.transform.rotation, UnityEngine.Vector3.get_right() } )
			else
				arr = util.TableToArray( { buildingblock.transform.rotation, UnityEngine.Vector3.get_back() } )
			end
		else
			return false
		end
		if(not arr) then return end
		local temppos = UnityEngine.Quaternion.op_Multiply.methodarray[1]:Invoke(nil,arr)
		if(temppos) then
			local arrr = util.TableToArray( { temppos, distAdjustment } )
			util.ConvertAndSetOnArray( arrr, 1, distAdjustment, System.Single._type )
			local newtemppos = UnityEngine.Vector3.op_Addition(buildingblock.transform.position, UnityEngine.Vector3.op_Multiply["methodarray"][0]:Invoke(nil,arrr))
			if(newtemppos) then
				local dist = Distance2D(closesthitpoint,newtemppos)
				if(dist < closestdist) then
					closestdist = dist
					newpos = newtemppos
				end
			end
		end
	end
	return newpos
end
local function isColliding(structure,pos)
	local radius = 1.4
	local arr = util.TableToArray( { pos , radius } )
	util.ConvertAndSetOnArray(arr, 1, radius, System.Single._type)
	local hits = UnityEngine.Physics.OverlapSphere["methodarray"][1]:Invoke(nil,arr)
	local it = hits:GetEnumerator()
	while (it:MoveNext()) do
		if(it.Current:GetComponentInParent(global.BuildingBlock._type)) then
			return true
		elseif(it.Current:GetComponentInParent(global.WorldItem._type)) then
			return true
		end
	end
	return false
end
local function isAlreadyBuilt(structure,pos, rot)
	local radius = 1.4
	local arr = util.TableToArray( { pos , radius } )
	util.ConvertAndSetOnArray(arr, 1, radius, System.Single._type)
	local hits = UnityEngine.Physics.OverlapSphere["methodarray"][1]:Invoke(nil,arr)
	local it = hits:GetEnumerator()
	while (it:MoveNext()) do
		if(it.Current:GetComponentInParent(global.BuildingBlock._type)) then
			local blockname = it.Current:GetComponentInParent(global.BuildingBlock._type).blockDefinition.fullname
			if(BlockToBuilding[blockname] and BlockToBuilding[blockname] == structure) then
				if(tostring(it.Current:GetComponentInParent(global.BuildingBlock._type).transform.position) == tostring(pos) and tostring(it.Current:GetComponentInParent(global.BuildingBlock._type).transform.rotation) == tostring(rot)) then
					return true
				end
			end
		end
	end
	return false
end

function PLUGIN:Build( player, structure, rotationAdjustment, heightAdjustment )
	local closestent, closesthitpoint = DoRaycastPlayer( player )
	if(not closestent) then ChatMessage(player,"Couldn't find any entity") return end
	local buildingblock = closestent:GetComponentInParent(global.BuildingBlock._type)
	if(not buildingblock) then ChatMessage(player,"The entity that you are looking at isn't a structure") return end
	local newpos = GetBestPositionFromPlayerView( closestent.gameObject:GetComponentInParent(global.BaseEntity._type), closesthitpoint, structure )
	if(not newpos) then ChatMessage(player,"Couldn't get a new position.") return end
	
	if(self.Config[structure]["foundationtofoundationonly"] and self.Config[structure]["foundationtofoundationonly"] == "true") then
		if(BuildingConfig[structure]["prefab"] ~= buildingblock.blockDefinition.fullname) then
			ChatMessage(player,"You may only build a foundation when it's against another one")
			return
		end
	end
	local newrot = buildingblock.transform.rotation
	if(rotationAdjustment) then
		local eulerRot = newrot.eulerAngles
		eulerRot.y = eulerRot.y + rotationAdjustment
		newrot.eulerAngles = eulerRot
	end
	if(heightAdjustment) then
		newpos.y = newpos.y + heightAdjustment
	end
	if( isAlreadyBuilt( structure, newpos, newrot ) ) then
		ChatMessage(player,"You already built here.")
		return
	end
	local newblock = self:CreateBuildingBlock( structure, newpos, newrot, player )
	if(self.Config.Settings.debug) then
		print(newblock.blockDefinition.fullname .. " was built by " .. player.displayName)
	end
end
function PLUGIN:Spawn( player, structure , args )
	local closestent, closesthitpoint = DoRaycastPlayer( player )
	if(not closestent) then ChatMessage(player,"Couldn't find any entity") return end
	local newpos = closesthitpoint
	if(not newpos) then ChatMessage(player,"Couldn't get a new position.") return end
	local transformheight = 0
	local transformrotation = false
	if(args.Length >= 2) then
		if(tonumber(args[1]) ~= nil) then
			transformrotation = tonumber(args[1])
		end
		if(args.Length >= 3) then
			if(tonumber(args[2]) ~= nil) then
				transformheight = tonumber(args[2])
			end
		end
	end
	
	newpos.y = newpos.y + transformheight
	local newrot = player.eyes.rotation
	newrot.x = 0
	newrot.z = 0
	if(transformrotation) then
		local eulerRot = newrot.eulerAngles
		eulerRot.y = eulerRot.y + transformrotation
		newrot.eulerAngles = eulerRot
	end
	local newblock = self:CreateBuildingBlock( structure, newpos, newrot, player )
	if(self.Config.Settings.debug) then
		print(newblock.blockDefinition.fullname .. " was spawned by " .. player.displayName)
	end
end
function PLUGIN:BuildUp( player, structure, heightadjustment )
	local closestent, closesthitpoint = DoRaycastPlayer( player )
	if(not closestent) then ChatMessage(player,"Couldn't find any entity") return end
	local buildingblock = closestent:GetComponentInParent(global.BuildingBlock._type)
	if(not buildingblock) then ChatMessage(player,"The entity that you are looking at isn't a structure") return end
	newpos = buildingblock.transform.position
	newpos.y = newpos.y + heightadjustment
	local newrot = buildingblock.transform.rotation
	if( isAlreadyBuilt( structure, newpos, newrot ) ) then
		ChatMessage(player,"You already built here.")
		return
	end
	local newblock = self:CreateBuildingBlock( structure, newpos, newrot, player )
	if(self.Config.Settings.debug) then
		print(newblock.blockDefinition.fullname .. " was built by " .. player.displayName)
	end
end
function PLUGIN:BuildHeal( player, arg )
	local closestent, closesthitpoint = DoRaycastPlayer( player )
	if(not closestent) then ChatMessage(player,"Couldn't find any entity") return end
	local buildingblock = closestent:GetComponentInParent(global.BuildingBlock._type)
	if(not buildingblock) then ChatMessage(player,"The entity that you are looking at isn't a structure") return end
	buildingToHeal = {}
	if(arg == "all") then
		buildingToHeal = GetBuilding( buildingblock )
	else
		buildingToHeal[buildingblock] = true
	end
	local success, number = DoHeal( buildingToHeal )
	if(success) then
		ChatMessage(player,tostring(number) .." building parts were healed")
		return
	end
end
function PLUGIN:BuildLevel( player, arg , arg2 )
	local closestent, closesthitpoint = DoRaycastPlayer( player )
	if(not closestent) then ChatMessage(player,"Couldn't find any entity") return end
	local buildingblock = closestent:GetComponentInParent(global.BuildingBlock._type)
	if(not buildingblock) then ChatMessage(player,"The entity that you are looking at isn't a structure") return end
	buildingToLevel = {}
	if(arg == "all") then
		buildingToLevel = GetBuilding( buildingblock )
	else
		buildingToLevel[buildingblock] = true
	end
	local success, number = DoLevel( buildingToLevel, arg2 )
	if(success) then
		ChatMessage(player,tostring(number) .." building parts were leveled to: " .. arg2)
		return
	end
end
function PLUGIN:cmdBuildHeal( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		if(args.Length == 0) then
			ChatMessage(player,"/bldheal all/select")
			return
		end
		local arg = string.lower(args[0])
		self:BuildHeal( player, arg )
	end
end
function PLUGIN:cmdBuildLevel( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		if(args.Length <= 1) then
			ChatMessage(player,"/bldlvl all/select LEVELNUMBER")
			return
		end
		local arg = string.lower(args[0])
		local arg2 = tonumber(args[1])
		if(arg2 == nil) then
			ChatMessage(player,"/bldlvl all/select LEVELNUMBER")
			return
		end
		self:BuildLevel( player, arg , arg2 )
	end
end
function PLUGIN:cmdBuildUp( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		if(args.Length == 0) then
			ChatMessage(player,"/bldup foundation/wall/window/floor")
			return
		end
		local arg = string.lower(args[0])
		if(self.Config[arg]) then
			if(authlevel >= tonumber(self.Config[arg]["authlevel"])) then
				local heightadjustment = 3
				if(args.Length >= 2) then
					if(tonumber(args[1]) ~= nil) then
						heightadjustment = tonumber(args[1])
					end
				end
				if(self.Config[arg]["build"]) then
					self:BuildUp( player, arg , heightadjustment)
				else
					ChatMessage(player,"Building a " .. arg .. " was deactivated.")
				end
			else
				ChatMessage(player,"You don't have enough power to force spawn a " .. arg )
			end
			return
		else
			ChatMessage(player,"Wrong Config. Use /bld, to get more informations")
			return
		end
	end
end
function PLUGIN:cmdBuildDown( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		ChatMessage(player,"To build down use: /buildup -3 (3m is the default height between levels)")
	end
end
function PLUGIN:cmdBuild( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		if(args.Length == 0) then
			ChatMessage(player,"/bld config")
			ChatMessage(player,"/bld \"structure\" \"optional:rotationadjustment\" \"optional:heightadjustment\"")
			ChatMessage(player,"structures avaible: foundation, wall, window, floor, stairs, doorway")
			return
		end
		local arg = string.lower(args[0])
		if(arg == "config") then
			if(args.Length == 1) then
				ChatMessage(player,"/bld config foundation")
				return
			end
			local arg1 = string.lower(args[1])
			if(not self.Config[arg1]) then
				ChatMessage(player,"Wrong Config. Use /bld config, to get the full list")
				return
			end
			if(args.Length == 2) then
				ChatMessage(player,"/bld config "..arg1.." health")
				ChatMessage(player,"/bld config "..arg1.." authlevel")
				ChatMessage(player,"/bld config "..arg1.." checkcollision")
				ChatMessage(player,"/bld config "..arg1.." build")
				ChatMessage(player,"/bld config "..arg1.." spawn")
				return
			end
			local arg2 = string.lower(args[2])
			if(not self.Config[arg1][arg2]) then
				ChatMessage(player,"Wrong Config. Use /build config "..arg1..", to get the full list")
				return
			end
			if(args.Length == 3) then
				ChatMessage(player,""..arg1.." " .. arg2.. " is set to: " .. self.Config[arg1][arg2] )
				return
			end
			local arg3 = tostring(args[3])
			self.Config[arg1][arg2] = arg3
			ChatMessage(player,""..arg1.." " .. arg2.. " is new set to: " .. self.Config[arg1][arg2] )
			return
		elseif(self.Config[arg]) then
			if(authlevel >= tonumber(self.Config[arg]["authlevel"])) then
				if(self.Config[arg]["build"]) then
					local rotationAdjustment = false
					local heightAdhustment = false
					if(args.Length >= 2) then
						if(tonumber(args[1]) ~= nil) then
							rotationAdjustment = tonumber(args[1])
						end
						if(args.Length >= 3) then
							if(tonumber(args[2]) ~= nil) then
								heightAdhustment = tonumber(args[2])
							end
						end
					end
					self:Build( player, arg, rotationAdjustment, heightAdhustment )
				else
					ChatMessage(player,"Building a " .. arg .. " was deactivated.")
				end
			else
				ChatMessage(player,"You don't have enough power to force spawn a " .. arg )
			end
			return
		else
			ChatMessage(player,"Wrong Config. Use /bld, to get more informations")
			return
		end
	else
		ChatMessage(player,"You don't have the level to do that" )
	end
end
function PLUGIN:cmdSpawn( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		if(args.Length == 0) then
			ChatMessage(player,"/spawn foundation/wall/window/floor")
			return
		end
		local arg = string.lower(args[0])
		if(self.Config[arg]) then
			if(authlevel >= tonumber(self.Config[arg]["authlevel"])) then
				if(self.Config[arg]["spawn"]) then
					self:Spawn( player, arg, args )
				else
					ChatMessage(player,"Spawning a " .. arg .. " was deactivated.")
				end
			else
				ChatMessage(player,"You don't have enough power to force spawn a " .. arg )
			end
			return
		else
			ChatMessage(player,"Use /spawn or /bld, to get more informations")
			return
		end
	else
		ChatMessage(player,"You don't have the level to do that" )
	end
end
local function InitializeTable()
	Table = {}
	local itemlist = global.ItemManager.GetItemDefinitions();
	local it = itemlist:GetEnumerator()
	while (it:MoveNext()) do
		local correctname = string.lower(it.Current.displayname,"%%","t")
		Table[correctname] = tostring(it.Current.shortname)
	end
end
local function DoDeploy( player, deployablename )
	if(not Table) then InitializeTable() end
	if(Table[deployablename]) then
		deployablename = Table[deployablename]
	end
	local newItem = global.ItemManager.CreateByName(deployablename,1)
	if(not newItem) then
		return false, "This item doesn't exist"
	end
	local it = newItem.info.modules:GetEnumerator()
	local deployModule = false
	while (it:MoveNext()) do
		if(it.Current.deployablePrefabName and it.Current.deployablePrefabName ~= "deployablePrefabName") then deployModule = it.Current end
	end
	if(not deployModule) then return false, "This item can't be deployed" end
	local closestent, closesthitpoint = DoRaycastPlayer( player )
	if(not closestent) then return false, "You may not deploy in the sky" end
	local entRot = closestent.transform.rotation
	local tempRotation = entRot
	if(not entRot or entRot == nil or (entRot and entRot.x > -1 and entRot.x < 1 and entRot.z > -1 and entRot.z < 1)) then
		tempRotation = player.eyes.rotation:ToEulerAngles()
		tempRotation.x = 0
		tempRotation.z = 0
	end
	tempRotation = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { tempRotation } ) )
	local newBaseEntity = global.GameManager.server:CreateEntity( deployModule.deployablePrefabName, closesthitpoint, tempRotation )
	if(not newBaseEntity) then
		return false, "Couldn't create the deployable: " .. deployModule.deployablePrefabName
	end
	newBaseEntity:SendMessage("SetDeployedBy", player, UnityEngine.SendMessageOptions.DontRequireReceiver )
	newBaseEntity:SendMessage("InitializeItem", newItem, UnityEngine.SendMessageOptions.DontRequireReceiver )
	newBaseEntity:Spawn(true)
	newItem:SetWorldEntity(newBaseEntity)
	return true
end
local function DoCreate( player, prefabname )
	local prefab = global.GameManager.server:FindPrefab(prefabname )
	if(not prefab) then return false, "This Prefab doesn't exist" end
	local closestent, closesthitpoint = DoRaycastPlayer( player )
	if(not closestent) then return false, "You may not create in the sky" end
	local entRot = closestent.transform.rotation
	local tempRotation = entRot
	if(not entRot or entRot == nil or (entRot and entRot.x > -1 and entRot.x < 1 and entRot.z > -1 and entRot.z < 1)) then
		tempRotation = player.eyes.rotation:ToEulerAngles()
		tempRotation.x = 0
		tempRotation.z = 0
	end
	tempRotation = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { tempRotation } ) )
	local newBaseEntity = global.GameManager.server:CreateEntity( prefabname, closesthitpoint, tempRotation )
	if(not newBaseEntity) then
		return false, "Couldn't create the prefab: " .. prefabname
	end
	newBaseEntity:Spawn(true)
	return true
end

function PLUGIN:cmdDeploy( player, cmd, args )
	if(not self.Config.deploy.allow) then
		ChatMessage(player,"This command is deactivated")
		return
	end
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.deploy.authLevel) then
		if(args.Length == 0) then
			ChatMessage(player,"/deploy DEPLOYABLENAME")
			return
		end
		local arg = string.lower(args[0])
		local dodeploy, err = DoDeploy( player, arg )
		if(not dodeploy) then
			ChatMessage(player,err)
			return
		end
		ChatMessage(player,"The item was successfully deployed" )
	else
		ChatMessage(player,"You don't have the level to do that" )
	end
end
function PLUGIN:cmdAnimal( player, cmd, args )
	if(not self.Config.animals.allow) then
		ChatMessage(player,"This command is deactivated")
		return
	end
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.animals.authLevel) then
		if(not AnimalList) then InitializeAnimalList() end
		if(args.Length == 0) then
			ChatMessage(player,"/animal list => to get the full animal list")
			ChatMessage(player,"/animal ANIMALID => to spawn an animal")
			return
		end
		local arg = string.lower(args[0])
		if(arg == "list") then
			ChatMessage(player,"ANIMALID  - full prefab name")
			for i=1, #AnimalList do
				ChatMessage(player,i .. " - " .. AnimalList[i])
			end
		elseif(tonumber(arg) == nil) then
			ChatMessage(player,"/animal list => to get the full animal list")
			ChatMessage(player,"/animal ANIMALID => to spawn an animal")
			return
		else
			local doanimal, err = DoCreate( player, AnimalList[tonumber(arg)] )
			if(not doanimal) then
				ChatMessage(player,err)
				return
			end
			ChatMessage(player,"You successfully added an animal." )
		end
		
	else
		ChatMessage(player,"You don't have the level to do that" )
	end
end
function PLUGIN:cmdPlant( player, cmd, args )
	if(not self.Config.plant.allow) then
		ChatMessage(player,"This command is deactivated")
		return
	end
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.plant.authLevel) then
		if(not PlantList) then InitializePlantList() end
		if(args.Length == 0) then
			ChatMessage(player,"/plant list => to get the full plant list")
			ChatMessage(player,"/plant PLANTID => to plant the tree")
			return
		end
		local arg = string.lower(args[0])
		if(arg == "list") then
			ChatMessage(player,"PLANTID  - full prefab name")
			for i=1, #PlantList do
				ChatMessage(player,i .. " - " .. PlantList[i])
			end
		elseif(tonumber(arg) == nil) then
			ChatMessage(player,"/plant list => to get the full plant list")
			ChatMessage(player,"/plant PLANTID => to plant the tree")
			return
		else
			local doplant, err = DoCreate( player, PlantList[tonumber(arg)] )
			if(not doplant) then
				ChatMessage(player,err)
				return
			end
			ChatMessage(player,"You successfully planted a tree." )
		end
		
	else
		ChatMessage(player,"You don't have the level to do that" )
	end
end
function PLUGIN:cmdBuildHelp( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.plant.authLevel) then
		ChatMessage(player,"-------- Building Plugin ---------" )
		ChatMessage(player,"/bld => to get help on how to build with AI" )
		ChatMessage(player,"/bldup => to get help on how to build up with AI" )
		ChatMessage(player,"/bldlvl => to get help on how to change the level of a structure" )
		ChatMessage(player,"/bldheal => to get help on how to heal a structure" )
		ChatMessage(player,"/spawn => to get help on how to build with NO AI" )
		ChatMessage(player,"/deploy => to get help on how to deploy a deployable" )
		ChatMessage(player,"/plant => to get help on how to plant a tree" )
		ChatMessage(player,"/animal => to get help on how to spawn an animal" )
	end
end
function PLUGIN:OnRunCommand( arg )
    if ( not arg ) then return end
    if ( not arg.cmd ) then return end
    if ( not arg.cmd.name ) then return end
    if ( not arg.connection ) then return end
    if ( not arg.connection.player ) then return end

    if arg.cmd.name == "wakeup" then
        local player = arg.connection.player
        position = player.transform.position
        rotation = player.transform.rotation
    end 
end
