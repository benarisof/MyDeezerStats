using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MyDeezerStats.Domain.Repositories;
using MyDeezerStats.Infrastructure.Mongo.Repositories;
using MyDeezerStats.Infrastructure.Settings;
using System.Text.RegularExpressions;


namespace MyDeezerStats.Infrastructure.Mongo.Search
{
    public class SearchRepository : ISearchRepository
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<ListeningRepository> _logger;

        public SearchRepository(IConfiguration config, ILogger<ListeningRepository> logger)
        {
            var settings = config.GetSection("MongoDbSettings").Get<MongoDbSettings>();
            if (settings is null)
            {
                throw new ArgumentNullException("MongoDbSettings", "MongoDbSettings configuration section is missing.");
            }

            var client = new MongoClient(settings.ConnectionString);
            _database = client.GetDatabase(settings.DatabaseName);
            _logger = logger;
        }

        public async Task<Dictionary<string, List<string>>> GetListAlbum(string album)
        {
            var collection = _database.GetCollection<BsonDocument>("listening");
            var filter = Builders<BsonDocument>.Filter.Regex("Album", new BsonRegularExpression(Regex.Escape(album), "i"));
            var pipeline = new[]
            {
        PipelineStageDefinitionBuilder.Match(filter),
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", new BsonDocument { { "Album", "$Album" }, { "Artist", "$Artist" } } }
        })
    };

            var result = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            return result
                .GroupBy(doc => doc["_id"]["Album"].AsString)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(doc => doc["_id"]["Artist"].AsString).Distinct().ToList()
                );
        }


        public async Task<List<string>> GetListArtist(string query)
        {
            var collection = _database.GetCollection<BsonDocument>("listening");
            var filter = Builders<BsonDocument>.Filter.Regex("Artist", new BsonRegularExpression($"{Regex.Escape(query)}", "i"));
            var pipeline = new[]
            {
                PipelineStageDefinitionBuilder.Match(filter),
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$Artist" }
                })
            };
            var result = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
            return result.Select(doc => doc["_id"].AsString).ToList();
        }
    }
}
