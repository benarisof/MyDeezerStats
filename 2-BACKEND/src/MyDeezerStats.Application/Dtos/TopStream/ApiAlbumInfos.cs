namespace MyDeezerStats.Application.Dtos.TopStream
{

    public class ShortAlbumInfos : ApiEntity
    {
        public string Title {  get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public int Count { get; set; }
        public int ListeningTime { get; set; }
    }


    public class FullAlbumInfos : ApiEntity
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public int PlayCount { get; set; }
        public int TotalDuration { get; set; }
        public int TotalListening { get; set; }
        public string ReleaseDate { get; set; } = string.Empty;
        public List<ApiTrackInfos> TrackInfos { get; set; } = [];
        public string CoverUrl { get; set; } = string.Empty;
    }
}
