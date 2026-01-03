using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Entities.ListeningInfos;
using MyDeezerStats.Domain.Repositories;
using System.Text.RegularExpressions;

namespace MyDeezerStats.Infrastructure.Mongo.Ecoutes
{
    public class ListeningRepository : IListeningRepository
    {
        private readonly IMongoCollection<ListeningEntry> _collection;
        private readonly IMongoCollection<BsonDocument> _bsonCollection;
        private readonly ILogger<ListeningRepository> _logger;

        // 1. Centralisation des noms de champs pour éviter les "Magic Strings"
        private static class Fields
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

        public ListeningRepository(IMongoDatabase database, ILogger<ListeningRepository> logger)
        {
            _collection = database.GetCollection<ListeningEntry>(Fields.CollectionName);
            _bsonCollection = database.GetCollection<BsonDocument>(Fields.CollectionName);
            _logger = logger;
        }

        public async Task<ListeningEntry?> GetLastEntryAsync()
        {
            return await _collection
                .Find(FilterDefinition<ListeningEntry>.Empty)
                .SortByDescending(x => x.Date)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ListeningEntry>> GetLatestListeningsAsync(int limit)
        {
            limit = ValidateLimit(limit, 1000, 100);

            try
            {
                return await _collection
                    .Find(FilterDefinition<ListeningEntry>.Empty)
                    .SortByDescending(x => x.Date)
                    .Limit(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest {Limit} listenings", limit);
                throw new ApplicationException("Erreur lors de la récupération des écoutes récentes", ex);
            }
        }

        public async Task InsertListeningsAsync(List<ListeningEntry> listenings)
        {
            if (listenings == null || !listenings.Any())
            {
                _logger.LogWarning("No listenings to insert");
                return;
            }

            try
            {
                var operations = new List<WriteModel<ListeningEntry>>();

                foreach (var listening in listenings)
                {
                    var filter = Builders<ListeningEntry>.Filter.And(
                        Builders<ListeningEntry>.Filter.Eq(x => x.Track, listening.Track),
                        Builders<ListeningEntry>.Filter.Eq(x => x.Artist, listening.Artist),
                        Builders<ListeningEntry>.Filter.Eq(x => x.Album, listening.Album),
                        Builders<ListeningEntry>.Filter.Eq(x => x.Date, listening.Date)
                    );

                    var update = Builders<ListeningEntry>.Update
                        .Set(x => x.Track, listening.Track)
                        .Set(x => x.Artist, listening.Artist)
                        .Set(x => x.Album, listening.Album)
                        .Set(x => x.Duration, listening.Duration)
                        .Set(x => x.Date, listening.Date)
                        .SetOnInsert(x => x.Id, string.IsNullOrEmpty(listening.Id)
                            ? ObjectId.GenerateNewId().ToString()
                            : listening.Id);

                    operations.Add(new UpdateOneModel<ListeningEntry>(filter, update) { IsUpsert = true });
                }

                if (operations.Any())
                {
                    var result = await _collection.BulkWriteAsync(operations, new BulkWriteOptions { IsOrdered = false });
                    _logger.LogInformation("Bulk op completed: {Matched} matched, {Modified} modified, {Upserts} upserted",
                        result.MatchedCount, result.ModifiedCount, result.Upserts.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk insert/update of {Count} listenings", listenings.Count);
                throw new ApplicationException("Erreur lors de l'insertion des écoutes", ex);
            }
        }

        public async Task<List<AlbumListening>> GetTopAlbumsWithAsync(int limit, DateTime? from = null, DateTime? to = null)
        {
            limit = ValidateLimit(limit);
            try
            {
                var pipeline = BuildTopAlbumsPipeline(limit, from, to);
                var results = await _bsonCollection.Aggregate<BsonDocument>(pipeline).ToListAsync();
                return results.Select(MapBsonToAlbumListening).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top {Limit} albums", limit);
                throw new ApplicationException("Erreur lors de la récupération des albums les plus écoutés", ex);
            }
        }

        public async Task<AlbumListening?> GetAlbumsWithAsync(string title, string artist, DateTime? from = null, DateTime? to = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                throw new ArgumentException("Title and artist are required");

            try
            {
                var pipeline = BuildAlbumDetailsPipeline(title, artist, from, to);
                var result = await _bsonCollection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

                return result == null ? null : MapBsonToAlbumListening(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving album {Title} by {Artist}", title, artist);
                throw new ApplicationException($"Erreur lors de la récupération de l'album '{title}'", ex);
            }
        }

        public async Task<List<ArtistListening>> GetTopArtistWithAsync(int limit, DateTime? from = null, DateTime? to = null)
        {
            limit = ValidateLimit(limit);
            try
            {
                var pipeline = BuildTopArtistsPipeline(limit, from, to);
                var results = await _bsonCollection.Aggregate<BsonDocument>(pipeline).ToListAsync();
                return results.Select(MapBsonToArtistListening).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top {Limit} artists", limit);
                throw new ApplicationException("Erreur lors de la récupération des artistes les plus écoutés", ex);
            }
        }

        public async Task<ArtistListening?> GetArtistWithAsync(string artist, DateTime? from = null, DateTime? to = null)
        {
            if (string.IsNullOrWhiteSpace(artist)) throw new ArgumentException("Artist name is required");

            try
            {
                var pipeline = BuildArtistDetailsPipeline(artist, from, to);
                var result = await _bsonCollection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

                return result == null ? null : MapBsonToArtistListening(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving artist {Artist}", artist);
                throw new ApplicationException($"Erreur lors de la récupération de l'artiste '{artist}'", ex);
            }
        }

        public async Task<List<TrackListening>> GetTopTrackWithAsync(int limit, DateTime? from = null, DateTime? to = null)
        {
            limit = ValidateLimit(limit);
            try
            {
                var pipeline = BuildTopTracksPipeline(limit, from, to);
                var results = await _bsonCollection.Aggregate<BsonDocument>(pipeline).ToListAsync();
                return results.Select(MapBsonToTrackListening).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top {Limit} tracks", limit);
                throw new ApplicationException("Erreur lors de la récupération des morceaux les plus écoutés", ex);
            }
        }

        #region Private Methods - Common Helpers

        private int ValidateLimit(int limit, int max = 100, int defaultValue = 10)
        {
            if (limit <= 0 || limit > max) return defaultValue;
            return limit;
        }

        // 2. Factorisation de la création de filtres
        private FilterDefinition<BsonDocument> BuildFieldWithContentFilter(string fieldName)
        {
            var f = Builders<BsonDocument>.Filter;
            return f.And(
                f.Exists(fieldName),
                f.Ne(fieldName, BsonNull.Value),
                f.Ne(fieldName, "")
            );
        }

        private FilterDefinition<BsonDocument> BuildDateFilter(DateTime? from, DateTime? to)
        {
            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Empty;

            if (from.HasValue) filter &= builder.Gte(Fields.Date, from.Value);
            if (to.HasValue) filter &= builder.Lte(Fields.Date, to.Value);

            return filter;
        }

        #endregion

        #region Private Methods - Pipeline Builders Helpers

        // 3. Helpers pour simplifier les expressions BSON complexes
        private static BsonDocument GetNormalizationExpression(string inputField)
        {
            // équivalent à: { $toLower: { $trim: { input: "$Field" } } }
            return new BsonDocument("$toLower",
                new BsonDocument("$trim", new BsonDocument("input", $"${inputField}")));
        }

        private static BsonDocument GetPrimaryArtistExpression(string inputField)
        {
            // Logique de split sur la virgule et récupération du premier élément
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

        #endregion

        #region Private Methods - Pipelines

        private PipelineDefinition<BsonDocument, BsonDocument> BuildTopAlbumsPipeline(int limit, DateTime? from, DateTime? to)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                BuildDateFilter(from, to),
                BuildFieldWithContentFilter(Fields.Album),
                BuildFieldWithContentFilter(Fields.Track)
            );

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(filter),
                new BsonDocument("$project", new BsonDocument
                {
                    { Fields.Album, 1 },
                    { Fields.Artist, 1 },
                    { Fields.Duration, 1 },
                    { "NormalizedAlbum", GetNormalizationExpression(Fields.Album) },
                    { "NormalizedArtist", GetNormalizationExpression(Fields.Artist) }
                }),
                new BsonDocument("$addFields", new BsonDocument("PrimaryArtist", GetPrimaryArtistExpression("NormalizedArtist"))),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", new BsonDocument { { Fields.Album, "$NormalizedAlbum" }, { Fields.Artist, "$PrimaryArtist" } } },
                    { "TotalCount", new BsonDocument("$sum", 1) },
                    { "TotalDuration", new BsonDocument("$sum", $"${Fields.Duration}") },
                    { "OriginalTitle", new BsonDocument("$first", $"${Fields.Album}") },
                    { "OriginalArtist", new BsonDocument("$first", $"${Fields.Artist}") }
                }),
                new BsonDocument("$sort", new BsonDocument("TotalCount", -1)),
                new BsonDocument("$limit", limit),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Title", "$OriginalTitle" },
                    { Fields.Artist, "$OriginalArtist" },
                    { Fields.StreamCount, "$TotalCount" },
                    { Fields.ListeningTime, "$TotalDuration" },
                    { Fields.Id, 0 }
                })
            };
        }

