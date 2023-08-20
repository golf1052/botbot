using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI.Events;
using golf1052.SlackAPI.Objects;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace botbot.Module.SlackAttachments
{
    public class ArchiveTodayAttachmentModule : ISlackAttachmentModule
    {
        private readonly List<string> domains = new List<string>(new string[] {
            "nytimes.com", "wsj.com", "theatlantic.com", "bloomberg.com",
            "businessinsider.com", "forbes.com", "economist.com"
        });

        public ArchiveTodayAttachmentModule()
        {
        }

        public Task<ModuleResponse> Handle(SlackMessage message, Attachment attachment)
        {
            string url = attachment.TitleLink;
            if (string.IsNullOrEmpty(url))
            {
                return Task.FromResult(new ModuleResponse());
            }

            string urlLower = url.ToLower();

            foreach (var domain in domains) 
            {
                if (urlLower.Contains(domain)) {
                    return Task.FromResult(new ModuleResponse() 
                    {
                        Message = "Paywall bypass: https://archive.today/" + url,
                        Timestamp = message.Message.ThreadTimestamp
                    });
                }
            }

            return Task.FromResult(new ModuleResponse());
        }
    }
}
