using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot.Command.NewReleases
{
    public class SeenSpotifyAlbum
    {
        public string Id { get; set; }
        public DateTimeOffset ReleaseDate { get; set; }
    }
}
