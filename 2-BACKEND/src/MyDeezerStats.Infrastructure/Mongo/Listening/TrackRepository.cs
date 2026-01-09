using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Entities.ListeningInfos;
using MyDeezerStats.Domain.Repositories;
using MyDeezerStats.Infrastructure.Mongo.Shared;
using System.Text.RegularExpressions;

namespace MyDeezerStats.Infrastructure.Mongo.Repositories
{
    public class TrackRepository : ITrackRepository
    {
        private readonly IMongoCollection<BsonDocument> _collection;
        private readonly ILogger<TrackRepository> _logger;

        public TrackRepository(IMongoDatabase database, ILogger<TrackRepository> logger)
        {
            _collection = database.GetCollection<BsonDocument>(DbFields.CollectionName);
            _logger = logger;
        }

        public async Task<List<TrackListening>> GetTopTracksAsync(int limit, DateTime? from = null, DateTime? to = null)
        {
            limit = QueryHelper.ValidateLimit(limit);
            try
            {
                var filter = Builders<BsonDocument>.Filter.And(
                    QueryHelper.BuildDateFilter(from, to),
                    QueryHelper.FieldHasContent(DbFields.Track),
                    QueryHelper.FieldHasContent(DbFields.Artist),
                    Builders<BsonDocument>.Filter.Exists(DbFields.Date)
                );

                var matchStage = PipelineStageDefinitionBuilder.Match(filter)
                    .Render(new RenderArgs<BsonDocument>(_collection.DocumentSerializer, _collection.Settings.SerializerRegistry))
                    .Document;

                var pipeline = new List<BsonDocument>
                {
                    matchStage,
                    new BsonDocument("$addFields", new BsonDocument {
                        { "NormalizedTrack", new BsonDocument("$trim", new BsonDocument("input", $"${DbFields.Track}")) },
                        { "PrimaryArtist", PipelineHelper.GetPrimaryArtistExpression(DbFields.Artist) }
                    }),
                    new BsonDocument("$addFields", new BsonDocument {
                        { "NormalizedTrackLower", new BsonDocument("$toLower", "$NormalizedTrack") },
                        { "NormalizedArtistLower", new BsonDocument("$toLower", "$PrimaryArtist") }
                    }),
                    new BsonDocument("$sort", new BsonDocument(DbFields.Date, -1)), 
                    new BsonDocument("$group", new BsonDocument {
                        { "_id", new BsonDocument { { DbFields.Track, "$NormalizedTrackLower" }, { DbFields.Artist, "$NormalizedArtistLower" } } },
                        { DbFields.StreamCount, new BsonDocument("$sum", 1) },
                        { DbFields.ListeningTime, new BsonDocument("$sum", $"${DbFields.Duration}") },
                        { DbFields.LastListening, new BsonDocument("$first", $"${DbFields.Date}") },
                        { "OriginalTrackName", new BsonDocument("$first", "$NormalizedTrack") },
                        { "OriginalArtistName", new BsonDocument("$first", "$PrimaryArtist") }
                    }),
                    new BsonDocument("$sort", new BsonDocument(DbFields.StreamCount, -1)),
                    new BsonDocument("$limit", limit)
                };

                var results = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
                return results.Select(MapToTrackListening).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top tracks");
                throw new ApplicationException("Erreur récupération top tracks", ex);
            }
        }

        
        private static TrackListening MapToTrackListening(BsonDocument doc)
        {
            return new TrackListening
            {
                Name = doc["OriginalTrackName"].AsString,
                Artist = doc["OriginalArtistName"].AsString,
                Album = string.Empty,
                StreamCount = doc[DbFields.StreamCount].AsInt32,
                ListeningTime = doc[DbFields.ListeningTime].AsInt32,
                LastListening = doc[DbFields.LastListening].ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }


    }
}