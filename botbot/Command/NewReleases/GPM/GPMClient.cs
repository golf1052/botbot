using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace botbot.Command.NewReleases.GPM
{
    public class GPMClient
    {
        private HttpClient httpClient;
        private readonly string baseUrl;

        public GPMClient(ushort portNumber)
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            baseUrl = $"http://localhost:{portNumber}";
        }

        public async Task<string> GetAuthorizeUrl()
        {
            JObject requestObject = new JObject();
            requestObject["api"] = "login";
            JObject responseObject = await SendRequest(requestObject);
            return (string)responseObject["url"]!;
        }

        public async Task<string> GetCredentials(string code)
        {
            JObject requestObject = new JObject();
            requestObject["auth"] = code;
            JObject responseObject = await SendRequest(requestObject);
            return (string)responseObject["creds"]!;
        }

        public async Task<List<GPMAlbum>> GetAlbums(string credentials)
        {
            JObject requestObject = new JObject();
            requestObject["api"] = "new_releases";
            requestObject["creds"] = credentials;
            JObject responseObject = await SendRequest(requestObject);
            List<GPMAlbum> albums = new List<GPMAlbum>();
            foreach (JObject item in (JArray)responseObject["albums"]!)
            {
                albums.Add(JsonConvert.DeserializeObject<GPMAlbum>(item.ToString()));
            }
            return albums;
        }

        private async Task<JObject> SendRequest(JObject requestObject)
        {
            HttpResponseMessage responseMessage = await httpClient.PostAsync(baseUrl, new StringContent(requestObject.ToString(), Encoding.UTF8, "application/json"));
            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new Exception();
            }
            return JObject.Parse(await responseMessage.Content.ReadAsStringAsync());
        }
    }
}
