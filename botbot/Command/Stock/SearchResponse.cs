using System.Collections.Generic;
using Newtonsoft.Json;

namespace botbot.Command.Stock
{
    public class SearchResponse
    {
        [JsonProperty]
        public List<BestMatch> BestMatches { get; private set; } = new List<BestMatch>();
    }
}
