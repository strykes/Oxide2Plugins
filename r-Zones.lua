PLUGIN.Name = "r-Zones"
PLUGIN.Title = "r-Zones"
PLUGIN.Version = V(1, 0, 6)
PLUGIN.Description = "Manage zones"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true

local DataFile = "rzones"
local ZonesData = {}

function PLUGIN:Init()

	------------------------------------------------------------------------
	-- command.AddChatCommand( "Command_name", self.Object, "target_function" )
	------------------------------------------------------------------------
	--command.AddChatCommand( "zone", self.Object, "cmdZone" )
	command.AddChatCommand( "zone_add", self.Object, "cmdZoneAdd" )
	command.AddChatCommand( "zone_list", self.Object, "cmdZoneList" )
	command.AddChatCommand( "zone_delete", self.Object, "cmdZoneDelete" )
	command.AddChatCommand( "zone_reset", self.Object, "cmdZoneReset" )
	------------------------------------------------------------------------
	
	------------------------------------------------------------------------
	-- Prepare the Plugin
	------------------------------------------------------------------------
	self:LoadDataFile()
	self:LoadZonesConfig()
	
	RadiationZones = {}
	PlayersZones = {}
	PlayersFlags = {}
	
	timer.Once(0.1, function() 
		self:InitZones()
		nulVector3 = new(UnityEngine.Vector3._type,nil) 
	end)
	
	------------------------------------------------------------------------
	-- Debug Config
	------------------------------------------------------------------------
	--self.Config = {}
	--self:LoadDefaultConfig()
	------------------------------------------------------------------------
end

------------------------------------------------------------------------
-- InitZones => initialize the zones.
------------------------------------------------------------------------
function PLUGIN:InitZones()
	for k,zonedata in pairs(ZonesData) do
		if(not self:isZone(zonedata)) then
			self:CreateZone(zonedata)
		end
	end
end
------------------------------------------------------------------------

------------------------------------------------------------------------
-- Plugin Default Configs
------------------------------------------------------------------------
function PLUGIN:LoadZonesConfig()
	ArgZones = {}
	ArgZones["-god"] = true
	ArgZones["-sleepgod"] = true
	ArgZones["-undestr"] = true
	ArgZones["-nobuild"] = true
	ArgZones["-notp"] = true
	ArgZones["-nochat"] = true
	ArgZones["-nokits"] = true
	ArgZones["-nosuicide"] = true
	ArgZones["-killsleepers"] = true
	ArgZones["-radiation"] = true
end
function PLUGIN:LoadDefaultConfig()
	self.Config.Settings = {}
	self.Config.Settings.authLevel = 1
	self.Config.AdminsCanForceBuild = true
end
------------------------------------------------------------------------

------------------------------------------------------------------------
-- Data Manipulation. Load & Save Zones
------------------------------------------------------------------------
local function hasAtLeastOneData(data)
	for k,v in pairs(data) do
		return true
	end
	return false
end
function PLUGIN:LoadDataFile()
	
    local data = datafile.GetDataTable(DataFile)
    ZonesData = data or {}
end
function PLUGIN:SaveData()
    datafile.SaveDataTable(DataFile)
end
function PLUGIN:GetFreeID()
	if(ZonesData == nil) then ZonesData = {} return 1 end
	for i=1,10000 do
		if(not ZonesData[tostring(i)]) then
			return i
		end
	end
	return false
end

------------------------------------------------------------------------
--------------------------------------------------------------------
-- refreshPlayers() => refresh players in a zone
--------------------------------------------------------------------
function PLUGIN:refreshPlayers()
	for player,zones in pairs(PlayersZones) do
		PlayersZones[player] = {}
		self:UpdatesPlayerFlags(player)
	end
		allRadiationZone = UnityEngine.Object.FindObjectsOfTypeAll(global.TriggerRadiation._type)
		for i=0, allRadiationZone.Length-1 do
			if(RadiationZones[allRadiationZone[i]]) then
				if(allRadiationZone[i].entityContents.Count > 0) then
					for o=0, allRadiationZone[i].entityContents.Count-1 do
						if( allRadiationZone[i].entityContents[o]:GetComponentInParent(global.BasePlayer._type) ) then
							self:addPlayerZone(allRadiationZone[i].entityContents[o]:GetComponentInParent(global.BasePlayer._type),allRadiationZone[i])
						end
					end
				end
			end
		end
end


