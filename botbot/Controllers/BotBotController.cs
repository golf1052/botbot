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
using botbot.Command;

namespace botbot.Controllers
{
    [Route("[controller]")]
    public class BotBotController : Controller
    {
        // <team id, client>
        public static Dictionary<string, Client> clients;
        public static List<Task> clientTasks;
        public static HttpClient httpClient;
        public static StockCommand stockCommand;
        public static PresidentApprovalCommand presidentCommand;
        public static string HubotWorkspace;

        static BotBotController()
        {
            clients = new Dictionary<string, Client>();
            clientTasks = new List<Task>();
            httpClient = new HttpClient();
            stockCommand = new StockCommand();
            presidentCommand = new PresidentApprovalCommand();
        }

        public static async Task StartClients(ILogger<Client> logger)
        {
            clients.Clear();
            clientTasks.Clear();
            JObject settings = JObject.Parse(System.IO.File.ReadAllText("settings.json"));
            foreach (var workspace in settings["workspaces"])
            {
                Settings workspaceSettings = JsonConvert.DeserializeObject<Settings>(workspace.ToString());
                Client client = new Client(workspaceSettings, logger);
                JObject connectionInfo = await client.GetConnectionInfo();
                string teamId = (string)connectionInfo["team"]["id"];
                if (workspaceSettings.HubotEnabled)
                {
                    HubotWorkspace = teamId;
                }
                clients.Add(teamId, client);
                clientTasks.Add(client.Connect(new Uri((string)connectionInfo["url"])));
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
        public async void SlashCommand()
        {
            RequestBody requestBody = new RequestBody(Request.Form);
            if (requestBody.Command == "/botbot")
            {
                JObject responseObject = ProcessSlashCommand(requestBody);
                httpClient.PostAsync(requestBody.ResponseUrl, new StringContent(responseObject.ToString()));
            }
            else if (requestBody.Command == "/stock")
            {
                JObject responseObject = new JObject();
                responseObject["text"] = await stockCommand.Handle(requestBody.Text, requestBody.UserId);
                httpClient.PostAsync(requestBody.ResponseUrl, new StringContent(responseObject.ToString()));
            }
            else if (requestBody.Command == "/president")
            {
                JObject responseObject = new JObject();
                responseObject["text"] = await presidentCommand.Handle(requestBody.Text, requestBody.UserId);
                httpClient.PostAsync(requestBody.ResponseUrl, new StringContent(responseObject.ToString()));
            }
        }

        [HttpPost("/hubot")]
        public async void HubotResponse([FromBody]JObject responseObject)
        {
            if ((string)responseObject["type"] == "message")
            {
                if (!string.IsNullOrEmpty(HubotWorkspace))
                {
                    string text = (string)responseObject["text"];
                    string channel = (string)responseObject["channel"];
                    if (responseObject["thread_ts"] != null)
                    {
                        clients[HubotWorkspace].SendSlackMessage(text, channel, (string)responseObject["thread_ts"]);
                    }
                    else
                    {
                        clients[HubotWorkspace].SendSlackMessage(text, channel);
                    }
                }
            }
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
