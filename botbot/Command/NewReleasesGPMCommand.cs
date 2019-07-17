using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using botbot.Command.NewReleases.GPM;
using golf1052.SlackAPI.Objects;
using MongoDB.Driver;

namespace botbot.Command
{
    public class NewReleasesGPMCommand : ISlackCommand
    {
        private IMongoDatabase database;
        private IMongoCollection<NewReleasesGPMObject> newReleasesCollection;
        private List<string> userCodesPending;
        private GPMClient gpmClient;

        public NewReleasesGPMCommand()
        {
            database = Client.Mongo.GetDatabase("new_releases");
            newReleasesCollection = database.GetCollection<NewReleasesGPMObject>("new_releases_gpm");
            userCodesPending = new List<string>();
            gpmClient = new GPMClient(Secrets.GPMServerPortNumber);
        }

        public async Task<string> Handle(string text, string userId)
        {
            NewReleasesGPMObject newReleasesObject = GetNewReleasesObject(userId);
            if (newReleasesObject == null)
            {
                if (!userCodesPending.Contains(userId))
                {
                    string authorizeUrl = await gpmClient.GetAuthorizeUrl();
                    userCodesPending.Add(userId);
                    return $"Follow this link then paste back the code back here as `botbot new releases gpm <code>`\n{authorizeUrl}";
                }
                else
                {
                    string credentials = await gpmClient.GetCredentials(text);
                    newReleasesObject = new NewReleasesGPMObject()
                    {
                        UserId = userId,
                        AuthInfo = credentials,
                        AlbumsSeen = new List<SeenGPMAlbum>(),
                        LastChecked = DateTimeOffset.UnixEpoch
                    };
                    SaveNewReleasesObject(newReleasesObject);
                    return GetOutputString(await GetNewReleasesForUser(newReleasesObject));
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
            List<NewReleasesGPMObject> newReleasesObjects = GetAllNewReleaseObjects();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (NewReleasesGPMObject newReleasesObject in newReleasesObjects)
            {
                if (newReleasesObject.LastChecked <= now.Subtract(TimeSpan.FromHours(1)))
                {
                    SlackConversation userConversation = conversations.FirstOrDefault(c => { return c.User == newReleasesObject.UserId; });
                    if (userConversation != null)
                    {
                        string messageForUser = await GetNewReleasesForUser(newReleasesObject);
                        if (!string.IsNullOrWhiteSpace(messageForUser))
                        {
                            sendMessageFunc.Invoke(messageForUser, userConversation.Id);
                        }
                    }
                }
            }
        }

        private async Task<string> GetNewReleasesForUser(NewReleasesGPMObject newReleasesObject)
        {
            newReleasesObject.LastChecked = DateTimeOffset.UtcNow;
            List<GPMAlbum> newReleases = await gpmClient.GetAlbums(newReleasesObject.AuthInfo);
            List<GPMAlbum> notSeenReleases = new List<GPMAlbum>();
            foreach (GPMAlbum album in newReleases)
            {
                if (!newReleasesObject.AlbumsSeenContainsAlbum(album))
                {
                    notSeenReleases.Add(album);
                    SeenGPMAlbum seenGPMAlbum = new SeenGPMAlbum()
                    {
                        AlbumId = album.AlbumId,
                        ReleaseYear = album.Year
                    };
                    newReleasesObject.AlbumsSeen.Add(seenGPMAlbum);
                }
            }

            List<string> outputAlbums = new List<string>(notSeenReleases.Count);
            foreach (GPMAlbum album in notSeenReleases)
            {
                string toAdd;
                if (string.IsNullOrWhiteSpace(album.Artist))
                {
                    toAdd = album.Name;
                }
                else
                {
                    toAdd = $"{album.Name} by {album.Artist}";
                }

                if (!outputAlbums.Contains(toAdd))
                {
                    outputAlbums.Add(toAdd);
                }
            }

            CleanAlbumsSeen(newReleasesObject);
            SaveNewReleasesObject(newReleasesObject);
            return string.Join('\n', outputAlbums);
        }

        private void CleanAlbumsSeen(NewReleasesGPMObject newReleasesObject)
        {
            int currentYear = DateTimeOffset.UtcNow.Year;
            for (int i = 0; i < newReleasesObject.AlbumsSeen.Count; i++)
            {
                SeenGPMAlbum seenAlbum = newReleasesObject.AlbumsSeen[i];
                if (seenAlbum.ReleaseYear < currentYear)
                {
                    newReleasesObject.AlbumsSeen.RemoveAt(i);
                    i--;
                }
            }
        }

        private NewReleasesGPMObject GetNewReleasesObject(string userId)
        {
            var filter = Builders<NewReleasesGPMObject>.Filter.Eq("_id", userId);
            NewReleasesGPMObject newReleaseObject = newReleasesCollection.Find(filter).FirstOrDefault();
            return newReleaseObject;
        }

        private void SaveNewReleasesObject(NewReleasesGPMObject newReleasesObject)
        {
            newReleasesCollection.ReplaceOne(Builders<NewReleasesGPMObject>.Filter.Eq<string>("_id", newReleasesObject.UserId),
                newReleasesObject,
                new UpdateOptions() { IsUpsert = true });
        }

        private List<NewReleasesGPMObject> GetAllNewReleaseObjects()
        {
            return newReleasesCollection.Find(_ => true).ToList();
        }
    }
}
