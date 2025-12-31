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
        private const string CollectionName = "listening";

        public ListeningRepository(IMongoDatabase database, ILogger<ListeningRepository> logger)
        {
            _collection = database.GetCollection<ListeningEntry>(CollectionName);
            _bsonCollection = database.GetCollection<BsonDocument>(CollectionName);
            _logger = logger;
        }

        public async Task<List<ListeningEntry>> GetLatestListeningsAsync(int limit)
        {
            if (limit <= 0 || limit > 1000)
            {
                _logger.LogWarning("Invalid limit parameter: {Limit}, using default 100", limit);
                limit = 100;
            }

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
                    // Filtre sur Track, Artist, Album et Date
                    var filter = Builders<ListeningEntry>.Filter.And(
                        Builders<ListeningEntry>.Filter.Eq(x => x.Track, listening.Track),
                        Builders<ListeningEntry>.Filter.Eq(x => x.Artist, listening.Artist),
                        Builders<ListeningEntry>.Filter.Eq(x => x.Album, listening.Album),
                        Builders<ListeningEntry>.Filter.Eq(x => x.Date, listening.Date)
                    );

                    // Mise à jour des champs
                    var update = Builders<ListeningEntry>.Update
                        .Set(x => x.Track, listening.Track)
                        .Set(x => x.Artist, listening.Artist)
                        .Set(x => x.Album, listening.Album)
                        .Set(x => x.Duration, listening.Duration)
                        .Set(x => x.Date, listening.Date)
                        .SetOnInsert(x => x.Id, string.IsNullOrEmpty(listening.Id)
                            ? ObjectId.GenerateNewId().ToString()
                            : listening.Id);

                    var operation = new UpdateOneModel<ListeningEntry>(filter, update)
                    {
                        IsUpsert = true
                    };

                    operations.Add(operation);
                }

                if (operations.Any())
                {
                    var result = await _collection.BulkWriteAsync(operations, new BulkWriteOptions
                    {
                        IsOrdered = false
                    });

                    _logger.LogInformation(
                        "Bulk insert/update completed: {Matched} matched, {Modified} modified, {Upserts} upserted",
                        result.MatchedCount, result.ModifiedCount, result.Upserts.Count);
                }
            }
            catch (MongoBulkWriteException bulkEx)
            {
                _logger.LogWarning(bulkEx, "Partial failure during bulk insert/update of {Count} listenings", listenings.Count);
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

                var albumListenings = results.Select(MapBsonToAlbumListening).ToList();
                _logger.LogDebug("Retrieved {Count} top albums", albumListenings.Count);
                return albumListenings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top {Limit} albums from {From} to {To}", limit, from, to);
                throw new ApplicationException("Erreur lors de la récupération des albums les plus écoutés", ex);
            }
        }

        public async Task<AlbumListening?> GetAlbumsWithAsync(string title, string artist, DateTime? from = null, DateTime? to = null)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            {
                throw new ArgumentException("Le titre et l'artiste sont requis");
            }

            try
            {
                var pipeline = BuildAlbumDetailsPipeline(title, artist, from, to);
                var result = await _bsonCollection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

                if (result == null)
                {
                    _logger.LogDebug("Album not found: {Title} by {Artist}", title, artist);
                    return null;
                }

                return MapBsonToAlbumListening(result);
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

                var artistListenings = results.Select(MapBsonToArtistListening).ToList();
                _logger.LogDebug("Retrieved {Count} top artists", artistListenings.Count);
                return artistListenings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top {Limit} artists from {From} to {To}", limit, from, to);
                throw new ApplicationException("Erreur lors de la récupération des artistes les plus écoutés", ex);
            }
        }

        public async Task<ArtistListening?> GetArtistWithAsync(string artist, DateTime? from = null, DateTime? to = null)
        {
            if (string.IsNullOrWhiteSpace(artist))
            {
                throw new ArgumentException("Le nom de l'artiste est requis");
            }

            try
            {
                var pipeline = BuildArtistDetailsPipeline(artist, from, to);
                var result = await _bsonCollection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync();

                if (result == null)
                {
                    _logger.LogDebug("Artist not found: {Artist}", artist);
                    return null;
                }

                return MapBsonToArtistListening(result);
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

                var trackListenings = results.Select(MapBsonToTrackListening).ToList();
                _logger.LogDebug("Retrieved {Count} top tracks", trackListenings.Count);
                return trackListenings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top {Limit} tracks from {From} to {To}", limit, from, to);
                throw new ApplicationException("Erreur lors de la récupération des morceaux les plus écoutés", ex);
            }
        }

        #region Private Methods - Validation and Filter Building

        private static int ValidateLimit(int limit)
        {
            return limit <= 0 || limit > 100 ? 10 : limit;
        }

        private FilterDefinition<BsonDocument> BuildDateFilter(DateTime? from, DateTime? to)
        {
            var builder = Builders<BsonDocument>.Filter;
            var filter = builder.Empty;

            if (from.HasValue)
            {
                filter &= builder.Gte("Date", from.Value);
            }
            if (to.HasValue)
            {
                filter &= builder.Lte("Date", to.Value);
            }

            return filter;
        }

        private FilterDefinition<BsonDocument> BuildCompleteAlbumFilter(DateTime? from, DateTime? to)
        {
            var dateFilter = BuildDateFilter(from, to);

            return Builders<BsonDocument>.Filter.And(
                dateFilter,
                Builders<BsonDocument>.Filter.Exists("Album"),
                Builders<BsonDocument>.Filter.Ne("Album", BsonNull.Value),
                Builders<BsonDocument>.Filter.Ne("Album", ""),
                Builders<BsonDocument>.Filter.Exists("Track"),
                Builders<BsonDocument>.Filter.Ne("Track", BsonNull.Value),
                Builders<BsonDocument>.Filter.Ne("Track", "")
            );
        }

        private FilterDefinition<BsonDocument> BuildCompleteArtistFilter(DateTime? from, DateTime? to)
        {
            var dateFilter = BuildDateFilter(from, to);

            return Builders<BsonDocument>.Filter.And(
                dateFilter,
                Builders<BsonDocument>.Filter.Exists("Artist"),
                Builders<BsonDocument>.Filter.Ne("Artist", BsonNull.Value),
                Builders<BsonDocument>.Filter.Ne("Artist", "")
            );
        }

        private FilterDefinition<BsonDocument> BuildCompleteTrackFilter(DateTime? from, DateTime? to)
        {
            var dateFilter = BuildDateFilter(from, to);

            return Builders<BsonDocument>.Filter.And(
                dateFilter,
                Builders<BsonDocument>.Filter.Exists("Track"),
                Builders<BsonDocument>.Filter.Ne("Track", BsonNull.Value),
                Builders<BsonDocument>.Filter.Ne("Track", ""),
                Builders<BsonDocument>.Filter.Exists("Artist"),
                Builders<BsonDocument>.Filter.Ne("Artist", BsonNull.Value),
                Builders<BsonDocument>.Filter.Ne("Artist", ""),
                Builders<BsonDocument>.Filter.Exists("Date")
            );
        }

        #endregion

        #region Private Methods - Pipeline Building

        private PipelineDefinition<BsonDocument, BsonDocument> BuildTopAlbumsPipeline(int limit, DateTime? from, DateTime? to)
        {
            var completeFilter = BuildCompleteAlbumFilter(from, to);

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(completeFilter),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Album", 1 },
                    { "Artist", 1 },
                    { "NormalizedAlbum", new BsonDocument("$toLower",
                        new BsonDocument("$trim", new BsonDocument("input", "$Album"))) },
                    { "NormalizedArtist", new BsonDocument("$toLower",
                        new BsonDocument("$trim", new BsonDocument("input", "$Artist"))) }
                }),
                new BsonDocument("$addFields", new BsonDocument("PrimaryArtist",
                    new BsonDocument("$let", new BsonDocument
                    {
                        { "vars", new BsonDocument("artists",
                            new BsonDocument("$split", new BsonArray { "$NormalizedArtist", "," })) },
                        { "in", new BsonDocument("$trim", new BsonDocument("input",
                            new BsonDocument("$cond", new BsonArray
                            {
                                new BsonDocument("$gt", new BsonArray {
                                    new BsonDocument("$size", "$$artists"), 0 }),
                                new BsonDocument("$arrayElemAt", new BsonArray { "$$artists", 0 }),
                                ""
                            }))) }
                    }))
                ),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", new BsonDocument
                            {
                                { "Album", "$NormalizedAlbum" },
                                { "Artist", "$PrimaryArtist" }
                            }
                    },
                    { "TotalCount", new BsonDocument("$sum", 1) },
                    { "OriginalTitle", new BsonDocument("$first", "$Album") },
                    { "OriginalArtist", new BsonDocument("$first", "$Artist") }
                }),
                new BsonDocument("$sort", new BsonDocument("TotalCount", -1)),
                new BsonDocument("$limit", limit),
                new BsonDocument("$project", new BsonDocument
                {
                    { "Title", "$OriginalTitle" },
                    { "Artist", "$OriginalArtist" },
                    { "StreamCount", "$TotalCount" },
                    { "_id", 0 }
                })
            };
        }

        private PipelineDefinition<BsonDocument, BsonDocument> BuildAlbumDetailsPipeline(string title, string artist, DateTime? from, DateTime? to)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                BuildDateFilter(from, to),
                Builders<BsonDocument>.Filter.Regex("Album", new BsonRegularExpression($"^{Regex.Escape(title)}$", "i")),
                Builders<BsonDocument>.Filter.Regex("Artist",
                    new BsonRegularExpression($"(^|[,&]\\s*){Regex.Escape(artist)}([,&]|$)", "i")),
                Builders<BsonDocument>.Filter.Exists("Track"),
                Builders<BsonDocument>.Filter.Ne("Track", "")
            );

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(filter),
                new BsonDocument("$addFields",
                    new BsonDocument("MainArtist", artist)),
                new BsonDocument("$group",
                    new BsonDocument
                    {
                        { "_id", "$Track" },
                        { "Count", new BsonDocument("$sum", 1) },
                        { "Album", new BsonDocument("$first", "$Album") },
                        { "Artist", new BsonDocument("$first", "$MainArtist") }
                    }),
                new BsonDocument("$group",
                    new BsonDocument
                    {
                        { "_id", new BsonDocument
                            {
                                { "Album", "$Album" },
                                { "Artist", "$Artist" }
                            }
                        },
                        { "Tracks", new BsonDocument("$push",
                            new BsonDocument
                            {
                                { "Track", "$_id" },
                                { "Count", "$Count" }
                            })},
                        { "TotalCount", new BsonDocument("$sum", "$Count") }
                    }),
                 new BsonDocument("$project",
                    new BsonDocument
                    {
                        { "Title", "$_id.Album" },
                        { "Artist", "$_id.Artist" },
                        { "StreamCount", "$TotalCount" },
                        { "StreamCountByTrack", "$Tracks" },
                        { "_id", 0 }
                    })
            };
        }

        private PipelineDefinition<BsonDocument, BsonDocument> BuildTopArtistsPipeline(int limit, DateTime? from, DateTime? to)
        {
            var completeFilter = BuildCompleteArtistFilter(from, to);

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(completeFilter),
                new BsonDocument("$project",
                    new BsonDocument
                    {
                        { "Artist", 1 },
                        { "NormalizedArtist", new BsonDocument("$trim", new BsonDocument("input", "$Artist")) }
                    }),
                new BsonDocument("$addFields",
                    new BsonDocument("PrimaryArtist",
                        new BsonDocument("$let",
                            new BsonDocument
                            {
                                { "vars",
                                    new BsonDocument("artists",
                                    new BsonDocument("$split", new BsonArray { "$NormalizedArtist", "," }))
                                },
                                { "in",
                                    new BsonDocument("$trim",
                                        new BsonDocument("input",
                                            new BsonDocument("$cond",
                                                new BsonArray
                                                {
                                                    new BsonDocument("$gt", new BsonArray { new BsonDocument("$size", "$$artists"), 0 }),
                                                    new BsonDocument("$arrayElemAt", new BsonArray { "$$artists", 0 }),
                                                    ""
                                                }
                                            )))
                                }
                            }
                        ))),
                new BsonDocument("$addFields",
                    new BsonDocument("PrimaryArtistNormalized",
                        new BsonDocument("$toLower", "$PrimaryArtist"))),
                new BsonDocument("$group",
                    new BsonDocument
                    {
                        { "_id", "$PrimaryArtistNormalized" },
                        { "TotalCount", new BsonDocument("$sum", 1) },
                        { "OriginalName", new BsonDocument("$first", "$PrimaryArtist") }
                    }),
                new BsonDocument("$sort", new BsonDocument("TotalCount", -1)),
                new BsonDocument("$limit", limit),
                new BsonDocument("$project",
                    new BsonDocument
                    {
                        { "Name", "$OriginalName" },
                        { "StreamCount", "$TotalCount" },
                        { "_id", 0 }
                    })
            };
        }

        private PipelineDefinition<BsonDocument, BsonDocument> BuildArtistDetailsPipeline(string artist, DateTime? from, DateTime? to)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                BuildDateFilter(from, to),
                Builders<BsonDocument>.Filter.Regex("Artist", new BsonRegularExpression($"(^|[,&]\\s*){Regex.Escape(artist)}([,&]|$)", "i")),
                Builders<BsonDocument>.Filter.Exists("Track"),
                Builders<BsonDocument>.Filter.Ne("Track", "")
            );

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(filter),
                new BsonDocument("$addFields",
                    new BsonDocument
                    {
                        { "IsMatch",
                            new BsonDocument("$eq",
                                new BsonArray {
                                    "$Artist",
                                    artist
                                })
                        },
                        { "IsFeaturing",
                            new BsonDocument("$regexMatch",
                                new BsonDocument
                                {
                                    { "input", "$Artist" },
                                    { "regex", $"(^|,\\s*){Regex.Escape(artist)}(\\s*,|$)" },
                                    { "options", "i" }
                                })
                        }
                    }
                ),
                new BsonDocument("$match",
                    new BsonDocument("$or", new BsonArray
                    {
                        new BsonDocument("IsMatch", true),
                        new BsonDocument("IsFeaturing", true)
                    })
                ),
                new BsonDocument("$group",
                    new BsonDocument
                    {
                        { "_id", "$Track" },
                        { "Count", new BsonDocument("$sum", 1) }
                    }
                ),
                new BsonDocument("$group",
                    new BsonDocument
                    {
                        { "_id", artist },
                        { "Tracks", new BsonDocument("$push",
                            new BsonDocument
                            {
                                { "Track", "$_id" },
                                { "Count", "$Count" }
                            })
                        },
                        { "TotalCount", new BsonDocument("$sum", "$Count") }
                    }
                ),
                new BsonDocument("$project",
                    new BsonDocument
                    {
                        { "Name", "$_id" },
                        { "StreamCount", "$TotalCount" },
                        { "StreamCountByTrack", "$Tracks" },
                        { "_id", 0 }
                    }
                )
            };
        }

        private PipelineDefinition<BsonDocument, BsonDocument> BuildTopTracksPipeline(int limit, DateTime? from, DateTime? to)
        {
            var completeFilter = BuildCompleteTrackFilter(from, to);

            return new[]
            {
                PipelineStageDefinitionBuilder.Match(completeFilter),
                new BsonDocument("$addFields",
                    new BsonDocument
                    {
                        { "NormalizedTrack", new BsonDocument("$trim", new BsonDocument("input", "$Track")) },
                        { "PrimaryArtist", new BsonDocument("$trim",
                            new BsonDocument("input",
                                new BsonDocument("$arrayElemAt",
                                    new BsonArray { new BsonDocument("$split", new BsonArray { "$Artist", "," }), 0 }))) }
                    }),
                new BsonDocument("$addFields",
                    new BsonDocument
                    {
                        { "NormalizedTrackLower", new BsonDocument("$toLower", "$NormalizedTrack") },
                        { "NormalizedArtistLower", new BsonDocument("$toLower", "$PrimaryArtist") }
                    }),
                new BsonDocument("$sort", new BsonDocument("Date", -1)),
                new BsonDocument("$group",
                    new BsonDocument
                    {
                        { "_id", new BsonDocument
                            {
                                { "Track", "$NormalizedTrackLower" },
                                { "Artist", "$NormalizedArtistLower" }
                            }
                        },
                        { "StreamCount", new BsonDocument("$sum", 1) },
                        { "LastListening", new BsonDocument("$first", "$Date") },
                        { "OriginalTrackName", new BsonDocument("$first", "$NormalizedTrack") },
                        { "OriginalArtistName", new BsonDocument("$first", "$PrimaryArtist") }
                    }),
                new BsonDocument("$sort", new BsonDocument("StreamCount", -1)),
                new BsonDocument("$limit", limit),
                new BsonDocument("$project",
                    new BsonDocument
                    {
                        { "Name", "$OriginalTrackName" },
                        { "Artist", "$OriginalArtistName" },
                        { "StreamCount", 1 },
                        { "LastListening", 1 },
                        { "_id", 0 }
                    })
            };
        }

        #endregion

        #region Private Methods - Mapping

        private static AlbumListening MapBsonToAlbumListening(BsonDocument doc)
        {
            return new AlbumListening
            {
                Title = doc["Title"].AsString,
                Artist = doc["Artist"].AsString,
                StreamCount = doc["StreamCount"].AsInt32,
                StreamCountByTrack = doc["StreamCountByTrack"]?.AsBsonArray?
                    .Select(t => new KeyValuePair<string, int>(
                        t["Track"].AsString,
                        t["Count"].AsInt32))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, int>()
            };
        }

        private static ArtistListening MapBsonToArtistListening(BsonDocument doc)
        {
            return new ArtistListening
            {
                Name = doc["Name"].AsString,
                StreamCount = doc["StreamCount"].AsInt32,
                StreamCountByTrack = doc["StreamCountByTrack"]?.AsBsonArray?
                    .Select(t => new KeyValuePair<string, int>(
                        t["Track"].AsString,
                        t["Count"].AsInt32))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, int>()
            };
        }

        private static TrackListening MapBsonToTrackListening(BsonDocument doc)
        {
            return new TrackListening
            {
                Name = doc["Name"].AsString,
                Artist = doc["Artist"].AsString,
                Album = string.Empty,
                StreamCount = doc["StreamCount"].AsInt32,
                LastListening = doc["LastListening"].ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }

        #endregion
    }
}