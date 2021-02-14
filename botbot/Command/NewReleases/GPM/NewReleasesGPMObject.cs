using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace botbot.Command.NewReleases.GPM
{
    public class NewReleasesGPMObject
    {
        [BsonId]
        public string? UserId { get; set; }
        public string? AuthInfo { get; set; }
        public List<SeenGPMAlbum> AlbumsSeen { get; set; } = new List<SeenGPMAlbum>();
        public DateTimeOffset LastChecked { get; set; }

        public bool AlbumsSeenContainsAlbum(GPMAlbum album)
        {
            foreach (SeenGPMAlbum seenAlbum in AlbumsSeen)
            {
                if (album.AlbumId == seenAlbum.AlbumId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
