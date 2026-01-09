using MyDeezerStats.Domain.Entities.ListeningInfos;


namespace MyDeezerStats.Domain.Repositories
{
    public interface IArtistRepository
    {
        Task<List<ArtistListening>> GetTopArtistsAsync(int limit, DateTime? from = null, DateTime? to = null);
        Task<ArtistListening?> GetArtistDetailsAsync(string artist, DateTime? from = null, DateTime? to = null);
    }
}
