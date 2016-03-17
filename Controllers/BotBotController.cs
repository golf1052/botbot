using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNet.Mvc;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

namespace botbot.Controllers
{
    [Route("api/[controller]")]
    public class BotBotController : Controller
    {
        public static Client client;
        
        public static async Task StartClient()
        {
            BotBotController.client = new Client();
            HttpClient webClient = new HttpClient();
            Uri uri = new Uri("https://slack.com/api/rtm.start?token=" + Secrets.Token);
            HttpResponseMessage response = await webClient.GetAsync(uri);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            await client.Connect(new Uri((string)responseObject["url"]));
        }
    }
}