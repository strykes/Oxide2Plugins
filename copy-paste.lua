PLUGIN.Name = "Copy-Paste"
PLUGIN.Title = "Copy-Paste"
PLUGIN.Version = V(1, 2, 2)
PLUGIN.Description = "Copy & Paste buildings"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true
 
function PLUGIN:Init()
    command.AddChatCommand( "copy",  self.Object, "cmdCopy" )
    command.AddChatCommand( "paste",  self.Object, "cmdPaste" )
    command.AddChatCommand( "cplist",  self.Object, "cmdCplist" )
    command.AddChatCommand( "cphelp",  self.Object, "cmdCphelp" )
    command.AddChatCommand( "placeback",  self.Object, "cmdPlaceback" )
end
function PLUGIN:LoadDefaultConfig()
	self.Config.Settings = {}
	self.Config.Settings.authLevel = 1
	self.Config.PluginFilesList = {}
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. msg .. "\"" );
end
function PLUGIN:Unload()
	if(self.Timer) then self.Timer:Destroy() end
end
local function getFileName( name, steamid )
	return "copypaste-"..name
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
	local deployeditem = {}
	local fakephysics = {}
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
			elseif(it.Current:GetComponentInParent(global.FakePhysics._type)) then
				local fworlditem = it.Current:GetComponentInParent(global.WorldItem._type)
				if(not fakephysics[fworlditem]) then
					fakephysics[fworlditem] = true
				end
			elseif(it.Current:GetComponentInParent(global.DeployedItem._type)) then
				local fworlditem = it.Current:GetComponentInParent(global.WorldItem._type)
				if(not deployeditem[fworlditem]) then
					deployeditem[fworlditem] = true
				end
			end
		end
	end
	return house, deployeditem, fakephysics
end
local function setGrade( buildingblock, newgrade )
	if(buildingblock.blockDefinition and buildingblock.blockDefinition.grades) then
		if(newgrade > (buildingblock.blockDefinition.grades.Length-1)) then
			newgrade = buildingblock.blockDefinition.grades.Length-1
		end
		buildingblock:SetGrade( newgrade )
	end
end
local function SpawnBuilding( spawning )
	local newobj = UnityEngine.Object.Instantiate.methodarray[1]:Invoke(nil, util.TableToArray( { spawning.pr } ))
	local newblock = newobj:GetComponent("BuildingBlock")
	newblock.transform.position = spawning.p
	newblock.transform.rotation = spawning.r
	newblock.gameObject:SetActive(true)
	local newblock2 = newblock.gameObject:GetComponent("BuildingBlock")
	newblock2.blockDefinition = global.Library.FindByPrefabID(newblock.prefabID)
	setGrade(newblock2, tonumber(spawning.s))
	newblock2:GetComponent("BaseCombatEntity").health = tonumber(spawning.h)
	timer.Once(0.1, function() newblock2:Spawn(true) end)
end
function PLUGIN:TheTick( )
	if(not listtospawn[1]) then return end
	SpawnBuilding( listtospawn[1] )
	table.remove(listtospawn,1)
	self.Timer = timer.Once(0.05, function() self:TheTick() end )
end
function PLUGIN:PasteBuilding( structure, originpos, rawRot, heightAdjustment )
	local OriginRotation = new( UnityEngine.Vector3._type, nil )
	originpos.y = originpos.y + heightAdjustment
	OriginRotation.x = 0
	OriginRotation.z = 0
	OriginRotation.y = rawRot.y
	spawned = 0
	local newRot = new( UnityEngine.Vector3._type, nil )
	local newPos = new( UnityEngine.Vector3._type, nil )
	local originrot = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { OriginRotation } ) )
	for i,buildingBlockBP in pairs(structure) do
		newPos.x = buildingBlockBP.pos.x
		newPos.y = buildingBlockBP.pos.y
		newPos.z = buildingBlockBP.pos.z
		newRot.x = buildingBlockBP.rot.x
		newRot.y = buildingBlockBP.rot.y
		newRot.z = buildingBlockBP.rot.z
		local quaternionRot = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { newRot } ) )
		local newAngles = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { UnityEngine.Vector3.op_Addition(newRot,rawRot) } ) )
		local arr = util.TableToArray( { originrot,newPos  } )
		local temppos = UnityEngine.Quaternion.op_Multiply.methodarray[1]:Invoke(nil,arr)
		local newtemppos = UnityEngine.Vector3.op_Addition(originpos,temppos)
		local prefab = global.GameManager.FindPrefab( buildingBlockBP.prefabname )
		if(prefab) then
			SpawnBuilding( {pr=prefab,p=newtemppos,r=newAngles,h=buildingBlockBP.health,s=buildingBlockBP.stage} )
		end
	end
	return true
end

