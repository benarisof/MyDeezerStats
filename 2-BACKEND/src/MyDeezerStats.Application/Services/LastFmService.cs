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
                // Utilisation de query helpers pour la sécurité
                var url = $"?method=user.getrecenttracks&user={_username}&api_key={_apiKey}&format=json&page={page}&limit=200&extended=1";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var historyResponse = JsonSerializer.Deserialize<LastFmHistoryResponse>(content);

                var tracks = historyResponse?.RecentTracks?.Tracks;
                if (tracks == null || !tracks.Any()) break;

                foreach (var track in tracks)
                {
                    if (track.ListenDate > sinceDate)
                    {
                        int.TryParse(track.Duration, out int durationSec);

                        allTracks.Add(new ListeningDto
                        {
                            Album = track.Album ?? "Unknown Album",
                            Artist = track.Artist ?? "Unknown Artist",
                            Track = track.Track ?? "Unknown Track",
                            Date = track.ListenDate.Value,
                            Duration = durationSec
                        });
                    }
                    else
                    {
                        hasMorePages = false;
                        break;
                    }
                }

                var totalPages = int.TryParse(historyResponse!.RecentTracks.Attributes?.TotalPages, out var p) ? p : 0;
                if (page >= totalPages) hasMorePages = false;
                page++;
            }

            return allTracks;
        }
    }
}

