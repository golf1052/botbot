using Microsoft.AspNetCore.Http;

namespace botbot
{
    public class RequestBody
    {
        public string Token { get; private set; }
        public string TeamId { get; private set; }
        public string TeamDomain { get; private set; }
        public string ChannelId { get; private set; }
        public string ChannelName { get; private set; }
        public string UserId { get; private set; }
        public string UserName { get; private set; }
        public string Command { get; private set; }
        public string Text { get; private set; }
        public string ResponseUrl { get; private set; }

        public RequestBody(IFormCollection formData)
        {
            if (formData.ContainsKey("token"))
            {
                Token = formData["token"];
            }
            if (formData.ContainsKey("team_id"))
            {
                TeamId = formData["team_id"];
            }
            if (formData.ContainsKey("team_domain"))
            {
                TeamDomain = formData["team_domain"];
            }
            if (formData.ContainsKey("channel_id"))
            {
                ChannelId = formData["channel_id"];
            }
            if (formData.ContainsKey("channel_name"))
            {
                ChannelName = formData["channel_name"];
            }
            if (formData.ContainsKey("user_id"))
            {
                UserId = formData["user_id"];
            }
            if (formData.ContainsKey("user_name"))
            {
                UserName = formData["user_name"];
            }
            if (formData.ContainsKey("command"))
            {
                Command = formData["command"];
            }
            if (formData.ContainsKey("text"))
            {
                Text = formData["text"].ToString().ToLower().Trim();
            }
            if (formData.ContainsKey("response_url"))
            {
                ResponseUrl = formData["response_url"];
            }
        }
    }
}
