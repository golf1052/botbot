using MongoDB.Bson.Serialization.Attributes;

namespace botbot.Status
{
    public class UserStatus
    {
        [BsonId]
        public string UserId { get; set; }
        public string LastStatus { get; set; }
    }
}
