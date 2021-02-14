using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot
{
    public class PlusPlusLog
    {
        public string? Id { get; set; }
        public Dictionary<string, Dictionary<string, DateTime>> Log { get; set; }
        public Dictionary<string, PlusPlusLastThing?> Last { get; set; }

        public PlusPlusLog()
        {
            Log = new Dictionary<string, Dictionary<string, DateTime>>();
            Last = new Dictionary<string, PlusPlusLastThing?>();
        }
    }
}
