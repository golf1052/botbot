using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using botbot.Command.NewReleases;
using golf1052.SlackAPI.Objects;
using MongoDB.Driver;
using Reverb;
using Reverb.Models;

namespace botbot.Command
{
    public class NewReleasesCommand : ISlackCommand
    {
        private IMongoDatabase database;
        private IMongoCollection<NewReleasesObject> newReleasesCollection;
        private List<string> userCodesPending;

        private readonly string[] releaseDateFormats = new string[]
        {
            "yyyy",
            "yyyy-MM",
            "yyyy-MM-dd"
        };

        public NewReleasesCommand()
        {
            database = Client.Mongo.GetDatabase("new_releases");
            newReleasesCollection = database.GetCollection<NewReleasesObject>("new_releases");
            userCodesPending = new List<string>();
        }

        public async Task<string> Handle(string text, string userId)
        {
            NewReleasesObject newReleasesObject = GetNewReleasesObject(userId);
            if (newReleasesObject == null)
            {
                if (!userCodesPending.Contains(userId))
                {
                    string authorizeUrl = SpotifyHelpers.GetAuthorizeUrl(Secrets.SpotifyNewReleasesClientId, Secrets.SpotifyRedirectUrl,
                        new List<SpotifyConstants.SpotifyScopes>()
                        {
                            SpotifyConstants.SpotifyScopes.UserLibraryRead,
                            SpotifyConstants.SpotifyScopes.UserReadPrivate
                        });
                    userCodesPending.Add(userId);
                    return $"Follow this link, then paste back the redirected link from golf1052.com back here as `botbot new releases <link>`\n{authorizeUrl}";
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
                        userCodesPending.Remove(userId);
                        return "Cancelled";
                    }
                    SpotifyClient client = new SpotifyClient(Secrets.SpotifyNewReleasesClientId, Secrets.SpotifyNewReleasesClientSecret, Secrets.SpotifyRedirectUrl);
                    try
                    {
                        await client.ProcessRedirect(redirectUri, null);
                    }
                    catch (Exception ex)
                    {
                        userCodesPending.Remove(userId);
                        return $"Couldn't login to Spotify. Error below. Please try again\n{ex.Message}";
                    }

                    SpotifyAuth spotifyAuth = new SpotifyAuth()
                    {
                        AccessToken = client.AccessToken,
                        ExpiresAt = client.AccessTokenExpiresAt,
                        RefreshToken = client.RefreshToken
                    };
                    newReleasesObject = new NewReleasesObject()
                    {
                        UserId = userId,
                        AuthInfo = spotifyAuth,
                        AlbumsSeen = new List<SeenSpotifyAlbum>(),
                        LastChecked = DateTimeOffset.UnixEpoch
                    };
                    SaveNewReleasesObject(newReleasesObject);
                    return GetOutputString(await GetNewReleasesForUser(client, newReleasesObject));
                }
            }
            else
            {
                return GetOutputString(await GetNewReleasesForUser(newReleasesObject));
            }
        }

        private string GetOutputString(string originalOutput)
        {
            if (string.IsNullOrWhiteSpace(originalOutput))
            {
                return "No new releases.";
            }
            else
            {
                return originalOutput;
            }
        }

        public async Task CheckNewReleasesForUsers(List<SlackConversation> conversations, Func<string, string, Task> sendMessageFunc)
        {
            List<NewReleasesObject> newReleasesObjects = GetAllNewReleaseObjects();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (NewReleasesObject newReleasesObject in newReleasesObjects)
            {
                if (newReleasesObject.LastChecked <= now.Subtract(TimeSpan.FromHours(1)))
                {
                    SlackConversation userConversation = conversations.FirstOrDefault(c => { return c.User == newReleasesObject.UserId; });
                    if (userConversation != null)
                    {
                        string messageForUser = await GetNewReleasesForUser(newReleasesObject);
                        if (!string.IsNullOrWhiteSpace(messageForUser))
                        {
                            _ = sendMessageFunc.Invoke(messageForUser, userConversation.Id);
                        }
                    }
                }
            }
        }

        private async Task<string> GetNewReleasesForUser(NewReleasesObject newReleasesObject)
        {
            if (newReleasesObject.AuthInfo!.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                SpotifyClient spotifyClient = new SpotifyClient(Secrets.SpotifyNewReleasesClientId, Secrets.SpotifyNewReleasesClientSecret, Secrets.SpotifyRedirectUrl);
                await spotifyClient.RefreshAccessToken(newReleasesObject.AuthInfo.RefreshToken);
                newReleasesObject.AuthInfo.AccessToken = spotifyClient.AccessToken;
                newReleasesObject.AuthInfo.ExpiresAt = spotifyClient.AccessTokenExpiresAt;
                return await GetNewReleasesForUser(spotifyClient, newReleasesObject);
            }
            else
            {
                SpotifyClient spotifyClient = new SpotifyClient(Secrets.SpotifyNewReleasesClientId,
                    Secrets.SpotifyNewReleasesClientSecret,
                    Secrets.SpotifyRedirectUrl);
                await spotifyClient.Authenticate(newReleasesObject.AuthInfo.AccessToken,
                    newReleasesObject.AuthInfo.ExpiresAt,
                    newReleasesObject.AuthInfo.RefreshToken);
                return await GetNewReleasesForUser(spotifyClient, newReleasesObject);
            }
        }

