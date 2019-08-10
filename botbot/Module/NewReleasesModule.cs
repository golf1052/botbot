using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using botbot.Command;

namespace botbot.Module
{
    public class NewReleasesModule : IMessageModule
    {
        private NewReleasesCommand newReleasesCommand;
        private NewReleasesGPMCommand newReleasesGPMCommand;

        public NewReleasesModule()
        {
            newReleasesCommand = new NewReleasesCommand();
            newReleasesGPMCommand = new NewReleasesGPMCommand();
        }

        public async Task<string> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot new releases"))
            {
                if (channel.StartsWith('D'))
                {
                    if (text.ToLower().StartsWith("botbot new releases gpm"))
                    {
                        return await newReleasesGPMCommand.Handle(text.Replace("botbot new releases gpm", "").Trim(), userId);
                    }
                    else
                    {
                        return await newReleasesCommand.Handle(text.Replace("botbot new releases", "").Trim(), userId);
                    }
                }
            }
            return null;
        }
    }
}
