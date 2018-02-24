using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace botbot
{
    public class Settings
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("testing_channel")]
        public string TestingChannel { get; private set; }

        [JsonProperty("tech_channel")]
        public string TechChannel { get; private set; }

        [JsonProperty("status_channel")]
        public string StatusChannel { get; private set; }
    }
}
