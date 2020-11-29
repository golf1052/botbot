using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using golf1052.SlackAPI.Events;

namespace botbot.Module.SlackAttachments
{
    public class HackerNewsModule : SlackAttachmentModule
    {
        private HackerNewsApi hackerNewsApi;

        public HackerNewsModule(Settings settings) : base(settings)
        {
            hackerNewsApi = new HackerNewsApi();
        }

        public override async Task<ModuleResponse> Handle(SlackMessage message, Attachment attachment)
        {
            if (message.Channel == settings.TechChannel)
            {
                string url = attachment.TitleLink;
                if (string.IsNullOrEmpty(url))
                {
                    return new ModuleResponse();
                }
                SearchItem hackerNewsItem = await hackerNewsApi.Search(url);
                if (hackerNewsItem == null)
                {
                    return new ModuleResponse();
                }
                return new ModuleResponse()
                {
                    Message = $"From Hacker News\nTitle: {hackerNewsItem.Title}\nPoints: {hackerNewsItem.Points}\nComments: {hackerNewsItem.NumComments}\nLink: {hackerNewsItem.GetUrl()}",
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
