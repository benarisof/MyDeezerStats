
namespace MyDeezerStats.Domain.Entities.ListeningInfos
{
    public class TrackListening
    {
        public string Name { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;   
        public string LastListening { get; set; }  = string.Empty;
        public int ListeningTime { get; set; }
        public int StreamCount { get; set; } 
    }
}
