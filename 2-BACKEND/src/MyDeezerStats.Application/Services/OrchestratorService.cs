using Microsoft.Extensions.Logging;
using MyDeezerStats.Application.Dtos.LastStream;
using MyDeezerStats.Application.Dtos.TopStream;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Entities.ListeningInfos;
using MyDeezerStats.Domain.Exceptions;
using MyDeezerStats.Domain.Repositories;
using System.Collections.Concurrent;

namespace MyDeezerStats.Application.Services
{
    public class OrchestratorService : IOrchestratorService
    {
        private readonly IListeningRepository _repository;
        private readonly IAlbumRepository _albumRepository;
        private readonly IArtistRepository _artistRepository;
        private readonly ITrackRepository _trackRepository;
        private readonly IDeezerService _deezerService;
        private readonly ILastFmService _lastFmService;
        private readonly ILogger<OrchestratorService> _logger;

        public OrchestratorService(
            IListeningRepository repository,
            IAlbumRepository albumRepository,
            ITrackRepository trackRepository,
            IArtistRepository artistRepository,
            IDeezerService deezerService,
            ILastFmService lastFmService,
            ILogger<OrchestratorService> logger)
        {
            _repository = repository;
            _albumRepository = albumRepository;
            _trackRepository = trackRepository; 
            _artistRepository = artistRepository;   
            _deezerService = deezerService;
            _lastFmService = lastFmService;
            _logger = logger;
        }

        #region Public Methods - Tops

