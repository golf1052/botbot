using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI;
using Newtonsoft.Json.Linq;

namespace botbot.Module
{
    public abstract class SlackEventModule : IEventModule
    {
        protected readonly SlackCore slackCore;
        protected readonly Func<string, Task> SendMessageFunc;

        public SlackEventModule(SlackCore slackCore, Func<string, Task> SendMessage)
        {
            this.slackCore = slackCore;
            SendMessageFunc = SendMessage;
        }

        public abstract Task Handle(string type, JObject e);

        public abstract RecurringModule RegisterRecurring();
    }
}
