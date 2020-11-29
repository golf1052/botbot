using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using golf1052.SlackAPI.Events;
using Reverb;

namespace botbot.Module.SlackAttachments
{
    public class GooglePlayMusicModule : ISlackAttachmentModule
    {
        private SpotifyClient spotifyClient;

        public GooglePlayMusicModule()
        {
            spotifyClient = new SpotifyClient(Secrets.SpotifyClientId, Secrets.SpotifyClientSecret, Secrets.SpotifyRedirectUrl);
        }

        public async Task<ModuleResponse> Handle(SlackMessage message, Attachment attachment)
        {
            if (string.IsNullOrEmpty(spotifyClient.AccessToken))
            {
                await spotifyClient.RequestAccessToken();
            }
            if (spotifyClient.AccessTokenExpiresAt <= DateTimeOffset.UtcNow)
            {
                await spotifyClient.RefreshAccessToken();
            }

            if (attachment.ServiceName == "play.google.com" && attachment.OriginalUrl.Contains("/music/"))
            {
                string title = attachment.Title;
                // ampersands are escaped as "&amp;" so turn it into a regular &
                title = title.Replace("&amp;", "&");
                string[] splitTitle = title.Split("-");
                string guessTitle = string.Empty;
                string guessArtist = string.Empty;
                if (splitTitle.Length > 0)
                {
                    guessTitle = splitTitle[0].Trim();
                }
                if (splitTitle.Length > 1)
                {
                    guessArtist = splitTitle[1].Trim();
                }

                if (spotifyClient.AccessTokenExpiresAt <= DateTimeOffset.UtcNow)
                {
                    await spotifyClient.RefreshAccessToken();
                }
                var searchResult = await spotifyClient.Search(title,
                    new List<SpotifyConstants.SpotifySearchTypes> { SpotifyConstants.SpotifySearchTypes.Album, SpotifyConstants.SpotifySearchTypes.Track });
                var firstAlbum = searchResult.Albums.Items.FirstOrDefault();
                var firstTrack = searchResult.Tracks.Items.FirstOrDefault();
                string spotifyLink = string.Empty;
                if (firstAlbum != null)
                {
                    var artist = firstAlbum.Artists.FirstOrDefault();
                    if (artist?.Name == guessArtist && firstAlbum.Name == guessTitle)
                    {
                        if (firstAlbum.ExternalUrls.ContainsKey("spotify"))
                        {
                            spotifyLink = firstAlbum.ExternalUrls["spotify"];
                        }
                    }
                }

                if (string.IsNullOrEmpty(spotifyLink) && firstTrack != null)
                {
                    var artist = firstTrack.Artists.FirstOrDefault();
                    if (artist?.Name == guessArtist && firstTrack.Name == guessTitle)
                    {
                        if (firstTrack.ExternalUrls.ContainsKey("spotify"))
                        {
                            spotifyLink = firstTrack.ExternalUrls["spotify"];
                        }
                    }
                }

                if (!string.IsNullOrEmpty(spotifyLink))
                {
                    return new ModuleResponse()
                    {
                        Message = $"Spotify Link: {spotifyLink}",
                        Timestamp = message.Message.ThreadTimestamp
                    };
                }
                else
                {
                    return new ModuleResponse();
                }
            }
            else
            {
                return new ModuleResponse();
            }
        }
    }
}
