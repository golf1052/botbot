using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace botbot.Command.NewReleases.GPM
{
    public class GPMAlbum
    {
        [JsonProperty("albumId")]
        public string AlbumId { get; private set; }

        [JsonProperty("artist")]
        public string Artist { get; private set; }

        [JsonProperty("name")]
        public string Name { get; private set; }

        [JsonProperty("year")]
        public int Year { get; private set; }
    }
}
