using MyDeezerStats.Domain.Entities.ListeningInfos;

namespace MyDeezerStats.Domain.Repositories
{
    public interface ITrackRepository
    {
        Task<List<TrackListening>> GetTopTracksAsync(int limit, DateTime? from = null, DateTime? to = null);
    }
}
