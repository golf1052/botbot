using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace botbot
{
    public class SlackMessageEventArgs : EventArgs
    {
        public JObject Message { get; set; }
    }
}
