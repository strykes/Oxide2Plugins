PLUGIN.Name = "r-deathmessages"
PLUGIN.Title = "Death Messages"
PLUGIN.Version = V(0, 2, 7)
PLUGIN.Description = "Death Messages"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true
 
function PLUGIN:Init()
end
function PLUGIN:LoadDefaultConfig()
	self.Config.Settings = {}
	self.Config.Settings.animaldeaths = true
	self.Config.Settings.animalkills = true
	self.Config.Settings.players = true
	self.Config.Settings.mysql = true
	self.Config.Mysql = {}
	self.Config.Mysql.pvp = true
	self.Config.Mysql.pve = true
	self.Config.Mysql.naturalcauses = true
	self.Config.Mysql.uploadpage = ""
	self.Config.Mysql.key = ""
	self.Config.SuicideDeaths = true
	self.Config.SuicideDeathsMessage = "@{killed} commited suicide"
	self.Config.naturalcausesdeath = true
	self.Config.naturalcausesdeathmessages = {
		["Hunger"] = "@{killed} died from hunger.",
		["Thirst"] = "@{killed} was too thirsty and died.",
		["Cold"] = "@{killed} frooze to death.",
		["Drowned"] = "@{killed} thought he could breath under water.",
		["Heat"] = "@{killed} became a dead human torch.",
		["Bleeding"] = "@{killed} bleed to death.",
		["Poison"] = "@{killed} was poisonned.",
		["Suicide"] = "@{killed} commit suicide.",
		["Generic"] = "@{killed} just died.",
		["Bullet"] = "@{killed} died from a bullet.",
		["Slash"] = "@{killed} got slashed.",
		["BluntTrauma"] = "@{killed} died from a blunt trauma.",
		["Fall"] = "@{killed} fell and died.",
		["Radiation"] = "@{killed} got irradiated.",
		["Bite"] = "@{killed} was bitten to death."
	} 
	self.Config.playerDeathMessage = "@{killer} killed @{killed} (@{weapon}) with a hit to their @{bodypart} at @{distance}m"
	self.Config.playerDeathWhileSleepingMessage = "@{killer} murdered @{killed} while sleeping with a @{weapon} at @{distance}m"
	self.Config.deathByEntityMessage = "@{killed} has died by a @{killer}"
	self.Config.deathByEntityWhileSleepingMessage = "@{killed} was eaten alive while sleeping by a @{killer}"
	self.Config.wildlifeDeathMessage    =   "@{killer} killed a @{killed} (@{weapon}) with a hit to their @{bodypart} at @{distance}m"
	self.Config.ChatName = "Death"
	
end
local function getWeapon(hitinfo)
	if(hitinfo.Weapon and hitinfo.Weapon.holdType) then
		holdtype = tostring(hitinfo.Weapon.holdType)
		return string.sub(holdtype,0,string.find(holdtype,":")-1)
	end
	return "Unknown"
end
function PLUGIN:OnEntityDeath(entity, hitinfo)
	if(hitinfo == nil) then
		if(self.Config.naturalcausesdeath) then
			if(entity:ToPlayer()) then
				self:PlayerDiedFromNaturalCause(entity)
			end
		end
	else
		if(entity:ToPlayer()) then
			self:PlayerDeath(entity,hitinfo)
		elseif(entity:GetComponent("BaseAnimal")) then
			self:EntityDeath(entity:GetComponent("BaseEntity"),hitinfo)
		end
	end
end
function PLUGIN:GetBodyPart(number)
	local arr = util.TableToArray( { number } )
	util.ConvertAndSetOnArray(arr, 0, number, System.UInt32._type)
	
	local bodypart = global.StringPool.Get["methodarray"][0]:Invoke(nil, arr)
	if(bodypart and bodypart ~= "") then
		return bodypart
	else
		return "body"
	end
end
function PLUGIN:PlayerDiedFromNaturalCause(entity)
	local lastDamage = tostring(entity.metabolism.lastDamage)
	if(lastDamage ~= nil) then
		local endpos = string.find(lastDamage,":")
		if(endpos) then
			lastDamage = string.sub(lastDamage,0,endpos-1)
		end
		if(self.Config.naturalcausesdeathmessages[lastDamage]) then
			local tags = {}
			tags.killed = entity.displayName
			tags.killedid = rust.UserIDFromPlayer(entity)
			tags.killer = lastDamage
			tags.type = "naturalcause"
			self:BuildDeathMessage(tags,self.Config.naturalcausesdeathmessages[lastDamage])
		end
	end
end
function PLUGIN:Distance3D(p1, p2)
    return math.sqrt(math.pow(p1.x - p2.x,2) + math.pow(p1.y - p2.y,2) + math.pow(p1.z - p2.z,2)) 
