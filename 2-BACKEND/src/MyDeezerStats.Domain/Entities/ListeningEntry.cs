using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;


namespace MyDeezerStats.Domain.Entities
{
    public class ListeningEntry
    {

        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string Track { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;    
        public DateTime Date { get; set; }
    }
}
