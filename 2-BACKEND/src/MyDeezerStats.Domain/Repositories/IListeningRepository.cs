

using MongoDB.Bson;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Entities.ListeningInfos;


namespace MyDeezerStats.Domain.Repositories
{
    public interface IListeningRepository
    {
        Task<List<AlbumListening>> GetTopAlbumsWithAsync(int limit, DateTime? from = null, DateTime? to = null);
        Task<AlbumListening?> GetAlbumsWithAsync(string title, string artist, DateTime? from = null, DateTime? to = null);


        Task<List<ArtistListening>> GetTopArtistWithAsync(int limit, DateTime? from = null, DateTime? to = null);
        Task<ArtistListening?> GetArtistWithAsync(string artist, DateTime? from = null, DateTime? to = null);

        Task<List<TrackListening>> GetTopTrackWithAsync(int limit, DateTime? from, DateTime? to);


        Task<List<ListeningEntry>> GetLatestListeningsAsync(int limit);

        Task InsertListeningsAsync(List<ListeningEntry> listenings);
        Task<ListeningEntry?> GetLastEntryAsync();
    }
}
