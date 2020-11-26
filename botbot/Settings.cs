using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace botbot
{
    public class Settings
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("testing_channel")]
        public string TestingChannel { get; set; }

        [JsonPropertyName("tech_channel")]
        public string TechChannel { get; set; }

        [JsonPropertyName("status_channel")]
        public string StatusChannel { get; set; }

        [JsonPropertyName("crosspost_team_id")]
        public string CrosspostTeamId { get; set; }

        [JsonPropertyName("crosspost_channel_id")]
        public string CrosspostChannelId { get; set; }

        [JsonPropertyName("hubot_enabled")]
        public bool HubotEnabled { get; set; }

        [JsonPropertyName("my_username")]
        public string MyUsername { get; set; }
    }
}
