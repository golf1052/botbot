using System;
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
        
        public Client()
        {
            webSocket = new ClientWebSocket();
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
                            string text = (string)o["text"];
                            if (text == "botbot ping")
                            {
                                JObject r = new JObject();
                                r["id"] = 1;
                                r["type"] = "message";
                                r["channel"] = "C0911CW3C";
                                r["text"] = "fuck";
                                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(r.ToString(Formatting.None))), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                            Debug.WriteLine(o["text"]);
                        }
                    }
                    Debug.WriteLine(read);
                }
                //StringBuilder sb = new StringBuilder();
                //string responseString = string.Empty;
                //if (response.MessageType == WebSocketMessageType.Text)
                //{
                //    string tmp = Encoding.UTF8.GetString(buffer.Array);
                //    Debug.WriteLine(tmp);
                //    sb.Append(tmp);
                //    if (response.EndOfMessage)
                //    {
                //        responseString = sb.ToString();
                //        Debug.WriteLine(responseString);
                //    }
                //}
            }
        }
    }
}