        private async Task<string> GetNewReleasesForUser(SpotifyClient spotifyClient, NewReleasesObject newReleasesObject)
        {
            newReleasesObject.LastChecked = DateTimeOffset.UtcNow;
            List<SpotifyAlbum> allNewReleases = await GetNewReleases(spotifyClient);
            List<SpotifyAlbum> notSeenReleases = new List<SpotifyAlbum>();
            foreach (SpotifyAlbum album in allNewReleases)
            {
                if (!newReleasesObject.AlbumsSeenContainsAlbum(album))
                {
                    notSeenReleases.Add(album);
                    SeenSpotifyAlbum seenSpotifyAlbum = new SeenSpotifyAlbum()
                    {
                        Id = album.Id,
                        ReleaseDate = ParseReleaseDate(album.ReleaseDate)
                    };
                    newReleasesObject.AlbumsSeen.Add(seenSpotifyAlbum);
                }
            }

            List<string> outputAlbums = new List<string>(notSeenReleases.Count);
            foreach (SpotifyAlbum album in notSeenReleases)
            {
                List<string> albumArtists = new List<string>();
                foreach (var artist in album.Artists)
                {
                    albumArtists.Add(artist.Name);
                }
                string albumArtistNames = string.Join(", ", albumArtists);

                string nameToAdd;
                if (string.IsNullOrWhiteSpace(albumArtistNames))
                {
                    nameToAdd = $"{album.Name}";
                }
                else
                {
                    nameToAdd = $"{album.Name} by {albumArtistNames}";
                }

                if (!outputAlbums.Contains(nameToAdd))
                {
                    outputAlbums.Add(nameToAdd);
                }
            }

            CleanAlbumsSeen(newReleasesObject);
            SaveNewReleasesObject(newReleasesObject);
            return string.Join('\n', outputAlbums);
        }

        private void CleanAlbumsSeen(NewReleasesObject newReleasesObject)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            for (int i = 0; i < newReleasesObject.AlbumsSeen.Count; i++)
            {
                SeenSpotifyAlbum seenAlbum = newReleasesObject.AlbumsSeen[i];
                if (seenAlbum.ReleaseDate <= now.Subtract(TimeSpan.FromDays(14)))
                {
                    newReleasesObject.AlbumsSeen.RemoveAt(i);
                    i--;
                }
            }
        }

        private async Task<List<SpotifyAlbum>> GetNewReleases(SpotifyClient spotifyClient)
        {
            List<SpotifyArtist> userArtists = new List<SpotifyArtist>();
            var userAlbums = await spotifyClient.GetUserSavedAlbums(50);
            foreach (var album in userAlbums.Items)
            {
                foreach (var artist in album.Album.Artists)
                {
                    if (!userArtists.Any(a => { return a.Id == artist.Id; }))
                    {
                        userArtists.Add(artist);
                    }
                }
            }

            while (userAlbums.Next != null)
            {
                userAlbums = await spotifyClient.GetNextPage(userAlbums);
                foreach (var album in userAlbums.Items)
                {
                    foreach (var artist in album.Album.Artists)
                    {
                        if (!userArtists.Any(a => { return a.Id == artist.Id; }))
                        {
                            userArtists.Add(artist);
                        }
                    }
                }
            }

            List<SpotifyAlbum> newReleases = new List<SpotifyAlbum>();
            DateTimeOffset currentDate = DateTimeOffset.UtcNow;
            foreach (var artist in userArtists)
            {
                var albums = await spotifyClient.GetArtistsAlbums(artist.Id, new List<SpotifyConstants.SpotifyArtistIncludeGroups>()
                {
                    SpotifyConstants.SpotifyArtistIncludeGroups.Album,
                    SpotifyConstants.SpotifyArtistIncludeGroups.Single
                });

                foreach (var album in albums.Items)
                {
                    if (ParseReleaseDate(album.ReleaseDate) >= currentDate.Subtract(TimeSpan.FromDays(14)))
                    {
                        if (!newReleases.Any(a => { return a.Id == album.Id; }))
                        {
                            newReleases.Add(album);
                        }
                    }
                }
            }

            newReleases.Sort((a1, a2) =>
            {
                DateTimeOffset a1ReleaseDate = ParseReleaseDate(a1.ReleaseDate);
                DateTimeOffset a2ReleaseDate = ParseReleaseDate(a2.ReleaseDate);
                return a2.ReleaseDate.CompareTo(a1.ReleaseDate);
            });

            return newReleases;
        }

        public DateTimeOffset ParseReleaseDate(string releaseDate)
        {
            DateTimeOffset.TryParseExact(releaseDate,
                releaseDateFormats,
                null,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset result);
            return result;
        }

        private NewReleasesObject GetNewReleasesObject(string userId)
        {
            var filter = Builders<NewReleasesObject>.Filter.Eq("_id", userId);
            NewReleasesObject newReleaseObject = newReleasesCollection.Find(filter).FirstOrDefault();
            return newReleaseObject;
        }

        private void SaveNewReleasesObject(NewReleasesObject newReleasesObject)
        {
            newReleasesCollection.ReplaceOne(Builders<NewReleasesObject>.Filter.Eq("_id", newReleasesObject.UserId!),
                newReleasesObject,
                new UpdateOptions() { IsUpsert = true });
        }

        private List<NewReleasesObject> GetAllNewReleaseObjects()
        {
            return newReleasesCollection.Find(_ => true).ToList();
        }
    }
}
