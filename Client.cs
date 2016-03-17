using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace botbot
{
    public class Client
    {
        public static ILogger logger;
        static Client()
        {
            logger = Startup.logFactory.CreateLogger<Client>();
            responded = false;
        }
        
        ClientWebSocket webSocket;
        static bool responded;
        List<string> pingResponses = new List<string>(new string[]
        { "pong", "hello", "hi", "what's up!", "I am always alive.", "hubot is an inferior bot.",
        "botbot at your service!", "lol", "fuck"});

        event EventHandler<SlackMessageEventArgs> MessageReceived;
        
        Dictionary<string, int> channelTypeCounts;
        Dictionary<string, int> secondsWithoutTyping;
        
        public Client()
        {
            webSocket = new ClientWebSocket();
            MessageReceived += Client_MessageReceived;
            channelTypeCounts = new Dictionary<string, int>();
            secondsWithoutTyping = new Dictionary<string, int>();
        }

        public async Task Connect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            SendTypings("C0911CW3C");
            // slack knows if you try to type into two channels at once, and then kills you for it
            //SendTypings("G0L8C7Q6L");
            CheckTypings("C0911CW3C");
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
        }

        public string GetRandomPingResponse()
        {
            Random random = new Random();
            return pingResponses[random.Next(pingResponses.Count)];
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
    }
}