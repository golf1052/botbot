using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace botbot
{
    public class SoundcloudApi
    {
        private const string BaseUrl = "https://api.soundcloud.com/";
        private const string botbotPlaylist = "https://api.soundcloud.com/playlists/300562390";

        HttpClient client;
        string accessToken;
        string refreshToken;
        DateTime tokenExpiration;

        public SoundcloudApi()
        {
            client = new HttpClient();
            refreshToken = string.Empty;
        }

        public async Task Auth()
        {
            string url = $"{BaseUrl}oauth2/token";
            Dictionary<string, string> options = new Dictionary<string, string>()
            {
                { "client_id", Secrets.SoundcloudClientId },
                { "client_secret", Secrets.SoundCloudClientSecret },
                { "username", "golf1052" },
                { "password", Secrets.SoundCloudPassword },
                { "grant_type", "password" }
            };
            HttpResponseMessage response = await client.PostAsync(url, new FormUrlEncodedContent(options));
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            UpdateAuth(responseObject);
        }

        public async Task RefreshToken()
        {
            string url = $"{BaseUrl}oauth/token";
            Dictionary<string, string> options = new Dictionary<string, string>()
            {
                { "client_id", Secrets.SoundcloudClientId },
                { "client_secret", Secrets.SoundCloudClientSecret },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" }
            };
            HttpResponseMessage response = await client.PostAsync(url, new FormUrlEncodedContent(options));
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            UpdateAuth(responseObject);
        }

        private void UpdateAuth(JObject responseObject)
        {
            tokenExpiration = DateTime.UtcNow + TimeSpan.FromSeconds((long)responseObject["expires_in"]);
            accessToken = (string)responseObject["access_token"];
            refreshToken = (string)responseObject["refresh_token"];
        }

        private async Task CheckAccess()
        {
            if (DateTime.UtcNow >= tokenExpiration)
            {
                await RefreshToken();
            }
        }

        public async Task<long> ResolveSoundcloud(string url)
        {
            string uri = FinishUrl($"{BaseUrl}resolve?url={url}");
            HttpResponseMessage response = await client.GetAsync(uri);
            if (response.IsSuccessStatusCode)
            {
                JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
                if ((string)responseObject["kind"] == "track")
                {
                    return (long)responseObject["id"];
                }
                else
                {
                    throw new Exception("Not a song");
                }
            }
            else
            {
                throw new Exception(response.StatusCode.ToString());
            }
        }

        public async Task AddSongToPlaylist(long id)
        {
            List<long> ids = new List<long>();
            ids.Add(id);
            await AddSongsToPlaylist(ids);
        }

        public async Task AddSongsToPlaylist(List<long> ids)
        {
            await CheckAccess();
            HttpResponseMessage playlistResponse = await client.GetAsync(FinishUrl(AuthUrl(botbotPlaylist)));
            JObject playlistObject = JObject.Parse(await playlistResponse.Content.ReadAsStringAsync());
            foreach (JObject track in playlistObject["tracks"])
            {
                ids.Add((long)track["id"]);
            }
            JArray tracks = new JArray();
            foreach (long id in ids)
            {
                JObject o = new JObject();
                o["id"] = id;
                tracks.Add(o);
            }
            JObject playlistO = new JObject();
            playlistO["playlist"] = new JObject();
            playlistO["playlist"]["tracks"] = tracks;
            StringContent content = new StringContent(playlistO.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
            string url = FinishUrl(AuthUrl(botbotPlaylist));
            HttpResponseMessage response = await client.PutAsync(url, content);
            Debug.WriteLine(await response.Content.ReadAsStringAsync());
        }

        private string FinishUrl(string url)
        {
            if (url.Contains("?"))
            {
                return $"{url}&client_id={Secrets.SoundcloudClientId}";
            }
            else
            {
                return $"{url}?client_id={Secrets.SoundcloudClientId}";
            }
        }

        private string AuthUrl(string url)
        {
            if (url.Contains("?"))
            {
                return $"{url}&oauth_token={accessToken}";
            }
            else
            {
                return $"{url}?oauth_token={accessToken}";
            }
        }
    }
}
