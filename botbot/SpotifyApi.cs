using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace botbot
{
    public class SpotifyApi
    {
        private const string AccountBaseUrl = "https://accounts.spotify.com/";
        private const string BaseUrl = "https://api.spotify.com/v1/";
        private const string UserId = "golf1052";
        private const string PlaylistId = "3IksVfwxIwjC7LDQMy3Yd2";
        public const string PlaylistUrl = "https://open.spotify.com/user/golf1052/playlist/3IksVfwxIwjC7LDQMy3Yd2";

        private readonly HttpClient client;
        private string? accessToken;
        private string? refreshToken;
        private DateTime tokenExpireTime;

        public SpotifyApi()
        {
            client = new HttpClient();
        }

        public string GetAuthUrl()
        {
            string scopes = "playlist-read-private playlist-read-collaborative playlist-modify-public playlist-modify-private user-read-private";
            string url = $"{AccountBaseUrl}authorize?client_id={Secrets.SpotifyClientId}&response_type=code&redirect_uri={Secrets.SpotifyRedirectUrl}&scope={scopes}";
            Debug.WriteLine(url);
            return url;
        }

        public async Task FinishAuth(string code)
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>()
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", Secrets.SpotifyRedirectUrl },
                { "client_id", Secrets.SpotifyClientId },
                { "client_secret", Secrets.SpotifyClientSecret }
            };
            FormUrlEncodedContent formContent = new FormUrlEncodedContent(pairs);
            HttpResponseMessage response = await client.PostAsync($"{AccountBaseUrl}api/token", formContent);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            UpdateAuth(responseObject);
        }

        private async Task RefreshToken()
        {
            Dictionary<string, string> pairs = new Dictionary<string, string>()
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken! },
                { "client_id", Secrets.SpotifyClientId },
                { "client_secret", Secrets.SpotifyClientSecret }
            };
            FormUrlEncodedContent formContent = new FormUrlEncodedContent(pairs);
            HttpResponseMessage response = await client.PostAsync($"{AccountBaseUrl}api/token", formContent);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            UpdateAuth(responseObject);
        }

        private void UpdateAuth(JObject responseObject)
        {
            tokenExpireTime = DateTime.UtcNow + TimeSpan.FromSeconds((long)responseObject["expires_in"]!);
            accessToken = (string)responseObject["access_token"]!;
            refreshToken = (string)responseObject["refresh_token"]!;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        private async Task CheckAccess()
        {
            if (DateTime.UtcNow >= tokenExpireTime)
            {
                await RefreshToken();
            }
        }

        public async Task<SpotifyTrack?> Search(string query)
        {
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }
            await CheckAccess();
            string url = $"{BaseUrl}search?q={query}&type=track&market=US";
            HttpResponseMessage response = await client.GetAsync(url);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            JArray items = (JArray)responseObject["tracks"]!["items"]!;
            if (items.Count > 0)
            {
                string uri = (string)items[0]["uri"]!;
                string name = (string)items[0]["name"]!;
                string artist = string.Empty;
                if (((JArray)items[0]["album"]!["artists"]!).Count > 0)
                {
                    artist = (string)items[0]["album"]!["artists"]![0]!["name"]!;
                }
                string album = (string)items[0]["album"]!["name"]!;
                return new SpotifyTrack(uri, name, artist, album);
            }
            else
            {
                if (query.Contains("(") && query.Contains(")"))
                {
                    int firstIndex = query.IndexOf('(');
                    int secondIndex = query.LastIndexOf(')');
                    if (firstIndex != -1 && secondIndex != -1)
                    {
                        query = query.Remove(firstIndex, secondIndex - firstIndex + 1);
                        return await Search(query);
                    }
                    else
                    {
                        return null;
                    }
                }
                return null;
            }
        }

        public async Task AddTrackToPlaylist(string track)
        {
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return;
            }
            List<string> tracks = new List<string>();
            tracks.Add(track);
            await AddTracksToPlaylist(tracks);
        }

        public async Task AddTracksToPlaylist(List<string> tracks)
        {
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return;
            }
            await CheckAccess();
            string url = $"{BaseUrl}users/{UserId}/playlists/{PlaylistId}/tracks?position=0";
            JObject uris = new JObject();
            JArray a = new JArray();
            foreach (var track in tracks)
            {
                a.Add(track);
            }
            uris["uris"] = a;
            StringContent content = new StringContent(uris.ToString(Formatting.None), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed");
            }
        }
    }
}
