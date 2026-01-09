using MyDeezerStats.Application.Dtos.LastStream;
using MyDeezerStats.Application.Dtos.TopStream;

namespace MyDeezerStats.Application.Interfaces
{
    public interface IOrchestratorService
    {
        Task<List<ShortAlbumInfos>> GetTopAlbumsAsync(DateTime? from, DateTime? to, int nb = 5);
        Task<FullAlbumInfos> GetAlbumAsync(string identifier);
        Task<List<ShortArtistInfos>> GetTopArtistsAsync(DateTime? from, DateTime? to, int nb = 5);
        Task<FullArtistInfos> GetArtistAsync(string identifier);
        Task<List<ApiTrackInfos>> GetTopTracksAsync(DateTime? from, DateTime? to, int nb = 5);
        Task<IEnumerable<ListeningDto>> GetLatestListeningsAsync(int limit = 100);

    }
}