        private PipelineDefinition<BsonDocument, BsonDocument> BuildTopArtistsPipeline(int limit, DateTime? from, DateTime? to)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                BuildDateFilter(from, to),
                BuildFieldWithContentFilter(Fields.Artist)
            );

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(filter),
                new BsonDocument("$project", new BsonDocument
                {
                    { Fields.Artist, 1 },
                    { Fields.Duration, 1 },
                    { "NormalizedArtist", new BsonDocument("$trim", new BsonDocument("input", $"${Fields.Artist}")) }
                }),
                new BsonDocument("$addFields", new BsonDocument("PrimaryArtist", GetPrimaryArtistExpression("NormalizedArtist"))),
                new BsonDocument("$addFields", new BsonDocument("PrimaryArtistNormalized", new BsonDocument("$toLower", "$PrimaryArtist"))),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$PrimaryArtistNormalized" },
                    { "TotalCount", new BsonDocument("$sum", 1) },
                    { "TotalDuration", new BsonDocument("$sum", $"${Fields.Duration}") },
                    { "OriginalName", new BsonDocument("$first", "$PrimaryArtist") }
                }),
                new BsonDocument("$sort", new BsonDocument("TotalCount", -1)),
                new BsonDocument("$limit", limit),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Name", "$OriginalName" },
                    { Fields.StreamCount, "$TotalCount" },
                    { Fields.ListeningTime, "$TotalDuration" },
                    { Fields.Id, 0 }
                })
            };
        }

        private PipelineDefinition<BsonDocument, BsonDocument> BuildTopTracksPipeline(int limit, DateTime? from, DateTime? to)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                BuildDateFilter(from, to),
                BuildFieldWithContentFilter(Fields.Track),
                BuildFieldWithContentFilter(Fields.Artist),
                Builders<BsonDocument>.Filter.Exists(Fields.Date)
            );

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(filter),
                new BsonDocument("$addFields", new BsonDocument
                {
                    { "NormalizedTrack", new BsonDocument("$trim", new BsonDocument("input", $"${Fields.Track}")) },
                    { "PrimaryArtist", GetPrimaryArtistExpression(Fields.Artist) }
                }),
                new BsonDocument("$addFields", new BsonDocument
                {
                    { "NormalizedTrackLower", new BsonDocument("$toLower", "$NormalizedTrack") },
                    { "NormalizedArtistLower", new BsonDocument("$toLower", "$PrimaryArtist") }
                }),
                new BsonDocument("$sort", new BsonDocument(Fields.Date, -1)),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", new BsonDocument { { Fields.Track, "$NormalizedTrackLower" }, { Fields.Artist, "$NormalizedArtistLower" } } },
                    { Fields.StreamCount, new BsonDocument("$sum", 1) },
                    { Fields.ListeningTime, new BsonDocument("$sum", $"${Fields.Duration}") },
                    { Fields.LastListening, new BsonDocument("$first", $"${Fields.Date}") },
                    { "OriginalTrackName", new BsonDocument("$first", "$NormalizedTrack") },
                    { "OriginalArtistName", new BsonDocument("$first", "$PrimaryArtist") }
                }),
                new BsonDocument("$sort", new BsonDocument(Fields.StreamCount, -1)),
                new BsonDocument("$limit", limit),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Name", "$OriginalTrackName" },
                    { Fields.Artist, "$OriginalArtistName" },
                    { Fields.StreamCount, 1 },
                    { Fields.LastListening, 1 },
                    { Fields.ListeningTime, 1 },
                    { Fields.Id, 0 }
                })
            };
        }

        private PipelineDefinition<BsonDocument, BsonDocument> BuildAlbumDetailsPipeline(string title, string artist, DateTime? from, DateTime? to)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                BuildDateFilter(from, to),
                Builders<BsonDocument>.Filter.Regex(Fields.Album, new BsonRegularExpression($"^{Regex.Escape(title)}$", "i")),
                Builders<BsonDocument>.Filter.Regex(Fields.Artist, new BsonRegularExpression($"(^|[,&]\\s*){Regex.Escape(artist)}([,&]|$)", "i")),
                BuildFieldWithContentFilter(Fields.Track)
            );

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(filter),
                new BsonDocument("$addFields", new BsonDocument("MainArtist", artist)),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", $"${Fields.Track}" },
                    { "Count", new BsonDocument("$sum", 1) },
                    { "Duration", new BsonDocument("$sum", $"${Fields.Duration}") },
                    { Fields.Album, new BsonDocument("$first", $"${Fields.Album}") },
                    { Fields.Artist, new BsonDocument("$first", "$MainArtist") }
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", new BsonDocument { { Fields.Album, $"${Fields.Album}" }, { Fields.Artist, $"${Fields.Artist}" } } },
                    { "Tracks", new BsonDocument("$push", new BsonDocument { { Fields.Track, "$_id" }, { "Count", "$Count" } }) },
                    { "TotalCount", new BsonDocument("$sum", "$Count") },
                    { "TotalDuration", new BsonDocument("$sum", "$Duration") }
                }),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Title", "$_id.Album" },
                    { Fields.Artist, "$_id.Artist" },
                    { Fields.StreamCount, "$TotalCount" },
                    { Fields.ListeningTime, "$TotalDuration" },
                    { Fields.StreamCountByTrack, "$Tracks" },
                    { Fields.Id, 0 }
                })
            };
        }

        private PipelineDefinition<BsonDocument, BsonDocument> BuildArtistDetailsPipeline(string artist, DateTime? from, DateTime? to)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                BuildDateFilter(from, to),
                Builders<BsonDocument>.Filter.Regex(Fields.Artist, new BsonRegularExpression($"(^|[,&]\\s*){Regex.Escape(artist)}([,&]|$)", "i")),
                BuildFieldWithContentFilter(Fields.Track)
            );

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(filter),
                new BsonDocument("$addFields", new BsonDocument
                {
                    { "IsMatch", new BsonDocument("$eq", new BsonArray { $"${Fields.Artist}", artist }) },
                    { "IsFeaturing", new BsonDocument("$regexMatch", new BsonDocument
                        {
                            { "input", $"${Fields.Artist}" },
                            { "regex", $"(^|,\\s*){Regex.Escape(artist)}(\\s*,|$)" },
                            { "options", "i" }
                        })
                    }
                }),
                new BsonDocument("$match", new BsonDocument("$or", new BsonArray { new BsonDocument("IsMatch", true), new BsonDocument("IsFeaturing", true) })),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", $"${Fields.Track}" },
                    { "Count", new BsonDocument("$sum", 1) },
                    { "Duration", new BsonDocument("$sum", $"${Fields.Duration}") }
                }),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", artist },
                    { "Tracks", new BsonDocument("$push", new BsonDocument { { Fields.Track, "$_id" }, { "Count", "$Count" } }) },
                    { "TotalCount", new BsonDocument("$sum", "$Count") },
                    { "TotalDuration", new BsonDocument("$sum", "$Duration") }
                }),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Name", "$_id" },
                    { Fields.StreamCount, "$TotalCount" },
                    { Fields.ListeningTime, "$TotalDuration" },
                    { Fields.StreamCountByTrack, "$Tracks" },
                    { Fields.Id, 0 }
                })
            };
        }

        #endregion

        #region Private Methods - Mapping

        // 4. Factorisation du mapping des pistes
        private static Dictionary<string, int> ExtractTrackCounts(BsonDocument doc, string fieldName = Fields.StreamCountByTrack)
        {
            if (!doc.Contains(fieldName) || !doc[fieldName].IsBsonArray)
                return new Dictionary<string, int>();

            return doc[fieldName].AsBsonArray
                .Select(t => new KeyValuePair<string, int>(t[Fields.Track].AsString, t["Count"].AsInt32))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private static AlbumListening MapBsonToAlbumListening(BsonDocument doc)
        {
            return new AlbumListening
            {
                Title = doc["Title"].AsString,
                Artist = doc[Fields.Artist].AsString,
                StreamCount = doc[Fields.StreamCount].AsInt32,
                ListeningTime = doc[Fields.ListeningTime].ToInt32(),
                StreamCountByTrack = ExtractTrackCounts(doc)
            };
        }

        private static ArtistListening MapBsonToArtistListening(BsonDocument doc)
        {
            return new ArtistListening
            {
                Name = doc["Name"].AsString,
                StreamCount = doc[Fields.StreamCount].AsInt32,
                ListeningTime = doc[Fields.ListeningTime].AsInt32,
                StreamCountByTrack = ExtractTrackCounts(doc)
            };
        }

        private static TrackListening MapBsonToTrackListening(BsonDocument doc)
        {
            return new TrackListening
            {
                Name = doc["Name"].AsString,
                Artist = doc[Fields.Artist].AsString,
                Album = string.Empty,
                StreamCount = doc[Fields.StreamCount].AsInt32,
                ListeningTime = doc[Fields.ListeningTime].AsInt32,
                LastListening = doc[Fields.LastListening].ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }

        #endregion
    }
}