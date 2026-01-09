using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MyDeezerStats.Domain.Entities.ListeningInfos;
using MyDeezerStats.Domain.Repositories;
using MyDeezerStats.Infrastructure.Mongo.Shared;
using System.Text.RegularExpressions;

namespace MyDeezerStats.Infrastructure.Mongo.Repositories
{
    public class ArtistRepository : IArtistRepository
    {
        private readonly IMongoCollection<BsonDocument> _collection;
        private readonly ILogger<ArtistRepository> _logger;

        public ArtistRepository(IMongoDatabase database, ILogger<ArtistRepository> logger)
        {
            _collection = database.GetCollection<BsonDocument>(DbFields.CollectionName);
            _logger = logger;
        }

        public async Task<List<ArtistListening>> GetTopArtistsAsync(int limit, DateTime? from = null, DateTime? to = null)
        {
            limit = QueryHelper.ValidateLimit(limit);
            try
            {
                var filter = Builders<BsonDocument>.Filter.And(
                    QueryHelper.BuildDateFilter(from, to),
                    QueryHelper.FieldHasContent(DbFields.Artist)
                );

                var matchStage = PipelineStageDefinitionBuilder.Match(filter)
                    .Render(new RenderArgs<BsonDocument>(_collection.DocumentSerializer, _collection.Settings.SerializerRegistry))
                    .Document;

                var pipeline = new List<BsonDocument>
                {
                    matchStage,
                    new BsonDocument("$project", new BsonDocument {
                        { DbFields.Artist, 1 }, { DbFields.Duration, 1 },
                        { "NormalizedArtist", new BsonDocument("$trim", new BsonDocument("input", $"${DbFields.Artist}")) }
                    }),
                    new BsonDocument("$addFields", new BsonDocument("PrimaryArtist", PipelineHelper.GetPrimaryArtistExpression("NormalizedArtist"))),
                    new BsonDocument("$addFields", new BsonDocument("PrimaryArtistNormalized", new BsonDocument("$toLower", "$PrimaryArtist"))),
                    new BsonDocument("$group", new BsonDocument {
                        { "_id", "$PrimaryArtistNormalized" },
                        { "TotalCount", new BsonDocument("$sum", 1) },
                        { "TotalDuration", new BsonDocument("$sum", $"${DbFields.Duration}") },
                        { "OriginalName", new BsonDocument("$first", "$PrimaryArtist") }
                    }),
                    new BsonDocument("$sort", new BsonDocument("TotalCount", -1)),
                    new BsonDocument("$limit", limit)
                };

                var results = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
                return results.Select(MapToArtistListening).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top artists");
                throw new ApplicationException("Erreur récupération top artistes", ex);
            }
        }

        public async Task<ArtistListening?> GetArtistDetailsAsync(string artist, DateTime? from = null, DateTime? to = null)
        {
            if (string.IsNullOrWhiteSpace(artist)) throw new ArgumentException("Artist required");

            try
            {
                // Filtre optimisé gérant les artistes seuls ou en collaboration
                var filter = Builders<BsonDocument>.Filter.And(
                    QueryHelper.BuildDateFilter(from, to),
                    Builders<BsonDocument>.Filter.Regex(DbFields.Artist, new BsonRegularExpression($"(^|[,&]\\s*){Regex.Escape(artist)}([,&]|$)", "i")),
                    QueryHelper.FieldHasContent(DbFields.Track)
                );

                var matchStage = PipelineStageDefinitionBuilder.Match(filter)
                    .Render(new RenderArgs<BsonDocument>(_collection.DocumentSerializer, _collection.Settings.SerializerRegistry))
                    .Document;

                var pipeline = new List<BsonDocument>
                {
                    matchStage,
                    // Premier group : On regroupe par morceau pour compter les écoutes de chaque titre
                    new BsonDocument("$group", new BsonDocument {
                        { "_id", $"${DbFields.Track}" },
                        { "Count", new BsonDocument("$sum", 1) },
                        { "Duration", new BsonDocument("$sum", $"${DbFields.Duration}") }
                    }),
                    // Second group : On regroupe tout sous l'artiste pour formater la réponse
                    new BsonDocument("$group", new BsonDocument {
                        { "_id", artist },
                        // CORRECTION : Utilisation de DbFields.StreamCountByTrack pour la cohérence avec PipelineHelper
                        { DbFields.StreamCountByTrack, new BsonDocument("$push", new BsonDocument {
                            { DbFields.Track, "$_id" },
                            { "Count", "$Count" }
                        }) },
                        { "TotalCount", new BsonDocument("$sum", "$Count") },
                        { "TotalDuration", new BsonDocument("$sum", "$Duration") }
                    })
                };

                var result = await _collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();
                return result == null ? null : MapToArtistListening(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting artist details for {Artist}", artist);
                throw new ApplicationException("Erreur récupération détails artiste", ex);
            }
        }

        private static ArtistListening MapToArtistListening(BsonDocument doc)
        {
            // Si OriginalName existe (vient du Top), sinon on prend l'ID (vient de Details)
            var name = doc.Contains("OriginalName") ? doc["OriginalName"].AsString : doc["_id"].AsString;

            return new ArtistListening
            {
                Name = name,
                StreamCount = doc.GetValue("TotalCount", 0).AsInt32,
                ListeningTime = doc.GetValue("TotalDuration", 0).ToInt32(),
                StreamCountByTrack = PipelineHelper.ExtractTrackCounts(doc)
            };
        }
    }
}