--------------------------------------------------------------------
-- resetZones() => shutdown all custom zones
--------------------------------------------------------------------
local function resetZones()
	allRadiationZone = UnityEngine.Object.FindObjectsOfTypeAll(global.TriggerRadiation._type)
	for i=0, allRadiationZone.Length-1 do
		if(RadiationZones[allRadiationZone[i]]) then
			allRadiationZone[i]:RemoveObject(allRadiationZone[i].gameObject)
		end
	end
	RadiationZones = {}
end
--------------------------------------------------------------------

--------------------------------------------------------------------
-- DeleteZone(zonenumber) => deletes a specific zone.
--------------------------------------------------------------------
function PLUGIN:DeleteZone(zonenum)
	if(not ZonesData[tostring(zonenum)]) then return false end
	zone = ZonesData[tostring(zonenum)]
	newpos = new( UnityEngine.Vector3._type , nil )
	newpos.x = zone.p.x
	newpos.y = zone.p.y
	newpos.z = zone.p.z
	allRadiationZone = UnityEngine.Object.FindObjectsOfTypeAll(global.TriggerRadiation._type)
	for i=0, allRadiationZone.Length-1 do
		if(allRadiationZone[i]:GetComponent(UnityEngine.Transform._type)) then
			if(allRadiationZone[i]:GetComponent(UnityEngine.Transform._type).transform.position.x == newpos.x and allRadiationZone[i]:GetComponent(UnityEngine.Transform._type).transform.position.z == newpos.z) then
				allRadiationZone[i]:RemoveObject(allRadiationZone[i].gameObject)
			end
		end
	end
	self:refreshPlayers()
end
--------------------------------------------------------------------

--------------------------------------------------------------------
-- function PLUGIN:Unload() => Do stuff on unload.
-- Here i will reset everything to prevent the server from keeping in memory all the data.
--------------------------------------------------------------------
function PLUGIN:Unload()
	resetZones()
	itPlayerList = global.BasePlayer.activePlayerList:GetEnumerator()
    playerList = {}
    while itPlayerList:MoveNext() do
        self:UpdatePlayerBuildingPrivilege(itPlayerList.Current,false) 
    end
	PlayersFlags = {}
	PlayersZones = {}
	
end
--------------------------------------------------------------------

--------------------------------------------------------------------
-- hasFlag(baseplayer,flag) => check if player has a specific flag
--------------------------------------------------------------------
local function hasFlag(baseplayer,flag)
	if(PlayersFlags[baseplayer] and PlayersFlags[baseplayer][flag]) then
		return true
	end
	return false
end


--------------------------------------------------------------------
-- Update Players Flags when entering/leaving zones
--------------------------------------------------------------------

