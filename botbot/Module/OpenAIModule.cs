using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GPT_3_Encoder_Sharp;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;

namespace botbot.Module
{
    public class OpenAIModule : IMessageModule
    {
        private const int MaxTokens = 4096;
        private readonly OpenAIService openAIService;
        private readonly Encoder encoder;

        public OpenAIModule()
        {
            openAIService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = Secrets.OpenAIApiKey,
                Organization = Secrets.OpenAIOrganization
            });
            encoder = Encoder.Get_Encoder();
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
                prompt = prefix + prompt;

                // Encode our text first so we know how many remaining tokens we have
                var tokens = encoder.Encode(prompt);

                var completionResult = await openAIService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest()
                {
                    Messages = new List<ChatMessage>()
                    {
                        ChatMessage.FromUser(prompt)
                    },
                    Model = Models.ChatGpt3_5Turbo,
                    Temperature = temp,
                    MaxTokens = MaxTokens - tokens.Count
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

        private string GetPrefix(string command)
        {
            if (command == "eli5")
            {
                return "Explain like I'm 5 years old: ";
            }

            return "";
        }
    }
}
