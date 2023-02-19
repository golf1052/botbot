using System;
using System.Threading.Tasks;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels;
using System.Linq;
using GPT_3_Encoder_Sharp;

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

        private string GetPrefix(string text) {
            string[] splitText = text.Split(' ');
            string command = splitText[1];
            if (String.Equals(command, "eli5") ){
                return "Explain like I'm 5 years old: ";
            }

            return "";
        }

        private string GetPrompt(string text) {
            string[] splitText = text.Split(' ');
            if (splitText[2].StartsWith("temp")) {
                return text.Substring(
                    splitText[0].Length + splitText[1].Length + splitText[2].Length + 2
                ).Trim();
            }
            return text.Substring(splitText[0].Length + splitText[1].Length + 1).Trim();
        }

        private float GetTemp(string text) {
            string[] splitText = text.Split(' ');
            if (splitText[2].StartsWith("temp")) {
                string[] splitTemp = splitText[2].Split(':');
                return float.Parse(splitTemp[1]);
            }
            return 1;
        }

        public async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot gpt") || text.ToLower().StartsWith("botbot eli5"))
            {
                string prefix = GetPrefix(text);
                string prompt = prefix + GetPrompt(text);
                float temp = GetTemp(text);

                // Encode our text first so we know how many remaining tokens we have
                var tokens = encoder.Encode(prompt);

                var completionResult = await openAIService.Completions.CreateCompletion(new CompletionCreateRequest()
                {
                    Prompt = prompt,
                    Model = Models.TextDavinciV3,
                    Temperature = temp,
                    MaxTokens = MaxTokens - tokens.Count
                });
                
                if (completionResult.Successful)
                {
                    return new ModuleResponse()
                    {
                        Message = completionResult.Choices.FirstOrDefault()!.Text
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
    }
}
