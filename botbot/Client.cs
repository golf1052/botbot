using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using golf1052.SlackAPI;
using golf1052.SlackAPI.Events;
using golf1052.SlackAPI.Objects;
using golf1052.SlackAPI.Other;
using MongoDB.Driver;
using botbot.Status;

namespace botbot
{
    public class Client
    {
        public const string BaseUrl = "https://slack.com/api/";
        public const double PsuedoRandomDistConst = 0.00380;
        public static MongoClient Mongo;
        public static IMongoDatabase PlusPlusDatabase;
        public static IMongoCollection<PlusPlusThing> ThingCollection;
        public static IMongoCollection<PlusPlusLog> PlusPlusLogCollection;
        public static StatusNotifier StatusNotifier;

        static Client()
        {
            responded = false;
            Mongo = new MongoClient(Secrets.MongoConnectionString);
            PlusPlusDatabase = Mongo.GetDatabase("plusplus");
            ThingCollection = PlusPlusDatabase.GetCollection<PlusPlusThing>("things");
            PlusPlusLogCollection = PlusPlusDatabase.GetCollection<PlusPlusLog>("log");
            StatusNotifier = new StatusNotifier();
        }

        private readonly Settings settings;
        private readonly ILogger logger;
        private SlackCore slackCore;
        private List<SlackUser> slackUsers;
        private List<SlackChannel> slackChannels;
        ClientWebSocket webSocket;
        static bool responded;
        List<string> pingResponses = new List<string>(new string[]
        { "pong", "hello", "hi", "what's up!", "I am always alive.", "hubot is an inferior bot.",
        "botbot at your service!", "lol", "fuck"});
        
        List<string> hiResponses = new List<string>(new string[]
        {
            "hello", "hi", "what's up!", "sup fucker"
        });

        List<string> helpResponses = new List<string>(new string[]
        {
            "Hi I'm botbot! I don't do much...",
            "Hi I'm botbot!",
            "Try botbot commands!"
        });
        
        List<string> commands = new List<string>(new string[]
        {
            "ping",
            "reactions",
            //"playlist",
            "help",
            "commands"
        });

        List<string> iDontKnow = new List<string>(new string[]
        {
            "¯\\_(ツ)_/¯",
            "???",
            "?",
            "http://i.imgur.com/u4VDi0a.gif",
            "what?",
            "idk"
        });

        event EventHandler<SlackMessageEventArgs> MessageReceived;

        // <channel, <user, timestamp>>
        Dictionary<string, Dictionary<string, DateTime>> typings;
        
        Dictionary<string, int> reactionMisses;

        SoundcloudApi soundcloud;
        SpotifyApi spotify;

        HttpClient httpClient;
        
        public Client(Settings settings, ILogger<Client> logger)
        {
            this.settings = settings;
            httpClient = new HttpClient();
            webSocket = new ClientWebSocket();
            MessageReceived += Client_MessageReceived;
            slackCore = new SlackCore(settings.Token);
            slackUsers = new List<SlackUser>();
            typings = new Dictionary<string, Dictionary<string, DateTime>>();
            reactionMisses = new Dictionary<string, int>();
            soundcloud = new SoundcloudApi();
            spotify = new SpotifyApi();
            this.logger = logger;
        }

        public async Task Connect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            slackUsers = await slackCore.UsersList();
            slackChannels = await slackCore.ChannelsList(1);
            //await soundcloud.Auth();
            Task.Run(() => CheckTypings());
            Task.Run(() => SendTypings(GetChannelIdByName(settings.TestingChannel)));
            //await SendSlackMessage(spotify.GetAuthUrl(), golf1052Channel);
            Task.Run(() => CanAccessMongo());
            await Receive();
        }

        private async Task CanAccessMongo()
        {
            try
            {
                PlusPlusLog log = PlusPlusScore.GetLog();
            }
            catch (TimeoutException)
            {
                await SendSlackMessage("I can't access mongo. Please.", GetChannelIdByName(settings.TestingChannel));
            }
            await Task.Delay(TimeSpan.FromMinutes(30));
        }