        public async Task<List<ShortAlbumInfos>> GetTopAlbumsAsync(DateTime? from, DateTime? to, int nb = 10)
        {
            nb = ValidateParams(from, to, nb);

            try
            {
                var topAlbums = await _albumRepository.GetTopAlbumsAsync(nb, from, to);

                var tasks = topAlbums.Select(async album =>
                {
                    try { return await _deezerService.EnrichShortAlbumWithDeezerData(album); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Enrichment failed for album {Album}", album.Title);
                        return MapToBasicShortAlbum(album);
                    }
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopAlbumsAsync");
                throw;
            }
        }

        public async Task<List<ShortArtistInfos>> GetTopArtistsAsync(DateTime? from, DateTime? to, int nb = 10)
        {
            nb = ValidateParams(from, to, nb);

            try
            {
                var topArtists = await _artistRepository.GetTopArtistsAsync(nb, from, to);

                var tasks = topArtists.Select(async artist =>
                {
                    try { return await _deezerService.EnrichShortArtistWithDeezerData(artist); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Enrichment failed for artist {Artist}", artist.Name);
                        return MapToBasicShortArtist(artist);
                    }
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopArtistsAsync");
                throw;
            }
        }

        public async Task<List<ApiTrackInfos>> GetTopTracksAsync(DateTime? from, DateTime? to, int nb = 10)
        {
            nb = ValidateParams(from, to, nb);

            try
            {
                var topTracks = await _trackRepository.GetTopTracksAsync(nb, from, to);

                var tasks = topTracks.Select(async track =>
                {
                    try { return await _deezerService.EnrichTrackWithDeezerData(track); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Enrichment failed for track {Track}", track.Name);
                        return MapToBasicTrackInfo(track);
                    }
                });

                return (await Task.WhenAll(tasks)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopTracksAsync");
                throw;
            }
        }

        #endregion

        #region Public Methods - Details

        public async Task<FullAlbumInfos> GetAlbumAsync(string fullId, DateTime? from, DateTime? to)
        {
            var (title, artist) = ParseFullId(fullId);
            var nb = ValidateParams(from, to, 0);

            var album = await _albumRepository.GetAlbumDetailsAsync(title, artist, from, to)
                        ?? throw new NotFoundException($"Album '{title}' par '{artist}' non trouvé");
            var albumFull = await _deezerService.EnrichFullAlbumWithDeezerData(album);
            albumFull.TotalListening = albumFull.TrackInfos.Sum(track => track.TotalListening);
            return albumFull;
        }

        public async Task<FullArtistInfos> GetArtistAsync(string fullId, DateTime? from, DateTime? to)
        {
            if (string.IsNullOrWhiteSpace(fullId)) throw new ArgumentException("Nom requis", nameof(fullId));
            var nb = ValidateParams(from, to, 0);
            var artist = await _artistRepository.GetArtistDetailsAsync(fullId, from, to)
                         ?? throw new NotFoundException($"Artiste '{fullId}' non trouvé");
            var artistFull = await _deezerService.EnrichFullArtistWithDeezerData(artist);
            artistFull.TotalListening = artistFull.TrackInfos.Sum(track => track.TotalListening);
            return artistFull;
        }

        #endregion

        #region Recent Listenings & Sync

        public async Task<IEnumerable<ApiTrackInfos>> GetLatestListeningsAsync(int limit = 100)
        {
            // 1. Synchro LastFM (inchangé, mais encapsulé dans un try/catch)
            try
            {
                await SynchronizeWithLastFmAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LastFM Sync failed, using local cache.");
            }

            // 2. Récupération des données locales
            var localEntries = await _repository.GetRecentListeningsAsync(limit);
            if (!localEntries.Any()) return Enumerable.Empty<ApiTrackInfos>();

            // 3. Récupération des counts en UNE SEULE FOIS (Correction du N+1)
            var countsMap = await _repository.GetBatchStreamCountsAsync(localEntries);

            // 4. Mapping initial
            var tracks = localEntries.Select(e =>
            {
                var track = MapEntryToTrack(e);
                // AJOUT DU TRIM ICI
                var key = $"{e.Artist.Trim().ToLower()}|{e.Track.Trim().ToLower()}";
                track.StreamCount = countsMap.TryGetValue(key, out var count) ? count : 1;
                return track;
            }).ToList();

            // 5. Enrichissement parallèle (Deezer) avec ton Semaphore
            return await EnrichTracksAsync(tracks);
        }

        #endregion

        #region Private Helpers

        private int ValidateParams(DateTime? from, DateTime? to, int nb)
        {
            if (from > to) throw new ArgumentException("Date 'from' > 'to'");
            if (nb <= 0 || nb > 100)
            {
                _logger.LogWarning("Invalid nb: {Nb}, using 10", nb);
                return 10;
            }
            return nb;
        }

        private (string title, string artist) ParseFullId(string fullId)
        {
            if (string.IsNullOrWhiteSpace(fullId)) throw new ArgumentException("ID requis");

            var parts = fullId.Split('|');
            if (parts.Length < 2) throw new ArgumentException("Format 'titre|artiste' requis");

            return (Uri.UnescapeDataString(parts[0].Trim()), Uri.UnescapeDataString(parts[1].Trim()));
        }

        private async Task SynchronizeWithLastFmAsync()
        {
            // C'est exactement ton code d'origine déplacé ici pour la propreté
            var lastEntry = await _repository.GetLastEntryAsync();
            var lastStreamDate = lastEntry?.Date ?? DateTime.MinValue;
            var newTracks = await _lastFmService.GetListeningHistorySince(lastStreamDate);

            if (newTracks.Any())
            {
                var entities = newTracks.Select(MapToEntity).ToList();
                await _repository.InsertListeningsAsync(entities);
            }
        }

        // Méthode privée pour centraliser l'enrichissement
        private async Task<List<ApiTrackInfos>> EnrichTracksAsync(List<TrackListening> tracks)
        {
            using var semaphore = new SemaphoreSlim(5);
            var cache = new ConcurrentDictionary<string, ApiTrackInfos>();

            var tasks = tracks.Select(async track =>
            {
                string cacheKey = $"{track.Artist}-{track.Name}".ToLower();

                // Si on a déjà enrichi ce morceau dans cette requête, on réutilise
                if (cache.TryGetValue(cacheKey, out var cached)) return cached;

                await semaphore.WaitAsync();
                try
                {
                    var enriched = await _deezerService.EnrichTrackWithDeezerData(track);
                    cache.TryAdd(cacheKey, enriched);
                    return enriched;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Enrichment failed for {Track}", track.Name);
                    return MapToBasicTrackInfo(track);
                }
                finally { semaphore.Release(); }
            });

            return (await Task.WhenAll(tasks)).ToList();
        }

        // --- Mappings ---

        private TrackListening MapEntryToTrack(ListeningEntry entry) => new()
        {
            Album = entry.Album,
            Artist = entry.Artist,
            LastListening = entry.Date.ToString(),
            Name = entry.Track
        };

        private ListeningEntry MapToEntity(ListeningDto x) => new()
        {
            Track = x.Track,
            Artist = x.Artist,
            Album = x.Album,
            Date = x.Date,
            Duration = x.Duration
        };

        private ShortAlbumInfos MapToBasicShortAlbum(AlbumListening a) => new()
        { Title = a.Title, Artist = a.Artist, Count = a.StreamCount };

        private ShortArtistInfos MapToBasicShortArtist(ArtistListening a) => new()
        { Artist = a.Name, Count = a.StreamCount };

        private ApiTrackInfos MapToBasicTrackInfo(TrackListening t) => new()
        {
            Track = t.Name,
            Artist = t.Artist,
            Album = t.Album,
            Count = t.StreamCount,
            LastListen = t.LastListening,
            TotalListening = t.ListeningTime
        };

        #endregion
    }
}