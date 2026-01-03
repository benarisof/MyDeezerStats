using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDeezerStats.Domain.Entities.ListeningInfos
{
    public class AlbumListening
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public int StreamCount { get; set; }
        public int ListeningTime { get; set; }
        public Dictionary<string, int> StreamCountByTrack { get; set; } = [];
    }
}
