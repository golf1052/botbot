using System.Threading.Tasks;

namespace botbot.Module
{
    public class HackerNewsMessageModule : IMessageModule
    {
        private HackerNewsApi hackerNewsApi;

        public HackerNewsMessageModule()
        {
            hackerNewsApi = new HackerNewsApi();
        }

        public async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot hn ") || text.ToLower().StartsWith("botbot hackernews "))
            {
                string searchQuery = text.ToLower().Replace("botbot hn ", "").Replace("botbot hackernews ", "");
                SearchItem? hackerNewsItem = await hackerNewsApi.Search(searchQuery);
                if (hackerNewsItem != null)
                {
                    return new ModuleResponse()
                    {
                        Message = hackerNewsItem.GetDisplayString()
                    };
                }
            }
            return new ModuleResponse();
        }
    }
}
