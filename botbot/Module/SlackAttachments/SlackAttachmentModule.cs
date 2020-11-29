using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI.Events;

namespace botbot.Module.SlackAttachments
{
    public abstract class SlackAttachmentModule : ISlackAttachmentModule
    {
        protected Settings settings;

        public SlackAttachmentModule(Settings settings)
        {
            this.settings = settings;
        }

        public abstract Task<ModuleResponse> Handle(SlackMessage message, Attachment attachment);
    }
}
