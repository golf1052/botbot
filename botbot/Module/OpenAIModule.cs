using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;

namespace botbot.Module
{
    public class OpenAIModule : IMessageModule
    {
        private const string EncodingName = "cl100k_base";

        private const int MaxTokens = 4096;
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
                string prompt = string.Join(" ", splitText.Skip(2).Take(splitText.Length - 1));
                float temp = 1;
                if (splitText[2].StartsWith("temp"))
                {
                    temp = float.Parse(splitText[2].Split(':')[1]);
                    prompt = string.Join(" ", splitText.Skip(3).Take(splitText.Length - 1));
                }

                List<ChatMessage> messages = new List<ChatMessage>();
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    messages.Add(ChatMessage.FromSystem(prefix));
                }
                messages.Add(ChatMessage.FromUser(prompt));

                var completionResult = await openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest()
                {
                    Messages = messages,
                    Model = "gpt-3.5-turbo-0613",
                    Temperature = temp,
                    MaxTokens = MaxTokens - GetTokenCount(messages)
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

                    string responseMessage;
                    if (chatChoiceResponse.FinishReason == "content_filter")
                    {
                        responseMessage = $"**NOTE: OpenAI omitted content due to a flag from OpenAI's content filters** {chatChoiceResponse.Message.Content}";
                    }
                    else
                    {
                        responseMessage = chatChoiceResponse.Message.Content;
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
