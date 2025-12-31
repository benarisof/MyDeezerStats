using ExcelDataReader;
using Microsoft.Extensions.Logging;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Entities.Excel;
using MyDeezerStats.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyDeezerStats.Application.Services
{
    public class ExcelService : IExcelService
    {
        private readonly IListeningRepository _listeningRepository;
        private readonly ILogger<ExcelService> _logger;

        // Configuration des colonnes Excel (basé sur votre structure)
        private readonly ExcelColumnMapping _columnMapping = new()
        {
            TitleColumn = 0,
            ArtistColumn = 1,
            AlbumColumn = 3,
            DurationColumn = 5,
            DateColumn = 8
        };

        public ExcelService(
            IListeningRepository listeningRepository,
            ILogger<ExcelService> logger)
        {
            _listeningRepository = listeningRepository ?? throw new ArgumentNullException(nameof(listeningRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ImportResult> ProcessExcelFileAsync(Stream fileStream, int batchSize = 1000)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ImportResult();

            // Validation des paramètres
            if (fileStream == null)
                throw new ArgumentException("Le flux de fichier ne peut pas être null");

            if (fileStream.Length == 0)
                throw new ArgumentException("Le flux de fichier est vide");

            if (batchSize <= 0 || batchSize > 10000)
                throw new ArgumentException("La taille du lot doit être comprise entre 1 et 10000");

            try
            {
                _logger.LogInformation(
                    "Début du traitement du fichier Excel. Taille: {FileSize} bytes, Batch size: {BatchSize}",
                    fileStream.Length, batchSize);

                // Configuration pour ExcelDataReader (gestion des encodings)
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using var reader = ExcelReaderFactory.CreateReader(fileStream);

                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true,
                        FilterRow = rowReader => !IsEmptyRow(rowReader) // Ignorer les lignes vides
                    }
                });

                if (dataSet.Tables.Count == 0)
                    throw new InvalidOperationException("Le fichier Excel ne contient aucune feuille");

                var sheet = dataSet.Tables[0];
                result.TotalRows = sheet.Rows.Count - 1; // Exclure l'en-tête

                _logger.LogDebug("Feuille Excel chargée avec {RowCount} lignes de données", result.TotalRows);

                await ProcessDataTable(sheet, result, batchSize);

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;

                LogImportResult(result);

                return result;
            }
            catch (Exception ex) when (ex is not ApplicationException)
            {
                _logger.LogError(ex, "Erreur lors du traitement du fichier Excel");
                throw new ApplicationException("Une erreur est survenue lors du traitement du fichier Excel", ex);
            }
        }

        private async Task ProcessDataTable(System.Data.DataTable sheet, ImportResult result, int batchSize)
        {
            var listeningsBatch = new List<ListeningEntry>(batchSize);
            var rowProcessor = new RowProcessor(_columnMapping, _logger);

            for (int i = 1; i < sheet.Rows.Count; i++) // Commencer à 1 pour sauter l'en-tête
            {
                try
                {
                    var row = sheet.Rows[i];

                    if (IsEmptyDataRow(row))
                    {
                        result.RowsSkipped++;
                        continue;
                    }

                    var parseResult = rowProcessor.TryParseRow(row, i);

                    if (parseResult.IsSuccess)
                    {
                        listeningsBatch.Add(parseResult.ListeningEntry!);
                        result.RowsProcessed++;
                    }
                    else
                    {
                        result.RowsSkipped++;
                        result.Errors.Add($"Ligne {i}: {parseResult.ErrorMessage}");

                        if (result.Errors.Count <= 50) // Limiter le nombre d'erreurs rapportées
                            _logger.LogWarning("Ligne {LineNumber} ignorée: {Error}", i, parseResult.ErrorMessage);
                    }

                    // Traitement par lot
                    if (listeningsBatch.Count >= batchSize)
                    {
                        await ProcessBatch(listeningsBatch, result);
                        listeningsBatch.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur inattendue lors du traitement de la ligne {LineNumber}", i);
                    result.RowsSkipped++;
                    result.Errors.Add($"Ligne {i}: Erreur inattendue - {ex.Message}");
                }
            }

            // Dernier batch
            if (listeningsBatch.Count > 0)
            {
                await ProcessBatch(listeningsBatch, result);
            }
        }

        private async Task ProcessBatch(List<ListeningEntry> batch, ImportResult result)
        {
            try
            {
                _logger.LogDebug("Traitement d'un lot de {BatchSize} enregistrements", batch.Count);
                await _listeningRepository.InsertListeningsAsync(batch);
                result.RowsImported += batch.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'insertion d'un lot de {BatchSize} enregistrements", batch.Count);
                result.Errors.Add($"Erreur d'insertion pour un lot de {batch.Count} enregistrements: {ex.Message}");
                result.RowsSkipped += batch.Count;
                result.RowsImported -= batch.Count; // Ajuster le compteur
            }
        }

        private void LogImportResult(ImportResult result)
        {
            if (result.Errors.Count > 0)
            {
                _logger.LogWarning(
                    "Traitement terminé en {ProcessingTime} avec {Processed} lignes traitées, {Imported} importées, {Skipped} ignorées. {ErrorCount} erreurs.",
                    result.ProcessingTime,
                    result.RowsProcessed,
                    result.RowsImported,
                    result.RowsSkipped,
                    result.Errors.Count);
            }
            else
            {
                _logger.LogInformation(
                    "Traitement terminé avec succès en {ProcessingTime}. {Processed} lignes traitées, {Imported} importées, {Skipped} ignorées.",
                    result.ProcessingTime,
                    result.RowsProcessed,
                    result.RowsImported,
                    result.RowsSkipped);
            }
        }

        private static bool IsEmptyRow(IExcelDataReader reader)
        {
            // Vérifie si la ligne est vide
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (!string.IsNullOrWhiteSpace(reader.GetValue(i)?.ToString()))
                    return false;
            }
            return true;
        }

        private static bool IsEmptyDataRow(System.Data.DataRow row)
        {
            // Vérifie si la ligne de DataTable est vide
            for (int i = 0; i < row.Table.Columns.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]?.ToString()))
                    return false;
            }
            return true;
        }
    }

    // Classe dédiée au traitement des lignes
    internal class RowProcessor
    {
        private readonly ExcelColumnMapping _columnMapping;
        private readonly ILogger<ExcelService> _logger;

        public RowProcessor(ExcelColumnMapping columnMapping, ILogger<ExcelService> logger)
        {
            _columnMapping = columnMapping;
            _logger = logger;
        }

        public ParseResult TryParseRow(System.Data.DataRow row, int lineNumber)
        {
            try
            {
                // Validation des colonnes requises
                if (!ValidateRequiredColumns(row, out var validationError))
                    return ParseResult.Failure(validationError!);

                var title = GetSafeString(row, _columnMapping.TitleColumn);
                var artist = GetSafeString(row, _columnMapping.ArtistColumn);
                var album = GetSafeString(row, _columnMapping.AlbumColumn);
                var duration = GetSafeString(row, _columnMapping.DurationColumn);

                // Validation des champs obligatoires
                if (string.IsNullOrWhiteSpace(title))
                    return ParseResult.Failure("Le titre de la piste est requis");

                if (string.IsNullOrWhiteSpace(artist))
                    return ParseResult.Failure("L'artiste est requis");

                if (string.IsNullOrWhiteSpace(album))
                    return ParseResult.Failure("L'album est requis");

                // Parse de la date
                if (!TryParseDate(GetSafeString(row, _columnMapping.DateColumn), out var date))
                    return ParseResult.Failure("Format de date invalide");

                var listeningEntry = new ListeningEntry
                {
                    Track = title.Trim(),
                    Artist = artist.Trim(),
                    Album = album.Trim(),
                    Duration = duration?.Trim() ?? string.Empty,
                    Date = date
                };

                // Validation métier supplémentaire
                if (!ValidateListeningEntry(listeningEntry, out var businessError))
                    return ParseResult.Failure(businessError!);

                return ParseResult.Success(listeningEntry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur lors du parsing de la ligne {LineNumber}", lineNumber);
                return ParseResult.Failure($"Erreur de parsing: {ex.Message}");
            }
        }

        private bool ValidateRequiredColumns(System.Data.DataRow row, out string? error)
        {
            var requiredColumns = new[]
            {
                _columnMapping.TitleColumn,
                _columnMapping.ArtistColumn,
                _columnMapping.AlbumColumn,
                _columnMapping.DateColumn
            };

            foreach (var colIndex in requiredColumns)
            {
                if (colIndex >= row.Table.Columns.Count)
                {
                    error = $"Colonne manquante à l'index {colIndex}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static string? GetSafeString(System.Data.DataRow row, int columnIndex)
        {
            if (columnIndex >= row.Table.Columns.Count)
                return null;

            var value = row[columnIndex];
            return value?.ToString()?.Trim();
        }

        private static bool TryParseDate(string? dateString, out DateTime date)
        {
            date = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(dateString))
                return false;

            return DateTime.TryParse(dateString, out date);
        }

        private static bool ValidateListeningEntry(ListeningEntry entry, out string? error)
        {
            if (entry.Date == DateTime.MinValue || entry.Date > DateTime.Now.AddDays(1))
            {
                error = "Date invalide";
                return false;
            }

            if (entry.Track.Length > 500)
            {
                error = "Le titre de la piste est trop long";
                return false;
            }

            if (entry.Artist.Length > 300)
            {
                error = "Le nom de l'artiste est trop long";
                return false;
            }

            if (entry.Album.Length > 300)
            {
                error = "Le nom de l'album est trop long";
                return false;
            }

            error = null;
            return true;
        }
    }

    // Classes de support
    internal class ExcelColumnMapping
    {
        public int TitleColumn { get; set; }
        public int ArtistColumn { get; set; }
        public int AlbumColumn { get; set; }
        public int DurationColumn { get; set; }
        public int DateColumn { get; set; }
    }

    internal class ParseResult
    {
        public bool IsSuccess { get; }
        public ListeningEntry? ListeningEntry { get; }
        public string? ErrorMessage { get; }

        private ParseResult(bool isSuccess, ListeningEntry? listeningEntry, string? errorMessage)
        {
            IsSuccess = isSuccess;
            ListeningEntry = listeningEntry;
            ErrorMessage = errorMessage;
        }

        public static ParseResult Success(ListeningEntry listeningEntry)
        {
            return new ParseResult(true, listeningEntry, null);
        }

        public static ParseResult Failure(string errorMessage)
        {
            return new ParseResult(false, null, errorMessage);
        }
    }
}