
using MyDeezerStats.Domain.Entities;
using System.Threading.Tasks;


namespace MyDeezerStats.Domain.Repositories
{
    public interface IListeningRepository
    {
        Task<List<ListeningEntry>> GetRecentListeningsAsync(int limit);
        Task InsertListeningsAsync(List<ListeningEntry> listenings);
        Task<ListeningEntry?> GetLastEntryAsync();

        Task<Dictionary<string, int>> GetBatchStreamCountsAsync(IEnumerable<ListeningEntry> entries);
    }
}
