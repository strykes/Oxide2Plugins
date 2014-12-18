PLUGIN.Name = "r-remover"
PLUGIN.Title = "R-Remover Tool"
PLUGIN.Version = V(1, 4, 5)
PLUGIN.Description = "Remove tool for admins only"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true


function PLUGIN:Init()
	if(self.Config.RemoveForPlayers and not plugins.Exists("buildingowners")) then
		print("Error in R-Remover, you may want to have the \"Building Owners\" plugin installed to give ownership of buildings to certain players. Else it will only use ToolCupboard") 
	end
	local pluginList = plugins.GetAll()
    for i = 0, pluginList.Length - 1 do
        local pluginTitle = pluginList[i].Object.Title
        if pluginTitle == "Building Owners" then
            buildingowners = pluginList[i].Object
            break
        end
    end
	command.AddChatCommand( "remove", self.Object, "cmdRemove" )
	self.isRemoving = {}
	self.Removetimers = {}	
	timer.Once(0.1, function() 
		nulVector3 = new(UnityEngine.Vector3._type,nil) 
	end )
end
function PLUGIN:Unload()
	for k,v in pairs(self.Removetimers) do
		self.Removetimers[k]:Destroy()
	end
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. msg .. "\"" );
end
function PLUGIN:LoadDefaultConfig()
	self.Config.RemoveForModerators = self.Config.RemoveForModerators or true
	self.Config.RemoveForPlayers = self.Config.RemoveForPlayers or true
	self.Config.DefaultActivationTime = self.Config.DefaultActivationTime or 30
	self.Config.MaxAllowedTime = self.Config.MaxAllowedTime or 120
	self.Config.Refund = {}
	self.Config.Refund.activated = true
	self.Config.Refund.refundrate = 0.5
	self.Config.useToolCupboard = true
end

