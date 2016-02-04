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

namespace botbot
{
    public class Client
    {
        ClientWebSocket webSocket;
        static bool responded;
        List<string> pingResponses = new List<string>(new string[]
        { "pong", "hello", "hi", "what's up!", "I am always alive.", "hubot is an inferior bot.",
        "botbot at your service!", "lol", "fuck"});

        event EventHandler<SlackMessageEventArgs> MessageReceived;
        
        public Client()
        {
            webSocket = new ClientWebSocket();
            responded = false;
            MessageReceived += Client_MessageReceived;
        }

        public async Task Connect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            await Receive();
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
                        if ((string)o["type"] == "message")
                        {
                            SlackMessageEventArgs newMessage = new SlackMessageEventArgs();
                            newMessage.Message = o;
                            MessageReceived(this, newMessage);
                        }
                    }
                }
            }
        }

        private async void Client_MessageReceived(object sender, SlackMessageEventArgs e)
        {
            Debug.WriteLine("recieved message");
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

        public async Task SendMessage(string message)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}