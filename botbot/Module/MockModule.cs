using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot.Module
{
    public class MockModule : IMessageModule
    {
        public Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot mock"))
            {
                string[] splitText = text.Split(" ");
                string phrase = string.Join(" ", splitText.Skip(2).Take(splitText.Length - 1));
                Random rand = new Random();
                string mockText = "";
                foreach (char c in phrase) {
                    bool isUpper = rand.NextDouble() < 0.5;
                    mockText += isUpper ? char.ToUpper(c) : char.ToLower(c);
                }

                return Task.FromResult(new ModuleResponse()
                {
                    Message = mockText
                });
            }
            return Task.FromResult(new ModuleResponse());
        }
    }
}
