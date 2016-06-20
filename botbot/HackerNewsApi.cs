using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace botbot
{
    public class HackerNewsApi
    {
        public static async Task<string> Search(string url)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync($"http://hn.algolia.com/api/v1/search?query={url}&restrictSearchableAttributes=url");
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return null;
            }
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            JArray hits = (JArray)responseObject["hits"];
            if (hits == null || hits.Count == 0)
            {
                return null;
            }
            JObject hit = (JObject)hits[0];
            string objectId = (string)hit["objectID"];
            return $"https://news.ycombinator.com/item?id={objectId}";
        }
    }
}
