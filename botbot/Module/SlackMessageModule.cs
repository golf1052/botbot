using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI;

namespace botbot.Module
{
    public abstract class SlackMessageModule : IMessageModule
    {
        protected readonly SlackCore slackCore;
        protected readonly Func<string, string, string, Task> SendSlackMessageFunc;

        public SlackMessageModule(SlackCore slackCore, Func<string, string, string, Task> SendSlackMessage)
        {
            this.slackCore = slackCore;
            SendSlackMessageFunc = SendSlackMessage;
        }

        public abstract Task<ModuleResponse> Handle(string text, string userId, string channel);

        protected async Task SendSlackMessage(string message, string channel)
        {
            await SendSlackMessageFunc.Invoke(message, channel, null);
        }
    }
}
