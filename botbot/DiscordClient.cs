using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using golf1052.DiscordAPI;
using golf1052.DiscordAPI.Objects.Channel;
using golf1052.DiscordAPI.Objects.Channel.Requests;
using golf1052.DiscordAPI.Objects.Gateway;
using golf1052.DiscordAPI.Objects.Guild;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace botbot
{
    public class DiscordClient
    {
        private ClientWebSocket webSocket;
        private JsonSerializerSettings jsonSerializerSettings;
        private DiscordCore discordCore;
        
        private int? lastSequenceNumber;
        private bool gotBackFirstHeartbeat;

        public DiscordClient()
        {
            webSocket = new ClientWebSocket();
            jsonSerializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                    {
                        OverrideSpecifiedNames = false
                    }
                },
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            };
            discordCore = new DiscordCore(Secrets.DiscordToken);
        }

        public async Task Connect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            
            while (true)
            {
                try
                {
                    await Receive();
                }
                catch (WebSocketException)
                {
                    if (webSocket.State == WebSocketState.Aborted)
                    {
                        webSocket = new ClientWebSocket();
                        await Reconnect(await GetConnectionInfo());
                    }
                }
            }
        }

        private async Task Reconnect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
        }

        public async Task Receive()
        {
            while (!webSocket.CloseStatus.HasValue)
            {
                Pipe pipe = new Pipe();
                while (true)
                {
                    Memory<byte> memory = pipe.Writer.GetMemory(128);
                    ValueWebSocketReceiveResult response;
                    try
                    {
                        response = await webSocket.ReceiveAsync(memory, CancellationToken.None);
                        pipe.Writer.Advance(response.Count);
                    }
                    catch (WebSocketException)
                    {
                        throw;
                    }

                    if (response.EndOfMessage)
                    {
                        await pipe.Writer.FlushAsync();
                        break;
                    }
                }
                pipe.Writer.Complete();

                while (true)
                {
                    ReadResult result = await pipe.Reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    while (true)
                    {
                        if (buffer.IsEmpty && result.IsCompleted)
                        {
                            break;
                        }

                        string str = GetString(buffer);
                        System.Diagnostics.Debug.WriteLine(str);
                        JObject o = JObject.Parse(str);
                        buffer = buffer.Slice(buffer.End);
                        if (o["op"] != null)
                        {
                            if (o["s"] != null && o["s"]!.Type != JTokenType.Null)
                            {
                                lastSequenceNumber = (int)o["s"]!;
                            }
                            int opcode = (int)o["op"]!;
                            
                            if (opcode == 0)
                            {
                                if (o["t"] != null && o["t"]!.Type != JTokenType.Null)
                                {
                                    string type = (string)o["t"]!;
                                    if (type == "GUILD_CREATE")
                                    {
                                        GatewayPayload<DiscordGuild> guildCreate = JsonConvert.DeserializeObject<GatewayPayload<DiscordGuild>>(str, jsonSerializerSettings)!;
                                        DiscordChannel channel = guildCreate.Data.Channels.FirstOrDefault(c => c.Name == "test");
                                        if (channel != null)
                                        {
                                            _ = WriteToChannel(channel.Id);
                                        }
                                    }
                                }
                            }
                            if (opcode == 10)
                            {
                                GatewayPayload<Hello> hello = JsonConvert.DeserializeObject<GatewayPayload<Hello>>(str, jsonSerializerSettings)!;
                                _ = SendHeartbeats(hello.Data.HeartbeatInterval);
                            }
                            else if (opcode == 11)
                            {
                                // got back heartbeat
                                if (!gotBackFirstHeartbeat)
                                {
                                    gotBackFirstHeartbeat = true;
                                    _ = SendIdentify();
                                }
                            }
                        }

                        pipe.Reader.AdvanceTo(buffer.Start, buffer.End);
                    }

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                pipe.Reader.Complete();
            }
        }

        private string GetString(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                return Encoding.UTF8.GetString(buffer.First.Span);
            }
            else
            {
                // Taken from https://gist.github.com/terrajobst/6e1bea5bec4591edd7c5fe5416ce7f56#file-sample6-cs
                // Explaination: https://msdn.microsoft.com/en-us/magazine/mt814808.aspx?f=255&MSPPError=-2147217396
                return string.Create((int)buffer.Length, buffer, (span, sequence) =>
                {
                    foreach (var segment in sequence)
                    {
                        Encoding.UTF8.GetChars(segment.Span, span);
                        span = span.Slice(segment.Length);
                    }
                });
            }
        }

        private async Task WriteToChannel(string channelId)
        {
            Console.WriteLine("Ready to write to #test");
            while (true)
            {
                string readCharacters = Console.ReadLine();
                if (readCharacters == "DISCONNECT")
                {
                    await SendOfflineStatus();
                    Console.WriteLine("It is now safe to shutdown your bot.");
                    break;
                }
                CreateMessageRequest createMessageRequest = new CreateMessageRequest()
                {
                    Content = readCharacters
                };
                await discordCore.CreateMessage(channelId, createMessageRequest);
            }
        }

        public async Task<Uri> GetConnectionInfo()
        {
            var response = await discordCore.GetGatewayBot();
            Url connectionUrl = new Url(response.Url)
                .SetQueryParam("v", "6")
                .SetQueryParam("encoding", "json");
            return new Uri(connectionUrl);
        }

        private async Task SendHeartbeats(int interval)
        {
            while (true)
            {
                await SendHeartbeat();
                await Task.Delay(interval);
            }
        }

        private async Task SendIdentify()
        {
            Identify identify = new Identify()
            {
                Token = Secrets.DiscordToken,
                Properties = new IdentifyConnectionProperties()
                {
                    OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    Browser = "botbot",
                    Device = "botbot"
                },
                Presence = new UpdateStatus()
                {
                    Status = "online"
                }
            };

            var payload = new GatewayPayload<Identify>(2, identify);
            await SendGatewayPayload(payload);
        }

        private async Task SendOfflineStatus()
        {
            UpdateStatus status = new UpdateStatus()
            {
                Status = "offline"
            };

            await SendGatewayPayload(new GatewayPayload<UpdateStatus>(3, status));
        }

        private async Task SendHeartbeat()
        {
            JObject o = new JObject();
            o["op"] = 1;
            if (lastSequenceNumber.HasValue)
            {
                o["d"] = lastSequenceNumber.Value;
            }
            else
            {
                o["d"] = null;
            }
            await SendRawMessage(o.ToString(Formatting.None));
        }

        private async Task SendGatewayPayload<T>(GatewayPayload<T> payload)
        {
            string json = JsonConvert.SerializeObject(payload, Formatting.None, jsonSerializerSettings);
            System.Diagnostics.Debug.WriteLine($"SENDING: {json}");
            await SendRawMessage(json);
        }

        public async Task SendRawMessage(string rawMessage)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(rawMessage)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
