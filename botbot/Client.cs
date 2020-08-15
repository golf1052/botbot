using botbot.Command;
using botbot.Module;
using botbot.Status;
using golf1052.SlackAPI;
using golf1052.SlackAPI.Objects;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reverb;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

        static Client()
        {
            responded = false;
            Mongo = new MongoClient(Secrets.MongoConnectionString);
            PlusPlusDatabase = Mongo.GetDatabase("plusplus");
            ThingCollection = PlusPlusDatabase.GetCollection<PlusPlusThing>("things");
            PlusPlusLogCollection = PlusPlusDatabase.GetCollection<PlusPlusLog>("log");
        }

        private readonly Settings settings;
        private readonly ILogger logger;
        private SlackCore slackCore;
        private List<SlackUser> slackUsers;
        private List<SlackChannel> slackChannels;
        private ClientWebSocket webSocket;
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
        event EventHandler<SlackRTMEventArgs> EventReceived;

        // <channel, <user, timestamp>>
        Dictionary<string, Dictionary<string, DateTime>> typings;

        Dictionary<string, int> reactionMisses;

        SoundcloudApi soundcloud;
        SpotifyApi spotify;
        SpotifyGetYearsAlbums spotifyGetYearsAlbums;
        SpotifyClient spotifyClient;

        NewReleasesCommand newReleasesCommand;
        NewReleasesGPMCommand newReleasesGPMCommand;

        List<IMessageModule> messageModules;
        List<IEventModule> eventModules;
        HubotModule hubotModule;
        StatusNotifier statusNotifier;

        HttpClient httpClient;

        public Client(Settings settings, ILogger<Client> logger)
        {
            this.settings = settings;
            httpClient = new HttpClient();
            webSocket = new ClientWebSocket();
            MessageReceived += Client_MessageReceived;
            EventReceived += Client_EventReceived;
            slackCore = new SlackCore(settings.Token);
            slackUsers = new List<SlackUser>();
            typings = new Dictionary<string, Dictionary<string, DateTime>>();
            reactionMisses = new Dictionary<string, int>();
            soundcloud = new SoundcloudApi();
            spotify = new SpotifyApi();
            spotifyGetYearsAlbums = new SpotifyGetYearsAlbums(SendSlackMessage);
            spotifyClient = new SpotifyClient(Secrets.SpotifyClientId, Secrets.SpotifyClientSecret, Secrets.SpotifyRedirectUrl);
            newReleasesCommand = new NewReleasesCommand();
            newReleasesGPMCommand = new NewReleasesGPMCommand();
            messageModules = new List<IMessageModule>();
            eventModules = new List<IEventModule>();
            statusNotifier = new StatusNotifier(settings.Id);
            this.logger = logger;
        }

        public async Task<JsonDocument> GetConnectionInfo()
        {
            Uri uri = new Uri($"{Client.BaseUrl}rtm.connect?token={settings.Token}");
            HttpResponseMessage response = await httpClient.GetAsync(uri);
            return JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        }

        public async Task<Uri> GetConnectionUrl()
        {
            JsonDocument connectionInfo = await GetConnectionInfo();
            return new Uri(connectionInfo.RootElement.GetProperty("url").GetString());
        }

        public async Task Connect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
            slackUsers = await slackCore.UsersList();
            slackChannels = await slackCore.ChannelsList(1);
            await spotifyClient.RequestAccessToken();

            messageModules.Add(new PingModule());
            messageModules.Add(new HiModule());
            messageModules.Add(new StockModule());
            messageModules.Add(new ReactionsModule(slackCore, SendSlackMessage));
            // messageModules.Add(new NewReleasesModule());
            messageModules.Add(new PresidentApprovalModule());
            messageModules.Add(new ElectionModelModule());
            messageModules.Add(new VersionModule());

            eventModules.Add(new TypingModule(slackCore, SendMessage));
            if (settings.HubotEnabled)
            {
                hubotModule = new HubotModule(slackCore, SendMessage);
                await hubotModule.Init(settings.Token);
                eventModules.Add(hubotModule);
            }

            foreach (var eventModule in eventModules)
            {
                RecurringModule recurring = eventModule.RegisterRecurring();
                if (recurring != null)
                {
                    Task t = Task.Run(async () =>
                    {
                        while (true)
                        {
                            try
                            {
                                await recurring.Func.Invoke();
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error while running recurring module");
                            }
                            await Task.Delay(recurring.Interval);
                        }
                    });
                }
            }

            //await soundcloud.Auth();
            if (!string.IsNullOrEmpty(settings.TestingChannel))
            {
                Task.Run(() => SendTypings(GetChannelIdByName(settings.TestingChannel)));
            }
            //await SendSlackMessage(spotify.GetAuthUrl(), golf1052Channel);
            Task.Run(() => CanAccessMongo());
            // Task.Run(() => CheckNewReleases());
            // Task.Run(() => CheckNewReleasesGPM());
            string botbotId = GetUserIdByName("botbot");
            while (true)
            {
                try
                {
                    await Receive();
                }
                catch (WebSocketException ex)
                {
                    if (webSocket.State == WebSocketState.Aborted)
                    {
                        logger.LogWarning("Websocket was aborted, reconnecting");
                        webSocket = new ClientWebSocket();
                        await Reconnect(await GetConnectionUrl());
                    }
                    else
                    {
                        logger.LogError("Websocket in unknown state, restarting websocket");
                        webSocket = new ClientWebSocket();
                        await Reconnect(await GetConnectionUrl());
                    }
                }
            }
        }

        private async Task Reconnect(Uri uri)
        {
            await webSocket.ConnectAsync(uri, CancellationToken.None);
        }

        private async Task CheckNewReleases()
        {
            while (true)
            {
                await newReleasesCommand.CheckNewReleasesForUsers(await slackCore.UsersConversations(types: "im"), SendSlackMessage);
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        private async Task CheckNewReleasesGPM()
        {
            while (true)
            {
                await newReleasesGPMCommand.CheckNewReleasesForUsers(await slackCore.UsersConversations(types: "im"), SendSlackMessage);
                await Task.Delay(TimeSpan.FromHours(1));
            }
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
            while (!string.IsNullOrEmpty(channel))
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

        public async Task Receive()
        {
            while (!webSocket.CloseStatus.HasValue)
            {
                Pipe pipe = new Pipe();
                while (true)
                {
                    Memory<byte> memory = pipe.Writer.GetMemory(128);
                    ValueWebSocketReceiveResult response;
                    try
                    {
                        response = await webSocket.ReceiveAsync(memory, CancellationToken.None);
                        pipe.Writer.Advance(response.Count);
                    }
                    catch (WebSocketException ex)
                    {
                        logger.LogError(ex, "Exception while receiving from websocket");
                        throw;
                    }
                    if (response.EndOfMessage)
                    {
                        await pipe.Writer.FlushAsync();
                        break;
                    }
                }
                pipe.Writer.Complete();
                while (true)
                {
                    ReadResult result = await pipe.Reader.ReadAsync();
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    while (true)
                    {
                        if (buffer.IsEmpty && result.IsCompleted)
                        {
                            break;
                        }
                        JObject o = JObject.Parse(GetString(buffer));
                        buffer = buffer.Slice(buffer.End);
                        logger.LogInformation(o.ToString());
                        if (o["type"] != null)
                        {
                            string messageType = (string)o["type"];
                            if (messageType == "message")
                            {
                                if (hubotModule != null)
                                {
                                    _ = hubotModule.Handle(messageType, o);
                                }
                                SlackMessageEventArgs newMessage = new SlackMessageEventArgs();
                                newMessage.Message = o;
                                MessageReceived(this, newMessage);
                            }
                            else if (messageType == "user_change")
                            {
                                if (!string.IsNullOrEmpty(settings.StatusChannel))
                                {
                                    await ProcessProfileChange(o);
                                }
                            }
                            else
                            {
                                SlackRTMEventArgs rtmEvent = new SlackRTMEventArgs();
                                rtmEvent.Type = messageType;
                                rtmEvent.Event = o;
                                EventReceived(this, rtmEvent);
                            }
                        }

                        pipe.Reader.AdvanceTo(buffer.Start, buffer.End);
                    }

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                pipe.Reader.Complete();
            }
        }

        private string GetString(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                return Encoding.UTF8.GetString(buffer.First.Span);
            }
            else
            {
                // Taken from https://gist.github.com/terrajobst/6e1bea5bec4591edd7c5fe5416ce7f56#file-sample6-cs
                // Explaination: https://msdn.microsoft.com/en-us/magazine/mt814808.aspx?f=255&MSPPError=-2147217396
                return string.Create((int)buffer.Length, buffer, (span, sequence) =>
                {
                    foreach (var segment in sequence)
                    {
                        Encoding.UTF8.GetChars(segment.Span, span);
                        span = span.Slice(segment.Length);
                    }
                });
            }
        }
        private async void Client_EventReceived(object sender, SlackRTMEventArgs e)
        {
            List<Task> eventModuleTasks = new List<Task>();
            foreach (var module in eventModules)
            {
                eventModuleTasks.Add(module.Handle(e.Type, e.Event));
            }

            bool allTasksDone = false;
            while (!allTasksDone)
            {
                allTasksDone = true;
                for (int i = 0; i < eventModuleTasks.Count; i++)
                {
                    var task = eventModuleTasks[i];
                    if (task.IsCompletedSuccessfully)
                    {
                        await task;
                        eventModuleTasks.RemoveAt(i);
                        i--;
                        continue;
                    }
                    else if (task.IsFaulted)
                    {
                        logger.LogWarning($"task failed {task.Exception.Message}");
                        eventModuleTasks.RemoveAt(i);
                        i--;
                        continue;
                    }
                    else
                    {
                        allTasksDone = false;
                    }
                }
            }
        }

        private async void Client_MessageReceived(object sender, SlackMessageEventArgs e)
        {
            string text = (string)e.Message["text"];
            string channel = (string)e.Message["channel"];
            string userId = (string)e.Message["user"];
            List<Task<string>> messageModuleTasks = new List<Task<string>>();
            foreach (var module in messageModules)
            {
                messageModuleTasks.Add(module.Handle(text, userId, channel));
            }
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

                    string threadTimestamp = null;
                    if (newMessage["thread_ts"] != null)
                    {
                        threadTimestamp = (string)newMessage["thread_ts"];
                    }

                    foreach (JObject attachment in attachments)
                    {
                        await ProcessAttachment(newMessage, attachment, channel, threadTimestamp);
                    }
                    //else if (channel == "C0ANB9SMV" || channel == "G0L8C7Q6L") // radio
                    //{
                    //    await ProcessRadioAttachment(e);
                    //}
                }
                return;
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
            if (text.ToLower() == "botbot help")
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
            else if (text.ToLower().StartsWith("botbot spotify"))
            {
                if (channel.StartsWith('D'))
                {
                    await spotifyGetYearsAlbums.Receive(text, channel, userId);
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
            else if (text.ToLower().StartsWith("botbot "))
            {
                //string plusPlusStatusMessage = string.Empty;
                //plusPlusStatusMessage = PlusPlus.CheckErase(text, channel, (string)e.Message["user"]);
                //if (!string.IsNullOrEmpty(plusPlusStatusMessage))
                //{
                //    await SendSlackMessage(plusPlusStatusMessage, channel);
                //    return;
                //}
                //plusPlusStatusMessage = PlusPlus.CheckScore(text, channel, (string)e.Message["user"]);
                //if (!string.IsNullOrEmpty(plusPlusStatusMessage))
                //{
                //    await SendSlackMessage(plusPlusStatusMessage, channel);
                //    return;
                //}
                //plusPlusStatusMessage = PlusPlus.CheckTopBottom(text, channel, (string)e.Message["user"]);
                //if (!string.IsNullOrEmpty(plusPlusStatusMessage))
                //{
                //    await SendSlackMessage(plusPlusStatusMessage, channel);
                //    return;
                //}
                //await SendSlackMessage(GetRandomFromList(iDontKnow), channel);
            }
            else if (channel.StartsWith('D'))
            {
                await spotifyGetYearsAlbums.Receive(text, channel, userId);
            }
            //if (GetUserIdByName("botbot") != userId &&
            //    GetChannelIdByName(settings.TechChannel) == channel)
            //{
            //    await BotBotController.Crosspost(settings.CrosspostTeamId,
            //        settings.CrosspostChannelId,
            //        GetUserRealNameById(userId),
            //        text);
            //}
            //string plusPlusMessage = PlusPlus.CheckPlusPlus(text, channel, (string)e.Message["user"]);
            //if (!string.IsNullOrEmpty(plusPlusMessage))
            //{
            //    await SendSlackMessage(plusPlusMessage, channel);
            //}
            //await HandleReaction(e);
            bool allTasksDone = false;
            while (!allTasksDone)
            {
                allTasksDone = true;
                for (int i = 0; i < messageModuleTasks.Count; i++)
                {
                    var task = messageModuleTasks[i];
                    if (task.IsCompletedSuccessfully)
                    {
                        string result = await task;
                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            await SendSlackMessage(result, channel);
                        }
                        messageModuleTasks.RemoveAt(i);
                        i--;
                        continue;
                    }
                    else if (task.IsFaulted)
                    {
                        // do something
                        logger.LogWarning($"task failed {task.Exception.Message}");
                        messageModuleTasks.RemoveAt(i);
                        i--;
                        continue;
                    }
                    else
                    {
                        allTasksDone = false;
                    }
                }
            }
        }

        async Task ProcessProfileChange(JObject responseObject)
        {
            var userId = (string)responseObject["user"]["id"];
            var status = $"{responseObject["user"]["profile"]["status_emoji"]} {responseObject["user"]["profile"]["status_text"]}";
            if (statusNotifier.HasChanged(userId, status) && !string.IsNullOrWhiteSpace(status))
            {
                await SendSlackMessage($"{responseObject["user"]["name"]} changed their status to {status}", GetChannelIdByName(settings.StatusChannel));
            }
            statusNotifier.SaveStatus(userId, status);
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

        private async Task ProcessAttachment(JObject newMessage, JObject attachment, string channel, string threadTimestamp)
        {
            await ProcessGooglePlayMusicAttachment(newMessage, attachment, channel, threadTimestamp);
            await ProcessHackerNewsAttachment(newMessage, attachment, channel, threadTimestamp);
        }

        private async Task ProcessGooglePlayMusicAttachment(JObject newMessage, JObject attachment, string channel, string threadTimestamp)
        {
            string serviceName = (string)attachment["service_name"];
            string originalUrl = (string)attachment["original_url"];
            if (serviceName == "play.google.com" && originalUrl.Contains("/music/"))
            {
                string title = (string)attachment["title"];
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
                    await SendSlackMessage($"Spotify Link: {spotifyLink}", channel, threadTimestamp);
                }
            }
        }

        private async Task ProcessHackerNewsAttachment(JObject newMessage, JObject attachment, string channel, string threadTimestamp)
        {
            if (channel == GetChannelIdByName(settings.TechChannel))
            {
                string url = (string)attachment["title_link"];
                if (string.IsNullOrEmpty(url))
                {
                    return;
                }
                SearchItem hackerNewsItem = await HackerNewsApi.Search(url);
                if (hackerNewsItem == null)
                {
                    return;
                }
                await SendSlackMessage($"From Hacker News\nTitle: {hackerNewsItem.Title}\nPoints: {hackerNewsItem.Points}\nComments: {hackerNewsItem.NumComments}\nLink: {hackerNewsItem.GetUrl()}", channel, threadTimestamp);
            }
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
            await SendSlackMessage(message, channel, null);
        }

        public async Task SendSlackMessage(string message, string channel, string threadTimestamp)
        {
            JObject o = new JObject();
            o["id"] = 1;
            o["type"] = "message";
            o["channel"] = channel;
            o["text"] = message;
            if (!string.IsNullOrEmpty(threadTimestamp))
            {
                o["thread_ts"] = threadTimestamp;
            }
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
            return slackChannels.FirstOrDefault(c => c.Name == name)?.Id;
        }

        private string GetUserIdByName(string name)
        {
            return slackUsers.First(u => u.Name == name).Id;
        }

        private string GetUserNameById(string id)
        {
            return slackUsers.First(u => u.Id == id).Name;
        }

        private string GetUserRealNameById(string id)
        {
            return slackUsers.First(u => u.Id == id).RealName;
        }
    }
}
