using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using botbot.Command;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

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
        public static string? HubotWorkspace;
        public static ImageCleaner imageCleaner;

        static BotBotController()
        {
            clients = new Dictionary<string, Client>();
            clientTasks = new List<Task>();
            httpClient = new HttpClient();
            stockCommand = new StockCommand();
            presidentCommand = new PresidentApprovalCommand();
            imageCleaner = new ImageCleaner();
        }

        public static async Task StartClients(ILogger<Client> logger)
        {
            // start image cleaner
            _ = imageCleaner.Run();

            clients.Clear();
            clientTasks.Clear();
            JsonDocument settings = JsonDocument.Parse(System.IO.File.ReadAllText("settings.json"), new JsonDocumentOptions()
            {
                CommentHandling = JsonCommentHandling.Skip
            });
            foreach (var workspace in settings.RootElement.GetProperty("workspaces").EnumerateArray())
            {
                var stream = new System.IO.MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    workspace.WriteTo(writer);
                }
                string str = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                Settings workspaceSettings = JsonSerializer.Deserialize<Settings>(str);
                stream.Dispose();
                if (string.IsNullOrEmpty(workspaceSettings.Id))
                {
                    // make sure workspace has ID (discord pending)
                    continue;
                }
                Client client = new Client(workspaceSettings, logger);
                JsonDocument connectionInfo = await client.GetConnectionInfo();
                string teamId = connectionInfo.RootElement.GetProperty("team").GetProperty("id").GetString();
                if (workspaceSettings.HubotEnabled)
                {
                    HubotWorkspace = teamId;
                }
                clients.Add(teamId, client);
                clientTasks.Add(client.Connect(new Uri(connectionInfo.RootElement.GetProperty("url").GetString())));
            }
            await Task.WhenAll(clientTasks);
        }

        public static async Task StartDiscordClient()
        {
            DiscordClient client = new DiscordClient();
            Uri connectionInfo = await client.GetConnectionInfo();
            await client.Connect(connectionInfo);
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
                _ = httpClient.PostAsync(requestBody.ResponseUrl, new StringContent(responseObject.ToString()));
            }
            //else if (requestBody.Command == "/stock")
            //{
            //    JObject responseObject = new JObject();
            //    responseObject["text"] = await stockCommand.Handle(requestBody.Text!, requestBody.UserId!);
            //    _ = httpClient.PostAsync(requestBody.ResponseUrl, new StringContent(responseObject.ToString()));
            //}
            else if (requestBody.Command == "/president")
            {
                JObject responseObject = new JObject();
                responseObject["text"] = await presidentCommand.Handle(requestBody.Text!, requestBody.UserId!);
                _ = httpClient.PostAsync(requestBody.ResponseUrl, new StringContent(responseObject.ToString()));
            }
        }

        [HttpPost("/hubot")]
        public void HubotResponse([FromBody]HubotResponseObject responseObject)
        {
            if (responseObject.Type == "message")
            {
                if (!string.IsNullOrEmpty(HubotWorkspace))
                {
                    if (!string.IsNullOrEmpty(responseObject.ThreadTimestamp))
                    {
                        _ = clients[HubotWorkspace].SendSlackMessage(responseObject.Text!, responseObject.Channel!, responseObject.ThreadTimestamp);
                    }
                    else
                    {
                        _ = clients[HubotWorkspace].SendSlackMessage(responseObject.Text!, responseObject.Channel!);
                    }
                }
            }
        }

        private JObject ProcessSlashCommand(RequestBody requestBody)
        {
            string[] splitText = requestBody.Text!.Split(' ');
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
