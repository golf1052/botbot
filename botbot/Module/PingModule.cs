﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot.Module
{
    public class PingModule : IMessageModule
    {
        private readonly List<string> pingResponses = new List<string>(new string[]
        { "pong", "hello", "hi", "what's up!", "I am always alive.", "hubot is an inferior bot.",
        "botbot at your service!", "lol", "fuck"});

        public Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower() == "botbot ping")
            {
                return Task.FromResult(new ModuleResponse()
                {
                    Message = GetRandomFromList(pingResponses)
                });
            }
            return Task.FromResult(new ModuleResponse());
        }

        public T GetRandomFromList<T>(List<T> list)
        {
            Random random = new Random();
            return list[random.Next(list.Count)];
        }
    }
}
