PLUGIN.Name = "Anti Wallhack"
PLUGIN.Title = "Anti Wallhack"
PLUGIN.Version = V(0, 0, 1)
PLUGIN.Description = "Basic wallhack detection for players that go threw walls."
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = false

local radius = 1
function PLUGIN:Init()
    FastTimers = {}
    SlowTimers = {}
    AdminList = {}
    local it = global.BasePlayer.activePlayerList
	for i=0, it.Count-1 do
		if(it[i]:GetComponent("BaseNetworkable").net.connection.authLevel > 0) then AdminList[it[i]] = true end
		SlowTimers[it[i]] = timer.Repeat( 15, 0, function() self:SlowTimerCheck(it[i]) end)
	end
end
local function logWarning(message)
	arrr =  util.TableToArray( { message } )
	util.ConvertAndSetOnArray(arrr, 0, message, UnityEngine.Object._type)
	UnityEngine.Debug.LogWarning.methodarray[0]:Invoke(nil, arrr)
end
local function Distance3D(p1, p2)
    return math.sqrt(math.pow(p1.x - p2.x,2) + math.pow(p1.z - p2.z,2) + math.pow(p1.y - p2.y,2)) 
end
local function SendAdmins(msg)
	for player,d in pairs(AdminList) do
		rust.SendChatMessage(player,"Anti-Wallhack",msg)
	end
end
local function isOnBuilding(player)
	arr = util.TableToArray( { player.transform.position , radius } )
	util.ConvertAndSetOnArray(arr, 1, radius, System.Single._type)
	hits = UnityEngine.Physics.OverlapSphere["methodarray"][1]:Invoke(nil,arr)
	it = hits:GetEnumerator()
	while (it:MoveNext()) do
		if(it.Current:GetComponentInParent(global.BuildingBlock._type)) then
			return true 
		end
	end
end
local function getWall(player)
	arr = util.TableToArray( { player.transform.position , radius } )
	util.ConvertAndSetOnArray(arr, 1, radius, System.Single._type)
	hits = UnityEngine.Physics.OverlapSphere["methodarray"][1]:Invoke(nil,arr)
	it = hits:GetEnumerator()
	while (it:MoveNext()) do
		if(it.Current:GetComponentInParent(global.BuildingBlock._type)) then
			if(tostring(it.Current:GetComponentInParent(global.BuildingBlock._type).blockDefinition.name) == "wall") then
				return it.Current:GetComponentInParent(global.BuildingBlock._type)
			end
		end
	end
	return false
end
function PLUGIN:Unload()
	for player,t in pairs(SlowTimers) do
		SlowTimers[player]:Destroy()
	end
	for player,t in pairs(FastTimers) do
		FastTimers[player]:Destroy()
	end
end
function PLUGIN:FastTimerCheck(player)
	playerwall = getWall(player)
	if(playerwall and (playerwall:Health() == playerwall:MaxHealth())) then
		if(Distance3D(playerwall.transform.position,player.transform.position) <= 0.5) then
			SendAdmins( player.displayName .. " is currently in a wall" )
			print( player.displayName .. " - " .. rust.UserIDFromPlayer(player) .. " - is currently in a wall @ " .. player.transform.position.x .. " " .. player.transform.position.y .. " " .. player.transform.position.z )
			logWarning( player.displayName .. " - " .. rust.UserIDFromPlayer(player) .. " - is currently in a wall" )
		end
	end
end
function PLUGIN:SlowTimerCheck(player)
	if(isOnBuilding(player)) then
		if(not FastTimers[player]) then
			FastTimers[player] = timer.Repeat( 1, 0, function() self:FastTimerCheck(player) end)
			return
		end
	end
	if(FastTimers[player]) then
		FastTimers[player]:Destroy()
		FastTimers[player] = nil
	end
end
function PLUGIN:OnPlayerInit( player )
	if(player:GetComponent("BaseNetworkable").net.connection.authLevel > 0) then AdminList[player] = true end
	if( SlowTimers[player] ) then
		SlowTimers[player]:Destroy()
	end
	SlowTimers[player] = timer.Repeat( 15, 0, function() self:SlowTimerCheck(player) end)
end

function PLUGIN:OnPlayerDisconnected(player,connection)
	if( SlowTimers[player] ) then
		SlowTimers[player]:Destroy()
		SlowTimers[player] = nil
	end
	if( FastTimers[player] ) then
		FastTimers[player]:Destroy()
		FastTimers[player] = nil
	end
	if(AdminList[player]) then
		AdminList[player] = nil
	end
end
