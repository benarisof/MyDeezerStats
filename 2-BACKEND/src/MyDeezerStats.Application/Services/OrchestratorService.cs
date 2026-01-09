using Microsoft.Extensions.Logging;
using MyDeezerStats.Application.Dtos.LastStream;
using MyDeezerStats.Application.Dtos.TopStream;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Entities.ListeningInfos;
using MyDeezerStats.Domain.Exceptions;
using MyDeezerStats.Domain.Repositories;

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

        public async Task<FullArtistInfos> GetArtistAsync(string fullId)
        {
            if (string.IsNullOrWhiteSpace(fullId)) throw new ArgumentException("Nom requis", nameof(fullId));

            var artist = await _artistRepository.GetArtistDetailsAsync(fullId, null, null)
                         ?? throw new NotFoundException($"Artiste '{fullId}' non trouvé");
            var artistFull = await _deezerService.EnrichFullArtistWithDeezerData(artist);
            artistFull.TotalListening = artistFull.TrackInfos.Sum(track => track.TotalListening);
            return artistFull;
        }

        #endregion

        #region Recent Listenings & Sync

        public async Task<IEnumerable<ListeningDto>> GetLatestListeningsAsync(int limit = 100)
        {
            try
            {
                // 1. Récupérer la date la plus récente en base 
                var lastEntry = await _repository.GetLastEntryAsync();
                var lastStreamDate = lastEntry?.Date ?? DateTime.MinValue;

                // 2. Synchro LastFM
                var newTracks = await _lastFmService.GetListeningHistorySince(lastStreamDate);

                if (newTracks.Any())
                {
                    var entities = newTracks.Select(MapToEntity).ToList();
                    await _repository.InsertListeningsAsync(entities);
                }

                // 3. Toujours renvoyer la source de vérité (la base de données)
                var localEntries = await _repository.GetRecentListeningsAsync(limit);
                return localEntries.Select(MapToDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sync recent tracks");
                var localEntries = await _repository.GetRecentListeningsAsync(limit);
                return localEntries.Select(MapToDto);
            }
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

        // --- Mappings ---

        private ListeningDto MapToDto(ListeningEntry x) => new()
        {
            Track = x.Track,
            Artist = x.Artist,
            Album = x.Album,
            Date = x.Date,
            Duration = x.Duration
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