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

        public HackerNewsAttachmentModule(List<SlackChannel> slackChannels, Settings settings) : base(settings)
        {
            this.slackChannels = slackChannels;
            hackerNewsApi = new HackerNewsApi();
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
                return new ModuleResponse()
                {
                    Message = hackerNewsItem.GetDisplayString(),
                    Timestamp = message.Message.ThreadTimestamp
                };
            }
            else
            {
                return new ModuleResponse();
            }
        }
    }
}
