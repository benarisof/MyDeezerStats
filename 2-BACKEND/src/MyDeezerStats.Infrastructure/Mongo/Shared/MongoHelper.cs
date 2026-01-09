using MongoDB.Bson;
using MongoDB.Driver;

namespace MyDeezerStats.Infrastructure.Mongo.Shared
{
    public static class DbFields
    {
        public const string CollectionName = "listening";
        public const string Id = "_id";
        public const string Artist = "Artist";
        public const string Album = "Album";
        public const string Track = "Track";
        public const string Date = "Date";
        public const string Duration = "Duration";
        public const string StreamCount = "StreamCount";
        public const string ListeningTime = "ListeningTime";
        public const string StreamCountByTrack = "StreamCountByTrack";
        public const string LastListening = "LastListening";
    }

    public static class QueryHelper
    {
        public static int ValidateLimit(int limit, int max = 1000, int defaultValue = 10)
            => (limit <= 0 || limit > max) ? defaultValue : limit;

        public static FilterDefinition<BsonDocument> BuildDateFilter(DateTime? from, DateTime? to)
        {
            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Empty;
            if (from.HasValue) filter &= builder.Gte(DbFields.Date, from.Value);
            if (to.HasValue) filter &= builder.Lte(DbFields.Date, to.Value);
            return filter;
        }

        public static FilterDefinition<BsonDocument> FieldHasContent(string fieldName)
        {
            var f = Builders<BsonDocument>.Filter;
            return f.And(f.Exists(fieldName), f.Ne(fieldName, BsonNull.Value), f.Ne(fieldName, ""));
        }
    }

    public static class PipelineHelper
    {
        public static BsonDocument Normalize(string inputField)
        {
            // { $toLower: { $trim: { input: "$Field" } } }
            return new BsonDocument("$toLower", new BsonDocument("$trim", new BsonDocument("input", $"${inputField}")));
        }

        public static BsonDocument GetPrimaryArtistExpression(string inputField)
        {
            // Récupère le premier artiste si séparé par une virgule
            return new BsonDocument("$let", new BsonDocument
            {
                { "vars", new BsonDocument("artists", new BsonDocument("$split", new BsonArray { $"${inputField}", "," })) },
                { "in", new BsonDocument("$trim", new BsonDocument("input",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { new BsonDocument("$size", "$$artists"), 0 }),
                        new BsonDocument("$arrayElemAt", new BsonArray { "$$artists", 0 }),
                        ""
                    }))) }
            });
        }

        public static Dictionary<string, int> ExtractTrackCounts(BsonDocument doc)
        {
            // CORRECTION : Vérification sécurisée de l'existence du champ
            if (!doc.Contains(DbFields.StreamCountByTrack) || !doc[DbFields.StreamCountByTrack].IsBsonArray)
                return new Dictionary<string, int>();

            var dictionary = new Dictionary<string, int>();
            foreach (var item in doc[DbFields.StreamCountByTrack].AsBsonArray)
            {
                var trackDoc = item.AsBsonDocument;
                if (trackDoc.Contains(DbFields.Track) && trackDoc.Contains("Count"))
                {
                    var trackName = trackDoc[DbFields.Track].AsString;
                    var count = trackDoc["Count"].AsInt32;
                    dictionary[trackName] = count;
                }
            }
            return dictionary;
        }
    
    }
}