local function FillContainer( baseItem, itemlist )
	local pref = baseItem.contents
	for i,item in pairs(itemlist) do
		local giveitem = global.ItemManager.CreateByItemID(item.ID,item.amount)
		if(giveitem) then
			giveitem:MoveToContainer( pref )
			if(item.container) then
				FillContainer( giveitem, item.container )
			end
		end
	end
end
local function PasteDeployables( player, deployables, originpos, rawRot, heightAdjustment )
	local newPos = new( UnityEngine.Vector3._type, nil )
	local newRot = new( UnityEngine.Vector3._type, nil )
	originpos.y = originpos.y + heightAdjustment
	newRot.x = 0
	newRot.z = 0
	newRot.y = rawRot.y
	local originrot = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { newRot } ) )
	for i,deployable in pairs(deployables) do
		newPos.x = deployable.pos.x
		newPos.y = deployable.pos.y
		newPos.z = deployable.pos.z
		newRot.x = deployable.rot.x
		newRot.y = deployable.rot.y
		newRot.z = deployable.rot.z
		local quaternionRot = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { newRot } ) )
		local newAngles = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { UnityEngine.Vector3.op_Addition(newRot,rawRot) } ) )
		local arr = util.TableToArray( { originrot, newPos  } )
		local temppos = UnityEngine.Quaternion.op_Multiply.methodarray[1]:Invoke(nil,arr)
		local newtemppos = UnityEngine.Vector3.op_Addition(originpos,temppos)
		newtemppos.y = newtemppos.y - 0.5
		local newItem = global.ItemManager.CreateByName(deployable.prefabname,1)
		if(newItem) then
			local it = newItem.info.modules:GetEnumerator()
			local deployModule = false
			while (it:MoveNext()) do
				if(it.Current.deployablePrefabName and it.Current.deployablePrefabName ~= "deployablePrefabName") then deployModule = it.Current end
			end
			if(deployModule) then
				local newBaseEntity = global.GameManager.CreateEntity(deployModule.deployablePrefabName, newtemppos, newAngles)
				if(newBaseEntity) then
					newBaseEntity:SendMessage("SetDeployedBy", player, UnityEngine.SendMessageOptions.DontRequireReceiver )
					newBaseEntity:SendMessage("InitializeItem", newItem, UnityEngine.SendMessageOptions.DontRequireReceiver )
					newBaseEntity:Spawn(true)
					newItem:SetWorldEntity(newBaseEntity)
					if( deployable.container ) then
						FillContainer( newItem , deployable.container )
					end
				end
			end
		end
	end
	return true
end
local function PasteFakePhysics( player, deployables, originpos, rawRot, heightAdjustment )
	local newPos = new( UnityEngine.Vector3._type, nil )
	local newRot = new( UnityEngine.Vector3._type, nil )
	originpos.y = originpos.y + heightAdjustment
	newRot.x = 0
	newRot.z = 0
	newRot.y = rawRot.y
	local originrot = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { newRot } ) )
	for i,deployable in pairs(deployables) do
		newPos.x = deployable.pos.x
		newPos.y = deployable.pos.y
		newPos.z = deployable.pos.z
		newRot.x = deployable.rot.x
		newRot.y = deployable.rot.y
		newRot.z = deployable.rot.z
		local quaternionRot = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { newRot } ) )
		local newAngles = UnityEngine.Quaternion.EulerRotation.methodarray[1]:Invoke(nil, util.TableToArray( { UnityEngine.Vector3.op_Addition(newRot,rawRot) } ) )
		local arr = util.TableToArray( { originrot, newPos  } )
		local temppos = UnityEngine.Quaternion.op_Multiply.methodarray[1]:Invoke(nil,arr)
		local newtemppos = UnityEngine.Vector3.op_Addition(originpos,temppos)
		newtemppos.y = newtemppos.y - 0.5
		local newItem = global.ItemManager.CreateByName(deployable.prefabname,deployable.amount)
		if(newItem) then
			local newBaseEntity = newItem:CreateWorldObject(newtemppos, newAngles)
			if( deployable.container ) then
				FillContainer( newItem , deployable.container )
			end
		end
	end
	return true
end
local function GeneratePos( position, diffrotation )
	local newstructurex = (position.x * math.cos(diffrotation)) + (position.z * math.sin(diffrotation))
	local newstructurez = (position.z * math.cos(diffrotation))  - (position.x * math.sin(diffrotation))
	position.x = newstructurex
	position.z = newstructurez
	return position
