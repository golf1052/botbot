using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using botbot.Status;

namespace botbot.Controllers
{
    [Route("[controller]")]
    public class BotBotController : Controller
    {
        public static Client client;

        public static async Task StartClient(ILogger<Client> logger)
        {
            BotBotController.client = new Client(Secrets.Token, logger);
            await client.SendApiCall("reactions.list?token=" + Secrets.Token + "&full=true&count=100");
            HttpClient webClient = new HttpClient();
            Uri uri = new Uri(Client.BaseUrl + "rtm.start?token=" + Secrets.Token);
            HttpResponseMessage response = await webClient.GetAsync(uri);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            await client.Connect(new Uri((string)responseObject["url"]));
        }

        [HttpPost]
        public string SlashCommand()
        {
            RequestBody requestBody = new RequestBody(Request.Form);

            string[] splitText = requestBody.Text.Split(' ');
            if (string.IsNullOrEmpty(requestBody.Text))
            {
                return "¯\\_(ツ)_/¯";
            }
            if (requestBody.Text.ToLower() == "subscribe status")
            {
                Client.StatusNotifier.Subscribe(requestBody.UserId);
                return "I've subscribed you to status updates.";
            }
            else if (requestBody.Text.ToLower() == "unsubscribe status")
            {
                Client.StatusNotifier.Unsubscribe(requestBody.UserId);
                return "I've unsubscribed you to status updates.";
            }
            else
            {
                return "😱";
            }
        }
    }
}
