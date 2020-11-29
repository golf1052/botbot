using System;
using System.Threading.Tasks;

namespace botbot.Module
{
    public class VersionModule : IMessageModule
    {
        private const string Version = "43.0.0";

        public Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower() == "botbot version")
            {
                return Task.FromResult(new ModuleResponse()
                {
                    Message = Version
                });
            }
            return Task.FromResult(new ModuleResponse());
        }
    }
}
