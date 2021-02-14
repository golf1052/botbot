using System;

namespace botbot.Command.NewReleases
{
    public class SpotifyAuth
    {
        public string? AccessToken { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public string? RefreshToken { get; set; }
    }
}
