PLUGIN.Name = "save"
PLUGIN.Title = "Save"
PLUGIN.Version = V(1, 2, 0)
PLUGIN.Description = "Manually save your world"
PLUGIN.Author = "Reneb"
PLUGIN.HasConfig = true

function PLUGIN:Init()
	command.AddChatCommand( "save", self.Object, "cmdSave" )
	command.AddConsoleCommand("save.all", self.Object, "ccmdSaveAll")
end
function PLUGIN:LoadDefaultConfig()
	self.Config.authLevel = 1
	self.Config.forceSaveOnQuit = true
end
function PLUGIN:ccmdSaveAll(arg)
    local player = nil
    local command = arg.cmd.namefull

    if arg.connection then
        player = arg.connection.player
    end

    if player and ( player:GetComponent("BaseNetworkable").net.connection.authLevel < self.Config.authLevel )  then
        return true
    end
    
    if command == "save.all" then
		arg:ReplyWith(tostring(self:SaveAll()))
    end
    return
end
function PLUGIN:cmdSave( player, com, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel 
	if(authlevel >= neededlevel) then
		rust.SendChatMessage(player,tostring(self:SaveAll()))
	end
end

function PLUGIN:SaveAll()
	global.SaveRestore.Save.methodarray[1]:Invoke(nil, util.TableToArray( { } ))
	return "World was Saved" 
end

function PLUGIN:OnRunCommand( arg )
	-- Check if the command is being send from an in-game player.
	if ( not arg ) then return end
	if ( not arg.cmd ) then return end
	if ( not arg.cmd.name ) then return end
	-- Check if the quit command was used.
	if arg.cmd.name == "quit" then
		if(self.Config.forceSaveOnQuit) then
			print( self:SaveAll() )
			print("Shutting down...")
			timer.Once(5, function() global.ConsoleGlobal.quit( arg ) end )
			return false
		end
	end
end
