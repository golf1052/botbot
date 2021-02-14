using System;
using Newtonsoft.Json.Linq;

namespace botbot
{
    public class SlackMessageEventArgs : EventArgs
    {
        public JObject Message { get; set; }

        public SlackMessageEventArgs(JObject message)
        {
            Message = message;
        }
    }
}
