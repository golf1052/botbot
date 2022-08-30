using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using botbot.Controllers;
using golf1052.SlackAPI.BlockKit.Blocks;

namespace botbot.Module
{
    public class StockModule : IMessageModule
    {
        public async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot stock "))
            {
                List<IBlock>? blocks = await BotBotController.stockCommand.HandleBlock(text.ToLower().Replace("botbot stock ", ""), userId);
                return new ModuleResponse()
                {
                    Blocks = blocks
                };
            }
            return new ModuleResponse();
        }
    }
}
