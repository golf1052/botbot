namespace botbot
{
    public class SpotifyTrack
    {
        public string Uri { get; private set; }
        public string Name { get; private set; }
        public string Artist { get; private set; }
        public string Album { get; private set; }

        public SpotifyTrack(string uri, string name, string artist, string album)
        {
            Uri = uri;
            Name = name;
            Artist = artist;
            Album = album;
        }

        public override string ToString()
        {
            string str = string.Empty;
            str += Name;
            if (!string.IsNullOrEmpty(Artist))
            {
                str += $" by {Artist}";
            }
            if (!string.IsNullOrEmpty(Album))
            {
                str += $" on {Album}";
            }
            return str;
        }
    }
}
