using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI.Events;
using golf1052.SlackAPI.Objects;

namespace botbot.Module.SlackAttachments
{
    public class HackerNewsAttachmentModule : SlackAttachmentModule
    {
        private List<SlackChannel> slackChannels;
        private HackerNewsApi hackerNewsApi;
        private Dictionary<string, Queue<string>> cachedResponses;

        public HackerNewsAttachmentModule(List<SlackChannel> slackChannels, Settings settings) : base(settings)
        {
            this.slackChannels = slackChannels;
            hackerNewsApi = new HackerNewsApi();
            cachedResponses = new Dictionary<string, Queue<string>>();
        }

        public override async Task<ModuleResponse> Handle(SlackMessage message, Attachment attachment)
        {
            if (message.Channel == slackChannels.FirstOrDefault(c => c.Name == settings.TechChannel)?.Id ||
                message.Channel == slackChannels.FirstOrDefault(c => c.Name == settings.TestingChannel)?.Id)
            {
                string url = attachment.TitleLink;
                if (string.IsNullOrEmpty(url))
                {
                    return new ModuleResponse();
                }
                SearchItem? hackerNewsItem = await hackerNewsApi.Search(url);
                if (hackerNewsItem == null)
                {
                    return new ModuleResponse();
                }

                ModuleResponse response = new ModuleResponse()
                {
                    Message = hackerNewsItem.GetDisplayString(),
                    Timestamp = message.Message.ThreadTimestamp
                };

                if (!cachedResponses.ContainsKey(message.Channel))
                {
                    cachedResponses.Add(message.Channel, new Queue<string>());
                }

                if (!cachedResponses[message.Channel].Contains(response.Message))
                {
                    cachedResponses[message.Channel].Enqueue(response.Message);
                    if (cachedResponses[message.Channel].Count > 5)
                    {
                        cachedResponses[message.Channel].Dequeue();
                    }
                    return response;
                }
                else
                {
                    return new ModuleResponse();
                }
            }
            else
            {
                return new ModuleResponse();
            }
        }
    }
}
