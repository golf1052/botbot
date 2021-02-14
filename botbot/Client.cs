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
using botbot.Command;
using botbot.Module;
using botbot.Module.SlackAttachments;
using botbot.Status;
using golf1052.SlackAPI;
using golf1052.SlackAPI.Events;
using golf1052.SlackAPI.Objects;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Reverb;

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

        private List<IMessageModule> messageModules;
        private List<IEventModule> eventModules;
        private List<ISlackAttachmentModule> attachmentModules;
        private HubotModule? hubotModule;
        private StatusNotifier statusNotifier;

        HttpClient httpClient;

        private JsonSerializerSettings jsonSerializerSettings;

        public Client(Settings settings, ILogger<Client> logger)
        {
            this.settings = settings;
            this.logger = logger;
            httpClient = new HttpClient();
            webSocket = new ClientWebSocket();
            MessageReceived += Client_MessageReceived!;
            EventReceived += Client_EventReceived!;
            slackCore = new SlackCore(settings.Token);
            slackUsers = new List<SlackUser>();
            slackChannels = new List<SlackChannel>();
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
            attachmentModules = new List<ISlackAttachmentModule>();
            statusNotifier = new StatusNotifier(settings.Id!);
            jsonSerializerSettings = new JsonSerializerSettings()
            {
                ContractResolver = new DefaultContractResolver()
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };
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
            slackChannels = await slackCore.ConversationsList(true, "public_channel,private_channel");
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
                await hubotModule.Init(settings.Token!);
                eventModules.Add(hubotModule);
            }

            attachmentModules.Add(new GooglePlayMusicModule());
            attachmentModules.Add(new HackerNewsModule(slackChannels, settings));
            attachmentModules.Add(new SpotifyDirectLinkModule());

            foreach (var eventModule in eventModules)
            {
                RecurringModule? recurring = eventModule.RegisterRecurring();
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
                _ = Task.Run(() => SendTypings(GetChannelIdByName(settings.TestingChannel)));
            }
            //await SendSlackMessage(spotify.GetAuthUrl(), golf1052Channel);
            _ = Task.Run(() => CanAccessMongo());
            // Task.Run(() => CheckNewReleases());
            // Task.Run(() => CheckNewReleasesGPM());
            string botbotId = GetUserIdByName("botbot");
            while (true)
            {
                try
                {
                    await Receive();
                }
                catch (WebSocketException)
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
                if (settings.TestingChannel != null)
                {
                    await SendSlackMessage("I can't access mongo. Please.", GetChannelIdByName(settings.TestingChannel));
                }
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
                            foreach (JObject attachment in o["attachments"]!)
                            {
                                if (attachment["from_url"] != null)
                                {
                                    string link = (string)attachment["from_url"]!;
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
                            string messageType = (string)o["type"]!;
                            if (messageType == "message")
                            {
                                if (hubotModule != null)
                                {
                                    _ = hubotModule.Handle(messageType, o);
                                }
                                // why is this an event? events are synchronous so this could just be a function call
                                SlackMessageEventArgs newMessage = new SlackMessageEventArgs(o);
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
                                SlackRTMEventArgs rtmEvent = new SlackRTMEventArgs(messageType, o);
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
                        logger.LogWarning($"task failed {task.Exception!.Message}");
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
            SlackMessage slackMessage = JsonConvert.DeserializeObject<SlackMessage>(e.Message.ToString(), jsonSerializerSettings)!;
            List<Task<ModuleResponse>> moduleTasks = new List<Task<ModuleResponse>>();
            if (!string.IsNullOrEmpty(slackMessage.Text))
            {
                // "normal" messages should have text, messages with subtypes will not have text (probably)
                foreach (var module in messageModules)
                {
                    moduleTasks.Add(module.Handle(slackMessage.Text, slackMessage.User, slackMessage.Channel));
                }
            }
            if (string.IsNullOrEmpty(slackMessage.Text) || string.IsNullOrEmpty(slackMessage.Channel))
            {
                if (slackMessage.Subtype == "message_changed")
                {
                    // threadTimestamp will be null if the message is not in a thread
                    // to create threads, the bot needs to reply using timestamp
                    foreach (Attachment attachment in slackMessage.Message.Attachments)
                    {
                        // testing to see if originalUrl and fromUrl will ever be different
                        if (attachment.OriginalUrl != attachment.FromUrl)
                        {
                            await SendMeMessage($"original_url and from_url are different. original_url: {attachment.OriginalUrl} ! from_url: {attachment.FromUrl}");
                        }

                        foreach (var module in attachmentModules)
                        {
                            moduleTasks.Add(module.Handle(slackMessage, attachment));
                        }
                    }
                    //else if (channel == "C0ANB9SMV" || channel == "G0L8C7Q6L") // radio
                    //{
                    //    await ProcessRadioAttachment(e);
                    //}
                }
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
            if (!string.IsNullOrEmpty(slackMessage.Text) && slackMessage.Text.ToLower() == "botbot help")
            {
                await SendSlackMessage(GetRandomFromList(helpResponses), slackMessage.Channel);
            }
            else if (!string.IsNullOrEmpty(slackMessage.Text) && slackMessage.Text.ToLower() == "botbot commands")
            {
                foreach (string command in commands)
                {
                    await SendSlackMessage($"botbot {command}", slackMessage.Channel);
                }
            }
            else if (!string.IsNullOrEmpty(slackMessage.Text) && slackMessage.Text.ToLower().StartsWith("botbot spotify"))
            {
                if (slackMessage.Channel.StartsWith('D'))
                {
                    await spotifyGetYearsAlbums.Receive(slackMessage.Text, slackMessage.Channel, slackMessage.User);
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
            else if (!string.IsNullOrEmpty(slackMessage.Text) && slackMessage.Text.ToLower().StartsWith("botbot "))
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
            else if (!string.IsNullOrEmpty(slackMessage.Text) && slackMessage.Channel.StartsWith('D'))
            {
                await spotifyGetYearsAlbums.Receive(slackMessage.Text, slackMessage.Channel, slackMessage.User);
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
                for (int i = 0; i < moduleTasks.Count; i++)
                {
                    var task = moduleTasks[i];
                    if (task.IsCompletedSuccessfully)
                    {
                        ModuleResponse result = await task;
                        if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                        {
                            await SendSlackMessage(result.Message, slackMessage.Channel, result.Timestamp);
                        }
                        moduleTasks.RemoveAt(i);
                        i--;
                        continue;
                    }
                    else if (task.IsFaulted)
                    {
                        // do something
                        logger.LogWarning($"task failed {task.Exception!.Message}");
                        moduleTasks.RemoveAt(i);
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
            var userId = (string)responseObject["user"]!["id"]!;
            string statusEmoji = (string)responseObject["user"]!["profile"]!["status_emoji"]!;
            var status = $"{statusEmoji} {responseObject["user"]!["profile"]!["status_text"]}";
            // filter out Slack automatic "in a call statuses"
            if (settings.StatusChannel != null && statusNotifier.HasChanged(userId, status) &&
                !string.IsNullOrWhiteSpace(status) && statusEmoji != "📞" && statusEmoji != ":slack_call:")
            {
                await SendSlackMessage($"{responseObject["user"]!["name"]} changed their status to {status}", GetChannelIdByName(settings.StatusChannel));
            }
            statusNotifier.SaveStatus(userId, status);
        }

        private string? FindDmChannel(string userId, JArray imList)
        {
            foreach (JObject im in imList)
            {
                if ((string)im["user"]! == userId)
                {
                    return (string)im["id"]!;
                }
            }
            return null;
        }

        async Task ProcessRadioAttachment(SlackMessageEventArgs e)
        {
            string text = (string)e.Message["text"]!;
            string channel = (string)e.Message["channel"]!;
            JObject newMessage = (JObject)e.Message["message"]!;
            JArray attachments = (JArray)newMessage["attachments"]!;

            foreach (JObject attachment in attachments)
            {
                string link = (string)attachment["from_url"]!;
                if (link.Contains("https") && link.Contains("soundcloud"))
                {
                    try
                    {
                        long id = await soundcloud.ResolveSoundcloud(link);
                        await soundcloud.AddSongToPlaylist(id);
                        await SendSlackMessage($"Added {(string)attachment["title"]!} to Soundcloud playlist", channel);
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
            foreach (JObject o in responseObject["messages"]!)
            {
                if (o["attachments"] != null)
                {
                    foreach (JObject attachment in o["attachments"]!)
                    {
                        if (attachment["from_url"] != null)
                        {
                            string link = (string)attachment["from_url"]!;
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

        /// <summary>
        /// Sends a message to me (Sanders)
        /// </summary>
        /// <param name="message">Message text</param>
        public async Task SendMeMessage(string message)
        {
            if (!string.IsNullOrEmpty(settings.MyUsername))
            {
                string imChannel = await slackCore.ConversationsOpen(null, false, new List<string>() { GetUserIdByName(settings.MyUsername) });
                await SendSlackMessage(message, imChannel);
            }
        }

        public async Task SendSlackMessage(string message, string channel)
        {
            await SendSlackMessage(message, channel, null);
        }

        public async Task SendSlackMessage(string message, string channel, string? threadTimestamp)
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
            return slackChannels.FirstOrDefault(c => c.Name == name)?.Id!;
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
