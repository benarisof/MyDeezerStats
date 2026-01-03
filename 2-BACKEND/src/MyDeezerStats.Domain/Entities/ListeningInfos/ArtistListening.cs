
namespace MyDeezerStats.Domain.Entities.ListeningInfos
{
    public class ArtistListening
    {
        public string Name { get; set; } = string.Empty;
        public int StreamCount { get; set; }
        public int ListeningTime { get; set; }
        public Dictionary<string, int> StreamCountByTrack { get; set; } = [];
    }
}
