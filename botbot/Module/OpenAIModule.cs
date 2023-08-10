using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;

namespace botbot.Module
{
    public class OpenAIModule : IMessageModule
    {
        private const string EncodingName = "cl100k_base";

        private readonly OpenAIService openAIService;
        private Tiktoken.Encoding encoder;

        public OpenAIModule()
        {
            openAIService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = Secrets.OpenAIApiKey,
                Organization = Secrets.OpenAIOrganization
            });
            encoder = Tiktoken.Encoding.Get(EncodingName);
        }

        public async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot gpt") || text.ToLower().StartsWith("botbot eli5"))
            {
                string[] splitText = text.Split(' ');
                string prefix = GetPrefix(splitText[1]);
                Args args = ParseArgs(splitText);

                List<ChatMessage> messages = new List<ChatMessage>();
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    messages.Add(ChatMessage.FromSystem(prefix));
                }
                messages.Add(ChatMessage.FromUser(args.Prompt));

                var completionResult = await openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest()
                {
                    Messages = messages,
                    Model = args.Model,
                    Temperature = args.Temp,
                    MaxTokens = args.MaxTokens - GetTokenCount(messages)
                });
                
                if (completionResult.Successful)
                {
                    var chatChoiceResponse = completionResult.Choices.FirstOrDefault();
                    if (chatChoiceResponse == null)
                    {
                        return new ModuleResponse()
                        {
                            Message = "*No responses from OpenAI*"
                        };
                    }

                    string price = Pricing.GetPrice(args.Model, completionResult.Usage);

                    string responseMessage;
                    if (chatChoiceResponse.FinishReason == "content_filter")
                    {
                        responseMessage = $"**NOTE: OpenAI omitted content due to a flag from OpenAI's content filters** {chatChoiceResponse.Message.Content}";
                    }
                    else
                    {
                        responseMessage = $"{chatChoiceResponse.Message.Content}\n\nPrice: ${price}";
                    }

                    return new ModuleResponse()
                    {
                        Message = responseMessage
                    };
                }
                else
                {
                    if (completionResult.Error == null)
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
                            Message = $"{completionResult.Error.Code}: {completionResult.Error.Message}"
                        };
                    }
                }
            }
            return new ModuleResponse();
        }

        private Args ParseArgs(string[] splitText)
        {
            Args args = new Args(string.Empty, 1, "gpt-3.5-turbo");
            int numberToSkip = 2;
            bool setMaxTokens = false;
            foreach (var item in splitText)
            {
                if (item.StartsWith($"{nameof(Args.Temp)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.Temp = float.Parse(item.Split('=')[1]);
                }
                else if (item.StartsWith($"{nameof(Args.Model)}=", System.StringComparison.InvariantCultureIgnoreCase))
                {
                    numberToSkip += 1;
                    args.Model = item.Split('=')[1];
                }
                else if (item.StartsWith($"{nameof(Args.MaxTokens)}=", System.StringComparison.InvariantCultureIgnoreCase))
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

        private struct Args
        {
            public string Prompt { get; set; }
            public float Temp { get; set; }
            public string Model { get; set; }
            public int MaxTokens { get; set; }

            public Args(string prompt = "", float temp = 1, string model = "gpt-3.5-turbo", int maxTokens = 4096)
            {
                Prompt = prompt;
                Temp = temp;
                Model = model;
                MaxTokens = maxTokens;
            }
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