-- make sure that players don't try to add a tool cupboard to overwrite the no build zone
function PLUGIN:UpdatePlayerBuildingPrivilege(baseplayer,newbuildpriv)
	if(hasFlag(baseplayer,"-nobuild")) then
		timer.Once(0.1, function()
			if(baseplayer:GetComponent("BaseNetworkable").net.connection  ~= nil) then
				if(self.Config.AdminsCanForceBuild and baseplayer:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
					baseplayer:SetPlayerFlag(global.PlayerFlags.HasBuildingPrivilege, true)
					baseplayer:SetPlayerFlag(global.PlayerFlags.InBuildingPrivilege, true)
				else
					baseplayer:SetPlayerFlag(global.PlayerFlags.HasBuildingPrivilege, false)
					baseplayer:SetPlayerFlag(global.PlayerFlags.InBuildingPrivilege, true)
				end
			end
		end)
	else
		if(not newbuildpriv) then
			timer.Once(0.1, function()
				if(baseplayer:GetComponent("BaseNetworkable").net.connection  ~= nil) then
					baseplayer:SetPlayerFlag(global.PlayerFlags.HasBuildingPrivilege, true)
					baseplayer:SetPlayerFlag(global.PlayerFlags.InBuildingPrivilege, false)
				end
			end)
		end
	end
end

-- update player flags when entering or leaving single or multiple zones.
function PLUGIN:UpdatesPlayerFlags(baseplayer)
	PlayersFlags[baseplayer] = {}
	if(PlayersZones[baseplayer]) then
		for zone,o in pairs(PlayersZones[baseplayer]) do
			if(o) then 
				if(RadiationZones[zone] and RadiationZones[zone].options) then
					for k,v in pairs(RadiationZones[zone].options) do
						PlayersFlags[baseplayer][k] = true
					end
				end
			end
		end
	end
	self:UpdatePlayerBuildingPrivilege(baseplayer,false)
end




--------------------------------------------------------------------
-- addPlayerZone(baseplayer,triggerzone) & removePlayerZone(baseplayer,triggerzone)
-- add or remove player from a zone.
--------------------------------------------------------------------
function PLUGIN:addPlayerZone(baseplayer,triggerzone)
	if(not PlayersZones[baseplayer]) then PlayersZones[baseplayer] = {} end
	PlayersZones[baseplayer][triggerzone] = true
	self:UpdatesPlayerFlags(baseplayer)
end
function PLUGIN:removePlayerZone(baseplayer,triggerzone)
	if(not PlayersZones[baseplayer]) then PlayersZones[baseplayer] = {} end
	PlayersZones[baseplayer][triggerzone] = false
	self:UpdatesPlayerFlags(baseplayer)
end

--------------------------------------------------------------------
-- detect the correct format of /zone_add, with all possible options
--------------------------------------------------------------------
local function getNewZoneFromArgs(args)
	zone_name = args[0]
	if(tonumber(args[1])==nil) then return false, "Invalid Radius, needs to be a number" end
	zone_radius = tonumber(args[1])
	if(args[2] == "default" or args[2] == "") then
		zone_enter = "default"
	else
		zone_enter = args[2]
	end
	if(args[3] == "default" or args[3] == "") then
		zone_leave = "default"
	else
		zone_leave = args[3]
	end
	local NewZoneArg = {
		n = zone_name,
		r=zone_radius,
		em = zone_enter,
		lm = zone_leave,
		o = {}
	}
	if(args.Length == 4) then return NewZoneArg end
	
	-- options are get from here.
	for i=4, args.Length-1 do
		if(string.sub(args[i],0,10)=="-radiation") then
			if(tonumber(string.sub(args[i],11))==nil) then return false, "Need to set damage from radiation" end
			NewZoneArg.o["-radiation"] = tonumber(string.sub(args[i],11))
		elseif(not ArgZones[args[i]]) then 
			return false, "Invalid option: " .. i-3
		else
			NewZoneArg.o[args[i]] = true
		end
	end
	return NewZoneArg
end


-- -----------------------------------------------------------------------------
-- plugins.CallHook("canRedeemKit", object[] {player} )
-- -----------------------------------------------------------------------------
-- Ask this plugin if a player can redeem a kit
-- -----------------------------------------------------------------------------
function PLUGIN:canRedeemKit(player)
	if(hasFlag(player,"-nokits")) then
		return "You may not redeem a kit inside this area"
	end
	-- don't return anything if you want to let other plugins give an answer.
end
-- -----------------------------------------------------------------------------
-- plugins.CallHook("canTeleport", object[] {player} )
-- -----------------------------------------------------------------------------
-- Ask this plugin if a player can use teleport commands
-- -----------------------------------------------------------------------------
function PLUGIN:canTeleport(player)
	if(hasFlag(player,"-notp")) then
		return "You may not teleport in this area"
	end
	-- don't return anything if you want to let other plugins give an answer.
end

-- -----------------------------------------------------------------------------
-- PLUGIN:cmdZone*(player,cmd,args)
-- Command functions
-- -----------------------------------------------------------------------------

function PLUGIN:cmdZoneList(player,cmd,args)
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then
		rust.SendChatMessage(player,"SERVER","You do not have the permissions to use this command")
		return
	end
	rust.SendChatMessage(player,"SERVER","------- Zones List -------")
	for k,v in pairs(ZonesData) do
		rust.SendChatMessage(player,"SERVER",k .. " - " .. math.ceil(v.p.x) .. " " .. math.ceil(v.p.y) .. " " .. math.ceil(v.p.z) .. " - Radius: " .. v.r .. "m - Enter MSG: " .. v.em .. " - Leave MSG: " .. v.lm )
	end
end

function PLUGIN:cmdZoneReset(player,cmd,args)
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then
		rust.SendChatMessage(player,"SERVER","You do not have the permissions to use this command")
		return
	end
	for k,v in pairs(ZonesData) do
		self:DeleteZone(k)
	end
	resetZones()
	PlayersZones = {}
	PlayersFlags = {}
	for k,v in pairs(ZonesData) do
		ZonesData[k] = nil
	end
	self:SaveData()
	rust.SendChatMessage(player,"SERVER","All zones were deleted")
end
function PLUGIN:cmdZoneDelete(player,cmd,args)
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then
		rust.SendChatMessage(player,"SERVER","You do not have the permissions to use this command")
		return
	end
	if(args.Length == 0) then
		rust.SendChatMessage(player,"SERVER","Use /zone_list to get the ID of the zone you want to delete")
		return
	end
	if(tonumber(args[0]) == nil) then
		rust.SendChatMessage(player,"SERVER","Use /zone_list to get the ID of the zone you want to delete")
		return
	end
	if(not ZonesData[tostring(args[0])]) then
		rust.SendChatMessage(player,"SERVER","This zone doesn't exist.")
		return
	end
	self:DeleteZone(args[0])
	ZonesData[tostring(args[0])] = nil
	self:SaveData()
	rust.SendChatMessage(player,"SERVER","Zone n°" .. args[0] .. " deleted")
end
function PLUGIN:cmdZoneAdd(player,cmd,args)
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.Settings.authLevel) then
		rust.SendChatMessage(player,"SERVER","You do not have the permissions to use this command")
		return
	end
	if(args.Length < 4) then
		rust.SendChatMessage(player,"SERVER","Please select the type of the zone that you want to add: (/zone_add \"NAME\" \"RADIUS\" \"ENTER MSG\" \"LEAVE MSG\" OPTIONS)")
		rust.SendChatMessage(player,"SERVER","MSG can be: \"default\" meaning no message will be displayed")
		rust.SendChatMessage(player,"SERVER","Options:")
		rust.SendChatMessage(player,"SERVER","-god => will prevent players from hurting each other")
		rust.SendChatMessage(player,"SERVER","-sleepgod => will prevent players from hurting sleepers")
		rust.SendChatMessage(player,"SERVER","-undestr => will prevent players from hurting buildings")
		rust.SendChatMessage(player,"SERVER","-nobuild => will prevent players from building")
		rust.SendChatMessage(player,"SERVER","-nochat => will prevent players from chatting")
		rust.SendChatMessage(player,"SERVER","-notp => will prevent players from teleporting out or in")
		rust.SendChatMessage(player,"SERVER","-nokits => will prevent players from getting kits from Kits plugin.")
		rust.SendChatMessage(player,"SERVER","-nosuicide => will prevent players from commiting suicide")
		rust.SendChatMessage(player,"SERVER","-killsleepers => will kill all players that try to sleep here")
		rust.SendChatMessage(player,"SERVER","-radiationXX => will add radiations to the zone with XX damage/s")
		return
	elseif(args.Length > 3) then
		NewZone,err = getNewZoneFromArgs(args)
		if(not NewZone) then
			rust.SendChatMessage(player,"SERVER",err)
			return
		end
		NewZone.p = {
						x=player.transform.position.x,
						y=player.transform.position.y,
						z=player.transform.position.z
		}
		newid = self:GetFreeID()
		if(not newid) then
			rust.SendChatMessage(player,"SERVER","Something went wrong, can't add a new zone")
			return
		end
		ZonesData[tostring(newid)] = NewZone
		success, err = self:CreateZone(NewZone)
		if(not success) then
			rust.SendChatMessage(player,"SERVER",err)
			return
		end
		rust.SendChatMessage(player,"SERVER","You have successfully added zone: " .. args[0])
		self:SaveData()
		NewZone = nil
	end
