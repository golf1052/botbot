using System;
using System.Threading.Tasks;

namespace botbot.Module
{
    public class VersionModule : IMessageModule
    {
        private const string Version = "42.0.0";

        public async Task<string> Handle(string text, string userId, string channel)
        {
            if (text.ToLower() == "botbot version")
            {
                return Version;
            }
            return null;
        }
    }
}
