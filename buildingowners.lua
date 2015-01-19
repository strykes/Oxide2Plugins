PLUGIN.Name = "buildingowners"
PLUGIN.Title = "Building Owners"
PLUGIN.Version = V(1, 3, 2)
PLUGIN.Description = "Manage building owners"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true
PLUGIN.ResourceId = 682

function PLUGIN:Init()
	self.ServerInitialized = false
	command.AddChatCommand( "changeowner", self.Object, "cmdChangeOwner" )
	timer.Once( 0.1, function() 
		self.ServerInitialized = true 
		
	end )
	self:LoadSavedData()
end
----------- All local functions for this plugin only -----------

----------- Get the position of the ground -----------
local function GetGround(pos)
	local Ray = new( UnityEngine.Ray._type, nil )
	Ray.direction = UnityEngine.Vector3.get_down()
	Ray.origin = pos
	local arr = util.TableToArray( { Ray } )
	local hits = UnityEngine.Physics.RaycastAll["methodarray"][1]:Invoke(nil,arr)
	local it = hits:GetEnumerator()
	local gotinfo = {}
	while (it:MoveNext()) do
		if(tostring(it.Current.collider.gameObject.name) == "Terrain") then
			return it.Current.point
		end
	end
	return false
end

----------- Get any building block around an entity -----------
local buildingradius = 3
local function FindBuilding(entity)
	local arr = util.TableToArray( { entity.transform.position , buildingradius } )
	util.ConvertAndSetOnArray(arr, 1, buildingradius, System.Single._type)
	local hits = UnityEngine.Physics.OverlapSphere["methodarray"][1]:Invoke(nil,arr)
	local it = hits:GetEnumerator()
	local buildingblock = false
	local distance = 9999
	while (it:MoveNext()) do
		if(it.Current:GetComponentInParent(global.BuildingBlock._type)) then
			if(string.find(tostring(it.Current:GetComponentInParent(global.BuildingBlock._type).blockDefinition.name),"foundation")) then
				buildingblock =  it.Current:GetComponentInParent(global.BuildingBlock._type)
				break
			end
		end
	end
	return buildingblock
end

----------- Load and save building owners data -----------
function PLUGIN:SaveData()  
    datafile.SaveDataTable( "BuildingOwners" )
end
function PLUGIN:LoadSavedData()
    ReverseData = datafile.GetDataTable( "BuildingOwners" )
    ReverseData = ReverseData or {}
	self:ReverseTable()
end

function PLUGIN:ReverseTable()
	OwnersData = {}
	for steamid,datable in pairs(ReverseData) do
		for dataid,height in pairs(datable) do
			OwnersData[height] = steamid
		end
	end
	print("Successfully initialized Building Owners")
end

----------- Load default configs -----------
function PLUGIN:LoadDefaultConfig()
	self.Config.useByType = 1
	self.Config.ChangeOwnerForModerators = true
end

----------- Change owner command -----------
function PLUGIN:cmdChangeOwner( player, com, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = 2
	if(self.Config.ChangeOwnerForModerators) then
		neededlevel = 1
	end
	if(authlevel and authlevel >= neededlevel) then
		if(args.Length >= 1) then
			----------- Get a player from the built in rust way -----------
			local targetplayer = global.BasePlayer.Find(tostring(args[0]))
			if(not targetplayer) then
				----------- Get a player from my way -----------
				local plistenum = player.activePlayerList
				local it = plistenum:GetEnumerator()
				while (it:MoveNext()) do
					if(string.find(it.Current.displayName,tostring(args[0]))) then
						if(targetplayer) then
							rust.SendChatMessage(player,"SERVER","Multiple players found")
							return
						end
						targetplayer = it.Current
					end
				end
				if(not targetplayer) then
					rust.SendChatMessage(player,"SERVER","This player " .. args[0] .. " doesn't exist")
					return
				end
			end
			local house = {}
			local gotdeployeditem = {}
			local checkfrom = {}
			local current = 0
			local buildingblock = FindBuilding(player)
			if(not buildingblock) then
				rust.SendChatMessage(player,"SERVER","You must be standing on a foundation of the building that you want to change the owner of")
				return
			end
			local posy = buildingblock.transform.position.y
			local userid = rust.UserIDFromPlayer(targetplayer)
			OwnersData[tostring(posy)] = userid
			
			if(not ReverseData[userid]) then ReverseData[userid] = {} end
			table.insert(ReverseData[userid],tostring(posy))
			local userid = rust.UserIDFromPlayer(targetplayer)
			local house = {}
			local checkfrom = {}
			local radius = 3
			house[buildingblock] = true
			checkfrom[ #checkfrom + 1 ] = buildingblock.transform.position
			local current = 0
			local gotdeployeditem = {}
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
							local posy = fbuildingblock.transform.position.y
							local groundpos = GetGround(fbuildingblock.transform.position)
							if(not groundpos) then groundpos = fbuildingblock.transform.position end
							local levels = math.floor( (posy - groundpos.y) / 3)
							local theposy = posy - (levels * 3)
							checkfrom[ #checkfrom + 1 ] = fbuildingblock.transform.position
							OwnersData[tostring(theposy)] = userid
							table.insert(ReverseData[userid],tostring(theposy))							
						end
					end
				end
			end
			rust.SendChatMessage(player,"SERVER","New owner of this house has been set to: " .. targetplayer.displayName)
			self:SaveData()
		end
	end
end

----------- Hook that sets a building owner for every buildingblock built (if needed) -----------
function PLUGIN:OnEntityBuilt(helditem, gameobject)
	if(self.ServerInitialized) then
		local buildingblock = gameobject:GetComponent("BuildingBlock")
		local posy = buildingblock.transform.position.y
		if(not OwnersData[tostring(posy)]) then
			local userid = rust.UserIDFromPlayer(helditem.ownerPlayer)
			OwnersData[tostring(posy)] = userid
			if(not ReverseData[userid]) then ReverseData[userid] = {} end
			table.insert(ReverseData[userid],tostring(posy))		
			self:SaveData()
		end
	end
end
function PLUGIN:AddBlockData(buildingblock,baseplayer)
	local userID = rust.UserIDFromPlayer(baseplayer)
	local pos = buildingblock.transform.position
	OwnersData[tostring(pos.y)] = userID
	if(not ReverseData[userID]) then ReverseData[userID] = {} end
	table.insert(ReverseData[userID],tostring(pos.y))		
end

----------- Function to get the owner of a building block from another plugins. -----------
function PLUGIN:FindBlockData(buildingblock)
	local posy = buildingblock.transform.position.y
	local groundpos = GetGround(buildingblock.transform.position)
	if(not groundpos) then groundpos = buildingblock.transform.position end
	local levels = math.floor( (posy - groundpos.y) / 3)
	local theposy = posy - (levels * 3)
	if(OwnersData[tostring(theposy)]) then
		return OwnersData[tostring(theposy)]
	end
	return false
end
