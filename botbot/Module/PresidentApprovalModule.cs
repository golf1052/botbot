using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Globalization;
using botbot.Controllers;

namespace botbot.Module
{
    public class PresidentApprovalModule : IMessageModule
    {
        public async Task<string> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot president") ||
                text.ToLower().StartsWith("botbot president approval") ||
                text.ToLower().StartsWith("botbot presidental approval") ||
                text.ToLower().StartsWith("botbot :trump:"))
            {
                return await BotBotController.presidentCommand.Handle(text, userId);
            }
            return null;
        }
    }
}
