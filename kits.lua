PLUGIN.Name = "kits"
PLUGIN.Title = "Kits"
PLUGIN.Version = V(1, 0, 2)
PLUGIN.Description = "Kits"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true
 
function PLUGIN:Init()
	self:LoadSavedData()
	command.AddChatCommand( "kit",self.Object, "cmdKit" )
end
function PLUGIN:InitializeTable()
	self.Table = {}
	local itemlist = global.ItemManager.GetItemDefinitions();
	local it = itemlist:GetEnumerator()
	while (it:MoveNext()) do
		local correctname = string.lower(it.Current.displayname)
		self.Table[correctname] = tostring(it.Current.shortname)
	end
end
local function ChatMessage(player,msg)
	player:SendConsoleCommand( "chat.add \"SERVER\" \"" .. tostring(msg) .. "\"" );
end
function PLUGIN:LoadDefaultConfig()
    self.Config.Kits = {
		["starter"] = {
			description = "infinite kit",
			main = {
				{ name = "Foundation", amount = 1 },
				{ name = "Pistol Bullet", amount = 250 }
			},
			wear = {
				{ name = "Hazmat Helmet", amount = 1 },
				{ name = "Hazmat Jacket", amount = 1 },
				{ name = "Hazmat Gloves", amount = 1 },
				{ name = "Hazmat Pants", amount = 1 },
				{ name = "Hazmat Boots", amount = 1 }
			},
			belt = {
				{ name = "Revolver", amount = 1 },
				{ name = "Large Medkit", amount = 5 }
			}
		},
		["dayly"] = {
			cooldown = 86400,
			main = {
				{ name = "Foundation", amount = 1 },
				{ name = "Pistol Bullet", amount = 250 }
			},
			wear = {
				{ name = "Hazmat Helmet", amount = 1 },
				{ name = "Hazmat Jacket", amount = 1 },
				{ name = "Hazmat Gloves", amount = 1 },
				{ name = "Hazmat Pants", amount = 1 },
				{ name = "Hazmat Boots", amount = 1 }
			},
			belt = {
				{ name = "Revolver", amount = 1 },
				{ name = "Large Medkit", amount = 5 }
			}
		},
		["twice"] = {
			max = 2,
			description = "Only 2 kits",
			main = {
				{ name = "Foundation", amount = 1 },
				{ name = "Pistol Bullet", amount = 250 }
			},
			wear = {
				{ name = "Hazmat Helmet", amount = 1 },
				{ name = "Hazmat Jacket", amount = 1 },
				{ name = "Hazmat Gloves", amount = 1 },
				{ name = "Hazmat Pants", amount = 1 },
				{ name = "Hazmat Boots", amount = 1 }
			},
			belt = {
				{ name = "Revolver", amount = 1 },
				{ name = "Large Medkit", amount = 5 }
			}
		},
		["moderator"] = {
			moderator = true,
			description = "for moderators",
			main = {
				{ name = "Foundation", amount = 1 },
				{ name = "Pistol Bullet", amount = 250 }
			},
			wear = {
				{ name = "Hazmat Helmet", amount = 1 },
				{ name = "Hazmat Jacket", amount = 1 },
				{ name = "Hazmat Gloves", amount = 1 },
				{ name = "Hazmat Pants", amount = 1 },
				{ name = "Hazmat Boots", amount = 1 }
			},
			belt = {
				{ name = "Revolver", amount = 1 },
				{ name = "Large Medkit", amount = 5 }
			}
		},
		["admin"] = {
			admin = true,
			description = "for admins",
			main = {
				{ name = "Foundation", amount = 1 },
				{ name = "Pistol Bullet", amount = 250 }
			},
			wear = {
				{ name = "Hazmat Helmet", amount = 1 },
				{ name = "Hazmat Jacket", amount = 1 },
				{ name = "Hazmat Gloves", amount = 1 },
				{ name = "Hazmat Pants", amount = 1 },
				{ name = "Hazmat Boots", amount = 1 }
			},
			belt = {
				{ name = "Revolver", amount = 1 },
				{ name = "Large Medkit", amount = 5 }
			}
		}
    }
    self.Config.AutoKits = {
    	allowed = true,
		main = {},
		wear = {},
		belt = {
			{ name = "Stone Hatchet", amount = 1 },
			{ name = "Foundation", amount = 1 },
			{ name = "Hammer", amount = 1 }
		},
		overrideDefaultKit = true,
		RustDefaultKit = {
			"Rock",
			"Torch"
		}
	}
	
	self.Config.Messages = {
		KitList                 = "Avaible Kits",
		NoKitAvaible            = "You have no avaible kits",
		ErrorKit                = "This kit doesn't exist",
		NoKitLeft               = "You don't have any kits left for this kit",
		NoPermissionKit         = "You don't have enough permissions to use this kit",
		CoolDown                = "This kit is still in cooldown",
		HelpMessage				= "/kit - to get the list of kits"
	}
	self.Config.AuthLevel = 1
