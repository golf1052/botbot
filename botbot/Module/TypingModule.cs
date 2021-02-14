using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace botbot.Module
{
    public class TypingModule : SlackEventModule
    {
        private Dictionary<string, Dictionary<string, DateTime>> typings;

        public TypingModule(SlackCore slackCore, Func<string, Task> SendMessage) : base(slackCore, SendMessage)
        {
            typings = new Dictionary<string, Dictionary<string, DateTime>>();
        }

        public override Task Handle(string type, JObject e)
        {
            if (type == "user_typing")
            {
                string channel = (string)e["channel"]!;
                string user = (string)e["user"]!;
                if (!typings.ContainsKey(channel))
                {
                    typings.Add(channel, new Dictionary<string, DateTime>());
                }
                if (!typings[channel].ContainsKey(user))
                {
                    typings[channel].Add(user, DateTime.UtcNow);
                }
                else
                {
                    typings[channel][user] = DateTime.UtcNow;
                }
            }

            return Task.CompletedTask;
        }

        public override RecurringModule RegisterRecurring()
        {
            return new RecurringModule(TimeSpan.FromSeconds(1), CheckTypings);
        }

        private async Task CheckTypings()
        {
            // clean stale
            foreach (var typing in typings)
            {
                List<string> typingUsers = typing.Value.Keys.ToList();
                foreach (string user in typingUsers)
                {
                    if (DateTime.UtcNow - typing.Value[user] >= TimeSpan.FromSeconds(3))
                    {
                        typing.Value.Remove(user);
                    }
                }
            }

            // send on active
            foreach (var typing in typings)
            {
                if (typing.Value.Count >= 4)
                {
                    await SendTyping(typing.Key);
                }
            }
        }

        public async Task SendTyping(string channel)
        {
            JObject o = new JObject();
            o["id"] = 1;
            o["type"] = "typing";
            o["channel"] = channel;
            await SendMessageFunc(o.ToString(Formatting.None));
        }
    }
}
