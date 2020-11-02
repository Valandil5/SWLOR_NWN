﻿using System;
using SWLOR.Game.Server.Legacy.ChatCommand.Contracts;
using SWLOR.Game.Server.Legacy.Enumeration;
using SWLOR.Game.Server.Legacy.GameObject;
using SWLOR.Game.Server.Legacy.Processor;
using SWLOR.Game.Server.Legacy.Service;

namespace SWLOR.Game.Server.Legacy.ChatCommand
{
    [CommandDetails("Gets the amount of time before the next restart.", CommandPermissionType.Player | CommandPermissionType.DM | CommandPermissionType.Admin)]
    public class RestartTime : IChatCommand
    {
        public void DoAction(NWPlayer user, NWObject target, NWLocation targetLocation, params string[] args)
        {
            if (ServerRestartProcessor.IsDisabled)
            {
                user.FloatingText("Server auto-restarts are currently disabled.");
            }
            else
            {
                var now = DateTime.UtcNow;
                var delta = ServerRestartProcessor.RestartTime - now;
                var rebootString = Server.Service.Time.GetTimeLongIntervals(delta.Days, delta.Hours, delta.Minutes, delta.Seconds, false);
                var message = "Server will automatically reboot in " + rebootString;
                user.FloatingText(message);
            }

        }

        public bool RequiresTarget => false;
        public string ValidateArguments(NWPlayer user, params string[] args)
        {
            return null;
        }
    }
}