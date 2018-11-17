using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace botbot
{
    public class HackerNewsApi
    {
        public static async Task<SearchItem> Search(string url)
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
            List<SearchItem> hitItems = new List<SearchItem>();
            foreach (var hit in hits)
            {
                hitItems.Add(JsonConvert.DeserializeObject<SearchItem>(hit.ToString()));
            }

            // If there's only one item just return that
            if (hitItems.Count == 1)
            {
                return hitItems[0];
            }

            // Check if there are any front page items, if there are return the first one
            SearchItem frontPageItem = hitItems.FirstOrDefault(i => i.OnFrontPage);
            if (frontPageItem != null)
            {
                return frontPageItem;
            }

            // Else just sort by date, descending, and return the first one
            hitItems.Sort((a, b) =>
            {
                return b.CreatedAt.CompareTo(a.CreatedAt);
            });
            return hitItems[0];
        }
    }

    public class SearchItem
    {
        [JsonProperty("title")]
        public string Title { get; private set; }

        [JsonProperty("points")]
        public int? Points { get; private set; }

        [JsonProperty("num_comments")]
        public int? NumComments { get; private set; }

        [JsonProperty("objectID")]
        public int Id { get; private set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; private set; }

        [JsonProperty("tags")]
        public List<string> Tags { get; private set; }

        public bool OnFrontPage
        {
            get
            {
                if (Tags != null)
                {
                    return Tags.Contains("front_page");
                }
                return false;
            }
        }

        public string GetUrl()
        {
            return $"https://news.ycombinator.com/item?id={Id}";
        }
    }
}
