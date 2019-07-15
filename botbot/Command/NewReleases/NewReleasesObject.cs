using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Reverb.Models;

namespace botbot.Command.NewReleases
{
    public class NewReleasesObject
    {
        [BsonId]
        public string UserId { get; set; }
        public SpotifyAuth AuthInfo { get; set; }
        public List<SeenSpotifyAlbum> AlbumsSeen { get; set; }
        public DateTimeOffset LastChecked { get; set; }

        public bool AlbumsSeenContainsAlbum(SpotifyAlbum album)
        {
            foreach (SeenSpotifyAlbum seenAlbum in AlbumsSeen)
            {
                if (album.Id == seenAlbum.Id)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
