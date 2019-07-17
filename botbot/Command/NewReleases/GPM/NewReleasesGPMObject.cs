using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json.Linq;

namespace botbot.Command.NewReleases.GPM
{
    public class NewReleasesGPMObject
    {
        [BsonId]
        public string UserId { get; set; }
        public string AuthInfo { get; set; }
        public List<SeenGPMAlbum> AlbumsSeen { get; set; }
        public DateTimeOffset LastChecked { get; set; }

        public bool AlbumsSeenContainsAlbum(GPMAlbum album)
        {
            foreach (SeenGPMAlbum seenAlbum in AlbumsSeen)
            {
                if (album.AlbumId == album.AlbumId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
