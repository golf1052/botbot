using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flurl;
using golf1052.SlackAPI.Events;

namespace botbot.Module.SlackAttachments
{
    public class SpotifyDirectLinkModule : ISlackAttachmentModule
    {
        public Task<ModuleResponse> Handle(SlackMessage message, Attachment attachment)
        {
            if (string.IsNullOrEmpty(attachment.FromUrl) || !attachment.FromUrl.StartsWith("https://open.spotify.com"))
            {
                return Task.FromResult(new ModuleResponse());
            }

            Url url = new Url(attachment.FromUrl);
            string[] splitPath = url.Path.Split('/');
            string directLink = $"spotify:{splitPath[splitPath.Length - 2]}:{splitPath[splitPath.Length - 1]}";
            if (string.IsNullOrEmpty(message.Message.ThreadTimestamp))
            {
                return Task.FromResult(new ModuleResponse()
                {
                    Message = directLink,
                    Timestamp = message.Message.Timestamp
                });
            }
            else
            {
                return Task.FromResult(new ModuleResponse()
                {
                    Message = directLink,
                    Timestamp = message.Message.ThreadTimestamp
                });
            }
        }
    }
}
