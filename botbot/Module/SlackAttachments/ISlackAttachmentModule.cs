using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI.Events;

namespace botbot.Module.SlackAttachments
{
    public interface ISlackAttachmentModule
    {
        Task<ModuleResponse> Handle(SlackMessage message, Attachment attachment);
    }
}