end

-- -----------------------------------------------------------------------------
-- OXIDE HOOK
-- -----------------------------------------------------------------------------

-- -----------------------------------------------------------------------------
-- PLUGIN:OnEntityBuilt(helditem,gameObject)
-- called after a player built a structure
-- No return behavior
-- -----------------------------------------------------------------------------
function PLUGIN:OnEntityBuilt(helditem,gameobject)
	if(hasFlag(helditem.ownerPlayer,"-nobuild")) then
		if(not self.Config.AdminsCanForceBuild or helditem.ownerPlayer:GetComponent("BaseNetworkable").net.connection.authLevel <= self.Config.Settings.authLevel) then
			rust.SendChatMessage(helditem.ownerPlayer,"SERVER","You are not allowed to build here")
			gameobject:GetComponent("BaseEntity"):Kill(ProtoBuf.Mode.None,0,0,nulVector3)
		end
	end
end

-- -----------------------------------------------------------------------------
-- PLUGIN:OnEntityAttacked(entity,hitinfo)
-- called when trying to hit an entity
-- if return behavior not null, will cancel the damage
-- -----------------------------------------------------------------------------

function PLUGIN:OnEntityAttacked(entity,hitinfo)
	if(entity:ToPlayer()) then
		if( (entity:ToPlayer():IsSleeping()) and hasFlag(entity:ToPlayer(),"-sleepgod") ) then
			return false
		elseif(not entity:ToPlayer():IsSleeping() and hasFlag(entity:ToPlayer(),"-god")) then
			return false
		end
	elseif(entity:GetComponent("BuildingBlock") or entity:GetComponent("WorldItem")) then
		if(hitinfo ~= nil and hitinfo.Initiator and hitinfo.Initiator:ToPlayer()) then
			if(hasFlag(hitinfo.Initiator:ToPlayer(),"-undestr")) then
				--rust.SendChatMessage(hitinfo.Initiator:ToPlayer(),"SERVER","This building can't be attacked")
				return false
			end
		end
	end
