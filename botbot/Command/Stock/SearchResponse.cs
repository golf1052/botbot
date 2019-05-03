using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace botbot.Command.Stock
{
    public class SearchResponse
    {
        [JsonProperty]
        public List<BestMatch> BestMatches { get; private set; }
    }
}
