using ExcelDataReader;
using Microsoft.Extensions.Logging;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Entities.Excel;
using MyDeezerStats.Domain.Repositories;
using System.Data;
using System.Diagnostics;


namespace MyDeezerStats.Application.Services
{
    public class ExcelService : IExcelService
    {
        private readonly IListeningRepository _listeningRepository;
        private readonly ILogger<ExcelService> _logger;

        // Configuration des colonnes Excel
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

            if (fileStream == null || fileStream.Length == 0)
                throw new ArgumentException("Le flux de fichier est invalide ou vide");

            if (batchSize <= 0 || batchSize > 10000)
                throw new ArgumentException("La taille du lot doit être comprise entre 1 et 10000");

            try
            {
                _logger.LogInformation("Début du traitement Excel. Taille: {Size} octets", fileStream.Length);

                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                using var reader = ExcelReaderFactory.CreateReader(fileStream);
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true,
                        FilterRow = rowReader => !IsEmptyRow(rowReader)
                    }
                });

                if (dataSet.Tables.Count <= 8)
                    throw new InvalidOperationException("Le fichier Excel ne contient pas la feuille attendue à l'index 8");

                var sheet = dataSet.Tables[8];
                result.TotalRows = sheet.Rows.Count; 

                _logger.LogDebug("Feuille chargée : {RowCount} lignes détectées", result.TotalRows);

                await ProcessDataTable(sheet, result, batchSize);

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;

                LogImportResult(result);
                return result;
            }
            catch (Exception ex) when (ex is not ApplicationException)
            {
                _logger.LogError(ex, "Erreur fatale lors du traitement Excel");
                throw new ApplicationException("Erreur lors du traitement du fichier", ex);
            }
        }

        private async Task ProcessDataTable(DataTable sheet, ImportResult result, int batchSize)
        {
            var listeningsBatch = new List<ListeningEntry>(batchSize);
            var rowProcessor = new RowProcessor(_columnMapping, _logger);

            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                try
                {
                    var row = sheet.Rows[i];

                    if (IsEmptyDataRow(row))
                    {
                        result.RowsSkipped++;
                        continue;
                    }

                    var parseResult = rowProcessor.TryParseRow(row, i + 1);

                    if (parseResult.IsSuccess)
                    {
                        listeningsBatch.Add(parseResult.ListeningEntry!);
                        result.RowsProcessed++;
                    }
                    else
                    {
                        result.RowsSkipped++;
                        result.Errors.Add($"Ligne {i + 1}: {parseResult.ErrorMessage}");
                    }

                    if (listeningsBatch.Count >= batchSize)
                    {
                        await ProcessBatch(listeningsBatch, result);
                        listeningsBatch.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur ligne {Line}", i + 1);
                    result.RowsSkipped++;
                }
            }

            if (listeningsBatch.Count > 0)
                await ProcessBatch(listeningsBatch, result);
        }

        private async Task ProcessBatch(List<ListeningEntry> batch, ImportResult result)
        {
            try
            {
                await _listeningRepository.InsertListeningsAsync(batch);
                result.RowsImported += batch.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur insertion batch");
                result.Errors.Add($"Erreur d'insertion batch: {ex.Message}");
                result.RowsSkipped += batch.Count;
            }
        }

        private void LogImportResult(ImportResult result)
        {
            _logger.LogInformation("Import terminé: {Imported}/{Total} lignes importées en {Time}",
                result.RowsImported, result.TotalRows, result.ProcessingTime);
        }

        private static bool IsEmptyRow(IExcelDataReader reader)
        {
            for (int i = 0; i < reader.FieldCount; i++)
                if (reader.GetValue(i) != null) return false;
            return true;
        }

        private static bool IsEmptyDataRow(DataRow row)
        {
            foreach (var item in row.ItemArray)
                if (item != null && !string.IsNullOrWhiteSpace(item.ToString())) return false;
            return true;
        }
    }

    // --- LOGIQUE DE PARSING AVEC CONVERSION INT ---

    internal class RowProcessor
    {
        private readonly ExcelColumnMapping _columnMapping;
        private readonly ILogger _logger;

        public RowProcessor(ExcelColumnMapping columnMapping, ILogger logger)
        {
            _columnMapping = columnMapping;
            _logger = logger;
        }

        public ParseResult TryParseRow(DataRow row, int lineNumber)
        {
            try
            {
                if (!ValidateRequiredColumns(row, out var error)) return ParseResult.Failure(error!);

                var title = GetSafeString(row, _columnMapping.TitleColumn);
                var artist = GetSafeString(row, _columnMapping.ArtistColumn);
                var album = GetSafeString(row, _columnMapping.AlbumColumn);

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                    return ParseResult.Failure("Titre ou Artiste manquant");

                if (!TryParseDate(row[_columnMapping.DateColumn], out var date))
                    return ParseResult.Failure("Date invalide");

                if (!TryParseDuration(row[_columnMapping.DurationColumn], out int duration))
                    return ParseResult.Failure("Format de durée invalide");

                return ParseResult.Success(new ListeningEntry
                {
                    Track = title.Trim(),
                    Artist = artist.Trim(),
                    Album = album?.Trim() ?? "Unknown",
                    Duration = duration,
                    Date = date
                });
            }
            catch (Exception ex)
            {
                return ParseResult.Failure($"Erreur: {ex.Message}");
            }
        }

        private bool TryParseDuration(object value, out int duration)
        {
            duration = 0;
            if (value == null || value == DBNull.Value) return true;
            if (value is double d) { duration = (int)Math.Round(d); return true; }
            if (value is int i) { duration = i; return true; }
            return int.TryParse(value.ToString(), out duration);
        }

        private bool TryParseDate(object value, out DateTime date)
        {
            date = DateTime.MinValue;
            if (value == null || value == DBNull.Value) return false;
            if (value is DateTime dt) { date = dt; return true; }
            return DateTime.TryParse(value.ToString(), out date);
        }

        private bool ValidateRequiredColumns(DataRow row, out string? error)
        {
            error = null;
            int maxCol = Math.Max(_columnMapping.DateColumn, _columnMapping.DurationColumn);
            if (row.Table.Columns.Count <= maxCol)
            {
                error = "Colonnes manquantes dans le fichier";
                return false;
            }
            return true;
        }

        private static string? GetSafeString(DataRow row, int col) => row[col]?.ToString();
    }

    // --- CLASSES DE SUPPORT ---

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
        private ParseResult(bool s, ListeningEntry? e, string? m) { IsSuccess = s; ListeningEntry = e; ErrorMessage = m; }
        public static ParseResult Success(ListeningEntry e) => new(true, e, null);
        public static ParseResult Failure(string m) => new(false, null, m);
    }
}