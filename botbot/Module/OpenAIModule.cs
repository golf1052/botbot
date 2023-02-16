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

        public async Task<ModuleResponse> Handle(string text, string userId, string channel)
        {
            if (text.ToLower().StartsWith("botbot gpt"))
            {
                text = text.Substring(10).Trim();
                float temp = 1;
                if (text.StartsWith("temp"))
                {
                    string[] splitText = text.Split(' ');
                    string[] splitTemp = splitText[0].Split(':');
                    temp = float.Parse(splitTemp[1]);
                    text = text.Substring(splitText[0].Length).Trim();
                }

                // Encode our text first so we know how many remaining tokens we have
                var tokens = encoder.Encode(text);

                var completionResult = await openAIService.Completions.CreateCompletion(new CompletionCreateRequest()
                {
                    Prompt = text,
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
