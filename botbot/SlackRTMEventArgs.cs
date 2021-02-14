using System;
using Newtonsoft.Json.Linq;

namespace botbot
{
    public class SlackRTMEventArgs : EventArgs
    {
        public string Type { get; set; }
        public JObject Event { get; set; }

        public SlackRTMEventArgs(string type, JObject _event)
        {
            Type = type;
            Event = _event;
        }
    }
}
