using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using golf1052.SlackAPI;
using Newtonsoft.Json.Linq;

namespace botbot.Module
{
    public class HubotModule : SlackEventModule
    {
        private readonly HttpClient httpClient;
        private readonly string baseUrl;

        public HubotModule(SlackCore slackCore, Func<string, Task> SendMessage) : base(slackCore, SendMessage)
        {
            httpClient = new HttpClient();
            baseUrl = $"http://localhost:{Secrets.HubotPortNumber}";
        }

        public override Task Handle(string type, JObject e)
        {
            if (type == "hello")
            {
            }
            else if (type == "goodbye")
            {
            }
            else
            {
                JObject requestObject = new JObject();
                requestObject["slack"] = e;
                _ = SendHubot("slack", requestObject);
            }

            return Task.CompletedTask;
        }

        public async Task Init(string token)
        {
            JObject requestObject = new JObject();
            requestObject["token"] = token;
            await SendHubot("init", requestObject);
        }

        private async Task SendHubot(string type, JObject requestObject)
        {
            requestObject["type"] = type;
            try
            {
                HttpResponseMessage responseMessage = await httpClient.PostAsync(baseUrl, new StringContent(requestObject.ToString(), Encoding.UTF8, "application/json"));
                if (!responseMessage.IsSuccessStatusCode)
                {
                    // maybe log?
                }
            }
            catch (Exception)
            {
                // if hubot is down don't really worry about it
            }
        }

        public override RecurringModule? RegisterRecurring()
        {
            return null;
        }
    }
}
