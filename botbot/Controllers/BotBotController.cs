using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
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
            BotBotController.client = new Client(Secrets.Token);
            await client.SendApiCall("reactions.list?token=" + Secrets.Token + "&full=true&count=100");
            HttpClient webClient = new HttpClient();
            Uri uri = new Uri(Client.BaseUrl + "rtm.start?token=" + Secrets.Token);
            HttpResponseMessage response = await webClient.GetAsync(uri);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            await client.Connect(new Uri((string)responseObject["url"]));
        }
    }
}