function PLUGIN:cmdRemove( player, com, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = 2
	if(self.Config.RemoveForModerators) then
		neededlevel = 1
	end
	if(args.Length >= 1) then
		local timeactivated = self.Config.DefaultActivationTime
		if(args.Length >= 2) then
			if(tonumber(args[1]) ~= nil) then
				timeactivated = tonumber(args[1])
				if(timeactivated > self.Config.MaxAllowedTime) then
					timeactivated = self.Config.MaxAllowedTime
				end
			end
		else
			if(tonumber(args[0]) ~= nil) then
				timeactivated = tonumber(args[0])
				if(timeactivated > self.Config.MaxAllowedTime) then
					timeactivated = self.Config.MaxAllowedTime
				end
				if(self.Config.RemoveForPlayers) then
					if(self.isRemoving[player]) then
						self:StopRemove(player)
					else
						self.Removetimers[player] = timer.Once( timeactivated, function() self:StopRemove(player) end )
						self.isRemoving[player] = "normal"
						ChatMessage(player,"Remover Tool Activated, auto-deactivation in " .. timeactivated .. "s")
					end
				else
					ChatMessage(player,"Remover tool is not activated for players")
				end
				return
			end
		end
		if(authlevel and authlevel >= neededlevel) then
			if(tostring(args[0]) == "admin") then
				if(self.isRemoving[player]) then
					self:StopRemove(player)
				else
					self.isRemoving[player] = "admin"
					self.Removetimers[player] = timer.Once( timeactivated, function() self:StopRemove(player) end )
					ChatMessage(player,"Remover Admin Tool Activated, auto-deactivation in " .. timeactivated .. "s" )
				end
				return
			elseif(tostring(args[0]) == "all") then
				if(self.isRemoving[player]) then
					self:StopRemove(player)
				else
					self.isRemoving[player] = "all"
					self.Removetimers[player] = timer.Once( timeactivated, function() self:StopRemove(player) end )
					ChatMessage(player,"Remover All Tool Activated, auto-deactivation in " .. timeactivated .. "s")
				end
				return
			end
			ChatMessage(player,"Wrong Arguments: /remove admin")
		else
			ChatMessage(player,"You are not allowed to use this command")
		end
	else
		local timeactivated = self.Config.DefaultActivationTime
		if(self.Config.RemoveForPlayers) then
			if(self.isRemoving[player]) then
				self:StopRemove(player)
			else
				self.Removetimers[player] = timer.Once( timeactivated, function() self:StopRemove(player) end )
				self.isRemoving[player] = "normal"
				ChatMessage(player,"Remover Tool Activated, auto-deactivation in " .. timeactivated .. "s")
			end
		end
	end
end

function PLUGIN:OnPlayerDisconnected(ply,connection)
	self.isRemoving[ply] = nil
	if(self.Removetimers[ply]) then self.Removetimers[ply]:Destroy() end
end

function PLUGIN:StopRemove(player)
	if(not player) then return end
	self.isRemoving[player] = nil
	if(self.Removetimers[player]) then self.Removetimers[player]:Destroy() end
	ChatMessage(player,"Remover Tool Auto Deactivated")
end
function PLUGIN:OnPlayerAttack(attacker,hitinfo)
	if(hitinfo.HitEntity) then
		if(self.isRemoving[attacker]) then
			local buildingblock = hitinfo.HitEntity:GetComponent("BuildingBlock")
			local deployable = hitinfo.HitEntity:GetComponent("WorldItem")
			if(buildingblock) then
				if(self.isRemoving[attacker] == "all") then
					print("all")
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
									checkfrom[ #checkfrom + 1 ] = fbuildingblock.transform.position
								end
							elseif(it.Current:GetComponentInParent(global.WorldItem._type)) then
								local fworlditem = it.Current:GetComponentInParent(global.WorldItem._type)
								if(not gotdeployeditem[fworlditem]) then
									gotdeployeditem[fworlditem] = true
								end
							end
						end
					end
					for buildingblock,k in pairs(house) do
						if(self.Config.Refund.activated) then
							self:RefundStructure(attacker,buildingblock)
						end
						if(buildingblock:GetComponent("BaseEntity"):HasSlot(global.Slot.Lock)) then
							local lock = buildingblock:GetComponent("BaseEntity"):GetSlot(global.Slot.Lock)
							if(lock) then
								lock:OnInvalidPosition()
								buildingblock:GetComponent("BaseEntity"):SetSlot(global.Slot.Lock,nil)
								if(self.Config.Refund.activated) then
									self:RefundDeployables(attacker,global.ItemManager.CreateByName("lock_code",1))
								end
							end
						end
						buildingblock:KillMessage()
					end
					for item,k in pairs(gotdeployeditem) do
						if(self.Config.Refund.activated) then
							self:RefundDeployables(attacker,item)
						end
						item:GetComponent("BaseEntity"):Kill(ProtoBuf.Mode.None,0,0,nulVector3)
					end
					return true
				else
					if(self:CanRemoveBlock(attacker,buildingblock)) then
						if(self.Config.Refund.activated) then
							self:RefundStructure(attacker,buildingblock)
						end
						if(buildingblock:GetComponent("BaseEntity"):HasSlot(global.Slot.Lock)) then
							local lock = buildingblock:GetComponent("BaseEntity"):GetSlot(global.Slot.Lock)
							if(lock) then
								lock:OnInvalidPosition()
								buildingblock:GetComponent("BaseEntity"):SetSlot(global.Slot.Lock,nil)
								if(self.Config.Refund.activated) then
									self:RefundItem(attacker,global.ItemManager.CreateByName("lock_code",1))
								end
							end
						end
						buildingblock:KillMessage()
						return true
					end
				end
			elseif(deployable and deployable.item) then
				if(self:CanRemoveEntity(attacker,deployable:GetComponent("BaseEntity"))) then
					if(self.Config.Refund.activated) then
						self:RefundDeployables(attacker,deployable)
					end
					deployable:GetComponent("BaseEntity"):Kill(ProtoBuf.Mode.None,0,0,nulVector3)
					return true
				end
			elseif(self.isRemoving[attacker] == "admin" and hitinfo.HitEntity:GetComponent("BaseEntity")) then
				hitinfo.HitEntity:GetComponent("BaseEntity"):Kill(ProtoBuf.Mode.None,0,0,nulVector3)
			end
		end
	end
	return
end 
function PLUGIN:RefundItem(player,item)
	player.inventory:GiveItem(item)
end
function PLUGIN:RefundDeployables(player,worlditem)
	if(worlditem and worlditem.item and worlditem.item.info) then
		player.inventory:GiveItem(worlditem.item.info.itemid,1)
	end
end
function PLUGIN:RefundStructure(player,buildingblock)
	if(buildingblock.blockDefinition) then
		for i=buildingblock.grade,0,-1 do
			for o=0, (buildingblock.blockDefinition.grades[i].costToBuild.Count - 1) do
				player.inventory:GiveItem(buildingblock.blockDefinition.grades[i].costToBuild[o].itemid,(buildingblock.blockDefinition.grades[i].costToBuild[o].amount*self.Config.Refund.refundrate))
			end
		end
	end
end
local function Distance2D(p1, p2)
    return math.sqrt(math.pow(p1.x - p2.x,2) + math.pow(p1.z - p2.z,2)) 
end
function PLUGIN:CanRemoveBlock(attacker,buildingblock)
	if(self.isRemoving[attacker] == "admin") then
		return true
	end
	if(buildingowners) then
		local ownerid = buildingowners:FindBlockData(buildingblock)
		if(ownerid and ownerid == rust.UserIDFromPlayer(attacker)) then
			return true
		end
	end
	if(self.Config.useToolCupboard and attacker:HasPlayerFlag(global.PlayerFlags.HasBuildingPrivilege)) then
		if(Distance2D(attacker.transform.position,buildingblock.transform.position) > 5) then
			ChatMessage(attacker, "You must get closer to remove")
			return false
		else
			return true
		end
	end
	return false
end
function PLUGIN:CanRemoveEntity(attacker,entity)
	if(self.isRemoving[attacker] == "admin") then
		return true
	end
	local deployeditem = entity:GetComponent("DeployedItem")
	if( deployeditem ) then
		if(attacker.userID == deployeditem.deployerUserID) then
			return true
		end
	end
	if(self.Config.useToolCupboard and attacker:HasPlayerFlag(global.PlayerFlags.HasBuildingPrivilege)) then
		if(Distance2D(attacker.transform.position,entity.transform.position) > 5) then
			ChatMessage(attacker, "You must get closer to remove")
			return false
		else
			return true
		end
	end
	return false
end

function PLUGIN:SendHelpText(player)
    local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = 2
	if(self.Config.RemoveForModerators) then
		neededlevel = 1
	end
	if(authlevel >= neededlevel) then
		ChatMessage(player,"/remove admin - To remove any building entity")
		ChatMessage(player,"/remove all - To remove all buildings (carefull some things can't be removed and will have to be removed manually)")
	end
	if(self.Config.RemoveForPlayers) then
		local refund = ""
		if(self.Config.Refund.activated) then
			refund = "(You will be refunded)"
		end
		ChatMessage(player,"/remove - To remove a building part that belongs to you. " ..refund)
	end
end
