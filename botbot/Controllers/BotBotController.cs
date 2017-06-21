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
        public static HttpClient httpClient;

        public static async Task StartClient(ILogger<Client> logger)
        {
            BotBotController.client = new Client(Secrets.Token, logger);
            await client.SendApiCall("reactions.list?token=" + Secrets.Token + "&full=true&count=100");
            httpClient = new HttpClient();
            Uri uri = new Uri(Client.BaseUrl + "rtm.start?token=" + Secrets.Token);
            HttpResponseMessage response = await httpClient.GetAsync(uri);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            await client.Connect(new Uri((string)responseObject["url"]));
        }

        [HttpPost]
        public void SlashCommand()
        {
            RequestBody requestBody = new RequestBody(Request.Form);
            JObject responseObject = ProcessSlashCommand(requestBody);
            httpClient.PostAsync(requestBody.ResponseUrl, new StringContent(responseObject.ToString()));
        }

        private JObject ProcessSlashCommand(RequestBody requestBody)
        {
            string[] splitText = requestBody.Text.Split(' ');
            string text = string.Empty;
            if (string.IsNullOrEmpty(requestBody.Text))
            {
                text = "¯\\_(ツ)_/¯";
            }
            if (requestBody.Text.ToLower() == "subscribe status")
            {
                text = "I've subscribed you to status updates.\nThis also doesn't do anything anymore.";
            }
            else if (requestBody.Text.ToLower() == "unsubscribe status")
            {
                text = "I've unsubscribed you to status updates.\nThis also doesn't do anything anymore.";
            }
            else
            {
                text = "😱";
            }
            JObject o = new JObject();
            o["text"] = text;
            return o;
        }
    }
}