end
function PLUGIN:EntityDeath(victim,hitinfo)
	local tags = {}
	if(self.Config.Settings.animaldeaths) then
		if(hitinfo.Initiator:ToPlayer()) then
			local attacker = hitinfo.Initiator:ToPlayer()
			local animal = victim:GetComponent("BaseAnimal")
			tags.killer = attacker.displayName
			tags.bodypart = self:GetBodyPart(hitinfo.HitBone)
			tags.killerid = rust.UserIDFromPlayer(attacker)
			local tempname = string.sub(victim.corpseEntity,13)
			local tempname = string.sub(tempname,0,string.find(tempname,"_")-1)
			tags.killed = tempname
			tags.killedid = "npc"
			tags.type = "pve"
			tags.weapon = getWeapon(hitinfo)
			tags.distance = math.floor(self:Distance3D(attacker.transform.position,victim.transform.position) + 0.5)
			self:BuildDeathMessage(tags,self.Config.wildlifeDeathMessage)
		end
	end
	
end
function PLUGIN:PlayerDeath(victim,hitinfo)
	local tags = {}
	if(tostring(hitinfo.damageTypes:GetMajorityDamageType()) == tostring(Rust.DamageType.Suicide)) then
		if(self.Config.SuicideDeaths) then
			tags.killed = victim.displayName
			tags.killedid = rust.UserIDFromPlayer(victim)
			tags.killer = victim.displayName
			tags.type = "suicide"
			self:BuildDeathMessage(tags,self.Config.SuicideDeathsMessage)
		end
	elseif(hitinfo.Initiator:ToPlayer()) then
		if(self.Config.Settings.players) then
			local attacker = hitinfo.Initiator:ToPlayer()
			tags.killer = attacker.displayName
			tags.killerid = rust.UserIDFromPlayer(attacker)
			tags.killed = victim.displayName
			tags.killedid = rust.UserIDFromPlayer(victim)
			tags.bodypart = self:GetBodyPart(hitinfo.HitBone)
			tags.type = "pvp"
			tags.weapon = getWeapon(hitinfo)
			tags.distance = math.floor(self:Distance3D(attacker.transform.position,victim:GetComponent("BaseEntity").transform.position) + 0.5)
			if(victim:IsSleeping()) then
				self:BuildDeathMessage(tags,self.Config.playerDeathWhileSleepingMessage)
			else
				self:BuildDeathMessage(tags,self.Config.playerDeathMessage)
			end
		end
	elseif(hitinfo.Initiator:GetComponentInParent(global.BaseAnimal._type)) then
		if(self.Config.Settings.animalkills) then
			local attacker = hitinfo.Initiator
			local animal = hitinfo.Initiator:GetComponentInParent(global.BaseAnimal._type)
			
			local tempname = string.sub(attacker.corpseEntity,13)
			local tempname = string.sub(tempname,0,string.find(tempname,"_")-1)
			tags.killer = tempname
			tags.killerid = "npc"
			tags.killed = victim.displayName
			tags.killedid = rust.UserIDFromPlayer(victim)
			tags.bodypart = self:GetBodyPart(hitinfo.HitBone)
			tags.type = "pve"
			tags.weapon = getWeapon(hitinfo)
			tags.distance = self:Distance3D(attacker.transform.position,victim:GetComponent("BaseEntity").transform.position)
			if(victim:IsSleeping()) then
				self:BuildDeathMessage(tags,self.Config.deathByEntityWhileSleepingMessage)
			else
				self:BuildDeathMessage(tags,self.Config.deathByEntityMessage)
			end
		end 
	end
end
function PLUGIN:BuildDeathMessage(tags, str)
	local customMessage = str
	for k, v in pairs(tags) do
		customMessage = string.gsub(customMessage, "@{".. k .. "-}", v )
	end
	print(customMessage)
	if(self.Config.Settings.mysql) then
		self:SendToMysql(tags)
	end
	global.ConsoleSystem.Broadcast("chat.add \"" .. self.Config.ChatName .. "\" \"" .. customMessage .. "\"")
end 
function PLUGIN:SendToMysql(tags)
	if(self.Config.Mysql.pve and tags.type == "pve") then
		 webrequests.EnqueueGet(self.Config.Mysql.uploadpage .. "?key="..self.Config.Mysql.key.."&type=pve&killerid=" .. tags.killerid .. "&killedid="..tags.killedid.."&weapon="..tags.weapon.."&distance="..tags.distance.."&bodypart=".. tags.bodypart .."&killer="..tags.killer.."&killed="..tags.killed, function(code, response) end, self.Object)
	elseif(self.Config.Mysql.pvp and tags.type == "pvp") then
		webrequests.EnqueueGet(self.Config.Mysql.uploadpage .. "?key="..self.Config.Mysql.key.."&type=pvp&killerid=" .. tags.killerid .. "&killedid="..tags.killedid.."&weapon="..tags.weapon.."&distance="..tags.distance.."&bodypart=".. tags.bodypart .."&killer="..tags.killer.."&killed="..tags.killed, function(code, response) end, self.Object)
	elseif(self.Config.Mysql.naturalcauses and tags.type == "naturalcause") then
		webrequests.EnqueueGet(self.Config.Mysql.uploadpage .. "?key="..self.Config.Mysql.key.."&type=natural&killedid="..tags.killedid.."&killer="..tags.killer.."&killed="..tags.killed, function(code, response) end, self.Object)
	end
end
