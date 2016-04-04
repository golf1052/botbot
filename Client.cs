using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using golf1052.SlackAPI;
using golf1052.SlackAPI.Events;
using golf1052.SlackAPI.Objects;
using golf1052.SlackAPI.Other;

namespace botbot
{
    public class Client
    {
        public const string BaseUrl = "https://slack.com/api/";
        public static ILogger logger;

        static Client()
        {
            logger = Startup.logFactory.CreateLogger<Client>();
            responded = false;
        }

        private SlackCore slackCore;
        private List<SlackUser> slackUsers;
        ClientWebSocket webSocket;
        static bool responded;
        List<string> pingResponses = new List<string>(new string[]
        { "pong", "hello", "hi", "what's up!", "I am always alive.", "hubot is an inferior bot.",
        "botbot at your service!", "lol", "fuck"});

        event EventHandler<SlackMessageEventArgs> MessageReceived;
        
        Dictionary<string, int> channelTypeCounts;
        Dictionary<string, int> secondsWithoutTyping;
        
        public Client(string accessToken)
        {
            webSocket = new ClientWebSocket();
            MessageReceived += Client_MessageReceived;
            channelTypeCounts = new Dictionary<string, int>();
            secondsWithoutTyping = new Dictionary<string, int>();
            slackCore = new SlackCore(accessToken);
            slackUsers = new List<SlackUser>();
        }

        public async Task Connect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            slackUsers = await slackCore.UsersList();
            SendTypings("C0911CW3C");
            // slack knows if you try to type into two channels at once, and then kills you for it
            //SendTypings("G0L8C7Q6L");
            //CheckTypings("C0911CW3C");
            await Receive();
        }
        
        public async Task SendTypings(string channel)
        {
            while (true)
            {
                Random random = new Random();
                int sendTypingFor = random.Next(1, 11);
                for (int i = 0; i < sendTypingFor; i++)
                {
                    await SendTyping(channel);
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }
                int waitFor = random.Next(5, 61);
                await Task.Delay(TimeSpan.FromMinutes(waitFor));
            }
        }
        
