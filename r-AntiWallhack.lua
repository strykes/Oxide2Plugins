PLUGIN.Name = "r-AntiWallhack"
PLUGIN.Title = "r-AntiWallhack"
PLUGIN.Version = V(0, 0, 1)
PLUGIN.Description = "Anti Wallhack, this will detect players walking in walls"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = false

function PLUGIN:Init()
	WHData = {}
end
local trigger
local intLayers
function PLUGIN:OnServerInitialized()
	trigger = UnityEngine.Object.FindObjectsOfTypeAll(global.TriggerRadiation._type)
	intLayers = trigger[trigger.Length-1]:GetComponent(global.TriggerBase._type).interestLayers
	
	local pluginList = plugins.GetAll()
    for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "Enhanced Ban System" then
            ebs = pluginList[i].Object
            break
        end
    end
   	
end

local function Distance2D(p1, p2)
    return math.sqrt(math.pow(p1.x - p2.x,2) + math.pow(p1.z - p2.z,2)) 
end
local buildingradius = 1
local function AntiWallHackSphere(entity)
	arr = util.TableToArray( { entity.transform.position , buildingradius } )
	util.ConvertAndSetOnArray(arr, 1, buildingradius, System.Single._type)
	hits = UnityEngine.Physics.OverlapSphere["methodarray"][1]:Invoke(nil,arr)
	it = hits:GetEnumerator()
	toremove = true
	while (it:MoveNext()) do 
		if(it.Current:GetComponentInParent(global.BuildingBlock._type) and it.Current:GetComponentInParent(global.BuildingBlock._type).blockDefinition.name == "wall") then
			toremove = false
		end
	end
	if(toremove) then
		triggerbase = entity.gameObject
		arr = util.TableToArray( { triggerbase } )
		UnityEngine.Object.Destroy.methodarray[1]:Invoke( nil , arr)
		return false
	end
	return true
end
function PLUGIN:addDetection(baseplayer)
	if(not WHData[baseplayer]) then 
		WHData[baseplayer] = {} 
		WHData[baseplayer].count = 0 
		WHData[baseplayer].last = time.GetUnixTimestamp()
	end
	if(time.GetUnixTimestamp() > (WHData[baseplayer].last + 10)) then
		WHData[baseplayer].count = 1
	else
		WHData[baseplayer].count = WHData[baseplayer].count + 1
	end
	WHData[baseplayer].last = time.GetUnixTimestamp()
	if(WHData[baseplayer].count >= 3) then
		ebs:Ban(nil, baseplayer, "r-Wallhack", false)
	end
end
function PLUGIN:OnEntitySpawn(gameobject)
	if(gameobject:GetComponentInParent(global.BuildingBlock._type) and gameobject:GetComponentInParent(global.BuildingBlock._type).blockDefinition.name == "wall") then
		local newgameabj = new( UnityEngine.GameObject._type , nil )
		if(newgameabj) then
			newpos = gameobject.transform.position
			if(newpos) then
				newpos.y = newpos.y + 1.5
				newgameabj.layer = UnityEngine.LayerMask.NameToLayer("Trigger")
				newgameabj.name = "AntiWallHack"
				newgameabj:GetComponentInParent(UnityEngine.Transform._type).position = newpos
				
				newgameabj:AddComponent(UnityEngine.SphereCollider._type)
				newgameabj:GetComponentInParent(UnityEngine.SphereCollider._type).radius = 0.01
				newgameabj:AddComponent(global.TriggerBase._type)
				newgameabj:GetComponentInParent(global.TriggerBase._type).interestLayers = intLayers
				newgameabj:SetActive(true);
			end
		end
	end
end

function PLUGIN:OnEntityEnter(triggerbase,entity)
	if(triggerbase.gameObject.name == "AntiWallHack") then
		if(	entity:GetComponentInParent(global.BasePlayer._type) ) then
			if(Distance2D(entity.transform.position,triggerbase.transform.position) < 0.5) then
				if(AntiWallHackSphere(triggerbase)) then
					self:addDetection(entity:GetComponentInParent(global.BasePlayer._type))
					print(tostring(entity:GetComponentInParent(global.BasePlayer._type).displayName .. " is inside a wall " .. Distance2D(entity.transform.position,triggerbase.transform.position)))
				end
			end
		end
	end
end

function PLUGIN:DumpGameObject(_gameObj)
    local types = UnityEngine.Component._type
    local _components = _gameObj:GetComponents(types)
    print("Found Component List?: " .. tostring(_components))
    print("Found Entries #: " .. tostring(_components.Length))
    if (_components.Length == 0) then
        print("Empty table")
    else
        for i = 0, _components.Length - 1 do
            print("Found Component: " .. tostring(_components[i]))
        end
    end
    print(" - - - - - - - - - - - - ")
    local _components = _gameObj:GetComponentsInChildren(types)
    print("Found Children Component List?: " .. tostring(_components))
    print("Found Entries #: " .. tostring(_components.Length))
    if (_components.Length == 0) then
        print("Empty table")
    else
        for i = 0, _components.Length - 1 do
            print("Found Component: " .. tostring(_components[i]))
        end
    end
    print(" - - - - - - - - - - - - ")
    local _components = _gameObj:GetComponentsInParent(types)
    print("Found Parent Component List?: " .. tostring(_components))
    print("Found Entries #: " .. tostring(_components.Length))
    if (_components.Length == 0) then
        print("Empty table")
    else
        for i = 0, _components.Length - 1 do
            print("Found Component: " .. tostring(_components[i]))
        end
    end
end
