using MyDeezerStats.Application.Dtos.Search;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Domain.Repositories;


namespace MyDeezerStats.Application.Services
{
    public class SearchService : ISearchService
    {
            
        private ISearchRepository _searchRepository;

        public SearchService(ISearchRepository searchRepository)
        {
            _searchRepository = searchRepository;
        }

        public async Task<List<SearchSuggestion>> SearchAsync(string query)
        {
            var albumTask = _searchRepository.GetListAlbum(query);
            var artistTask = _searchRepository.GetListArtist(query);

            await Task.WhenAll(albumTask, artistTask);

            var albums = albumTask.Result;
            var artists = artistTask.Result;

            var albumSuggestions = albums
                .SelectMany(album => album.Value.Select(artist =>
                    new SearchSuggestion
                    {
                        Title = album.Key,
                        Artist = artist,
                        Type = EntityType.Album
                    }))
                .ToList();

            var artistSuggestions = artists
                .Select(artist => new SearchSuggestion { Artist = artist, Type = EntityType.Artist })
                .ToList();

            return albumSuggestions.Concat(artistSuggestions).ToList();
        }
    }
}
