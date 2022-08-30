using System.Collections.Generic;
using golf1052.SlackAPI.BlockKit.Blocks;

namespace botbot.Module
{
    public class ModuleResponse
    {
        public string? Message { get; set; }
        public List<IBlock>? Blocks { get; set; }
        public string? Timestamp { get; set; }
    }
}
