using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;

namespace botbot
{
    public class PlusPlusScore
    {
        public static PlusPlusThing GetThing(string thing)
        {
            var filter = Builders<PlusPlusThing>.Filter.Eq<string>("_id", thing);
            PlusPlusThing plusPlusThing = Client.ThingCollection.Find(filter).FirstOrDefault();
            if (plusPlusThing == null)
            {
                plusPlusThing = new PlusPlusThing();
                plusPlusThing.Id = thing;
                plusPlusThing.Score = 0;
                SaveThing(plusPlusThing);
            }
            return plusPlusThing;
        }

        public static KeyValuePair<int?, int?> Add(string thing, string from, string room, string reason)
        {
            if (Validate(thing, from))
            {
                PlusPlusThing plusPlusThing = GetThing(thing);
                plusPlusThing.Score++;
                if (!string.IsNullOrEmpty(reason))
                {
                    if (!plusPlusThing.Reasons.ContainsKey(reason))
                    {
                        plusPlusThing.Reasons.Add(reason, 0);
                    }
                    plusPlusThing.Reasons[reason]++;
                }
                SaveLog(thing, from, room, reason);
                SaveThing(plusPlusThing);
                if (!string.IsNullOrEmpty(reason))
                {
                    return new KeyValuePair<int?, int?>(plusPlusThing.Score, plusPlusThing.Reasons[reason]);
                }
                else
                {
                    return new KeyValuePair<int?, int?>(plusPlusThing.Score, null);
                }
            }
            else
            {
                return new KeyValuePair<int?, int?>(null, null);
            }
        }

        public static KeyValuePair<int?, int?> Subtract(string thing, string from, string room, string reason)
        {
            if (Validate(thing, from))
            {
                PlusPlusThing plusPlusThing = GetThing(thing);
                plusPlusThing.Score--;
                if (!string.IsNullOrEmpty(reason))
                {
                    if (!plusPlusThing.Reasons.ContainsKey(reason))
                    {
                        plusPlusThing.Reasons.Add(reason, 0);
                    }
                    plusPlusThing.Reasons[reason]--;
                }
                SaveLog(thing, from, room, reason);
                SaveThing(plusPlusThing);
                if (!string.IsNullOrEmpty(reason))
                {
                    return new KeyValuePair<int?, int?>(plusPlusThing.Score, plusPlusThing.Reasons[reason]);
                }
                else
                {
                    return new KeyValuePair<int?, int?>(plusPlusThing.Score, null);
                }
            }
            else
            {
                return new KeyValuePair<int?, int?>(null, null);
            }
        }

        public static void Erase(string thing, string from, string room, string reason)
        {
            PlusPlusThing plusPlusThing = GetThing(thing);
            if (!string.IsNullOrEmpty(reason))
            {
                if (plusPlusThing.Reasons.ContainsKey(thing))
                {
                    plusPlusThing.Reasons.Remove(thing);
                    SaveThing(plusPlusThing);
                    return;
                }
            }
            else
            {
                Client.ThingCollection.DeleteOne(Builders<PlusPlusThing>.Filter.Eq<string>("_id", plusPlusThing.Id));
            }
        }

        public static int ScoreForThing(string thing)
        {
            return GetThing(thing).Score;
        }

        public static Dictionary<string, int> ReasonsForThing(string thing)
        {
            return GetThing(thing).Reasons;
        }

        public static PlusPlusLastThing Last(string room)
        {
            PlusPlusLog log = GetLog();
            if (log.Last.ContainsKey(room))
            {
                return log.Last[room];
            }
            else
            {
                return null;
            }
        }

        public static List<KeyValuePair<string, int>> Top(int amount)
        {
            List<KeyValuePair<string, int>> tops = new List<KeyValuePair<string, int>>();
            var things = Client.ThingCollection.Find(new BsonDocument()).ToList();
            var topThings = (from thing in things
                             orderby thing.Score descending
                             select thing).ToList();
            for (int i = 0; i < Math.Min(amount, topThings.Count); i++)
            {
                tops.Add(new KeyValuePair<string, int>(topThings[i].Id, topThings[i].Score));
            }
            return tops;
        }

        public static List<KeyValuePair<string, int>> Bottom(int amount)
        {
            List<KeyValuePair<string, int>> tops = new List<KeyValuePair<string, int>>();
            var things = Client.ThingCollection.Find(new BsonDocument()).ToList();
            var topThings = (from thing in things
                             orderby thing.Score ascending
                             select thing).ToList();
            for (int i = 0; i < Math.Min(amount, topThings.Count); i++)
            {
                tops.Add(new KeyValuePair<string, int>(topThings[i].Id, topThings[i].Score));
            }
            return tops;
        }

        private static void SaveThing(PlusPlusThing thing)
        {
            Client.ThingCollection.ReplaceOne(Builders<PlusPlusThing>.Filter.Eq<string>("_id", thing.Id),
                thing,
                new UpdateOptions { IsUpsert = true });
        }

        public static void SaveLog(string thing, string from, string room, string reason)
        {
            PlusPlusLog log = GetLog();
            if (!log.Log.ContainsKey(from))
            {
                log.Log.Add(from, new Dictionary<string, DateTime>());
            }
            if (!log.Log[from].ContainsKey(thing))
            {
                log.Log[from].Add(thing, DateTime.UtcNow);
            }
            log.Log[from][thing] = DateTime.UtcNow;
            
            if (!log.Last.ContainsKey(room))
            {
                log.Last.Add(room, null);
            }
            log.Last[room] = new PlusPlusLastThing(thing, reason);
            Client.PlusPlusLogCollection.ReplaceOne(Builders<PlusPlusLog>.Filter.Eq("_id", "log"),
                log,
                new UpdateOptions { IsUpsert = true });
        }

        public static PlusPlusLog GetLog()
        {
            var filter = Builders<PlusPlusLog>.Filter.Eq<string>("_id", "log");
            try
            {
                PlusPlusLog log = Client.PlusPlusLogCollection.Find(filter).FirstOrDefault();
                if (log == null)
                {
                    log = new PlusPlusLog();
                    log.Id = "log";
                    Client.PlusPlusLogCollection.ReplaceOne(Builders<PlusPlusLog>.Filter.Eq<string>("_id", "log"),
                        log,
                        new UpdateOptions { IsUpsert = true });
                }
                return log;
            }
            catch (TimeoutException)
            {
                throw;
            }
        }

        private static bool Validate(string thing, string from)
        {
            return thing != from && !string.IsNullOrEmpty(thing);
        }
    }
}
