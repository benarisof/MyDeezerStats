using MyDeezerStats.Domain.Entities.ListeningInfos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDeezerStats.Domain.Repositories
{
    public interface IAlbumRepository
    {
        Task<List<AlbumListening>> GetTopAlbumsAsync(int limit, DateTime? from = null, DateTime? to = null);
        Task<AlbumListening?> GetAlbumDetailsAsync(string title, string artist, DateTime? from = null, DateTime? to = null);
    }
}
