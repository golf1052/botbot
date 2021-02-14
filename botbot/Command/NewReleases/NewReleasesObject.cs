using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using Reverb.Models;

namespace botbot.Command.NewReleases
{
    public class NewReleasesObject
    {
        [BsonId]
        public string? UserId { get; set; }
        public SpotifyAuth? AuthInfo { get; set; }
        public List<SeenSpotifyAlbum> AlbumsSeen { get; set; } = new List<SeenSpotifyAlbum>();
        public DateTimeOffset LastChecked { get; set; }

        public bool AlbumsSeenContainsAlbum(SpotifyAlbum album)
        {
            if (AlbumsSeen == null)
            {
                return false;
            }
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
