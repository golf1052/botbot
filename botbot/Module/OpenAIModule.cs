using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using golf1052.SlackAPI;
using golf1052.SlackAPI.BlockKit.Blocks;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.ResponseModels.ImageResponseModel;

namespace botbot.Module
{
    public class OpenAIModule : SlackMessageModule
    {
        private const string EncodingName = "cl100k_base";

        private readonly OpenAIService openAIService;
        private readonly HttpClient httpClient;
        private Tiktoken.Encoding encoder;

        public OpenAIModule(SlackCore slackCore,
            Func<string, string, string?, Task> SendSlackMessage,
            Func<string, string, string?, Task<JObject>> SendPostMessage,
            Func<string, string, string, Task<JObject>> SendUpdateMessage) :
            base(slackCore, SendSlackMessage, SendPostMessage, SendUpdateMessage)
        {
            openAIService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = Secrets.OpenAIApiKey,
                Organization = Secrets.OpenAIOrganization
            });
            httpClient = new HttpClient();
            encoder = Tiktoken.Encoding.Get(EncodingName);
        }

        public override async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            string[] splitText = text.Split(' ');
            if (text.ToLower().StartsWith("botbot gpt-image") || text.ToLower().StartsWith("botbot gpt-img") || text.ToLower().StartsWith("botbot gptimg"))
            {
                ImageArgs args = ParseImageArgs(splitText);
                List<Task<ImageCreateResponse>> imageCreateTasks = new List<Task<ImageCreateResponse>>();
                if (args.DallE3N > 1)
                {
                    for (int i = 0; i < args.DallE3N; i++)
                    {
                        imageCreateTasks.Add(openAIService.Image.CreateImage(new ImageCreateRequest()
                        {
                            Prompt = args.Prompt,
                            Model = args.Model,
                            N = args.N,
                            Size = args.Size,
                            Quality = args.Quality,
                            Style = args.Style,
                            ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
                            User = userId
                        }));
                    }
                }
                else
                {
                    imageCreateTasks.Add(openAIService.Image.CreateImage(new ImageCreateRequest()
                    {
                        Prompt = args.Prompt,
                        Model = args.Model,
                        N = args.N,
                        Size = args.Size,
                        Quality = args.Quality,
                        Style = args.Style,
                        ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
                        User = userId
                    }));
                }

                ModuleResponse moduleResponse = new ModuleResponse();
                var results = await Task.WhenAll(imageCreateTasks);
                List<IBlock> blocks = new List<IBlock>();
                for (int i = 0; i < results.Length; i++)
                {
                    ImageCreateResponse result = results[i];
                    if (result.Successful)
                    {
                        if (result.Results.Count == 0)
                        {
                            blocks.Add(new Section($"{i + 1}: *No images from OpenAI*"));
                        }

                        foreach (var image in result.Results)
                        {
                            HttpResponseMessage imageDownloadResponse = await httpClient.GetAsync(image.Url);
                            byte[] imageBytes = await imageDownloadResponse.Content.ReadAsByteArrayAsync();
                            // From https://stackoverflow.com/a/13617375/6681022
                            char[] invalids = System.IO.Path.GetInvalidFileNameChars();
                            string filename = string.Join("_", args.Prompt.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
                            await slackCore.FilesUpload(imageBytes, $"{filename}.png", args.Prompt, new List<string>() { channel }, "png");
                        }

                        if (result.Results.Count != 0) {
                            string price = Pricing.GetImagePrice(args.N, args.Quality, args.Size);
                            blocks.Add(new Section($"Price: ${price}"));
                        }
                    }
                    else
                    {
                        if (result.Error == null)
                        {
                            blocks.Add(new Section($"{i + 1}: *Unknown error*"));
                        }
                        else
                        {
                            blocks.Add(new Section($"{i + 1}: {result.Error.Code}: {result.Error.Message}"));
                        }
                    }
                }

                moduleResponse.Blocks = blocks;
                return moduleResponse;
            }
            else if (text.ToLower().StartsWith("botbot gpt") || text.ToLower().StartsWith("botbot eli5"))
            {
                string prefix = GetPrefix(splitText[1]);
                TextArgs args = ParseTextArgs(splitText);

                List<ChatMessage> messages = new List<ChatMessage>();
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    messages.Add(ChatMessage.FromSystem(prefix));
                }
                messages.Add(ChatMessage.FromUser(args.Prompt));

                var completionResult = openAIService.ChatCompletion.CreateCompletionAsStream(new ChatCompletionCreateRequest()
                {
                    Messages = messages,
                    Model = args.Model,
                    Temperature = args.Temp,
                    MaxTokens = args.MaxTokens - GetTokenCount(messages)
                });

                bool streamStarted = false;
                JObject? lastSentMessage = null;

                Pipe pipe = new Pipe();

                async Task<ModuleResponse> FillPipe(PipeWriter writer)
                {
                    try
                    {
                        await foreach (var completion in completionResult)
                        {
                            if (completion.Successful)
                            {
                                var chatChoiceResponse = completion.Choices.FirstOrDefault();
                                if (chatChoiceResponse == null)
                                {
                                    return new ModuleResponse()
                                    {
                                        Message = "*No responses from OpenAI*"
                                    };
                                }

                                string? content = chatChoiceResponse.Delta.Content;
                                if (content != null)
                                {
                                    byte[] bytes = Encoding.UTF8.GetBytes(content);
                                    await writer.WriteAsync(bytes);
                                    // Loop is tight enough that we have to manually insert a delay so the reader can run.
                                    await Task.Delay(1);
                                }

                                if (!string.IsNullOrWhiteSpace(chatChoiceResponse.FinishReason))
                                {
                                    if (chatChoiceResponse.FinishReason == "stop")
                                    {
                                        return new ModuleResponse();
                                    }
                                    else if (chatChoiceResponse.FinishReason == "content_filter")
                                    {
                                        return new ModuleResponse()
                                        {
                                            Message = "**NOTE: OpenAI omitted content due to a flag from OpenAI's content filters**"
                                        };
                                    }
                                    else
                                    {
                                        return new ModuleResponse()
                                        {
                                            Message = $"Unknown finish reason: {chatChoiceResponse.FinishReason}"
                                        };
                                    }
                                }
                            }
                            else
                            {
                                if (completion.Error == null)
                                {
                                    return new ModuleResponse()
                                    {
                                        Message = "*Unknown error*"
                                    };
                                }
                                else
                                {
                                    return new ModuleResponse()
                                    {
                                        Message = $"{completion.Error.Code}: {completion.Error.Message}"
                                    };
                                }
                            }
                        }
                        return new ModuleResponse();
                    }
                    finally
                    {
                        await writer.CompleteAsync();
                    }
                }

                async Task<ModuleResponse> ReadPipe(PipeReader reader)
                {
                    while (true)
                    {
                        ReadResult readResult = await reader.ReadAsync();
                        var buffer = readResult.Buffer;
                        string content = Encoding.UTF8.GetString(buffer);
                        if (!streamStarted && string.IsNullOrWhiteSpace(content))
                        {
                            continue;
                        }
                        
                        if (!streamStarted && !string.IsNullOrWhiteSpace(content))
                        {
                            lastSentMessage = await SendPostMessage(content, channel);
                            streamStarted = true;
                        }
                        else
                        {
                            string timestamp = (string)lastSentMessage!["ts"]!;
                            string message = content;
                            lastSentMessage = await SendUpdateMessage(message, channel, timestamp);
                        }

                        reader.AdvanceTo(buffer.Start, buffer.End);
                        if (readResult.IsCompleted)
                        {
                            break;
                        }
                    }

                    await reader.CompleteAsync();
                    return new ModuleResponse();
                }

                Task<ModuleResponse> writing = FillPipe(pipe.Writer);
                Task<ModuleResponse> reading = ReadPipe(pipe.Reader);

                ModuleResponse[] results = await Task.WhenAll(writing, reading);
                return results[0];
            }
            return new ModuleResponse();
        }

        private TextArgs ParseTextArgs(string[] splitText)
        {
            TextArgs args = new TextArgs(string.Empty, 1, "gpt-4");
            int numberToSkip = 2;
            bool setMaxTokens = false;
            foreach (var item in splitText)
            {
                if (item.StartsWith($"{nameof(TextArgs.Temp)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.Temp = float.Parse(item.Split('=')[1]);
                }
                else if (item.StartsWith($"{nameof(TextArgs.Model)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.Model = item.Split('=')[1];
                }
                else if (item.StartsWith($"{nameof(TextArgs.MaxTokens)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.MaxTokens = int.Parse(item.Split('=')[1]);
                    setMaxTokens = true;
                }
            }
            args.Prompt = string.Join(" ", splitText.Skip(numberToSkip).Take(splitText.Length - 1));

            if (!setMaxTokens)
            {
                if (args.Model.Contains("16k"))
                {
                    args.MaxTokens = 16384;
                }
                else if (args.Model.Contains("32k"))
                {
                    args.MaxTokens = 32768;
                }
                else if (args.Model == "gpt-4")
                {
                    args.MaxTokens = 8191;
                }
                else if (args.Model == "gpt-3.5-turbo")
                {
                    args.MaxTokens = 4096;
                }
            }

            return args;
        }

        private ImageArgs ParseImageArgs(string[] splitText)
        {
            ImageArgs args = new ImageArgs(string.Empty, "dall-e-3", 1);
            int numberToSkip = 2;
            foreach (var item in splitText)
            {
                if (item.StartsWith($"{nameof(ImageArgs.Model)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.Model = item.Split('=')[1];
                }
                else if (item.StartsWith($"{nameof(ImageArgs.N)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.N = int.Parse(item.Split('=')[1]);
                }
                else if (item.StartsWith($"{nameof(ImageArgs.Size)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.Size = item.Split('=')[1];
                }
                else if (item.StartsWith($"{nameof(ImageArgs.Quality)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.Quality = item.Split('=')[1];
                }
                else if (item.StartsWith($"{nameof(ImageArgs.Style)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.Style = item.Split('=')[1];
                }
            }
            args.Prompt = string.Join(" ", splitText.Skip(numberToSkip).Take(splitText.Length - 1));

            if (args.Model == "dall-e-3" && args.N > 1)
            {
                args.DallE3N = args.N;
                args.N = 1;
            }

            return args;
        }

        private class TextArgs : Args
        {
            public float Temp { get; set; }
            public int MaxTokens { get; set; }

            public TextArgs(string prompt = "", float temp = 1, string model = "gpt-4", int maxTokens = 8191)
            {
                Prompt = prompt;
                Temp = temp;
                Model = model;
                MaxTokens = maxTokens;
            }
        }

        private class ImageArgs : Args
        {
            public int N { get; set; }
            public string Size { get; set; }
            public string Quality { get; set; }
            public string Style { get; set; }
            public int DallE3N { get; set; }

            public ImageArgs(string prompt = "", string model = "dall-e-3", int n = 1, string size = "1024x1024", string quality = "standard", string style = "natural")
            {
                Prompt = prompt;
                Model = model;
                N = n;
                Size = size;
                Quality = quality;
                Style = style;
            }
        }

        private abstract class Args
        {
            public string Prompt { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
        }

        private static class Pricing
        {
            public static readonly (double input, double output) gpt35 = (0.0000015, 0.000002);
            public static readonly (double input, double output) gpt35_16k = (0.000003, 0.000004);
            public static readonly (double input, double output) gpt4 = (0.00003, 0.00006);
            public static readonly (double input, double output) gpt4_32k = (0.00006, 0.00012);

            public static string GetPrice(string model, UsageResponse usage)
            {
                double price;
                if (model.StartsWith("gpt-4-32k"))
                {
                    price = usage.PromptTokens * gpt4_32k.input + usage.CompletionTokens!.Value * gpt4_32k.output;
                }
                else if (model.StartsWith("gpt-4"))
                {
                    price = usage.PromptTokens * gpt4.input + usage.CompletionTokens!.Value * gpt4.output;
                }
                else if (model.StartsWith("gpt-3.5-turbo-16k"))
                {
                    price = usage.PromptTokens * gpt35_16k.input + usage.CompletionTokens!.Value * gpt35_16k.output;
                }
                else
                {
                    price = usage.PromptTokens * gpt35.input + usage.CompletionTokens!.Value * gpt35.output;
                }

                return price.ToString();
            }

            public static string GetImagePrice(int n, string quality, string size)
            {
                double basePrice = 0.0;

                if (quality.ToLower().StartsWith("standard")) {
                    if (size.ToLower().StartsWith("1024x1024")) {
                        basePrice = 0.04;
                    } else {
                        basePrice = 0.08;
                    }
                } else if (quality.ToLower().StartsWith("hd")) {
                    if (size.ToLower().StartsWith("1024x1024")) {
                        basePrice = 0.08;
                    } else {
                        basePrice = 0.12;
                    }
                }

                return (basePrice * (double) n).ToString();
            }
        }

        private int GetTokenCount(List<ChatMessage> messages)
        {
            // Using external tokenizer currently because Betalgo.OpenAI tokenizer (TokenizerGpt3) uses p50k_base
            // which is incorrect for gpt-3.5 and higher.
            // Using Tiktoken over TiktokenSharp or SharpToken because it is the fastest tokenizer as of 2023-06-28
            // BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22621.1926/22H2/2022Update/SunValley2)
            // AMD Ryzen 9 5900X, 1 CPU, 24 logical and 12 physical cores
            //.NET SDK = 7.0.304
            //  [Host]     : .NET 7.0.7(7.0.723.27404), X64 RyuJIT AVX2
            //  DefaultJob : .NET 7.0.7(7.0.723.27404), X64 RyuJIT AVX2
            //| Method | Mean | Error | StdDev |
            //| -------------------- | ------------:| ---------:| ---------:|
            //| TikTokenSharpEncode (v1.0.6)  | 247.72 us   | 0.997 us | 0.933 us |
            //| SharpTokenEncode (v1.0.30)    | 1,390.72 us | 6.996 us | 6.544 us |
            //| TikTokenEncode (v1.1.0)       | 92.29 us    | 0.289 us | 0.271 us |

            // Encode our messages so we know how many remaining tokens we have
            // This is from https://github.com/openai/openai-cookbook/blob/main/examples/How_to_count_tokens_with_tiktoken.ipynb
            // because OpenAI adds stuff which gets counted in the final token count

            const int TokensPerMessage = 4;
            const int TokensPerName = 1;
            const int ReplyTokens = 3;

            int numTokens = 0;
            foreach (var message in messages)
            {
                numTokens += TokensPerMessage;
                if (!string.IsNullOrWhiteSpace(message.Name))
                {
                    numTokens += TokensPerName;
                }
                numTokens += encoder.CountTokens(message.Content);
            }
            numTokens += ReplyTokens;
            return numTokens;
        }

        private string GetPrefix(string command)
        {
            if (command == "eli5")
            {
                return "Explain like I'm 5 years old: ";
            }
            else if (command == "gpt_venmo")
            {
                return VenmoHelp.VenmoHelpMessage + "\nSurround the command in ``";
            }

            return string.Empty;
        }
    }
}
