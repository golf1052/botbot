using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using botbot.Controllers;

namespace botbot.Module
{
    public class StockModule : IMessageModule
    {
        public async Task<string> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot stock "))
            {
                return await BotBotController.stockCommand.Handle(text.ToLower().Replace("botbot stock ", ""), userId);
            }
            return null;
        }
    }
}
