using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MyDeezerStats.Application.Dtos.TopStream
{
    public class ShortArtistInfos : ApiEntity
    {
        public string Artist { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public int Count { get; set; }
        public int ListeningTime { get; set; }
    }

    public class FullArtistInfos : ApiEntity
    {
        public string Artist { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public int PlayCount { get; set; }
        public int TotalListening { get; set; }
        public List<ApiTrackInfos> TrackInfos { get; set; } = [];
        public int NbFans { get; set; }
    }
}