end

function PLUGIN:LoadSavedData()
    KitData = datafile.GetDataTable( "kits" )
    KitData = KitData or {}
end
function PLUGIN:SaveData()  
    if( KitData ) then
        if ( self:Count( KitData ) == 0 ) then
            KitData = nil 
        end
    end 
    datafile.SaveDataTable( "kits" )
end
function PLUGIN:Count( tbl ) 
  local count = 0
  for _ in pairs( tbl ) do 
    count = count + 1 
    end
  return count
end
function PLUGIN:cmdKit( player, cmd, args )
	if(not self.Table) then self:InitializeTable() end
	local userID = tostring(rust.UserIDFromPlayer( player ))
	local authLevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	if(args.Length == 0) then
		local avaible = false
		ChatMessage(player,self.Config.Messages.KitList)
		for kitname,data in pairs(self.Config.Kits) do
			if(self:CanUseKit(player,userID,authLevel,kitname,data)) then
				avaible = true
				local text = ""
				if(data.description) then text = data.description end
                if(data.cooldown) then 
                	if(text == "") then text = data.cooldown .. "s Cooldown"
                    else text = text .. ", " .. data.cooldown .. "s Cooldown" end
                end
                if(data.max) then 
                	if(text == "") then text = data.max .. " max uses"
                    else text = text .. ", " .. data.max .. " max uses" end
                end
				ChatMessage(player,"/kit " .. tostring(kitname) .. " - " .. text)
			end
		end
		if(not avaible) then
			ChatMessage(player,self.Config.Messages.NoKitAvaible)
		end
	elseif(args.Length >= 1) then
		local targetkit = args[0]
		if(not self.Config.Kits[targetkit]) then
			ChatMessage(player,self.Config.Messages.ErrorKit)
			return
		end
		local allowed, err = self:CanUseKit(player,userID,authLevel,targetkit,self.Config.Kits[targetkit])
		if(not allowed) then
			ChatMessage(player,err)
			return
		end
		self:RedeemKit(player,targetkit, userID)
	end
