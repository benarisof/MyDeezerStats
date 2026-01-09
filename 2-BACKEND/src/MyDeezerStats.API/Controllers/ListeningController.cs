using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Domain.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace MyDeezerStats.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ListeningController : ControllerBase 
    {
        private readonly IOrchestratorService _service;
        private readonly ILogger<ListeningController> _logger;

        public ListeningController(IOrchestratorService service, ILogger<ListeningController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Récupère les albums les plus écoutés
        /// </summary>
        /// <param name="from">Date de début (optionnelle)</param>
        /// <param name="to">Date de fin (optionnelle)</param>
        /// <param name="nb">Nombre d'albums à retourner (1-100, défaut: 10)</param>
        [Authorize]
        [HttpGet("top-albums")]
        public async Task<IActionResult> GetTopAlbums(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery][Range(1, 100)] int nb = 10)
        {
            _logger.LogInformation("GET /top-albums called with from={From} to={To} nb={Count}", from, to, nb);

            // Validation des dates
            if (from > to)
            {
                _logger.LogWarning("Invalid date range: from={From} to={To}", from, to);
                return BadRequest("La date 'from' ne peut pas être après la date 'to'");
            }

            try
            {
                var result = await _service.GetTopAlbumsAsync(from, to, nb);
                _logger.LogInformation("Successfully retrieved {AlbumCount} albums", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top albums with from={From} to={To} nb={Count}", from, to, nb);
                return StatusCode(500, "Une erreur est survenue lors de la récupération des albums");
            }
        }

        /// <summary>
        /// Récupère les détails d'un album spécifique
        /// </summary>
        /// <param name="identifier">Identifiant de l'album (format: titre|artiste)</param>
        [Authorize]
        [HttpGet("album")]
        public async Task<IActionResult> GetAlbum([FromQuery][Required] string? identifier, [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            _logger.LogInformation("GET /album called with identifier={Identifier}", identifier);

            // Validation des dates
            if (from > to)
            {
                _logger.LogWarning("Invalid date range: from={From} to={To}", from, to);
                return BadRequest("La date 'from' ne peut pas être après la date 'to'");
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                _logger.LogWarning("Album identifier is null or empty");
                return BadRequest("L'identifiant de l'album est requis");
            }

            try
            {
                var result = await _service.GetAlbumAsync(identifier, from, to);
                _logger.LogInformation("Successfully retrieved album {Identifier}", identifier);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid album identifier format: {Identifier}", identifier);
                return BadRequest("Format d'identifiant d'album invalide");
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Album not found: {Identifier}", identifier);
                return NotFound($"Album non trouvé : {identifier}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving album {Identifier}", identifier);
                return StatusCode(500, "Une erreur est survenue lors de la récupération de l'album");
            }
        }

        /// <summary>
        /// Récupère les artistes les plus écoutés
        /// </summary>
        /// <param name="from">Date de début (optionnelle)</param>
        /// <param name="to">Date de fin (optionnelle)</param>
        /// <param name="nb">Nombre d'artistes à retourner (1-100, défaut: 10)</param>
        [Authorize]
        [HttpGet("top-artists")]
        public async Task<IActionResult> GetTopArtists(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery][Range(1, 100)] int nb = 10)
        {
            _logger.LogInformation("GET /top-artists called with from={From} to={To} nb={Count}", from, to, nb);

            if (from > to)
            {
                _logger.LogWarning("Invalid date range: from={From} to={To}", from, to);
                return BadRequest("La date 'from' ne peut pas être après la date 'to'");
            }

            try
            {
                var result = await _service.GetTopArtistsAsync(from, to, nb);
                _logger.LogInformation("Successfully retrieved {ArtistCount} artists", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top artists with from={From} to={To} nb={Count}", from, to, nb);
                return StatusCode(500, "Une erreur est survenue lors de la récupération des artistes");
            }
        }

        /// <summary>
        /// Récupère les détails d'un artiste spécifique
        /// </summary>
        /// <param name="identifier">Nom de l'artiste</param>
        [Authorize]
        [HttpGet("artist")]
        public async Task<IActionResult> GetArtist([FromQuery][Required] string? identifier)
        {
            _logger.LogInformation("GET /artist called with identifier={Identifier}", identifier);

            if (string.IsNullOrWhiteSpace(identifier))
            {
                _logger.LogWarning("Artist identifier is null or empty");
                return BadRequest("L'identifiant de l'artiste est requis");
            }

            try
            {
                var result = await _service.GetArtistAsync(identifier);
                _logger.LogInformation("Successfully retrieved artist {Identifier}", identifier);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid artist identifier: {Identifier}", identifier);
                return BadRequest("Identifiant d'artiste invalide");
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Artist not found: {Identifier}", identifier);
                return NotFound($"Artiste non trouvé : {identifier}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving artist {Identifier}", identifier);
                return StatusCode(500, "Une erreur est survenue lors de la récupération de l'artiste");
            }
        }

        /// <summary>
        /// Récupère les morceaux les plus écoutés
        /// </summary>
        /// <param name="from">Date de début (optionnelle)</param>
        /// <param name="to">Date de fin (optionnelle)</param>
        /// <param name="nb">Nombre de morceaux à retourner (1-100, défaut: 10)</param>
        [Authorize]
        [HttpGet("top-tracks")]
        public async Task<IActionResult> GetTopTracks(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery][Range(1, 100)] int nb = 10)
        {
            _logger.LogInformation("GET /top-tracks called with from={From} to={To} nb={Count}", from, to, nb);

            if (from > to)
            {
                _logger.LogWarning("Invalid date range: from={From} to={To}", from, to);
                return BadRequest("La date 'from' ne peut pas être après la date 'to'");
            }

            try
            {
                var result = await _service.GetTopTracksAsync(from, to, nb);
                _logger.LogInformation("Successfully retrieved {TrackCount} tracks", result.Count);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top tracks with from={From} to={To} nb={Count}", from, to, nb);
                return StatusCode(500, "Une erreur est survenue lors de la récupération des morceaux");
            }
        }

        /// <summary>
        /// Récupère les écoutes récentes
        /// </summary>
        [Authorize]
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecentTracks()
        {
            _logger.LogInformation("GET /recent tracks called");

            try
            {
                var result = await _service.GetLatestListeningsAsync();
                _logger.LogInformation("Successfully retrieved {TrackCount} recent tracks", result.Count());
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent tracks");
                return StatusCode(500, "Une erreur est survenue lors de la récupération des écoutes récentes");
            }
        }
    }
}