end

-- -----------------------------------------------------------------------------
-- PLUGIN:OnRunCommand(arg,wantsfeedback)
-- called when trying to do a console command
-- if return behavior not null, will cancel the command
-- -----------------------------------------------------------------------------
function PLUGIN:OnRunCommand(arg, wantsfeedback)
    -- Sanity checks
    if (not arg) then return end
    if (not arg.connection) then return end
    if (not arg.connection.player) then return end
    if (not arg.cmd) then return end
    if (not arg.cmd.name) then return end

    -- Create friendly variables
    local player = arg.connection.player
    local command = arg.cmd.name
    local blocked = false
	if(player and command == "kill") then
		if(hasFlag(player,"-nosuicide")) then
			rust.SendChatMessage(player,"You are not allowed to suicide here")
			return false
		end
	end
end

-- -----------------------------------------------------------------------------
-- PLUGIN:OnPlayerChat( arg )
-- called when trying to chat
-- if return behavior not null, will cancel the chat
-- -----------------------------------------------------------------------------

function PLUGIN:OnPlayerChat( arg )
	if(not arg.connection) then return end
	if (not arg.connection.player) then return end
	if(arg.connection.player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then return end
	if(hasFlag(arg.connection.player,"-nochat")) then
		rust.SendChatMessage(arg.connection.player,"You are not allowed to chat here")
		return false
	end
end

-- -----------------------------------------------------------------------------
-- PLUGIN:OnPlayerDisconnected(baseplayer,connection)
-- called when disconnecting
-- No return behavior
-- -----------------------------------------------------------------------------
function PLUGIN:OnPlayerDisconnected(player,connection)
	if(hasFlag(player,"-killsleepers")) then
		player:Die()
	end
end

-- -----------------------------------------------------------------------------
-- PLUGIN:OnEntityEnter(triggerbase,entity)
-- called when an entity is entering a zone (animals or players)
-- No return behavior
-- -----------------------------------------------------------------------------
function PLUGIN:OnEntityEnter(triggerbase,entity)
	if(triggerbase:GetComponent(global.TriggerRadiation._type)) then
		if(entity:GetComponentInParent(global.BasePlayer._type)) then
			if(RadiationZones[triggerbase:GetComponent(global.TriggerRadiation._type)]) then
				if(RadiationZones[triggerbase:GetComponent(global.TriggerRadiation._type)].enter  ~= "default") then
					rust.SendChatMessage(entity,tostring(RadiationZones[triggerbase:GetComponent(global.TriggerRadiation._type)].enter))
					self:addPlayerZone(entity:GetComponentInParent(global.BasePlayer._type),triggerbase:GetComponent(global.TriggerRadiation._type))
				end
			end
		end
	elseif(triggerbase:GetComponent(global.BuildPrivilegeTrigger._type)) then
		if(entity:GetComponentInParent(global.BasePlayer._type)) then
			self:UpdatePlayerBuildingPrivilege(entity:GetComponentInParent(global.BasePlayer._type),true)
		end
	end
end
-- -----------------------------------------------------------------------------
-- PLUGIN:OnEntityLeave(triggerbase,entity)
-- called when an entity is leaving a zone (animals or players)
-- No return behavior
-- -----------------------------------------------------------------------------
function PLUGIN:OnEntityLeave(triggerbase,entity)
	if(triggerbase:GetComponent(global.TriggerRadiation._type)) then
		if(entity:GetComponentInParent(global.BasePlayer._type)) then
			if(RadiationZones[triggerbase:GetComponent(global.TriggerRadiation._type)]) then
				if(RadiationZones[triggerbase:GetComponent(global.TriggerRadiation._type)].leave  ~= "default") then
					rust.SendChatMessage(entity,tostring(RadiationZones[triggerbase:GetComponent(global.TriggerRadiation._type)].leave))
					self:removePlayerZone(entity:GetComponentInParent(global.BasePlayer._type),triggerbase:GetComponent(global.TriggerRadiation._type))
				end
			end
		end
	elseif(triggerbase:GetComponent(global.BuildPrivilegeTrigger._type)) then
		if(entity:GetComponentInParent(global.BasePlayer._type)) then
			self:UpdatePlayerBuildingPrivilege(entity:GetComponentInParent(global.BasePlayer._type),true)
		end
	end
end
-- -----------------------------------------------------------------------------
-- -----------------------------------------------------------------------------
-- -----------------------------------------------------------------------------


-- -----------------------------------------------------------------------------
-- PLUGIN:isZone(zone)
-- check if the zone was already made, and if so adds it in the plugin active zones.
-- -----------------------------------------------------------------------------
function PLUGIN:isZone(zone)
	newpos = new( UnityEngine.Vector3._type , nil )
	newpos.x = zone.p.x
	newpos.y = zone.p.y
	newpos.z = zone.p.z
	allRadiationZone = UnityEngine.Object.FindObjectsOfTypeAll(global.TriggerRadiation._type)
	for i=0, allRadiationZone.Length-1 do
		if(allRadiationZone[i]:GetComponent(UnityEngine.Transform._type)) then
			if(allRadiationZone[i]:GetComponent(UnityEngine.Transform._type).transform.position.x == newpos.x and allRadiationZone[i]:GetComponent(UnityEngine.Transform._type).transform.position.z == newpos.z) then
				allRadiationZone[i].RadiationAmount = 0
				allRadiationZone[i]:GetComponent(UnityEngine.SphereCollider._type).radius = zone.r
				RadiationZones[allRadiationZone[i]] = {
					name=zone.name,
					enter=zone.em,
					leave=zone.lm,
					options=zone.o,
				}
				if(zone.o["-radiation"]) then
					allRadiationZone[i].RadiationAmount = zone.o["-radiation"]
				end
				return true
			end
		end
	end
	return false
end

-- -----------------------------------------------------------------------------
-- PLUGIN:CreateZone(zone)
-- creates a new zone and adds it in the plugin active zone
-- called on new zone or on server initializing
-- -----------------------------------------------------------------------------
function PLUGIN:CreateZone(zone)
	newpos = new( UnityEngine.Vector3._type , nil )
	newpos.x = zone.p.x
	newpos.y = zone.p.y
	newpos.z = zone.p.z
	arr = util.TableToArray( { "autospawn/monument/generic/random_radiation", newpos, new( UnityEngine.Quaternion._type , nil ), true } )
	local newBaseEntity = global.GameManager.CreatePrefab.methodarray[0]:Invoke(nil,arr)
	if(not newBaseEntity) then
		return false, "couldnt create a new zone"
	end
	allRadiationZone = UnityEngine.Object.FindObjectsOfTypeAll(global.TriggerRadiation._type)
	for i=0, allRadiationZone.Length-1 do
		if(allRadiationZone[i]:GetComponent(UnityEngine.Transform._type) and allRadiationZone[i]:GetComponent(UnityEngine.SphereCollider._type)) then
			if(allRadiationZone[i]:GetComponent(UnityEngine.Transform._type).transform.position.x == newpos.x and allRadiationZone[i]:GetComponent(UnityEngine.Transform._type).transform.position.z == newpos.z) then
				allRadiationZone[i].RadiationAmount = 0
				allRadiationZone[i]:GetComponent(UnityEngine.SphereCollider._type).radius = zone.r
				RadiationZones[allRadiationZone[i]] = {
					name=zone.n,
					enter=zone.em,
					leave=zone.lm,
					options=zone.o
				}
				if(zone.o["-radiation"]) then
					allRadiationZone[i].RadiationAmount = zone.o["-radiation"]
				end
				print("Successfully created zone name: " .. tostring(zone.n))
				return true
			end
		end
	end
	return false, "zone was created but couldn't set any arguments"
end