        public async Task CheckTypings(string channel)
        {
            while (true)
            {
                if (channelTypeCounts.ContainsKey(channel))
                {
                    if (channelTypeCounts[channel] >= 2)
                    {
                        channelTypeCounts[channel] = 0;
                        await SendSlackMessage("_several people are typing_", channel);
                        await Task.Delay(TimeSpan.FromHours(2));
                    }
                    else
                    {
                        secondsWithoutTyping[channel]++;
                        if (secondsWithoutTyping[channel] >= 20)
                        {
                            channelTypeCounts[channel] = 0;
                            secondsWithoutTyping[channel] = 0;
                        }
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
        
        public async Task Receive()
        {
            while (!webSocket.CloseStatus.HasValue)
            {
                byte[] buf = new byte[1024 * 4];
                ArraySegment<byte> buffer = new ArraySegment<byte>(buf);
                MemoryStream stream = new MemoryStream();
                StreamReader reader = new StreamReader(stream);
                WebSocketReceiveResult response = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                stream.Write(buffer.Array, buffer.Offset, buffer.Count);
                stream.Seek(0, SeekOrigin.Begin);
                while (!reader.EndOfStream)
                {
                    string read = reader.ReadLine();
                    JObject o = JObject.Parse(read);
                    if (o["type"] != null)
                    {
                        string messageType = (string)o["type"];
                        if (messageType == "message")
                        {
                            SlackMessageEventArgs newMessage = new SlackMessageEventArgs();
                            newMessage.Message = o;
                            MessageReceived(this, newMessage);
                        }
                        else if (messageType == "user_typing")
                        {
                            string channel = (string)o["channel"];
                            if (!channelTypeCounts.ContainsKey(channel))
                            {
                                secondsWithoutTyping.Add(channel, 0);
                                channelTypeCounts.Add(channel, 0);
                            }
                            channelTypeCounts[channel]++;
                            secondsWithoutTyping[channel] = 0;
                        }
                    }
                }
            }
        }

        private async void Client_MessageReceived(object sender, SlackMessageEventArgs e)
        {
            string text = (string)e.Message["text"];
            string channel = (string)e.Message["channel"];
            if (text.ToLower() == "botbot ping")
            {
                await SendSlackMessage(GetRandomPingResponse(), channel);
            }
            else if (text.ToLower() == "hubot ping")
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                if (!responded)
                {
                    await SendSlackMessage("Hubot is dead. I killed him.", channel);
                }
                responded = false;
            }
            else if (text.ToLower() == "pong")
            {
                if ((string)e.Message["user"] == "U09763X54")
                {
                    responded = true;
                }
            }
            else if (text.ToLower() == "botbot reactions")
            {
                await CalculateReactions(channel);
            }
        }

        private async Task CalculateReactions(string channel)
        {
            List<SlackEvent> reactions = new List<SlackEvent>();
            foreach (SlackUser user in slackUsers)
            {
                reactions.AddRange(await slackCore.ReactionsList(user.Id, allItems: true));
            }
            List<SlackEvent> text = reactions.Where(q =>
            {
                Message m = q as Message;
                return m.Text == "Try again";
            }).ToList();
            Dictionary<string, Dictionary<DateTime, int>> topReactions = new Dictionary<string, Dictionary<DateTime, int>>();
            Dictionary<string, int> topR = new Dictionary<string, int>();
            foreach (SlackEvent reaction in reactions)
            {
                if (reaction.Type == SlackEvent.SlackEventType.Message)
                {
                    Message message = reaction as Message;
                    foreach (Reaction r in message.Reactions)
                    {
                        if (!topReactions.ContainsKey(r.Name))
                        {
                            topReactions.Add(r.Name, new Dictionary<DateTime, int>());
                        }
                        if (!topReactions[r.Name].ContainsKey(message.Timestamp))
                        {
                            topReactions[r.Name].Add(message.Timestamp, r.Count);
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, Dictionary<DateTime, int>> pair in topReactions)
            {
                foreach (KeyValuePair<DateTime, int> timePair in pair.Value)
                {
                    if (!topR.ContainsKey(pair.Key))
                    {
                        topR.Add(pair.Key, 0);
                    }
                    topR[pair.Key] += timePair.Value;
                }
            }
            List<string> finalMessage = new List<string>();
            finalMessage.Add("Top 10 reactions to messages");
            var sortedReactions = (from entry in topR orderby entry.Value descending select entry).ToList();
            for (int i = 0; i < 10; i++)
            {
                finalMessage.Add($":{sortedReactions[i].Key}: - {sortedReactions[i].Value}");
            }
            await SendSlackMessage(finalMessage, channel);
        }

        public string GetRandomPingResponse()
        {
            Random random = new Random();
            return pingResponses[random.Next(pingResponses.Count)];
        }

        public async Task SendSlackMessage(List<string> lines, string channel)
        {
            string message = string.Empty;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i != lines.Count - 1)
                {
                    message += lines[i] += '\n';
                }
                else
                {
                    message += lines[i];
                }
            }
            await SendSlackMessage(message, channel);
        }

        public async Task SendSlackMessage(string message, string channel)
        {
            JObject o = new JObject();
            o["id"] = 1;
            o["type"] = "message";
            o["channel"] = channel;
            o["text"] = message;
            await SendMessage(o.ToString(Formatting.None));
        }
        
        public async Task SendTyping(string channel)
        {
            JObject o = new JObject();
            o["id"] = 1;
            o["type"] = "typing";
            o["channel"] = channel;
            await SendMessage(o.ToString(Formatting.None));
        }

        public async Task SendMessage(string message)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task SendApiCall(string endpoint)
        {
            Uri url = new Uri(BaseUrl + endpoint);
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(url);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
        }
    }
}