end
function PLUGIN:RedeemAutoKit( player )
	if(not self.Config.AutoKits.allowed) then return end
	if(not self.Table) then self:InitializeTable() end
	local data = self.Config.AutoKits
	local inv = player.inventory
	if(inv:AllItems().Length > #self.Config.AutoKits.RustDefaultKit) then
		return
	else
		local defaultkitnum = #self.Config.AutoKits.RustDefaultKit
		for i=0, inv:AllItems().Length-1 do
			for o=1, #self.Config.AutoKits.RustDefaultKit do
				if(self.Config.AutoKits.RustDefaultKit[o] == inv:AllItems()[i].info.displayname) then
					defaultkitnum = defaultkitnum - 1
				end
			end
		end
		if(defaultkitnum > 0) then return end
	end
	if(self.Config.AutoKits.overrideDefaultKit) then 
		inv:Strip() 
	end
	if(data.main) then
		for i,subdata in pairs(data.main) do
			if(subdata.name and subdata.amount) then
				local giveitem, err = self:GiveItem(inv,subdata.name,subdata.amount,"main")
				if(not giveitem) then print("Error while giving kit " .. kit .. ": " .. err) end
			end
		end
	end
	if(data.belt) then
		for i,subdata in pairs(data.belt) do
			if(subdata.name and subdata.amount) then
				local giveitem, err = self:GiveItem(inv,subdata.name,subdata.amount,"belt")
				if(not giveitem) then print("Error while giving kit " .. kit .. ": " .. err) end
			end
		end
	end
	if(data.wear) then
		for i,subdata in pairs(data.wear) do
			if(subdata.name and subdata.amount) then
				local giveitem, err = self:GiveItem(inv,subdata.name,subdata.amount,"wear")
				if(not giveitem) then print("Error while giving kit " .. kit .. ": " .. err) end
			end
		end
	end
end
function PLUGIN:RedeemKit( player, kit, userID )

	if(not self.Table) then self:InitializeTable() end
	local data = self.Config.Kits[kit]
	local inv = player.inventory
	
	if(data.main) then
		for i,subdata in pairs(data.main) do
			if(subdata.name and subdata.amount) then
				local giveitem, err = self:GiveItem(inv,subdata.name,subdata.amount,"main")
				if(not giveitem) then print("Error while giving kit " .. kit .. ": " .. err) end
			end
		end
	end
	if(data.belt) then
		for i,subdata in pairs(data.belt) do
			if(subdata.name and subdata.amount) then
				local giveitem, err = self:GiveItem(inv,subdata.name,subdata.amount,"belt")
				if(not giveitem) then print("Error while giving kit " .. kit .. ": " .. err) end
			end
		end
	end
	if(data.wear) then
		for i,subdata in pairs(data.wear) do
			if(subdata.name and subdata.amount) then
				local giveitem, err = self:GiveItem(inv,subdata.name,subdata.amount,"wear")
				if(not giveitem) then print("Error while giving kit " .. kit .. ": " .. err) end
			end
		end
	end
	if(data.max) then
		if(not KitData[userID]) then KitData[userID] = {} end
		if(not KitData[userID][kit]) then KitData[userID][kit] = {} end
		if(not KitData[userID][kit]["u"]) then KitData[userID][kit]["u"] = "0" end
		KitData[userID][kit]["u"] = tostring(tonumber(KitData[userID][kit]["u"]) + 1)
	end
	if(data.cooldown) then
		if(not KitData[userID]) then KitData[userID] = {} end
		if(not KitData[userID][kit]) then KitData[userID][kit] = {} end
		if(not KitData[userID][kit]["c"]) then KitData[userID][kit]["c"] = "0" end
		KitData[userID][kit]["c"] = tostring(time.GetUnixTimestamp())
	end
	self:SaveData()
end
function PLUGIN:checkPlugins(player)
	local arr = util.TableToArray( { player } )
	util.ConvertAndSetOnArray(arr, 0, player, UnityEngine.Object._type)
	local thereturn = plugins.CallHook("canRedeemKit", arr )
	if(thereturn == nil) then
		return true
	end
	return false, tostring(thereturn)
end
function PLUGIN:CanUseKit( player, userID, authLevel,kitname,data)
	local allowed = true
	local err = ""
	if(data.admin and (not authLevel or (authLevel and authLevel < 2))) then 
		allowed = false 
		err = self.Config.Messages.NoPermissionKit
		return allowed, err
	end
	if(data.moderator and (not authLevel or (authLevel and authLevel < 1))) then 
		allowed = false 
		err = self.Config.Messages.NoPermissionKit
		return allowed, err
	end
	if(data.max and KitData[userID] and KitData[userID][kitname] and KitData[userID][kitname]["u"] and tonumber(KitData[userID][kitname]["u"]) >= data.max) then
		allowed = false
	 	err = self.Config.Messages.NoKitLeft
		return allowed, err
	end
	if(data.cooldown and KitData[userID] and KitData[userID][kitname] and KitData[userID][kitname]["c"] and (time.GetUnixTimestamp() - tonumber(KitData[userID][kitname]["c"])) < data.cooldown ) then
		allowed = false
	 	err = self.Config.Messages.CoolDown 
		return allowed, err
	end
	allowed, err = self:checkPlugins(player)
	if(not allowed) then
		return allowed, err
	end
	return allowed, err
end
function PLUGIN:PermissionsCheck(player)
    local authLevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
    local neededLevel = tonumber(self.Config.AuthLevel) or 2
  
    if (authLevel and authLevel >= neededLevel) then
        return true
    else
        return false
    end
end

function PLUGIN:GiveItem(inv,name,amount,type)
	local itemname = false
	name = string.lower(name)
	if(self.Table[name]) then
		itemname = self.Table[name]
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

function PLUGIN:OnRunCommand(arg, wantsfeedback)
    if (not arg) then return end
    if (not arg.connection) then return end
    if (not arg.connection.player) then return end
    if (not arg.cmd) then return end
    if (not arg.cmd.name) then return end
    if(arg.cmd.name ~= "wakeup") then return end
    if(arg.connection.player == nil) then return end
	if(not arg.connection.player:IsSleeping()) then return end
	if(arg.connection.player:IsSpectating()) then return end
	if(self:checkPlugins(arg.connection.player)) then
		self:RedeemAutoKit(arg.connection.player)
	end
	return
end

function PLUGIN:SendHelpText(player)
    ChatMessage(player,self.Config.Messages.HelpMessage)
end
