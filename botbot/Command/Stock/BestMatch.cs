using Newtonsoft.Json;

namespace botbot.Command.Stock
{
    public class BestMatch
    {
        [JsonProperty("1. symbol")]
        public string? Symbol { get; private set; }

        [JsonProperty("2. name")]
        public string? Name { get; private set; }

        [JsonProperty("3. type")]
        public string? Type { get; private set; }

        [JsonProperty("4. region")]
        public string? Region { get; private set; }

        [JsonProperty("5. marketOpen")]
        public string? MarketOpen { get; private set; }

        [JsonProperty("6. marketClose")]
        public string? MarketClose { get; private set; }

        [JsonProperty("7. timezone")]
        public string? Timezone { get; private set; }

        [JsonProperty("8. currency")]
        public string? Currency { get; private set; }

        [JsonProperty("9. matchScore")]
        public float MatchScore { get; private set; }
    }
}
