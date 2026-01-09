using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MyDeezerStats.Domain.Entities.ListeningInfos;
using MyDeezerStats.Domain.Repositories;
using MyDeezerStats.Infrastructure.Mongo.Shared;
using System.Text.RegularExpressions;

namespace MyDeezerStats.Infrastructure.Mongo.Repositories
{
    public class AlbumRepository : IAlbumRepository
    {
        private readonly IMongoCollection<BsonDocument> _collection;
        private readonly ILogger<AlbumRepository> _logger;

        public AlbumRepository(IMongoDatabase database, ILogger<AlbumRepository> logger)
        {
            _collection = database.GetCollection<BsonDocument>(DbFields.CollectionName);
            _logger = logger;
        }

        public async Task<List<AlbumListening>> GetTopAlbumsAsync(int limit, DateTime? from = null, DateTime? to = null)
        {
            limit = QueryHelper.ValidateLimit(limit);
            try
            {
                var filter = Builders<BsonDocument>.Filter.And(
                    QueryHelper.BuildDateFilter(from, to),
                    QueryHelper.FieldHasContent(DbFields.Album),
                    QueryHelper.FieldHasContent(DbFields.Track)
                );

                var matchStage = PipelineStageDefinitionBuilder
                    .Match(filter)
                    .Render(new RenderArgs<BsonDocument>(_collection.DocumentSerializer, _collection.Settings.SerializerRegistry))
                    .Document;

                var pipeline = new List<BsonDocument>
                {
                    matchStage,
                    new BsonDocument("$project", new BsonDocument {
                        { DbFields.Album, 1 }, { DbFields.Artist, 1 }, { DbFields.Duration, 1 },
                        { "NormalizedAlbum", PipelineHelper.Normalize(DbFields.Album) },
                        { "NormalizedArtist", PipelineHelper.Normalize(DbFields.Artist) }
                    }),
                    new BsonDocument("$addFields", new BsonDocument("PrimaryArtist", PipelineHelper.GetPrimaryArtistExpression("NormalizedArtist"))),
                    new BsonDocument("$group", new BsonDocument {
                        { "_id", new BsonDocument { { DbFields.Album, "$NormalizedAlbum" }, { DbFields.Artist, "$PrimaryArtist" } } },
                        { "TotalCount", new BsonDocument("$sum", 1) },
                        { "TotalDuration", new BsonDocument("$sum", $"${DbFields.Duration}") },
                        { "OriginalTitle", new BsonDocument("$first", $"${DbFields.Album}") },
                        { "OriginalArtist", new BsonDocument("$first", $"${DbFields.Artist}") }
                    }),
                    new BsonDocument("$sort", new BsonDocument("TotalCount", -1)),
                    new BsonDocument("$limit", limit)
                };

                var results = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
                return results.Select(MapToAlbumListening).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top albums");
                throw new ApplicationException("Erreur récupération top albums", ex);
            }
        }

        public async Task<AlbumListening?> GetAlbumDetailsAsync(string title, string artist, DateTime? from = null, DateTime? to = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                throw new ArgumentException("Title/Artist required");

            try
            {
                var filter = Builders<BsonDocument>.Filter.And(
                    QueryHelper.BuildDateFilter(from, to),
                    Builders<BsonDocument>.Filter.Regex(DbFields.Album, new BsonRegularExpression($"^{Regex.Escape(title)}$", "i")),
                    Builders<BsonDocument>.Filter.Regex(DbFields.Artist, new BsonRegularExpression($"(^|[,&]\\s*){Regex.Escape(artist)}([,&]|$)", "i")),
                    QueryHelper.FieldHasContent(DbFields.Track)
                );

                var matchStage = PipelineStageDefinitionBuilder
                    .Match(filter)
                    .Render(new RenderArgs<BsonDocument>(_collection.DocumentSerializer, _collection.Settings.SerializerRegistry))
                    .Document;

                var pipeline = new List<BsonDocument>
                {
                    matchStage,
                    new BsonDocument("$addFields", new BsonDocument("MainArtist", artist)),
                    new BsonDocument("$group", new BsonDocument {
                        { "_id", $"${DbFields.Track}" },
                        { "Count", new BsonDocument("$sum", 1) },
                        { "Duration", new BsonDocument("$sum", $"${DbFields.Duration}") },
                        { DbFields.Album, new BsonDocument("$first", $"${DbFields.Album}") },
                        { DbFields.Artist, new BsonDocument("$first", "$MainArtist") }
                    }),
                    new BsonDocument("$group", new BsonDocument {
                        { "_id", new BsonDocument { { DbFields.Album, $"${DbFields.Album}" }, { DbFields.Artist, $"${DbFields.Artist}" } } },
                        // CORRECTION : Utilisation de DbFields.StreamCountByTrack au lieu de "Tracks"
                        { DbFields.StreamCountByTrack, new BsonDocument("$push", new BsonDocument {
                            { DbFields.Track, "$_id" },
                            { "Count", "$Count" }
                        }) },
                        { "TotalCount", new BsonDocument("$sum", "$Count") },
                        { "TotalDuration", new BsonDocument("$sum", "$Duration") }
                    })
                };

                var result = await _collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

                return result == null ? null : MapToAlbumListening(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting album details for {Title}", title);
                throw new ApplicationException("Erreur récupération détails album", ex);
            }
        }

        private static AlbumListening MapToAlbumListening(BsonDocument doc)
        {
            var title = doc.Contains("OriginalTitle") ? doc["OriginalTitle"].AsString : doc["_id"][DbFields.Album].AsString;
            var artist = doc.Contains("OriginalArtist") ? doc["OriginalArtist"].AsString : doc["_id"][DbFields.Artist].AsString;

            return new AlbumListening
            {
                Title = title,
                Artist = artist,
                StreamCount = doc.GetValue("TotalCount", 0).AsInt32,
                ListeningTime = doc.GetValue("TotalDuration", 0).ToInt32(),
                StreamCountByTrack = PipelineHelper.ExtractTrackCounts(doc)
            };
        }
    }
}