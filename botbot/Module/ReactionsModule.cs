using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI;
using golf1052.SlackAPI.Events;
using golf1052.SlackAPI.Objects;
using golf1052.SlackAPI.Other;

namespace botbot.Module
{
    public class ReactionsModule : SlackMessageModule
    {
        public ReactionsModule(SlackCore slackCore, Func<string, string, string, Task> SendSlackMessage) : base(slackCore, SendSlackMessage)
        {
        }

        public override async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower() == "botbot reactions")
            {
                await SendSlackMessage("Calculating reactions count, this might take me a minute...", channel);
                string message = await CalculateReactions(await slackCore.UsersList());
                return new ModuleResponse()
                {
                    Message = message
                };
            }
            return new ModuleResponse();
        }

        private async Task<string> CalculateReactions(List<SlackUser> slackUsers)
        {
            List<SlackEvent> reactions = new List<SlackEvent>();
            foreach (SlackUser user in slackUsers)
            {
                reactions.AddRange(await slackCore.ReactionsList(user.Id, allItems: true));
            }
            Dictionary<string, Dictionary<string, int>> topReactions = new Dictionary<string, Dictionary<string, int>>();
            Dictionary<string, int> topR = new Dictionary<string, int>();
            foreach (SlackEvent reaction in reactions)
            {
                if (reaction.Type == SlackEvent.SlackEventType.Message)
                {
                    SlackMessage message = reaction as SlackMessage;
                    foreach (Reaction r in message.Reactions)
                    {
                        if (!topReactions.ContainsKey(r.Name))
                        {
                            topReactions.Add(r.Name, new Dictionary<string, int>());
                        }
                        if (!topReactions[r.Name].ContainsKey(message.Timestamp))
                        {
                            topReactions[r.Name].Add(message.Timestamp, r.Count);
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, Dictionary<string, int>> pair in topReactions)
            {
                foreach (KeyValuePair<string, int> timePair in pair.Value)
                {
                    if (!topR.ContainsKey(pair.Key))
                    {
                        topR.Add(pair.Key, 0);
                    }
                    topR[pair.Key] += timePair.Value;
                }
            }
            List<string> finalMessage = new List<string>();
            finalMessage.Add("Top 10 reactions to messages");
            var sortedReactions = (from entry in topR orderby entry.Value descending select entry).ToList();
            for (int i = 0; i < 10; i++)
            {
                finalMessage.Add($":{sortedReactions[i].Key}: - {sortedReactions[i].Value}");
            }
            return string.Join('\n', finalMessage);
        }
    }
}
