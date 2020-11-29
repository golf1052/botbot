﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot.Module
{
    public interface IMessageModule
    {
        Task<ModuleResponse> Handle(string text, string userId, string channel);
    }
}
