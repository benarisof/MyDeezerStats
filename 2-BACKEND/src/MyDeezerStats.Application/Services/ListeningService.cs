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
    public class ListeningService : IListeningService
    {
        private readonly IListeningRepository _repository;
        private readonly IDeezerService _deezerService;
        private readonly ILastFmService _lastFmService;
        private readonly ILogger<ListeningService> _logger;

        public ListeningService(
            IListeningRepository repository,
            IDeezerService deezerService,
            ILastFmService lastFmService,
            ILogger<ListeningService> logger)
        {
            _repository = repository;
            _deezerService = deezerService;
            _lastFmService = lastFmService;
            _logger = logger;
        }

        public async Task<List<ShortAlbumInfos>> GetTopAlbumsAsync(DateTime? from, DateTime? to, int nb = 10)
        {
            // Validation des paramètres
            if (nb <= 0 || nb > 100)
            {
                _logger.LogWarning("Invalid nb parameter: {Nb}, using default value 10", nb);
                nb = 10;
            }

            if (from > to)
            {
                throw new ArgumentException("La date 'from' ne peut pas être après la date 'to'");
            }

            try
            {
                _logger.LogDebug("Retrieving top {Count} albums from {From} to {To}", nb, from, to);
                var topAlbums = await _repository.GetTopAlbumsWithAsync(nb, from, to);

                var albumTasks = topAlbums.Select(async album =>
                {
                    try
                    {
                        ShortAlbumInfos enrichedAlbum = await _deezerService.EnrichShortAlbumWithDeezerData(album);
                        enrichedAlbum.Count = album.StreamCount;
                        return enrichedAlbum;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to enrich album {AlbumTitle}, returning basic info", album.Title);
                        // Fallback pour éviter de casser le flux
                        return new ShortAlbumInfos
                        {
                            Title = album.Title,
                            Artist = album.Artist,
                            Count = album.StreamCount
                        };
                    }
                });

                var result = await Task.WhenAll(albumTasks);
                _logger.LogInformation("Successfully retrieved and enriched {AlbumCount} albums", result.Length);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top albums with from={From} to={To} nb={Count}", from, to, nb);
                throw; 
            }
        }

        public async Task<FullAlbumInfos> GetAlbumAsync(string fullId)
        {
            if (string.IsNullOrWhiteSpace(fullId))
            {
                throw new ArgumentException("L'identifiant de l'album est requis", nameof(fullId));
            }

            var pipeIndex = fullId.IndexOf('|');
            if (pipeIndex < 0 || pipeIndex == fullId.Length - 1)
            {
                throw new ArgumentException("Format d'identifiant d'album invalide. Attendu: 'titre|artiste'", nameof(fullId));
            }

            try
            {
                var title = Uri.UnescapeDataString(fullId.Substring(0, pipeIndex));
                var artist = Uri.UnescapeDataString(fullId.Substring(pipeIndex + 1));

                _logger.LogDebug("Retrieving album {Title} by {Artist}", title, artist);

                var album = await _repository.GetAlbumsWithAsync(title, artist, null, null)
                    ?? throw new NotFoundException($"Album '{title}' par '{artist}' non trouvé");

                var enrichedAlbum = await _deezerService.EnrichFullAlbumWithDeezerData(album);
                _logger.LogInformation("Successfully retrieved album {Title}", title);

                return enrichedAlbum;
            }
            catch (NotFoundException)
            {
                throw; 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving album with identifier {Identifier}", fullId);
                throw new ApplicationException("Une erreur est survenue lors de la récupération de l'album", ex);
            }
        }

        public async Task<List<ShortArtistInfos>> GetTopArtistsAsync(DateTime? from, DateTime? to, int nb = 10)
        {
            // Validation des paramètres
            if (nb <= 0 || nb > 100)
            {
                _logger.LogWarning("Invalid nb parameter: {Nb}, using default value 10", nb);
                nb = 10;
            }

            if (from > to)
            {
                throw new ArgumentException("La date 'from' ne peut pas être après la date 'to'");
            }

            try
            {
                _logger.LogDebug("Retrieving top {Count} artists from {From} to {To}", nb, from, to);
                var topArtists = await _repository.GetTopArtistWithAsync(nb, from, to);

                _logger.LogInformation("Number of artists retrieved from repository: {ArtistCount}", topArtists.Count);

                var artistTasks = topArtists.Select(async artist =>
                {
                    try
                    {
                        ShortArtistInfos enrichedArtist = await _deezerService.EnrichShortArtistWithDeezerData(artist);
                        enrichedArtist.Count = artist.StreamCount;
                        return enrichedArtist;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to enrich artist {ArtistName}, returning basic info", artist.Name);
                        // Fallback pour éviter de casser le flux
                        return new ShortArtistInfos
                        {
                            Artist = artist.Name,
                            Count = artist.StreamCount
                        };
                    }
                });

                var result = await Task.WhenAll(artistTasks);
                _logger.LogInformation("Successfully retrieved and enriched {ArtistCount} artists", result.Length);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top artists with from={From} to={To} nb={Count}", from, to, nb);
                throw;
            }
        }

        public async Task<FullArtistInfos> GetArtistAsync(string fullId)
        {
            if (string.IsNullOrWhiteSpace(fullId))
            {
                throw new ArgumentException("Le nom de l'artiste est requis", nameof(fullId)); 
            }

            try
            {
                _logger.LogDebug("Retrieving artist {ArtistName}", fullId);

                ArtistListening artist = await _repository.GetArtistWithAsync(fullId, null, null)
                    ?? throw new NotFoundException($"Artiste '{fullId}' non trouvé");

                FullArtistInfos enrichedArtist = await _deezerService.EnrichFullArtistWithDeezerData(artist);
                _logger.LogInformation("Successfully retrieved artist {ArtistName}", fullId);

                return enrichedArtist;
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving artist {ArtistName}", fullId);
                throw new ApplicationException("Une erreur est survenue lors de la récupération de l'artiste", ex);
            }
        }

        public async Task<List<ApiTrackInfos>> GetTopTracksAsync(DateTime? from, DateTime? to, int nb = 10)
        {
            // Validation des paramètres
            if (nb <= 0 || nb > 100)
            {
                _logger.LogWarning("Invalid nb parameter: {Nb}, using default value 10", nb);
                nb = 10;
            }

            if (from > to)
            {
                throw new ArgumentException("La date 'from' ne peut pas être après la date 'to'");
            }

            try
            {
                _logger.LogDebug("Retrieving top {Count} tracks from {From} to {To}", nb, from, to);
                var topTracks = await _repository.GetTopTrackWithAsync(nb, from, to);

                var trackTasks = topTracks.Select(async track =>
                {
                    try
                    {
                        ApiTrackInfos enrichedTrack = await _deezerService.EnrichTrackWithDeezerData(track);
                        return enrichedTrack;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to enrich track {TrackName}, returning basic info", track.Name);
                        // Fallback pour éviter de casser le flux
                        return new ApiTrackInfos
                        {
                            Track = track.Name,
                            Artist = track.Artist,
                            Album = track.Album,
                            Count = track.StreamCount,
                            LastListen = track.LastListening
                        };
                    }
                });

                var result = await Task.WhenAll(trackTasks);
                _logger.LogInformation("Successfully retrieved and enriched {TrackCount} tracks", result.Length);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top tracks with from={From} to={To} nb={Count}", from, to, nb);
                throw;
            }
        }

        public async Task<IEnumerable<ListeningDto>> GetLatestListeningsAsync(int limit = 100)
        {
            // Validation du paramètre
            if (limit <= 0 || limit > 1000)
            {
                _logger.LogWarning("Invalid limit parameter: {Limit}, using default value 100", limit);
                limit = 100;
            }

            try
            {
                _logger.LogDebug("Retrieving {Limit} recent listenings", limit);

                var listenings = await _repository.GetLatestListeningsAsync(limit);

                if (!listenings.Any())
                {
                    _logger.LogInformation("No recent listenings found");
                    return Enumerable.Empty<ListeningDto>();
                }

                var result = listenings.Select(x => new ListeningDto
                {
                    Track = x.Track,
                    Artist = x.Artist,
                    Album = x.Album,
                    Date = x.Date,
                    Duration = x.Duration
                }).OrderByDescending(x => x.Date);

                await SynchronizeRecentListeningsAsync(listenings);

                // Rechargement seulement si nécessaire
                var finalListenings = await _repository.GetLatestListeningsAsync(limit);
                var finalResult = finalListenings.Select(x => new ListeningDto
                {
                    Track = x.Track,
                    Artist = x.Artist,
                    Album = x.Album,
                    Date = x.Date,
                    Duration = x.Duration
                }).OrderByDescending(x => x.Date);

                _logger.LogInformation("Successfully retrieved {Count} recent listenings", finalResult.Count());
                return finalResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent listenings with limit {Limit}", limit);
                throw new ApplicationException("Une erreur est survenue lors de la récupération des écoutes récentes", ex);
            }
        }

        /// <summary>
        /// Méthode privée pour extraire la logique de synchronisation
        /// </summary>
        private async Task SynchronizeRecentListeningsAsync(List<ListeningEntry> currentListenings)
        {
            try
            {
                var lastStreamDate = currentListenings.Max(x => x.Date);
                _logger.LogDebug("Synchronizing listenings since {LastStreamDate}", lastStreamDate);

                var lastStreams = await _lastFmService.GetListeningHistorySince(lastStreamDate);

                if (!lastStreams.Any())
                {
                    _logger.LogDebug("No new listenings to synchronize");
                    return;
                }

                var lastStreamToUpsert = lastStreams.Select(listeningEntry => new ListeningEntry
                {
                    Album = listeningEntry.Album,
                    Date = listeningEntry.Date,
                    Track = listeningEntry.Track,
                    Artist = listeningEntry.Artist,
                    Duration = listeningEntry.Duration,
                }).ToList();

                _logger.LogInformation("Synchronizing {Count} new listenings", lastStreamToUpsert.Count);
                await _repository.InsertListeningsAsync(lastStreamToUpsert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during listenings synchronization");
            }
        }
    }
}