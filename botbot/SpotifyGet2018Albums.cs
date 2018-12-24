using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reverb;
using Reverb.Models;

namespace botbot
{
    public class SpotifyGet2018Albums
    {
        private List<string> UserCodesPending;
        private Dictionary<string, SpotifyClient> Clients;
        private Func<string, string, Task> sendMessageFunc;

        public SpotifyGet2018Albums(Func<string, string, Task> sendMessageFunc)
        {
            this.sendMessageFunc = sendMessageFunc;
            UserCodesPending = new List<string>();
            Clients = new Dictionary<string, SpotifyClient>();
        }

        public async Task Receive(string text, string channel, string userId)
        {
            if (!Clients.ContainsKey(userId))
            {
                if (!UserCodesPending.Contains(userId))
                {
                    if (text.ToLower() == "botbot spotify")
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
                    Uri redirectUri = null;
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
                    await Get2018Albums(client, channel);
                }
            }
            else
            {
                if (text.ToLower() == "botbot spotify")
                {
                    await Get2018Albums(Clients[userId], channel);
                }
            }
        }

        public async Task Get2018Albums(SpotifyClient client, string channel)
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
            List<SpotifyAlbum> _2018Albums = albums.Where(a => a.ReleaseDate.Contains("2018")).ToList();
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var album in _2018Albums)
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
