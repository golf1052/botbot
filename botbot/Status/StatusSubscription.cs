using MongoDB.Bson.Serialization.Attributes;

namespace botbot.Status
{
    public class StatusSubscription
    {
        [BsonId]
        public string? UserId { get; set; }
        public bool Subscribed { get; set; }
    }
}
