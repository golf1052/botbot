using System.Threading.Tasks;
using botbot.Controllers;

namespace botbot.Module
{
    public class PresidentApprovalModule : IMessageModule
    {
        public async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot president") ||
                text.ToLower().StartsWith("botbot president approval") ||
                text.ToLower().StartsWith("botbot presidental approval") ||
                text.ToLower().StartsWith("botbot :trump:"))
            {
                string message = await BotBotController.presidentCommand.Handle(text, userId);
                return new ModuleResponse()
                {
                    Message = message
                };
            }
            return new ModuleResponse();
        }
    }
}
