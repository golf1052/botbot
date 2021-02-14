using System.Text.Json.Serialization;

namespace botbot
{
    public class HubotResponseObject
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("thread_ts")]
        public string? ThreadTimestamp { get; set; }
    }
}