end
local function GenerateCleanHouse( rawHouse, originpos, originrot )
	local cleanHouse = {}
	for rawStructure,k in pairs(rawHouse) do
		local normRotY = (rawStructure.transform.rotation:ToEulerAngles().y - originrot)
		local transformedPos = rawStructure.transform.position
		transformedPos.x = transformedPos.x - originpos.x
		transformedPos.y = transformedPos.y - originpos.y
		transformedPos.z = transformedPos.z - originpos.z
		local normPos = GeneratePos( transformedPos , -originrot)
		local tbl = {}
		tbl.prefabname = rawStructure.blockDefinition.fullname
		tbl.health = rawStructure:GetComponent("BaseCombatEntity").health
		tbl.grade = rawStructure.grade
		tbl.pos = {}
		tbl.pos.x = normPos.x
		tbl.pos.y = normPos.y
		tbl.pos.z = normPos.z
		tbl.rot = {}
		tbl.rot.x = rawStructure.transform.rotation:ToEulerAngles().x
		tbl.rot.y = normRotY
		tbl.rot.z = rawStructure.transform.rotation:ToEulerAngles().z
		table.insert(cleanHouse,tbl)
	end
	return cleanHouse
end
local function GetItemContainer( itemContainer )
	local items = {}
	local itemlist = itemContainer.contents.itemList
	if(itemlist and itemlist ~= nil ) then
		local it = itemlist:GetEnumerator()
		while (it:MoveNext()) do
			local item = {}
			item.ID = it.Current.info.itemid
			item.amount = it.Current.amount
			if(it.Current.contents and it.Current.contents ~= "contents") then
				item.container = GetItemContainer( it.Current )
			end
			table.insert(items,item)
		end
	end
	return items
end
local function GenerateCleanDeployables( rawDeployables, originpos, originrot )
	local cleanDeployables = {}
	for rawDeployable,k in pairs(rawDeployables) do
		local normRotY = (rawDeployable.transform.rotation:ToEulerAngles().y - originrot)
		local transformedPos = rawDeployable.transform.position
		transformedPos.x = transformedPos.x - originpos.x
		transformedPos.y = transformedPos.y - originpos.y
		transformedPos.z = transformedPos.z - originpos.z
		local normPos = GeneratePos( transformedPos , -originrot)
		local tbl = {}
		tbl.prefabname = rawDeployable.item.info.shortname
		tbl.health = rawDeployable.item.health
		tbl.amount = rawDeployable.item.amount
		tbl.pos = {}
		tbl.pos.x = normPos.x
		tbl.pos.y = normPos.y
		tbl.pos.z = normPos.z
		tbl.rot = {}
		tbl.rot.x = rawDeployable.transform.rotation:ToEulerAngles().x
		tbl.rot.y = normRotY
		tbl.rot.z = rawDeployable.transform.rotation:ToEulerAngles().z
		if(rawDeployable.item.contents and rawDeployable.item.contents ~= "contents") then
			tbl.container = GetItemContainer( rawDeployable.item )
		end
		table.insert(cleanDeployables,tbl)
	end
	return cleanDeployables
