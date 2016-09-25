using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot
{
    public class PlusPlusLastThing
    {
        public string Thing { get; set; }
        public string Reason { get; set; }

        public PlusPlusLastThing(string thing, string reason)
        {
            Thing = thing;
            Reason = reason;
        }
    }
}
