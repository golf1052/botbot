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

        public async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot new releases"))
            {
                if (channel.StartsWith('D'))
                {
                    if (text.ToLower().StartsWith("botbot new releases gpm"))
                    {
                        string message = await newReleasesGPMCommand.Handle(text.Replace("botbot new releases gpm", "").Trim(), userId);
                        return new ModuleResponse()
                        {
                            Message = message
                        };
                    }
                    else
                    {
                        string message = await newReleasesCommand.Handle(text.Replace("botbot new releases", "").Trim(), userId);
                        return new ModuleResponse()
                        {
                            Message = message
                        };
                    }
                }
            }
            return new ModuleResponse();
        }
    }
}
