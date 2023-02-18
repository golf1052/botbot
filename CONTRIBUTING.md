# Setup

- [.NET 7](https://dot.net)
- [Visual Studio Code](https://code.visualstudio.com) or [Visual Studio 2022](https://visualstudio.microsoft.com)

## Dependencies

You'll need to `git clone` these repositories into the same directory where you cloned `botbot`.

- [golf1052.DiscordAPI](https://github.com/golf1052/golf1052.DiscordAPI)
- [golf1052.SlackAPI](https://github.com/golf1052/SlackAPI)
- [Reverb](https://github.com/golf1052/Reverb)

# Secrets

## settings.json

There are two secret files. One is the `settings.json` file. This file is required in order for botbot to connect to any Slack instance. Here's an example file.

```json
{
    "workspaces": [
        {
            "id": "", // Slack team id
            "token": "" // Slack integration bot token
        }
    ]
}
```

The token must be a [Slack **legacy** bot token](https://api.slack.com/authentication/token-types#bot) as botbot still uses the Real Time Messaging (RTM) API. You can create a legacy bot token using the [links found on the RTM API page](https://api.slack.com/rtm).

## Secrets.cs

The second secret file is the `Secrets.cs` file. This file needs to exist with the required strings but does not need any strings filled in in order for botbot to run. Most of the strings enable botbot's different commands or modules to run.

# Architecture

botbot is a ASP.NET Core app which allows it to receive webhooks but its core is a websocket thread that will listen and respond to Slack. botbot can connect to multiple Slacks (or even Discords) at once if there are multiple endpoints defined in `settings.json`. `Startup.cs` will call the static method `BotBotControllers.StartClients()` which will read all defined endpoints, create a `Client.cs` instance, and connect using `client.Connect()`. `Client.cs` is the heart of botbot as this is where all modules are defined.

## Modules

Every new integration into botbot should be a module. There are three types of modules currently, message modules (`IMessageModule`), event modules (`IEventModule`), and Slack attachment modules (`ISlackAttachmentModule`). When messages are sent to channels botbot is a part of botbot will receive those messages from the RTM API. A message module will be able to read that message and act on it. Event modules instead act on events that are sent from Slack. Slack attachment modules act on messages with attachments.

### Message Modules

To create a new message module inherit from `IMessageModule`. If you need to send a Slack message outside of a singular response message instead inherit from `SlackMessageModule` as that abstract class has the ability to send more Slack messages. `IMessageModule` is simple and only defines one method.

```csharp
Task<ModuleResponse> Handle(string text, string userId, string channel);
```

You define a method that takes in the message text, the user id of the user who sent the message, and the channel the message was sent in. You respond with a `ModuleResponse` which will contain the message text or message blocks you want to send back to slack. Here's a simple module

```csharp
public class PingModule : IMessageModule
{
    public Task<ModuleResponse> Handle(string text, string userId, string channel)
    {
        if (text.ToLower() == "botbot ping")
        {
            return Task.FromResult(new ModuleResponse()
            {
                Message = "pong"
            });
        }
        return Task.FromResult(new ModuleResponse());
    }
}
```

This module checks the message text to see if someone said `botbot ping`. If that message was sent, botbot will respond back with a message saying `pong`. Note that you must return a ModuleResponse back. If the message text you received wasn't what you were expecting you can just ignore it by sending an empty ModuleResponse. An empty ModuleResponse will not send anything back to Slack.

After you write your message module you must register it as a module in `Client.cs` by adding it to the `messageModules` list in `Task Connect(Uri uri)`. The new line will look something like
```csharp
messageModules.Add(new YourNewMessageModule());
```
Message modules do run in order but the order you place a message module in the list should not matter to you.

Once you've registered your message module you can run botbot, send a message that will trigger your message module, and watch botbot respond.
