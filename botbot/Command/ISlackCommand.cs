using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI.BlockKit.Blocks;
using golf1052.SlackAPI.BlockKit.CompositionObjects;

namespace botbot.Command
{
    public interface ISlackCommand
    {
        Task<string> Handle(string text, string userId);

        async Task<List<IBlock>?> HandleBlock(string text, string userId)
        {
            string response = await Handle(text, userId);
            List<IBlock> blocks = new List<IBlock>()
            {
                new Section(response)
            };
            return blocks;
        }
    }
}
