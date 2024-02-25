using System;
using System.Threading.Tasks;
using golf1052.SlackAPI;
using Newtonsoft.Json.Linq;

namespace botbot.Module
{
    public abstract class SlackMessageModule : IMessageModule
    {
        protected readonly SlackCore slackCore;
        protected readonly Func<string, string, string?, Task> SendSlackMessageFunc;
        protected readonly Func<string, string, string?, Task<JObject>> SendPostMessageFunc;
        protected readonly Func<string, string, string, Task<JObject>> SendUpdateMessageFunc;

        public SlackMessageModule(SlackCore slackCore,
            Func<string, string, string?, Task> SendSlackMessage,
            Func<string, string, string?, Task<JObject>> SendPostMessage,
            Func<string, string, string, Task<JObject>> SendUpdateMessage)
        {
            this.slackCore = slackCore;
            SendSlackMessageFunc = SendSlackMessage;
            SendPostMessageFunc = SendPostMessage;
            SendUpdateMessageFunc = SendUpdateMessage;
        }

        public abstract Task<ModuleResponse> Handle(string text, string userId, string channel);

        protected async Task SendSlackMessage(string message, string channel)
        {
            await SendSlackMessageFunc.Invoke(message, channel, null);
        }

        protected async Task<JObject> SendPostMessage(string message, string channel, string? threadTimestamp = null)
        {
            return await SendPostMessageFunc.Invoke(message, channel, threadTimestamp);
        }

        protected async Task<JObject> SendUpdateMessage(string message, string channel, string timestamp)
        {
            return await SendUpdateMessageFunc.Invoke(message, channel, timestamp);
        }
    }
}
