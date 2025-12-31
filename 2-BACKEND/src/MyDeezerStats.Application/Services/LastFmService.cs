using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Application.Dtos.LastStream;
using System.Text.Json;
using Microsoft.Extensions.Options;


namespace MyDeezerStats.Application.Services
{
    public class LastFmService : ILastFmService
    {
        private readonly string _apiKey;
        private readonly string _username;
        private readonly HttpClient _httpClient;

        public LastFmService(IOptions<LastFmOptions> options, HttpClient httpClient)
        {
            var opts = options.Value;
            _apiKey = opts.ApiKey ?? throw new ArgumentNullException(nameof(opts.ApiKey));
            _username = opts.Username ?? throw new ArgumentNullException(nameof(opts.Username));
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/");
        }

        /// <summary>
        /// Récupère l'historique d'écoute depuis une date spécifique
        /// </summary>
        /// <param name="sinceDate">Date à partir de laquelle récupérer l'historique</param>
        /// <returns>Liste des pistes écoutées</returns>
        public async Task<List<ListeningDto>> GetListeningHistorySince(DateTime sinceDate)
        {
            var allTracks = new List<ListeningDto>();
            int page = 1;
            bool hasMorePages = true;

            while (hasMorePages)
            {
                var response = await _httpClient.GetAsync(
                    $"?method=user.getrecenttracks&user={_username}&api_key={_apiKey}" +
                    $"&format=json&page={page}&limit=200&extended=1");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var historyResponse = JsonSerializer.Deserialize<LastFmHistoryResponse>(content);

                if (historyResponse?.RecentTracks?.Tracks == null || historyResponse.RecentTracks.Tracks.Count == 0)
                    break;

                foreach (var track in historyResponse.RecentTracks.Tracks)
                {
                    if (track.ListenDate >= sinceDate)
                    {
                        allTracks.Add(new ListeningDto
                        {
                            Album = track.Album,
                            Artist = track.Artist,
                            Track = track.Track,
                            Date = (DateTime)track.ListenDate,
                            Duration = track.Duration 
                        });
                    }
                    else
                    {
                        hasMorePages = false;
                        break;
                    }
                }

                // Vérifier s'il y a d'autres pages
                var totalPages = int.TryParse(historyResponse.RecentTracks.Attributes?.TotalPages, out var pages) ? pages : 0;
                if (page >= totalPages)
                {
                    hasMorePages = false;
                }

                page++;
            }

            return allTracks;
        }
    }

}

