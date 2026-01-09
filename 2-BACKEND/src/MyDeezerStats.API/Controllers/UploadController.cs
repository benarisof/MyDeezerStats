using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyDeezerStats.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace MyDeezerStats.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IExcelService _excelService;
        private readonly ILogger<UploadController> _logger;

        public UploadController(IExcelService excelService, ILogger<UploadController> logger)
        {
            _excelService = excelService;
            _logger = logger;
        }

        /// <summary>
        /// Importe un fichier Excel contenant des données d'écoute
        /// </summary>
        /// <param name="file">Fichier Excel (.xlsx, .xls)</param>
        /// <param name="batchSize">Taille des lots pour l'importation (défaut: 1000)</param>
        /// <returns>Résultat de l'importation</returns>
        [HttpPost("import-excel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ImportExcel(
            [Required] IFormFile file,
            [FromForm] int batchSize = 1000)
        {
            // Validation préliminaire
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("ImportExcel called with null or empty file");
                return BadRequest(new { Message = "Aucun fichier sélectionné ou fichier vide." });
            }

            try
            {
                _logger.LogInformation(
                    "Starting Excel import for file {FileName} ({FileSize} bytes) with batch size {BatchSize}",
                    file.FileName, file.Length, batchSize);

                // Validation du type de fichier
                if (!IsValidExcelFile(file))
                {
                    _logger.LogWarning("Invalid file type for file {FileName}", file.FileName);
                    return BadRequest(new { Message = "Type de fichier non supporté. Seuls les fichiers Excel (.xlsx, .xls) sont acceptés." });
                }

                // Validation de la taille du fichier (10MB max)
                if (file.Length > 10 * 1024 * 1024) 
                {
                    _logger.LogWarning("File too large: {FileName} ({FileSize} bytes)", file.FileName, file.Length);
                    return StatusCode(413, new { Message = "Le fichier est trop volumineux. Taille maximale: 10MB." });
                }

                // Validation de la taille du batch
                if (batchSize <= 0 || batchSize > 10000)
                {
                    _logger.LogWarning("Invalid batch size: {BatchSize}", batchSize);
                    return BadRequest(new { Message = "La taille du lot doit être comprise entre 1 et 10000." });
                }

                // Traitement du fichier
                using var stream = file.OpenReadStream();
                var result = await _excelService.ProcessExcelFileAsync(stream, batchSize);

                _logger.LogInformation(
                    "Excel import completed successfully for file {FileName}. Rows processed: {RowsProcessed}",
                    file.FileName, result.RowsProcessed);

                return Ok(new
                {
                    Message = "Données importées avec succès.",
                    RowsProcessed = result.RowsProcessed,
                    FileName = file.FileName,
                    ImportDate = DateTime.UtcNow
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Argument error during Excel import for file {FileName}", file.FileName);
                return BadRequest(new { Message = $"Erreur dans les paramètres: {ex.Message}" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation during Excel import for file {FileName}", file.FileName);
                return BadRequest(new { Message = $"Opération invalide: {ex.Message}" });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied during Excel import for file {FileName}", file.FileName);
                return StatusCode(403, new { Message = "Accès refusé au fichier." });
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error during Excel import for file {FileName}", file.FileName);
                return StatusCode(500, new { Message = "Erreur d'accès au fichier." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during Excel import for file {FileName}", file.FileName);
                return StatusCode(500, new
                {
                    Message = "Une erreur inattendue s'est produite lors de l'importation.",
                    Details = ex.Message
                });
            }
        }

        /// <summary>
        /// Vérifie si le fichier est un fichier Excel valide
        /// </summary>
        private static bool IsValidExcelFile(IFormFile file)
        {
            if (string.IsNullOrEmpty(file.FileName))
                return false;

            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            // Vérification de l'extension
            if (!allowedExtensions.Contains(fileExtension))
                return false;

            // Vérification du type MIME 
            var allowedMimeTypes = new[]
            {
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                "application/vnd.ms-excel", 
                "application/octet-stream"
            };

            return allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant());
        }
    }
}