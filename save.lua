PLUGIN.Name = "save"
PLUGIN.Title = "Save"
PLUGIN.Version = V(1, 1, 2)
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

    if player and not player:IsAdmin() then
        return true
    end
    
    if command == "save.all" then
		local filename = "0"
        if arg.Args and arg.Args.Length >= 1 then
           filename = arg.Args[0]
        end
		arg:ReplyWith(tostring(self:SaveAll(filename)))
    end
    return
end
function PLUGIN:cmdSave( player, com, args )
	local authlevel = player:GetComponent("BaseNetworkable").net.connection.authLevel
	local neededlevel = self.Config.authLevel 
	if(authlevel >= neededlevel) then
		local filename = "0"
		if(args.Length >= 1) then
			filename = args[0]
		end
		rust.SendChatMessage(player,tostring(self:SaveAll(filename)))
	end
end

function PLUGIN:SaveAll(filename)
	if(not filename) then filename = "0" end
	local folder = global.server.GetServerFolder("save/".. UnityEngine.Application.get_loadedLevelName() .. "_" .. global.World.get_Size() .. "_" .. global.World.get_Seed() )
	local fullpath = folder .. "/" .. filename .. ".sav"
	global.SaveRestore.Save(fullpath)
	return "File " .. fullpath .. " was Saved" 
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
