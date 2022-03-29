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
        private HttpClient httpClient;

        public HackerNewsApi() : this(new HttpClient())
        {
        }

        public HackerNewsApi(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<SearchItem?> Search(string url)
        {
            List<SearchItem> hitItemsUrls = await Search(url, true);
            
            if (hitItemsUrls.Count == 0)
            {
                return null;
            }

            List<SearchItem> hitItemsTitles = await Search(hitItemsUrls[0].Title!, false);

            List<SearchItem> hitItems = new List<SearchItem>();
            hitItems.AddRange(hitItemsUrls);
            for (int i = 0; i < hitItemsTitles.Count; i++)
            {
                foreach (var hitItem in hitItems)
                {
                    if (hitItem.Id == hitItemsTitles[i].Id)
                    {
                        hitItemsTitles.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }
            hitItems.AddRange(hitItemsTitles);

            DedupeOnSameDate(hitItems);

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

        private async Task<List<SearchItem>> Search(string query, bool onlyUrls)
        {
            string searchUrl = $"http://hn.algolia.com/api/v1/search?query={query}";
            if (onlyUrls)
            {
                searchUrl += "&restrictSearchableAttributes=url";
            }

            HttpResponseMessage response = await httpClient.GetAsync(searchUrl);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return new List<SearchItem>();
            }

            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            JArray? hits = (JArray?)responseObject["hits"];
            if (hits == null || hits.Count == 0)
            {
                return new List<SearchItem>();
            }

            List<SearchItem> hitItems = new List<SearchItem>();
            foreach (var hit in hits)
            {
                hitItems.Add(JsonConvert.DeserializeObject<SearchItem>(hit.ToString()));
            }
            return hitItems;
        }

        private void DedupeOnSameDate(List<SearchItem> hitItems)
        {
            // This will be way easier once DateOnly lands
            // https://devblogs.microsoft.com/dotnet/date-time-and-time-zone-enhancements-in-net-6/
            Dictionary<DateTime, SearchItem> dates = new Dictionary<DateTime, SearchItem>();
            foreach (var hitItem in hitItems)
            {
                DateTime date = new DateTime(hitItem.CreatedAt.Year, hitItem.CreatedAt.Month, hitItem.CreatedAt.Day);
                if (!dates.ContainsKey(date))
                {
                    dates.Add(date, hitItem);
                }
                else
                {
                    if (hitItem.Points > dates[date].Points)
                    {
                        dates[date] = hitItem;
                    }
                }
            }

            hitItems.Clear();
            hitItems.AddRange(dates.Values);
        }
    }

    public class SearchItem
    {
        [JsonProperty("title")]
        public string? Title { get; private set; }

        [JsonProperty("points")]
        public int? Points { get; private set; }

        [JsonProperty("num_comments")]
        public int? NumComments { get; private set; }

        [JsonProperty("objectID")]
        public int Id { get; private set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; private set; }

        [JsonProperty("tags")]
        public List<string>? Tags { get; private set; }

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

        public string GetDisplayString()
        {
            return $"From Hacker News\nTitle: {Title}\nPoints: {Points}\nComments: {NumComments}\nLink: {GetUrl()}";
        }
    }
}
