using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Repositories;
using MyDeezerStats.Infrastructure.Mongo.Shared;
using System.Text.RegularExpressions;

namespace MyDeezerStats.Infrastructure.Mongo.Repositories
{
    public class ListeningRepository : IListeningRepository
    {
        private readonly IMongoCollection<ListeningEntry> _collection;
        private readonly ILogger<ListeningRepository> _logger;

        public ListeningRepository(IMongoDatabase database, ILogger<ListeningRepository> logger)
        {
            _collection = database.GetCollection<ListeningEntry>(DbFields.CollectionName);
            _logger = logger;
        }

        public async Task<ListeningEntry?> GetLastEntryAsync()
        {
            return await _collection
                .Find(FilterDefinition<ListeningEntry>.Empty)
                .SortByDescending(x => x.Date)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ListeningEntry>> GetRecentListeningsAsync(int limit)
        {
            limit = QueryHelper.ValidateLimit(limit, 1000, 100);
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

        public async Task<Dictionary<string, int>> GetBatchStreamCountsAsync(IEnumerable<ListeningEntry> entries)
        {
            try
            {
                // On récupère les couples uniques pour limiter la taille du filtre OR
                var uniqueEntries = entries
                    .GroupBy(e => new { e.Artist, e.Track })
                    .Select(g => g.First());

                var filters = uniqueEntries.Select(e => Builders<ListeningEntry>.Filter.And(
                    Builders<ListeningEntry>.Filter.Regex(x => x.Track, new BsonRegularExpression($"^{Regex.Escape(e.Track)}$", "i")),
                    Builders<ListeningEntry>.Filter.Regex(x => x.Artist, new BsonRegularExpression($"^{Regex.Escape(e.Artist)}$", "i"))
                ));

                var combinedFilter = Builders<ListeningEntry>.Filter.Or(filters);

                var results = await _collection.Aggregate()
                    .Match(combinedFilter)
                    .Group(new BsonDocument
                    {
                { "_id", new BsonDocument { 
                    { "t", new BsonDocument("$toLower", new BsonDocument("$trim", new BsonDocument("input", "$Track"))) },
                    { "a", new BsonDocument("$toLower", new BsonDocument("$trim", new BsonDocument("input", "$Artist"))) }
                }},
                { "count", new BsonDocument("$sum", 1) }
                    })
                    .ToListAsync();

                return results.ToDictionary(
                    // On s'assure que la clé est propre (Trim déjà fait dans l'agrégation)
                    x => $"{x["_id"]["a"].AsString}|{x["_id"]["t"].AsString}",
                    x => x["count"].AsInt32
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BatchStreamCounts");
                return new Dictionary<string, int>();
            }
        }

        public async Task InsertListeningsAsync(List<ListeningEntry> listenings)
        {
            if (listenings == null || !listenings.Any()) return;

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
                    _logger.LogInformation("Bulk op: {Matched} matched, {Upserts} upserted", result.MatchedCount, result.Upserts.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk insert/update");
                throw new ApplicationException("Erreur lors de l'insertion des écoutes", ex);
            }
        }
    }
}