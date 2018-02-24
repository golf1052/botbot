using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace botbot
{
    public class Settings
    {
        [JsonProperty("id")]
        public string Id { get; private set; }

        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("testing_channel")]
        public string TestingChannel { get; private set; }

        [JsonProperty("tech_channel")]
        public string TechChannel { get; private set; }

        [JsonProperty("status_channel")]
        public string StatusChannel { get; private set; }

        [JsonProperty("crosspost_team_id")]
        public string CrosspostTeamId { get; private set; }

        [JsonProperty("crosspost_channel_id")]
        public string CrosspostChannelId { get; private set; }
    }
}
