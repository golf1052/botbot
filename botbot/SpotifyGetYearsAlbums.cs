using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reverb;
using Reverb.Models;

namespace botbot
{
    public class SpotifyGetYearsAlbums
    {
        private List<string> UserCodesPending;
        private Dictionary<string, SpotifyClient> Clients;
        private Func<string, string, Task> sendMessageFunc;

        public SpotifyGetYearsAlbums(Func<string, string, Task> sendMessageFunc)
        {
            this.sendMessageFunc = sendMessageFunc;
            UserCodesPending = new List<string>();
            Clients = new Dictionary<string, SpotifyClient>();
        }

        public async Task Receive(string text, string channel, string userId)
        {
            string[] splitText = text.Trim().Split(' ');
            string year;
            if (splitText.Length == 3)
            {
                year = splitText[2];
            }
            else
            {
                year = DateTimeOffset.UtcNow.Year.ToString();
            }
            if (!Clients.ContainsKey(userId))
            {
                if (!UserCodesPending.Contains(userId))
                {
                    if (text.ToLower().StartsWith("botbot spotify"))
                    {
                        string authorizeUrl = SpotifyHelpers.GetAuthorizeUrl(Secrets.SpotifyClientId,
                        Secrets.SpotifyRedirectUrl,
                        new List<SpotifyConstants.SpotifyScopes>()
                        {
                            SpotifyConstants.SpotifyScopes.UserTopRead,
                            SpotifyConstants.SpotifyScopes.UserReadRecentlyPlayed,
                            SpotifyConstants.SpotifyScopes.UserReadPrivate,
                            SpotifyConstants.SpotifyScopes.UserLibraryRead
                        });
                        UserCodesPending.Add(userId);
                        await sendMessageFunc.Invoke("Follow this link then paste the redirected link from golf1052.com back here", channel);
                        await sendMessageFunc.Invoke(authorizeUrl, channel);
                        return;
                    }
                }
                else
                {
                    Uri redirectUri;
                    try
                    {
                        redirectUri = new Uri(HelperMethods.ParseSlackUrl(text));
                    }
                    catch (UriFormatException)
                    {
                        UserCodesPending.Remove(userId);
                        await sendMessageFunc.Invoke("Cancelled", channel);
                        return;
                    }
                    SpotifyClient client = new SpotifyClient(Secrets.SpotifyClientId, Secrets.SpotifyClientSecret, Secrets.SpotifyRedirectUrl);
                    try
                    {
                        await client.ProcessRedirect(redirectUri, null);
                    }
                    catch (Exception ex)
                    {
                        UserCodesPending.Remove(userId);
                        await sendMessageFunc.Invoke("Couldn't login to Spotify. Error below. Please try botbot spotify again", channel);
                        await sendMessageFunc.Invoke(ex.Message, channel);
                        return;
                    }
                    Clients.Add(userId, client);
                    await GetYearsAlbums(client, channel, year);
                }
            }
            else
            {
                if (text.ToLower().StartsWith("botbot spotify"))
                {
                    await GetYearsAlbums(Clients[userId], channel, year);
                }
            }
        }

        public async Task GetYearsAlbums(SpotifyClient client, string channel, string year)
        {
            List<SpotifyAlbum> albums = new List<SpotifyAlbum>();
            var albumsPage = await client.GetUserSavedAlbums(50);
            do
            {
                albums.AddRange(albumsPage.Items.Select(a => a.Album).ToList());
                if (albumsPage.Next != null)
                {
                    albumsPage = await client.GetNextPage(albumsPage);
                }
            }
            while (albumsPage.Next != null);
            List<SpotifyAlbum> yearsAlbums = albums.Where(a => a.ReleaseDate.Contains(year)).ToList();
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var album in yearsAlbums)
            {
                SpotifyArtist artist = album.Artists.FirstOrDefault();
                string artistName = "";
                if (artist != null)
                {
                    artistName = artist.Name;
                }
                stringBuilder.Append($"{album.Name} by {artistName}. Released {album.ReleaseDate}\n");
            }
            await sendMessageFunc.Invoke(stringBuilder.ToString(), channel);
        }
    }
}
