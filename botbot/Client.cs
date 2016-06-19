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
        public const double PsuedoRandomDistConst = 0.00380;
        // public static ILogger logger;

        static Client()
        {
            // logger = Startup.logFactory.CreateLogger<Client>();
            responded = false;
        }

        private SlackCore slackCore;
        private List<SlackUser> slackUsers;
        private List<SlackChannel> slackChannels;
        ClientWebSocket webSocket;
        static bool responded;
        List<string> pingResponses = new List<string>(new string[]
        { "pong", "hello", "hi", "what's up!", "I am always alive.", "hubot is an inferior bot.",
        "botbot at your service!", "lol", "fuck"});
        
        List<string> helpResponses = new List<string>(new string[]
        {
            "Hi I'm botbot! I don't do much...",
            "Hi I'm botbot!",
            "Try botbot commands!"
        });
        
        List<string> commands = new List<string>(new string[]
        {
            "ping",
            "reactions",
            "help",
            "commands"
        });

        List<string> iDontKnow = new List<string>(new string[]
        {
            "¯\\_(ツ)_/¯",
            "???",
            "?",
            "http://i.imgur.com/u4VDi0a.gif",
            "what?",
            "idk man",
            "idk"
        });

        event EventHandler<SlackMessageEventArgs> MessageReceived;

        // <channel, <user, timestamp>>
        Dictionary<string, Dictionary<string, DateTime>> typings;
        
        Dictionary<string, int> reactionMisses;
        
        public Client(string accessToken)
        {
            webSocket = new ClientWebSocket();
            MessageReceived += Client_MessageReceived;
            slackCore = new SlackCore(accessToken);
            slackUsers = new List<SlackUser>();
            typings = new Dictionary<string, Dictionary<string, DateTime>>();
            reactionMisses = new Dictionary<string, int>();
        }

        public async Task Connect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            slackUsers = await slackCore.UsersList();
            slackChannels = await slackCore.ChannelsList(1);
            Task.Run(() => CheckTypings());
            Task.Run(() => SendTypings(slackChannels.First(c => c.Name == "testing").Id));
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

        public async Task CheckTypings()
        {
            while (true)
            {
                // clean stale
                foreach (var typing in typings)
                {
                    List<string> typingUsers = typing.Value.Keys.ToList();
                    foreach (string user in typingUsers)
                    {
                        if (DateTime.UtcNow - typing.Value[user] >= TimeSpan.FromSeconds(3))
                        {
                            typing.Value.Remove(user);
                        }
                    }
                }

                // send on active
                foreach (var typing in typings)
                {
                    if (typing.Value.Count >= 3)
                    {
                        await SendTyping(typing.Key);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
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
                            string user = (string)o["user"];
                            if (!typings.ContainsKey(channel))
                            {
                                typings.Add(channel, new Dictionary<string, DateTime>());
                            }
                            if (!typings[channel].ContainsKey(user))
                            {
                                typings[channel].Add(user, DateTime.UtcNow);
                            }
                            else
                            {
                                typings[channel][user] = DateTime.UtcNow;
                            }
                        }
                    }
                }
            }
        }

        private async void Client_MessageReceived(object sender, SlackMessageEventArgs e)
        {
            string text = (string)e.Message["text"];
            string channel = (string)e.Message["channel"];
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(channel))
            {
                return;
            }
            if (text.ToLower() == "botbot ping")
            {
                await SendSlackMessage(GetRandomFromList(pingResponses), channel);
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
                if ((string)e.Message["user"] == slackUsers.First(u => u.Name == "hubot").Id)
                {
                    responded = true;
                }
            }
            else if (text.ToLower() == "botbot reactions")
            {
                await SendSlackMessage("Calculating reactions count, this might take me a minute...", channel);
                await CalculateReactions(channel);
            }
            else if (text.ToLower() == "botbot help")
            {
                await SendSlackMessage(GetRandomFromList(helpResponses), channel);
            }
            else if (text.ToLower() == "botbot commands")
            {
                foreach (string command in commands)
                {
                    await SendSlackMessage($"botbot {command}", channel);
                }
            }
            else if (text.ToLower().StartsWith("botbot "))
            {
                await SendSlackMessage(GetRandomFromList(iDontKnow), channel);
            }
            //await HandleReaction(e);
        }
        
        public async Task HandleReaction(SlackMessageEventArgs e)
        {
            string text = (string)e.Message["text"];
            string channel = (string)e.Message["channel"];
            string user = (string)e.Message["user"];
            string ts = (string)e.Message["ts"];
            if (user != "U0KNBSMT7")
            {
                return;
            }
            if (!reactionMisses.ContainsKey(user))
            {
                reactionMisses.Add(user, 1);
            }
            Random random = new Random();
            if (random.NextDouble() < reactionMisses[user] * PsuedoRandomDistConst)
            {
                await slackCore.ReactionsAdd("deirdre", channel: channel, timestamp: ts);
                reactionMisses[user] = 1;
            }
            else
            {
                reactionMisses[user]++;
            }
        }

        private async Task CalculateReactions(string channel)
        {
            List<SlackEvent> reactions = new List<SlackEvent>();
            foreach (SlackUser user in slackUsers)
            {
                reactions.AddRange(await slackCore.ReactionsList(user.Id, allItems: true));
            }
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
        
        public T GetRandomFromList<T>(List<T> list)
        {
            Random random = new Random();
            return list[random.Next(list.Count)];
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