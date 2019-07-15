using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot.Command
{
    public interface ISlackCommand
    {
        Task<string> Handle(string text, string userId);
    }
}
