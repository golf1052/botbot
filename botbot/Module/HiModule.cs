using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot.Module
{
    public class HiModule : IMessageModule
    {
        private readonly List<string> hiResponses = new List<string>(new string[]
        {
            "hello", "hi", "what's up!", "sup fucker"
        });

        public async Task<string> Handle(string text, string userId, string channel)
        {
            if (text.ToLower() == "hi botbot")
            {
                return GetRandomFromList(hiResponses);
            }
            return null;
        }

        public T GetRandomFromList<T>(List<T> list)
        {
            Random random = new Random();
            return list[random.Next(list.Count)];
        }
    }
}
