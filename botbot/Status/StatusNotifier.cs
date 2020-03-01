using golf1052.SlackAPI.Objects;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace botbot.Status
{
    public class StatusNotifier
    {
        private IMongoDatabase slackDatabase;
        private IMongoCollection<UserStatus> statusesCollection;
        private IMongoCollection<StatusSubscription> statusSubscriptionsCollection;

        public StatusNotifier(string workspaceId)
        {
            slackDatabase = Client.Mongo.GetDatabase($"slack_{workspaceId}");

            try
            {
                slackDatabase.CreateCollection("statuses");
            }
            catch (MongoCommandException)
            {
                // collection already exists
            }
            statusesCollection = slackDatabase.GetCollection<UserStatus>("statuses");

            try
            {
                slackDatabase.CreateCollection("status_subscriptions");
            }
            catch (MongoCommandException)
            {
                // collection already exists
            }
            statusSubscriptionsCollection = slackDatabase.GetCollection<StatusSubscription>("status_subscriptions");
        }

        public void Subscribe(string userId)
        {
            StatusSubscription subscription = GetSubscription(userId);
            if (subscription == null)
            {
                subscription = new StatusSubscription();
                subscription.UserId = userId;
            }
            subscription.Subscribed = true;
            SaveSubscription(subscription);
        }

        public void Unsubscribe(string userId)
        {
            StatusSubscription subscription = GetSubscription(userId);
            if (subscription == null)
            {
                subscription = new StatusSubscription();
                subscription.UserId = userId;
            }
            subscription.Subscribed = false;
            SaveSubscription(subscription);
        }

        public void Backfill(List<SlackUser> users)
        {
            foreach (var user in users)
            {
                UserStatus status = new UserStatus();
                status.UserId = user.Id;
                status.LastStatus = $"{user.Profile.StatusEmoji} {user.Profile.StatusText}";
                SaveUserStatus(status);
            }
        }

        public bool HasChanged(string userId, string status)
        {
            UserStatus userStatus = GetUserStatus(userId);
            if (userStatus == null)
            {
                userStatus = new UserStatus();
                userStatus.UserId = userId;
                userStatus.LastStatus = status;
                return true;
            }
            return userStatus.LastStatus != status;
        }

        public void SaveStatus(string userId, string status)
        {
            UserStatus userStatus = GetUserStatus(userId);
            if (userStatus == null)
            {
                userStatus = new UserStatus();
                userStatus.UserId = userId;
            }
            userStatus.LastStatus = status;
            SaveUserStatus(userStatus);
        }

        private void SaveUserStatus(UserStatus status)
        {
            statusesCollection.ReplaceOne(Builders<UserStatus>.Filter.Eq<string>("_id", status.UserId),
                status,
                new UpdateOptions { IsUpsert = true });
        }

        private UserStatus GetUserStatus(string userId)
        {
            var filter = Builders<UserStatus>.Filter.Eq("_id", userId);
            UserStatus status = statusesCollection.Find(filter).FirstOrDefault();
            return status;
        }

        public List<StatusSubscription> GetAllSubscriptions()
        {
            return statusSubscriptionsCollection.Find(_ => true).ToList();
        }

        private void SaveSubscription(StatusSubscription subscription)
        {
            statusSubscriptionsCollection.ReplaceOne(Builders<StatusSubscription>.Filter.Eq<string>("_id", subscription.UserId),
                subscription,
                new UpdateOptions { IsUpsert = true });
        }

        private StatusSubscription GetSubscription(string userId)
        {
            var filter = Builders<StatusSubscription>.Filter.Eq("_id", userId);
            StatusSubscription status = statusSubscriptionsCollection.Find(filter).FirstOrDefault();
            return status;
        }
    }
}