end
function PLUGIN:cmdCopy( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		if(args.Length == 0) then
			ChatMessage(player,"Usage: /copy TEXTFILE, will save the building in a text file")
			return
		end
		local closestent, closesthitpoint = DoRaycastPlayer( player )
		if(not closestent) then ChatMessage(player,"Couldn't find any entity") return end
		local buildingblock = closestent:GetComponentInParent(global.BuildingBlock._type)
		if(not buildingblock) then ChatMessage(player,"The entity that you are looking at isn't a structure") return end
		local rawHouse, rawDeployables, rawFakePhysics = GetBuilding( buildingblock )
		if(rawHouse.Length == 0) then ChatMessage(player,"Something went wrong!! rawHouse is empty?") end
		local cleanHouse = GenerateCleanHouse( rawHouse , buildingblock.transform.position , player.eyes.rotation:ToEulerAngles().y )
		local cleanDeployables = GenerateCleanDeployables( rawDeployables , buildingblock.transform.position , player.eyes.rotation:ToEulerAngles().y )
		local cleanFakePhysics = GenerateCleanDeployables( rawFakePhysics , buildingblock.transform.position , player.eyes.rotation:ToEulerAngles().y )
		if(#cleanHouse == 0) then ChatMessage(player,"Something went wrong!! cleanHouse is empty?") end
		local defaultValues = {}
		defaultValues.yrotation = tostring(buildingblock.transform.rotation:ToEulerAngles().y)
		defaultValues.position = {}
		defaultValues.position.x = tostring(buildingblock.transform.position.x)
		defaultValues.position.y = tostring(buildingblock.transform.position.y)
		defaultValues.position.z = tostring(buildingblock.transform.position.z)
		local filename = getFileName( tostring(args[0]), rust.UserIDFromPlayer( player ) )
		local Data = datafile.GetDataTable( filename )
		Data = Data or {}
		Data.structure = cleanHouse
		Data.deployables = cleanDeployables
		Data.fakephysics = cleanFakePhysics
		Data.default = defaultValues
		datafile.SaveDataTable( filename )
		self.Config.PluginFilesList[ #self.Config.PluginFilesList + 1 ] = {name=tostring(args[0]),steamid=rust.UserIDFromPlayer( player )}
		self:SaveConfig()
		ChatMessage(player,"The house \"" .. args[0] .. "\" was successfully saved")
		ChatMessage(player,#cleanHouse .. " building parts detected")
		ChatMessage(player,#cleanDeployables .. " deployables detected")
		ChatMessage(player,#cleanFakePhysics .. " bags detected")
	end
end

local function CheckDataIntegrity(Data)
	for i,buildingBlockBP in pairs(Data.structure) do
		if(not buildingBlockBP.stage) then
			Data.structure[i].stage = 1
		end
	end
	return Data
end

function PLUGIN:cmdPlaceback(player, cmd, args)
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		if(args.Length == 0) then
			ChatMessage(player,"Usage: /placeback TEXTFILE, to placeback a building where it was.")
			return
		end
		if(args.Length == 2) then
			if(tonumber(args[1]) ~= nil) then
				deltaheight = tonumber(args[1])
			end
		end
		local Data = datafile.GetDataTable( getFileName(args[0], rust.UserIDFromPlayer( player )) )
		Data = Data or {}
		if(not Data.structure) then ChatMessage(player,"This file doesn't exist or doesnt contain a building blueprint.") return end
		local Data = CheckDataIntegrity( Data )
		local rawRot = player.eyes.rotation:ToEulerAngles()
		rawRot.x = 0
		rawRot.z = 0
		rawRot.y = tonumber(Data.default.yrotation)
		local rawPos = new( UnityEngine.Vector3._type , nil )
		rawPos.x = tonumber(Data.default.position.x)
		rawPos.y = tonumber(Data.default.position.y)
		rawPos.z = tonumber(Data.default.position.z)
		local success, err = self:PasteBuilding(Data.structure, rawPos, rawRot, 0 )
		if(not success) then
			ChatMessage(player,err)
			return
		end
	end
end

function PLUGIN:cmdPaste( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel >= self.Config.Settings.authLevel) then
		if(args.Length == 0) then
			ChatMessage(player,"Usage: /paste TEXTFILE optional:HeightAdjustment, to paste a building.")
			return
		end
		local deltaheight = 0.5
		if(args.Length == 2) then
			if(tonumber(args[1]) ~= nil) then
				deltaheight = tonumber(args[1])
			end
		end
		local closestent, closesthitpoint = DoRaycastPlayer( player )
		if(not closestent) then ChatMessage(player,"No solid ground was found. Look at the ground to paste your building.") return end
		local Data = datafile.GetDataTable( getFileName(args[0], rust.UserIDFromPlayer( player )) )
		Data = Data or {}
		if(not Data.structure) then ChatMessage(player,"This file doesn't exist or doesnt contain a building blueprint.") return end
		local Data = CheckDataIntegrity( Data )
		local rawRot = player.eyes.rotation:ToEulerAngles()
		rawRot.x = 0
		rawRot.z = 0
		local success, err = self:PasteBuilding(Data.structure, closesthitpoint,rawRot, deltaheight )
		if(not success) then
			ChatMessage(player,err)
			return
		end
		if(Data.deployables and #Data.deployables > 0) then
			local success, err = PasteDeployables( player, Data.deployables, closesthitpoint,rawRot, deltaheight )
			if(not success) then
				ChatMessage(player,err)
				return
			end
		end
		if(Data.fakephysics and #Data.fakephysics > 0) then
			local success, err = PasteFakePhysics( player, Data.fakephysics, closesthitpoint,rawRot, deltaheight )
			if(not success) then
				ChatMessage(player,err)
				return
			end
		end
	end
end
function PLUGIN:cmdCplist( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local playerID = rust.UserIDFromPlayer( player )
	if(authlevel >= self.Config.Settings.authLevel) then
		ChatMessage(player,"---------- Saved houses ----------")
		for i=1, #self.Config.PluginFilesList do
			local data = self.Config.PluginFilesList[i]
			
				ChatMessage(player,data.name)
			
		end
		ChatMessage(player,"-------------------------------------")
	end
end
function PLUGIN:cmdCphelp( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(authlevel >= self.Config.Settings.authLevel) then
		ChatMessage(player,"---------- Copy-Paste Plugin ----------")
		ChatMessage(player,"/cplist => to get all the buildings that were saved")
		ChatMessage(player,"/copy NAME => to copy a building into a file")
		ChatMessage(player,"/paste NAME => to paste a building from a file")
		ChatMessage(player,"/placeback NAME => to placeback a building were it was saved it")
		ChatMessage(player,"-------------------------------------------")
	end
end
function PLUGIN:SendHelpText(player)
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(authlevel >= self.Config.Settings.authLevel) then
		ChatMessage(player,"/cphelp => To get the full commands on the Copy-Paste Plugin")
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
