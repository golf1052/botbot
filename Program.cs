using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace botbot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Client client = new Client();
            Task main = new Task(async () =>
            {
                HttpClient webClient = new HttpClient();
                Uri uri = new Uri("https://slack.com/api/rtm.start?token=" + Secrets.Token);
                Debug.WriteLine(uri.ToString());
                HttpResponseMessage response = await webClient.GetAsync(uri);
                Debug.WriteLine(response.StatusCode.ToString());
                JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
                await client.Connect(new Uri((string)responseObject["url"]));
            });
            main.RunSynchronously();
            Console.WriteLine("Hello World");
            Console.Read();
        }
    }
}
