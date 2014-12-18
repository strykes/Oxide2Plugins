PLUGIN.Name = "r-prod"
PLUGIN.Title = "r-Prod"
PLUGIN.Version = V(1,2,1)
PLUGIN.Description = "Know who owns a building, deployables, or have access to a Tool Cupboard"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true

function PLUGIN:Init()
    command.AddChatCommand( "prod",  self.Object, "cmdProd" )
	local pluginList = plugins.GetAll()
    for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "Building Owners" then
            buildingowners = pluginList[i].Object
            break
        end
    end
	for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "deadPlayerList" then
            deadplayerlist = pluginList[i].Object
            break
        end
    end
	if(not buildingowners) then
		print("To use the prod plugin you may want to use the buildingowners plugin")
	end
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. msg .. "\"" );
end
function PLUGIN:LoadDefaultConfig()
    self.Config.ProdForModerators = true
	self.Config.ProdForPlayers = false
end

function PLUGIN:cmdProd( player, cmd, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local levelneeded = 2
	if(self.Config.ProdForModerators) then
		levelneeded = 0
	end 
	if(self.Config.ProdForPlayers) then
		levelneeded = 0
	end 
	if(authlevel and authlevel >= levelneeded) then
		local arr = util.TableToArray( { player.eyes:Ray()  } )
		local hits = UnityEngine.Physics.RaycastAll["methodarray"][1]:Invoke(nil,arr)
		local it = hits:GetEnumerator()
		local gotinfo = {}
		while (it:MoveNext()) do
			if(buildingowners and it.Current.collider:GetComponentInParent(global.BuildingBlock._type)) then
				local buildingblock = it.Current.collider:GetComponentInParent(global.BuildingBlock._type)
				if(buildingblock) then
					local ownerid = buildingowners:FindBlockData(buildingblock)
					if(ownerid) then
						local target = self:FindPlayer(ownerid)
						if(not target) then
							ChatMessage(player,"Owner of this house is: "..ownerid)
						else
							ChatMessage(player,"Owner of this house is: " ..target.displayName .. " - " .. ownerid)
						end
					else
						ChatMessage(player,"No owner was found for this house")
					end
				end
			elseif(it.Current.collider:GetComponentInParent(global.BuildingPrivlidge._type)) then
				if(string.find(it.Current.collider.gameObject.name,"cupboard")) then
					local bp = rust.UserIDsFromBuildingPrivilege( it.Current.collider:GetComponentInParent(global.BuildingPrivlidge._type) )
					if(bp.Length >= 1) then
						ChatMessage(player,"Players with building permission:")
						for i=0, bp.Length - 1 do 
							local target = self:FindPlayer(bp[i])
							if(not target) then
								ChatMessage(player,bp[i])
							else
								ChatMessage(player,bp[i] .. " - " .. target.displayName)
							end
						end
					else
						ChatMessage(player,"No players have building permission here")
					end
				end
			elseif(it.Current.collider:GetComponentInParent(global.DeployedItem._type)) then
				if(string.find(it.Current.collider.gameObject.name,"world")) then
					local steamid = rust.UserIDFromDeployedItem(it.Current.collider:GetComponentInParent(global.DeployedItem._type))
					local target = self:FindPlayer(steamid)
					if(not target) then
						ChatMessage(player,"Owner of this deployable: " .. steamid)
					else
						ChatMessage(player,"Owner of this deployable: " .. target.displayName .. " - " .. steamid)
					end
				end
			end
		end
	end
end

function PLUGIN:FindPlayer(steamid)
	local targetplayer = false
	local allBasePlayer = UnityEngine.Object.FindObjectsOfTypeAll(global.BasePlayer._type)
	for i = 0, tonumber(allBasePlayer.Length - 1) do
		local currentplayer = allBasePlayer[ i ];
		if(steamid == rust.UserIDFromPlayer(currentplayer)) then
			return currentplayer
		end
	end
	if(not targetplayer) then 
		if deadplayerlist then
			targetsteamid,targetplayer = deadplayerlist:FindDeadPlayer(target)
		end
		if(not targetsteamid) then
			return false, "No players found" 
		end
		targetplayer.userID = targetsteamid
	end
	return targetplayer
end
