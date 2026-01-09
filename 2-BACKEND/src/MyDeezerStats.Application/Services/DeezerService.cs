using MyDeezerStats.Application.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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

        // Sémaphore pour limiter les appels parallèles (5 requêtes max en même temps)
        private readonly SemaphoreSlim _apiThrottler = new SemaphoreSlim(5);

        public DeezerService(HttpClient httpClient, ILogger<DeezerService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        #region Short Infos Enrichment

        public async Task<ShortAlbumInfos> EnrichShortAlbumWithDeezerData(AlbumListening album)
        {
            if (album == null) throw new ArgumentNullException(nameof(album));

            try
            {
                var deezerAlbum = await SearchAlbumOnDeezer(album.Title, album.Artist);

                return new ShortAlbumInfos
                {
                    Title = album.Title,
                    Artist = deezerAlbum?.Artist.Name ?? album.Artist,
                    CoverUrl = deezerAlbum?.CoverXl ?? deezerAlbum?.CoverBig ?? string.Empty,
                    Count = album.StreamCount,
                    ListeningTime = album.ListeningTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching small album {Title}", album.Title);
                return new ShortAlbumInfos { Title = album.Title, Artist = album.Artist, Count = album.StreamCount, ListeningTime = album.ListeningTime };
            }
        }

        public async Task<ShortArtistInfos> EnrichShortArtistWithDeezerData(ArtistListening artist)
        {
            if (artist == null) throw new ArgumentNullException(nameof(artist));

            try
            {
                var deezerArtist = await SearchArtistOnDeezer(artist.Name);

                return new ShortArtistInfos
                {
                    Artist = artist.Name,
                    CoverUrl = deezerArtist?.PictureXl ?? deezerArtist?.PictureBig ?? string.Empty,
                    Count = artist.StreamCount,
                    ListeningTime = artist.ListeningTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching small artist {Artist}", artist.Name);
                return new ShortArtistInfos { Artist = artist.Name, Count = artist.StreamCount, ListeningTime = artist.ListeningTime };
            }
        }

        #endregion

        #region Full Infos Enrichment

        public async Task<FullAlbumInfos> EnrichFullAlbumWithDeezerData(AlbumListening album)
        {
            if (album == null) throw new ArgumentNullException(nameof(album));

            try
            {
                var deezerAlbum = await SearchAlbumOnDeezer(album.Title, album.Artist);
                if (deezerAlbum == null)
                {
                    return CreateBasicFullAlbum(album);
                }

                var fullDetails = await GetFullAlbumDetails(deezerAlbum.Id);
                if (fullDetails == null)
                {
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
            if (artist == null) throw new ArgumentNullException(nameof(artist));

            try
            {
                var deezerArtist = await SearchArtistOnDeezer(artist.Name);
                if (deezerArtist == null)
                {
                    return CreateBasicFullArtist(artist);
                }

                var artistDetails = await GetFullArtistDetails(deezerArtist.Id);
                if (artistDetails == null)
                {
                    return CreateBasicFullArtistWithPartialDeezerData(artist, deezerArtist);
                }

                // Utilisation de await ici, plus de .Result !
                return await MapToFullArtistInfosAsync(artist, artistDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching artist {ArtistName}", artist.Name);
                return CreateBasicFullArtist(artist);
            }
        }

        public async Task<ApiTrackInfos> EnrichTrackWithDeezerData(TrackListening track)
        {
            if (track == null) throw new ArgumentNullException(nameof(track));

            // Initialisation avec les données Mongo (y compris le temps réel d'écoute)
            var fullTrack = new ApiTrackInfos
            {
                Artist = track.Artist,
                Track = track.Name,
                Album = track.Album,
                Count = track.StreamCount,
                LastListen = track.LastListening,
                TotalListening = track.ListeningTime 
            };

            try
            {
                var deezerTrack = await GetTrackFromDeezer(track.Name, track.Artist);

                if (deezerTrack != null)
                {
                    fullTrack.Duration = deezerTrack.Duration;
                    fullTrack.TrackUrl = deezerTrack.CoverUrl; // Attention: DeezerTrack search ne retourne pas toujours l'URL preview, parfois Cover. À vérifier selon ton DTO.
                    fullTrack.Album = string.IsNullOrEmpty(fullTrack.Album) ? deezerTrack.Album?.Title ?? "" : fullTrack.Album;
                }

                // Fallback : Si le ListeningTime Mongo est 0 (ex: anciennes données), on le calcule
                if (fullTrack.TotalListening == 0 && fullTrack.Duration > 0)
                {
                    fullTrack.TotalListening = fullTrack.Count * fullTrack.Duration;
                }

                return fullTrack;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enriching track {Title}", track.Name);
                return fullTrack;
            }
        }

        #endregion

        #region Private Helpers - Mappers

        private FullAlbumInfos CreateBasicFullAlbum(AlbumListening album)
        {
            return new FullAlbumInfos
            {
                Title = album.Title,
                Artist = album.Artist,
                PlayCount = album.StreamCount,
                TotalListening = album.ListeningTime, // Vient de Mongo
                TrackInfos = album.StreamCountByTrack.Select(t => new ApiTrackInfos
                {
                    Track = t.Key,
                    Album = album.Title,
                    Artist = album.Artist,
                    Count = t.Value,
                    // Ici on ne peut pas deviner le temps par track car le dico n'a que le count
                    // On laisse 0 ou on estimera plus tard si on avait la durée moyenne
                }).ToList()
            };
        }

        private FullAlbumInfos CreateBasicFullAlbumWithPartialDeezerData(AlbumListening album, DeezerAlbum deezerAlbum)
        {
            var result = CreateBasicFullAlbum(album);
            result.Title = deezerAlbum.Title ?? album.Title;
            result.CoverUrl = deezerAlbum.CoverXl ?? deezerAlbum.CoverBig ?? string.Empty;
            return result;
        }

        private FullAlbumInfos MapToFullAlbumInfos(AlbumListening album, DeezerAlbum deezerAlbum, DeezerAlbumDetails fullDetails)
        {
            var trackInfos = new List<ApiTrackInfos>();

            foreach (var deezerTrack in fullDetails.Tracks?.Data ?? Enumerable.Empty<DeezerTrack>())
            {
                int localPlayCount = 0;
                // Essaie de matcher le titre Deezer avec nos clés du dictionnaire (ignorance case)
                var key = album.StreamCountByTrack.Keys
                    .FirstOrDefault(k => k.Equals(deezerTrack.Title, StringComparison.OrdinalIgnoreCase));

                if (key != null)
                {
                    localPlayCount = album.StreamCountByTrack[key];
                }

                trackInfos.Add(new ApiTrackInfos
                {
                    Track = deezerTrack?.Title ?? "Unknown",
                    Album = deezerAlbum.Title ?? album.Title,
                    Artist = deezerTrack?.Artist?.Name ?? deezerAlbum.Artist?.Name ?? album.Artist,
                    TrackUrl = deezerTrack?.Preview ?? string.Empty,
                    Count = localPlayCount,
                    Duration = deezerTrack?.Duration ?? 0,
                    TotalListening = localPlayCount * (deezerTrack?.Duration ?? 0) // Calcul théorique obligé ici
                });
            }

            // On ajoute les tracks de Mongo qui n'auraient pas été trouvés dans l'album Deezer (bonus tracks, fautes de frappe...)
            foreach (var kvp in album.StreamCountByTrack)
            {
                if (!trackInfos.Any(t => t.Track.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    trackInfos.Add(new ApiTrackInfos { Track = kvp.Key, Count = kvp.Value, Artist = album.Artist, Album = album.Title });
                }
            }

            return new FullAlbumInfos
            {
                Title = deezerAlbum.Title ?? album.Title,
                Artist = deezerAlbum.Artist?.Name ?? album.Artist,
                PlayCount = album.StreamCount, // Total Streams
                TotalListening = album.ListeningTime, // Total Time (Réel Mongo)
                TotalDuration = fullDetails.Duration, // Durée album officiel
                ReleaseDate = fullDetails.ReleaseDate ?? string.Empty,
                TrackInfos = trackInfos.OrderByDescending(t => t.Count).ToList(),
                CoverUrl = deezerAlbum.CoverXl ?? deezerAlbum.CoverBig ?? string.Empty
            };
        }

        private FullArtistInfos CreateBasicFullArtist(ArtistListening artist)
        {
            return new FullArtistInfos
            {
                Artist = artist.Name,
                TotalListening = artist.ListeningTime,
                PlayCount = artist.StreamCount,
                TrackInfos = artist.StreamCountByTrack.Select(t => new ApiTrackInfos
                {
                    Track = t.Key,
                    Count = t.Value
                }).ToList()
            };
        }

        private FullArtistInfos CreateBasicFullArtistWithPartialDeezerData(ArtistListening artist, DeezerArtist deezerArtist)
        {
            var result = CreateBasicFullArtist(artist);
            result.Artist = deezerArtist.Name ?? artist.Name;
            result.CoverUrl = deezerArtist.PictureBig ?? deezerArtist.Picture ?? string.Empty;
            result.NbFans = deezerArtist.NbFan;
            return result;
        }

        private async Task<FullArtistInfos> MapToFullArtistInfosAsync(ArtistListening artist, DeezerArtistDetails artistDetails)
        {
            // Correction critique : Utilisation d'un sémaphore pour ne pas spammer l'API
            // On limite aussi aux 20 titres les plus écoutés pour la performance (optionnel mais recommandé)
            var topTracks = artist.StreamCountByTrack
                .OrderByDescending(kvp => kvp.Value)
                .Take(50); // On traite les 50 premiers max

            var tasks = topTracks.Select(async kvp =>
            {
                await _apiThrottler.WaitAsync(); // Attente d'un slot libre
                try
                {
                    return await ProcessTrackAsync(kvp.Key, kvp.Value, artistDetails.Name ?? artist.Name);
                }
                finally
                {
                    _apiThrottler.Release(); // Libération du slot
                }
            });

            var enrichedTracks = await Task.WhenAll(tasks);

            return new FullArtistInfos
            {
                Artist = artistDetails.Name ?? artist.Name,
                PlayCount = artist.StreamCount,
                TotalListening = artist.ListeningTime, // Temps réel Mongo
                NbFans = artistDetails.NbFan,
                TrackInfos = enrichedTracks.ToList(),
                CoverUrl = artistDetails.PictureBig ?? artistDetails.Picture ?? string.Empty
            };
        }

        private async Task<ApiTrackInfos> ProcessTrackAsync(string trackName, int playCount, string artistName)
        {
            // Note: Ici nous n'avons pas le ListeningTime individuel venant de l'objet ArtistListening (juste le count).
            // Donc le TotalListening sera estimé.

            var deezerTrack = await GetTrackFromDeezer(trackName, artistName);
            int trackDuration = deezerTrack?.Duration ?? 0;
            int estimatedTime = playCount * trackDuration;

            return new ApiTrackInfos
            {
                Track = trackName,
                Artist = artistName,
                Count = playCount,
                TrackUrl = deezerTrack?.CoverUrl ?? string.Empty, // Ou Preview selon ta prop
                Duration = trackDuration,
                TotalListening = estimatedTime,
                Album = deezerTrack?.Album?.Title ?? string.Empty
            };
        }

        #endregion

        #region Private Helpers - API Calls

        private async Task<DeezerAlbum?> SearchAlbumOnDeezer(string title, string artist)
        {
            try
            {
                var query = $"artist:\"{Uri.EscapeDataString(artist)}\" album:\"{Uri.EscapeDataString(title)}\"";
                var response = await _httpClient.GetFromJsonAsync<DeezerSearchResponse<DeezerAlbum>>(
                    $"{DeezerApiBaseUrl}/search/album?q={query}&limit=5");

                foreach(var album in response!.Data)
                {
                    if (album.Title == title)
                    {
                        if (artist.Contains(album.Artist.Name)){
                            _logger.LogInformation($"cover d'album recupere : {album.CoverXl ?? album.CoverBig}");
                            return album;
                        }
                    }
                }
                return response?.Data?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Deezer search failed for album {Title}", title);
                return null;
            }
        }

        private async Task<DeezerArtist?> SearchArtistOnDeezer(string artistName)
        {
            var url = $"{DeezerApiBaseUrl}/search/artist?q={Uri.EscapeDataString(artistName)}&limit=1";

            _logger.LogDebug("Deezer API search started for artist '{Artist}' | URL: {Url}", artistName, url);

            try
            {
                using var response = await _httpClient.GetAsync(url);

                _logger.LogDebug(
                    "Deezer API response received for artist '{Artist}' | StatusCode: {StatusCode}",
                    artistName,
                    response.StatusCode
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();

                    _logger.LogWarning(
                        "Deezer API returned non-success status for artist '{Artist}' | StatusCode: {StatusCode} | Body: {Body}",
                        artistName,
                        response.StatusCode,
                        errorBody
                    );

                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json))
                {
                    _logger.LogWarning(
                        "Deezer API returned empty body for artist '{Artist}'",
                        artistName
                    );
                    return null;
                }

                DeezerSearchResponse<DeezerArtist>? data;

                try
                {
                    data = JsonSerializer.Deserialize<DeezerSearchResponse<DeezerArtist>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }
                    );
                }
                catch (JsonException jex)
                {
                    _logger.LogError(
                        jex,
                        "Failed to deserialize Deezer API response for artist '{Artist}' | Raw JSON: {Json}",
                        artistName,
                        json
                    );
                    return null;
                }

                var artist = data?.Data?.FirstOrDefault();

                if (artist == null)
                {
                    _logger.LogInformation(
                        "Deezer API returned no artist result for '{Artist}'",
                        artistName
                    );
                }
                else
                {
                    _logger.LogInformation(
                        "Deezer artist found | Search='{Search}' | DeezerId={Id} | Name='{Name}'",
                        artistName,
                        artist.Id,
                        artist.Name
                    );
                }

                return artist;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Deezer API call timed out for artist '{Artist}'",
                    artistName
                );
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "HTTP error while calling Deezer API for artist '{Artist}'",
                    artistName
                );
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error while searching Deezer artist '{Artist}'",
                    artistName
                );
                return null;
            }
        }

        private async Task<DeezerAlbumDetails?> GetFullAlbumDetails(int albumId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<DeezerAlbumDetails>(
                    $"{DeezerApiBaseUrl}/album/{albumId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to get album details {Id}: {Message}", albumId, ex.Message);
                return null;
            }
        }

        private async Task<DeezerArtistDetails?> GetFullArtistDetails(long artistId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<DeezerArtistDetails>(
                    $"{DeezerApiBaseUrl}/artist/{artistId}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to get artist details {Id}: {Message}", artistId, ex.Message);
                return null;
            }
        }

        public async Task<DeezerTrack?> GetTrackFromDeezer(string trackName, string artistName)
        {
            if (string.IsNullOrWhiteSpace(trackName)) return null;

            try
            {
                var query = $"artist:\"{Uri.EscapeDataString(artistName)}\" track:\"{Uri.EscapeDataString(trackName)}\"";

                // Utilisation de GetFromJsonAsync au lieu de parsing manuel JsonDocument
                var response = await _httpClient.GetFromJsonAsync<DeezerSearchResponse<DeezerTrack>>(
                    $"{DeezerApiBaseUrl}/search?q={query}&limit=1");

                var track = response?.Data?.FirstOrDefault();

                if (track != null)
                {
                    // Petit hack : Si l'objet DeezerTrack ne mappe pas directement l'image dans une propriété simple,
                    // assure-toi que ta classe DeezerTrack a bien les propriétés imbriquées mappées ou gère-le ici.
                    // Supposons que DeezerTrack a une prop 'Album' qui contient 'CoverBig'.
                    if (string.IsNullOrEmpty(track.CoverUrl) && track.Album != null)
                    {
                        track.CoverUrl = track.Album.CoverBig ?? track.Album.CoverXl ?? "";
                    }
                }

                return track;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to search track {Track}: {Message}", trackName, ex.Message);
                return null;
            }
        }

        #endregion
    }
}