using MyDeezerStats.Application.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Net;
using MyDeezerStats.Domain.Entities.ListeningInfos;
using MyDeezerStats.Domain.Entities.DeezerApi;
using MyDeezerStats.Application.Dtos.TopStream;


namespace MyDeezerStats.Application.Services
{
    public class DeezerService : IDeezerService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DeezerService> _logger;
        private const string DeezerApiBaseUrl = "https://api.deezer.com";

        public DeezerService(HttpClient httpClient, ILogger<DeezerService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
   
        public async Task<ShortAlbumInfos> EnrichShortAlbumWithDeezerData(AlbumListening album)
        {
            if (album == null)
            {
                _logger.LogWarning("Album cannot be null");
                throw new ArgumentNullException(nameof(album));
            }

            try
            {
                // 1. Appel unique à l'API pour récupérer les infos basiques
                var deezerAlbum = await SearchAlbumOnDeezer(album.Title, album.Artist);

                if (deezerAlbum == null)
                {
                    _logger.LogInformation("Album not found on Deezer: {Title}", album.Title);
                    return new ShortAlbumInfos
                    {
                        Title = album.Title,
                        Artist = album.Artist,
                        Count = album.StreamCount,
                    };
                }

                // 2. On utilise uniquement les données de la recherche (pas d'appel supplémentaire)
                return new ShortAlbumInfos
                {
                    Title = deezerAlbum.Title ?? album.Title,
                    Artist = deezerAlbum.Artist?.Name ?? album.Artist,
                    CoverUrl = deezerAlbum.CoverXl ?? deezerAlbum.CoverBig ?? string.Empty,
                    Count = album.StreamCountByTrack?.Values.Sum() ?? 0,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching small album {Title}", album.Title);
                return new ShortAlbumInfos
                {
                    Title = album.Title,
                    Artist = album.Artist,
                    Count = album.StreamCount
                };
            }
        }

        public async Task<ShortArtistInfos> EnrichShortArtistWithDeezerData(ArtistListening artist)
        {
            if (artist == null)
            {
                _logger.LogWarning("Artist cannot be null");
                throw new ArgumentNullException(nameof(artist));
            }

            try
            {
                // 1. Appel unique à l'API pour récupérer les infos basiques
                var deezerArtist = await SearchArtistOnDeezer(artist.Name);

                if (deezerArtist == null)
                {
                    _logger.LogInformation("Artist not found on Deezer: {Artist}", artist.Name);
                    return new ShortArtistInfos
                    {
                        Artist = artist.Name,
                        Count = artist.StreamCount
                    };
                }

                var fullDetails = await GetFullArtistDetails(deezerArtist.Id);

                // 2. On utilise uniquement les données de la recherche (pas d'appel supplémentaire)
                return new ShortArtistInfos
                {
                    Artist = deezerArtist.Name,
                    CoverUrl = fullDetails?.PictureXl ?? "",
                    Count = artist.StreamCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching small artist {Artist}", artist.Name);
                return new ShortArtistInfos
                {
                    Artist = artist.Name,
                    Count = artist.StreamCount
                };
            }
        }

        public async Task<FullAlbumInfos> EnrichFullAlbumWithDeezerData(AlbumListening album)
        {
            if (album == null)
            {
                _logger.LogWarning("Album cannot be null");
                throw new ArgumentNullException(nameof(album));
            }

            try
            {
                // Recherche de l'album
                var deezerAlbum = await SearchAlbumOnDeezer(album.Title, album.Artist);
                if (deezerAlbum == null)
                {
                    _logger.LogInformation("Album not found on Deezer: {Title}", album.Title);
                    return CreateBasicFullAlbum(album);
                }

                // Récupération des détails
                var fullDetails = await GetFullAlbumDetails(deezerAlbum.Id);
                if (fullDetails == null)
                {
                    _logger.LogWarning("Album details not found for ID: {Id}", deezerAlbum.Id);
                    return CreateBasicFullAlbumWithPartialDeezerData(album, deezerAlbum);
                }

                return MapToFullAlbumInfos(album, deezerAlbum, fullDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching album {Title}", album.Title);
                return CreateBasicFullAlbum(album);
            }
        }

        public async Task<FullArtistInfos> EnrichFullArtistWithDeezerData(ArtistListening artist)
        {
            if (artist == null)
            {
                _logger.LogWarning("Artist cannot be null");
                throw new ArgumentNullException(nameof(artist));
            }

            try
            {
                // 1. Recherche de l'artiste sur Deezer
                var deezerArtist = await SearchArtistOnDeezer(artist.Name);

                if (deezerArtist == null)
                {
                    _logger.LogInformation("Artist not found on Deezer: {ArtistName}", artist.Name);
                    return CreateBasicFullArtist(artist);
                }

                // 2. Récupération des détails complets
                var artistDetails = await GetFullArtistDetails(deezerArtist.Id);

                if (artistDetails == null)
                {
                    _logger.LogWarning("Artist details not found for Deezer ID: {ArtistId}", deezerArtist.Id);
                    return CreateBasicFullArtistWithPartialDeezerData(artist, deezerArtist);
                }

                // 3. Construction du résultat final
                return MapToFullArtistInfosAsync(artist, artistDetails).Result;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error while calling Deezer API for artist {ArtistName}", artist.Name);
                return CreateBasicFullArtist(artist);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error enriching artist {ArtistName}", artist.Name);
                return CreateBasicFullArtist(artist);
            }
        }
     


        private FullAlbumInfos CreateBasicFullAlbum(AlbumListening album)
        {
            return new FullAlbumInfos
            {
                Title = album.Title,
                Artist = album.Artist,
                PlayCount = album.StreamCountByTrack.Values.Sum(),
                TrackInfos = album.StreamCountByTrack.Select(t => new ApiTrackInfos
                {
                    Track = t.Key,
                    Album = album.Title,
                    Artist = album.Artist,
                    Count = t.Value,
                    TotalListening = t.Value 
                }).ToList(),
                CoverUrl = string.Empty
            };
        }

        private FullArtistInfos CreateBasicFullArtist(ArtistListening artist)
        {
            return new FullArtistInfos
            {
                Artist = artist.Name,
                TotalListening = artist.StreamCount,
                TrackInfos = artist.StreamCountByTrack.Select(t => new ApiTrackInfos
                {
                    Track = t.Key,
                    Count = t.Value,
                    TotalListening = t.Value
                }).ToList()
            };
        }

        private FullAlbumInfos MapToFullAlbumInfos(AlbumListening album, DeezerAlbum deezerAlbum, DeezerAlbumDetails fullDetails)
        {
            // Validation des paramètres
            ArgumentNullException.ThrowIfNull(album);
            ArgumentNullException.ThrowIfNull(deezerAlbum);
            ArgumentNullException.ThrowIfNull(fullDetails);

            var trackInfos = new List<ApiTrackInfos>();
            int totalListeningTime = 0;

            // Gestion null-safe des tracks
            foreach (var deezerTrack in fullDetails.Tracks?.Data ?? Enumerable.Empty<DeezerTrack>())
            {
                int localPlayCount = 0;
                album.StreamCountByTrack?.TryGetValue(deezerTrack.Title ?? string.Empty, out localPlayCount);
                var trackListeningTime = localPlayCount * (deezerTrack?.Duration ?? 0);
                totalListeningTime += trackListeningTime;

                trackInfos.Add(new ApiTrackInfos
                {
                    Track = deezerTrack?.Title ?? "Unknown Track",
                    Album = deezerAlbum.Title ?? album.Title,
                    Artist = deezerTrack?.Artist?.Name ?? deezerAlbum.Artist?.Name ?? album.Artist,
                    TrackUrl = deezerTrack?.Preview ?? string.Empty,
                    Count = localPlayCount,
                    Duration = deezerTrack?.Duration ?? 0,
                    TotalListening = trackListeningTime
                });
            }

            return new FullAlbumInfos
            {
                Title = deezerAlbum.Title ?? album.Title,
                Artist = deezerAlbum.Artist?.Name ?? album.Artist,
                PlayCount = album.StreamCountByTrack?.Values.Sum() ?? 0,
                TotalDuration = fullDetails.Duration,
                TotalListening = totalListeningTime,
                ReleaseDate = fullDetails.ReleaseDate ?? string.Empty,
                TrackInfos = trackInfos,
                CoverUrl = deezerAlbum.CoverXl ?? deezerAlbum.CoverBig ?? string.Empty
            };
        }

        private async Task<FullArtistInfos> MapToFullArtistInfosAsync(ArtistListening artist, DeezerArtistDetails artistDetails)
        {
            ArgumentNullException.ThrowIfNull(artist);
            ArgumentNullException.ThrowIfNull(artistDetails);

            var trackTasks = artist.StreamCountByTrack
                .OrderByDescending(kvp => kvp.Value) 
                .Select( kvp => ProcessTrackAsync(kvp.Key, kvp.Value, artistDetails.Name ?? artist.Name)).ToList();

            var trackInfos = await Task.WhenAll(trackTasks);

            return new FullArtistInfos
            {
                Artist = artistDetails.Name ?? artist.Name,
                PlayCount = artist.StreamCount,
                TotalListening = trackInfos.Sum(t => t.TotalListening),
                NbFans = artistDetails.NbFan,
                TrackInfos = trackInfos.ToList(),
                CoverUrl = artistDetails.PictureBig ?? artistDetails.Picture ?? string.Empty
            };
        }

        private async Task<ApiTrackInfos> ProcessTrackAsync(string trackName, int playCount, string artistName)
        {
            var trackDetail = await GetTrackFromDeezer(trackName, artistName);
            int trackDuration = trackDetail?.Duration ?? 0;
            int trackListeningTime = playCount * trackDuration;

            return new ApiTrackInfos
            {
                Track = trackName,
                Artist = artistName,
                Count = playCount,
                TrackUrl = trackDetail?.CoverUrl ?? string.Empty,
                Duration = trackDuration,
                TotalListening = trackListeningTime,
                Album = trackDetail?.Album?.Title ?? string.Empty
            };
        }

        private FullAlbumInfos CreateBasicFullAlbumWithPartialDeezerData(AlbumListening album, DeezerAlbum deezerAlbum)
        {
            return new FullAlbumInfos
            {
                Title = deezerAlbum.Title ?? album.Title,
                Artist = deezerAlbum.Artist?.Name ?? album.Artist,
                PlayCount = album.StreamCountByTrack?.Values.Sum() ?? 0,
                CoverUrl = deezerAlbum.CoverXl ?? deezerAlbum.CoverBig ?? string.Empty,
                TrackInfos = album.StreamCountByTrack?
                    .Select(t => new ApiTrackInfos
                    {
                        Track = t.Key,
                        Album = deezerAlbum.Title ?? album.Title,
                        Artist = deezerAlbum.Artist?.Name ?? album.Artist,
                        Count = t.Value
                    }).ToList() ?? new List<ApiTrackInfos>()
            };
        }

        private FullArtistInfos CreateBasicFullArtistWithPartialDeezerData(ArtistListening artist, DeezerArtist deezerArtist)
        {
            return new FullArtistInfos
            {
                Artist = deezerArtist.Name ?? artist.Name,
                CoverUrl = deezerArtist.PictureBig ?? deezerArtist.Picture ?? string.Empty,
                PlayCount = artist.StreamCount,
                TotalListening = artist.StreamCount,
                NbFans = deezerArtist.NbFan,
                TrackInfos = artist.StreamCountByTrack?.Select(t => new ApiTrackInfos
                {
                    Track = t.Key,
                    Artist = deezerArtist.Name ?? artist.Name,
                    Count = t.Value,
                    TotalListening = t.Value
                }).ToList() ?? new List<ApiTrackInfos>()
            };
        }

        private async Task<DeezerAlbum?> SearchAlbumOnDeezer(string title, string artist)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{DeezerApiBaseUrl}/search/album?q=artist:\"{Uri.EscapeDataString(artist)}\" album:\"{Uri.EscapeDataString(title)}\"&limit=10");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadFromJsonAsync<DeezerSearchResponse<DeezerAlbum>>();

                // Filtrage manuel pour une correspondance exacte du titre
                return content?.Data?
                    .FirstOrDefault(album =>
                        album.Title?.Equals(title, StringComparison.OrdinalIgnoreCase) == true);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error searching album {title} on Deezer", title);
                // Retourner null ou une valeur par défaut, ou relancer une exception personnalisée
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching album {Title} by {Artist} on Deezer", title, artist);
                return null;
            }
        }




        private async Task<DeezerArtist?> SearchArtistOnDeezer(string artistName)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"{DeezerApiBaseUrl}/search/artist?q={Uri.EscapeDataString(artistName)}&limit=1");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadFromJsonAsync<DeezerSearchResponse<DeezerArtist>>();
                return content?.Data?.FirstOrDefault();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error searching artist {ArtistName} on Deezer", artistName);
                // Retourner null ou une valeur par défaut, ou relancer une exception personnalisée
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching artist {ArtistName} on Deezer", artistName);
                return null;
            }
        }


        private async Task<DeezerAlbumDetails?> GetFullAlbumDetails(int albumId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{DeezerApiBaseUrl}/album/{albumId}");
                response.EnsureSuccessStatusCode();

                // Debug: affichez la réponse brute
                var rawResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Deezer API Response: {Response}", rawResponse);

                return JsonSerializer.Deserialize<DeezerAlbumDetails>(rawResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting details for album ID {AlbumId}", albumId);
                return null;
            }
        }

        private async Task<DeezerArtistDetails?> GetFullArtistDetails(long artistId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{DeezerApiBaseUrl}/artist/{artistId}");
                response.EnsureSuccessStatusCode();

                // Debug: affichez la réponse brute
                var rawResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Deezer API Response: {Response}", rawResponse);

                return JsonSerializer.Deserialize<DeezerArtistDetails>(rawResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting details for artist ID {ArtistId}", artistId);
                return null;
            }
        }

        public async Task<DeezerTrack?> GetTrackFromDeezer(string trackName, string artistName)
        {
            if (string.IsNullOrWhiteSpace(trackName))
            {
                _logger.LogWarning("Track name cannot be null or empty");
                return null;
            }

            try
            {
                // Construction et appel API
                var encodedTrack = WebUtility.UrlEncode(trackName);
                var encodedArtist = WebUtility.UrlEncode(artistName);
                var url = $"{DeezerApiBaseUrl}/search?q=artist:\"{encodedArtist}\" track:\"{encodedTrack}\"&limit=1";

                using var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                // Désérialisation
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
                    return null;

                var firstResult = data[0];

                // Mapping direct sans méthodes helpers
                return new DeezerTrack
                {
                    Title = firstResult.TryGetProperty("title", out var title)
                           ? title.GetString() ?? trackName
                           : trackName,

                    Duration = firstResult.TryGetProperty("duration", out var duration)
                              ? duration.GetInt32()
                              : 0,

                    CoverUrl = firstResult.TryGetProperty("album", out var album)
                             && album.TryGetProperty("cover_big", out var cover)
                              ? cover.GetString() ?? string.Empty
                              : string.Empty,
                    Album = new DeezerAlbum {Title = firstResult.TryGetProperty("album", out var albumElement)
                            ? albumElement.GetProperty("title").GetString() ?? string.Empty
                            : string.Empty}
                };
            }
            catch
            {
                return null; 
            }
        }

        public async Task<ApiTrackInfos> EnrichTrackWithDeezerData(TrackListening track)
        {
            if (track == null)
            {
                _logger.LogWarning("Album cannot be null");
                throw new ArgumentNullException(nameof(track));
            }

            var fullTrack = new ApiTrackInfos
            {
                Artist = track.Artist,
                Track = track.Name,
                Album = track.Album,
                Count = track.StreamCount,
                LastListen = track.LastListening
            };

            try
            {
                var deezerTrack = await GetTrackFromDeezer(track.Name, track.Artist);
                if (deezerTrack == null)
                {
                    _logger.LogInformation("Track not found on Deezer: {Title}", track.Name);
                    return fullTrack;
                }
                fullTrack.Duration = deezerTrack.Duration;
                fullTrack.TrackUrl = deezerTrack.CoverUrl;
                fullTrack.TotalListening = track.StreamCount * fullTrack.Duration;
                return fullTrack;
               
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching track {Title}", track.Name);
                return fullTrack;
            }
        }
    }
}
