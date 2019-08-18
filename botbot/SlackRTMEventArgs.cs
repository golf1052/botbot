using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace botbot
{
    public class SlackRTMEventArgs : EventArgs
    {
        public string Type { get; set; }
        public JObject Event { get; set; }
    }
}
