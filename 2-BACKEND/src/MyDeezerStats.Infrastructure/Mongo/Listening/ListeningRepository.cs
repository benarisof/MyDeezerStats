using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Repositories;
using MyDeezerStats.Infrastructure.Mongo.Shared;

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