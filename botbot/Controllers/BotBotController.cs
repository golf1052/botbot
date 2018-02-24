using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using botbot.Status;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace botbot.Controllers
{
    [Route("[controller]")]
    public class BotBotController : Controller
    {
        // <team id, client>
        public static Dictionary<string, Client> clients;
        public static List<Task> clientTasks;
        public static HttpClient httpClient;

        static BotBotController()
        {
            clients = new Dictionary<string, Client>();
            clientTasks = new List<Task>();
            httpClient = new HttpClient();
        }

        public static async Task StartClients(ILogger<Client> logger)
        {
            JObject settings = JObject.Parse(System.IO.File.ReadAllText("settings.json"));
            foreach (var workspace in settings["workspaces"])
            {
                Settings workspaceSettings = JsonConvert.DeserializeObject<Settings>(workspace.ToString());
                Client client = new Client(workspaceSettings, logger);
                Uri uri = new Uri(Client.BaseUrl + "rtm.connect?token=" + workspaceSettings.Token);
                HttpResponseMessage response = await httpClient.GetAsync(uri);
                JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
                string teamId = (string)responseObject["team"]["id"];
                clients.Add(teamId, client);
                clientTasks.Add(client.Connect(new Uri((string)responseObject["url"])));
            }
            await Task.WhenAll(clientTasks);
        }

        public static async Task Crosspost(string teamId, string channelId, string user, string message)
        {
            await Crosspost(teamId, channelId, $"{user}\n{message}");
        }

        public static async Task Crosspost(string teamId, string channelId, string message)
        {
            if (clients.ContainsKey(teamId))
            {
                await clients[teamId].SendSlackMessage(message, channelId);
            }
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