        private async Task ProcessRadioArchive()
        {
            var files = Directory.GetFiles("radio");
            List<long> ids = new List<long>();
            foreach (var file in files)
            {
                using (StreamReader reader = new StreamReader(File.OpenRead(file)))
                {
                    JArray a = JArray.Load(new JsonTextReader(reader));
                    foreach (JObject o in a)
                    {
                        if (o["attachments"] != null)
                        {
                            foreach (JObject attachment in o["attachments"])
                            {
                                if (attachment["from_url"] != null)
                                {
                                    string link = (string)attachment["from_url"];
                                    if (link.Contains("https") && link.Contains("soundcloud"))
                                    {
                                        try
                                        {
                                            ids.Add(await soundcloud.ResolveSoundcloud(link));
                                        }
                                        catch (Exception)
                                        {
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            await soundcloud.Auth();
            await soundcloud.AddSongsToPlaylist(ids);
        }
        
        public async Task SendTypings(string channel)
        {
            while (true)
            {
                Random random = new Random();
                int sendTypingFor = random.Next(1, 11);
                for (int i = 0; i < sendTypingFor; i++)
                {
                    await SendTyping(channel);
                    await Task.Delay(TimeSpan.FromSeconds(3));
                }
                int waitFor = random.Next(5, 61);
                await Task.Delay(TimeSpan.FromMinutes(waitFor));
            }
        }

        public async Task CheckTypings()
        {
            while (true)
            {
                // clean stale
                foreach (var typing in typings)
                {
                    List<string> typingUsers = typing.Value.Keys.ToList();
                    foreach (string user in typingUsers)
                    {
                        if (DateTime.UtcNow - typing.Value[user] >= TimeSpan.FromSeconds(3))
                        {
                            typing.Value.Remove(user);
                        }
                    }
                }

                // send on active
                foreach (var typing in typings)
                {
                    if (typing.Value.Count >= 4)
                    {
                        await SendTyping(typing.Key);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
        
        public async Task Receive()
        {
            while (!webSocket.CloseStatus.HasValue)
            {
                MemoryStream stream = new MemoryStream();
                StreamReader reader = new StreamReader(stream);
                bool endOfData = false;
                while (!endOfData)
                {
                    byte[] buf = new byte[8192];
                    ArraySegment<byte> buffer = new ArraySegment<byte>(buf);
                    WebSocketReceiveResult response = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                    stream.Write(buffer.Array, buffer.Offset, buffer.Count);
                    endOfData = response.EndOfMessage;
                }
                stream.Seek(0, SeekOrigin.Begin);
                while (!reader.EndOfStream)
                {
                    string read = reader.ReadLine();
                    JObject o = JObject.Parse(read);
                    logger.LogInformation(o.ToString());
                    if (o["type"] != null)
                    {
                        string messageType = (string)o["type"];
                        if (messageType == "message")
                        {
                            SlackMessageEventArgs newMessage = new SlackMessageEventArgs();
                            newMessage.Message = o;
                            MessageReceived(this, newMessage);
                        }
                        else if (messageType == "user_typing")
                        {
                            string channel = (string)o["channel"];
                            string user = (string)o["user"];
                            if (!typings.ContainsKey(channel))
                            {
                                typings.Add(channel, new Dictionary<string, DateTime>());
                            }
                            if (!typings[channel].ContainsKey(user))
                            {
                                typings[channel].Add(user, DateTime.UtcNow);
                            }
                            else
                            {
                                typings[channel][user] = DateTime.UtcNow;
                            }
                        }
                        else if (messageType == "user_change")
                        {
                            if (!string.IsNullOrEmpty(settings.StatusChannel))
                            {
                                await ProcessProfileChange(o);
                            }
                        }
                    }
                }
            }
        }

        private async void Client_MessageReceived(object sender, SlackMessageEventArgs e)
        {
            string text = (string)e.Message["text"];
            string channel = (string)e.Message["channel"];
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(channel))
            {
                string subtype = (string)e.Message["subtype"];
                if (string.IsNullOrEmpty(subtype))
                {
                    return;
                }
                if (subtype == "message_changed")
                {
                    JObject newMessage = (JObject)e.Message["message"];
                    JArray attachments = (JArray)newMessage["attachments"];
                    if (attachments == null)
                    {
                        return;
                    }
                    if (channel == GetChannelIdByName(settings.TechChannel))
                    {
                        foreach (JObject attachment in attachments)
                        {
                            string url = (string)attachment["title_link"];
                            if (string.IsNullOrEmpty(url))
                            {
                                return;
                            }
                            string hackerNewsUrl = await HackerNewsApi.Search(url);
                            if (hackerNewsUrl == null)
                            {
                                return;
                            }
                            await SendSlackMessage($"Here's the Hacker News link: {hackerNewsUrl}", channel);
                        }
                    }
                    //else if (channel == "C0ANB9SMV" || channel == "G0L8C7Q6L") // radio
                    //{
                    //    await ProcessRadioAttachment(e);
                    //}
                }
                return;
            }
            if (text.ToLower() == "botbot ping")
            {
                await SendSlackMessage(GetRandomFromList(pingResponses), channel);
            }
            else if (text.ToLower() == "hi botbot")
            {
                await SendSlackMessage(GetRandomFromList(hiResponses), channel);
            }
            //else if (text.ToLower() == "hubot ping")
            //{
            //    await Task.Delay(TimeSpan.FromSeconds(30));
            //    if (!responded)
            //    {
            //        await SendSlackMessage("Hubot is dead. I killed him.", channel);
            //    }
            //    responded = false;
            //}
            //else if (text.ToLower() == "pong")
            //{
            //    if ((string)e.Message["user"] == slackUsers.First(u => u.Name == "hubot").Id)
            //    {
            //        responded = true;
            //    }
            //}
            else if (text.ToLower() == "botbot reactions")
            {
                await SendSlackMessage("Calculating reactions count, this might take me a minute...", channel);
                await CalculateReactions(channel);
            }
            else if (text.ToLower() == "botbot help")
            {
                await SendSlackMessage(GetRandomFromList(helpResponses), channel);
            }
            else if (text.ToLower() == "botbot commands")
            {
                foreach (string command in commands)
                {
                    await SendSlackMessage($"botbot {command}", channel);
                }
            }
            //else if (text.ToLower() == "botbot playlist")
            //{
            //    List<string> l = new List<string>()
            //    {
            //        "Soundcloud: https://soundcloud.com/golf1052/sets/botbot",
            //        $"Spotify: {SpotifyApi.PlaylistUrl}"
            //    };
            //    await SendSlackMessage(l, channel);
            //}
            //else if (text.ToLower() == "botbot playlist soundcloud")
            //{
            //    await SendSlackMessage("https://soundcloud.com/golf1052/sets/botbot", channel);
            //}
            //else if (text.ToLower() == "botbot playlist spotify")
            //{
            //    await SendSlackMessage(SpotifyApi.PlaylistUrl, channel);
            //}
            //else if (text.ToLower().StartsWith("botbot code"))
            //{
            //    var split = text.Split(' ');
            //    if (split.Length == 3)
            //    {
            //        await spotify.FinishAuth(split[2]);
            //        await SendSlackMessage("Finished Spotify auth", channel);
            //    }
            //}
            //else if (text.ToLower().StartsWith("botbot "))
            //{
            //    string plusPlusStatusMessage = string.Empty;
            //    plusPlusStatusMessage = PlusPlus.CheckErase(text, channel, (string)e.Message["user"]);
            //    if (!string.IsNullOrEmpty(plusPlusStatusMessage))
            //    {
            //        await SendSlackMessage(plusPlusStatusMessage, channel);
            //        return;
            //    }
            //    plusPlusStatusMessage = PlusPlus.CheckScore(text, channel, (string)e.Message["user"]);
            //    if (!string.IsNullOrEmpty(plusPlusStatusMessage))
            //    {
            //        await SendSlackMessage(plusPlusStatusMessage, channel);
            //        return;
            //    }
            //    plusPlusStatusMessage = PlusPlus.CheckTopBottom(text, channel, (string)e.Message["user"]);
            //    if (!string.IsNullOrEmpty(plusPlusStatusMessage))
            //    {
            //        await SendSlackMessage(plusPlusStatusMessage, channel);
            //        return;
            //    }
            //    await SendSlackMessage(GetRandomFromList(iDontKnow), channel);
            //}
            //string plusPlusMessage = PlusPlus.CheckPlusPlus(text, channel, (string)e.Message["user"]);
            //if (!string.IsNullOrEmpty(plusPlusMessage))
            //{
            //    await SendSlackMessage(plusPlusMessage, channel);
            //}
            //await HandleReaction(e);
        }

        async Task ProcessProfileChange(JObject responseObject)
        {
            var userId = (string)responseObject["user"]["id"];
            var status = $"{responseObject["user"]["profile"]["status_emoji"]} {responseObject["user"]["profile"]["status_text"]}";
            if (StatusNotifier.HasChanged(userId, status))
            {
                await SendSlackMessage($"{responseObject["user"]["name"]} changed their status to {status}", GetChannelIdByName(settings.StatusChannel));
            }
            StatusNotifier.SaveStatus(userId, status);
        }

        private string FindDmChannel(string userId, JArray imList)
        {
            foreach (JObject im in imList)
            {
                if ((string)im["user"] == userId)
                {
                    return (string)im["id"];
                }
            }
            return null;
        }

        async Task ProcessRadioAttachment(SlackMessageEventArgs e)
        {
            string text = (string)e.Message["text"];
            string channel = (string)e.Message["channel"];
            JObject newMessage = (JObject)e.Message["message"];
            JArray attachments = (JArray)newMessage["attachments"];

            foreach (JObject attachment in attachments)
            {
                string link = (string)attachment["from_url"];
                if (link.Contains("https") && link.Contains("soundcloud"))
                {
                    try
                    {
                        long id = await soundcloud.ResolveSoundcloud(link);
                        await soundcloud.AddSongToPlaylist(id);
                        await SendSlackMessage($"Added {(string)attachment["title"]} to Soundcloud playlist", channel);
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    //if (link.Contains("https") && link.Contains("spotify") && link.Contains("track"))
                    //{
                    //    var splitLink = link.Split('/');
                    //    await spotify.AddTrackToPlaylist($"spotify:track:{splitLink[splitLink.Length - 1]}");
                    //    await SendSlackMessage($"Added {(string)attachment["title"]} to Spotify playlist", channel);
                    //}
                    //else
                    //{
                    //    if (attachment["title"] != null)
                    //    {
                    //        string title = (string)attachment["title"];
                    //        if (title == "botbot")
                    //        {
                    //            return;
                    //        }
                    //        var track = await spotify.Search(title);
                    //        if (track != null)
                    //        {
                    //            await spotify.AddTrackToPlaylist(track.Uri);
                    //            await SendSlackMessage($"Added {track.ToString()} to Spotify playlist", channel);
                    //        }
                    //    }
                    //}
                }
            }
        }

        public async Task<List<long>> ProcessRadio()
        {
            string url = $"https://slack.com/api/channels.history?token={settings.Token}&channel=C0ANB9SMV&count=1000";
            HttpResponseMessage response = await httpClient.GetAsync(url);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
            List<long> ids = new List<long>();
            foreach (JObject o in responseObject["messages"])
            {
                if (o["attachments"] != null)
                {
                    foreach (JObject attachment in o["attachments"])
                    {
                        if (attachment["from_url"] != null)
                        {
                            string link = (string)attachment["from_url"];
                            if (link.Contains("https") && link.Contains("soundcloud"))
                            {
                                try
                                {
                                    ids.Add(await soundcloud.ResolveSoundcloud(link));
                                }
                                catch (Exception)
                                {
                                }
                            }
                        }
                    }
                }
            }
            return ids;
        }

        private async Task CalculateReactions(string channel)
        {
            List<SlackEvent> reactions = new List<SlackEvent>();
            foreach (SlackUser user in slackUsers)
            {
                reactions.AddRange(await slackCore.ReactionsList(user.Id, allItems: true));
            }
            Dictionary<string, Dictionary<DateTime, int>> topReactions = new Dictionary<string, Dictionary<DateTime, int>>();
            Dictionary<string, int> topR = new Dictionary<string, int>();
            foreach (SlackEvent reaction in reactions)
            {
                if (reaction.Type == SlackEvent.SlackEventType.Message)
                {
                    Message message = reaction as Message;
                    foreach (Reaction r in message.Reactions)
                    {
                        if (!topReactions.ContainsKey(r.Name))
                        {
                            topReactions.Add(r.Name, new Dictionary<DateTime, int>());
                        }
                        if (!topReactions[r.Name].ContainsKey(message.Timestamp))
                        {
                            topReactions[r.Name].Add(message.Timestamp, r.Count);
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, Dictionary<DateTime, int>> pair in topReactions)
            {
                foreach (KeyValuePair<DateTime, int> timePair in pair.Value)
                {
                    if (!topR.ContainsKey(pair.Key))
                    {
                        topR.Add(pair.Key, 0);
                    }
                    topR[pair.Key] += timePair.Value;
                }
            }
            List<string> finalMessage = new List<string>();
            finalMessage.Add("Top 10 reactions to messages");
            var sortedReactions = (from entry in topR orderby entry.Value descending select entry).ToList();
            for (int i = 0; i < 10; i++)
            {
                finalMessage.Add($":{sortedReactions[i].Key}: - {sortedReactions[i].Value}");
            }
            await SendSlackMessage(finalMessage, channel);
        }
        
        public T GetRandomFromList<T>(List<T> list)
        {
            Random random = new Random();
            return list[random.Next(list.Count)];
        }

        public async Task SendSlackMessage(List<string> lines, string channel)
        {
            string message = string.Empty;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i != lines.Count - 1)
                {
                    message += lines[i] += '\n';
                }
                else
                {
                    message += lines[i];
                }
            }
            await SendSlackMessage(message, channel);
        }

        public async Task SendSlackMessage(string message, string channel)
        {
            JObject o = new JObject();
            o["id"] = 1;
            o["type"] = "message";
            o["channel"] = channel;
            o["text"] = message;
            await SendMessage(o.ToString(Formatting.None));
        }
        
        public async Task SendTyping(string channel)
        {
            JObject o = new JObject();
            o["id"] = 1;
            o["type"] = "typing";
            o["channel"] = channel;
            await SendMessage(o.ToString(Formatting.None));
        }

        public async Task SendMessage(string message)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public async Task SendApiCall(string endpoint)
        {
            Uri url = new Uri(BaseUrl + endpoint);
            HttpResponseMessage response = await httpClient.GetAsync(url);
            JObject responseObject = JObject.Parse(await response.Content.ReadAsStringAsync());
        }

        private string GetChannelIdByName(string name)
        {
            return slackChannels.First(c => c.Name == name).Id;
        }
    }
}
