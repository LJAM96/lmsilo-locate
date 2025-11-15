using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using GeoLens.Models;
using Microsoft.Data.Sqlite;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace GeoLens.Services;

/// <summary>
/// Service for audit logging all image processing operations.
/// Provides comprehensive tracking for compliance and review purposes.
/// </summary>
public class AuditLogService : IDisposable
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _disposed;

    public AuditLogService()
    {
        // Store audit log in LocalApplicationData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var geoLensPath = Path.Combine(appDataPath, "GeoLens");
        Directory.CreateDirectory(geoLensPath);

        _dbPath = Path.Combine(geoLensPath, "audit.db");
        _connectionString = $"Data Source={_dbPath}";

        InitializeDatabaseAsync().Wait();
    }

    /// <summary>
    /// Initializes the SQLite database and creates tables if needed.
    /// </summary>
    private async Task InitializeDatabaseAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS audit_log (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp TEXT NOT NULL,
                    filename TEXT NOT NULL,
                    filepath TEXT NOT NULL,
                    image_hash TEXT NOT NULL,
                    windows_user TEXT NOT NULL,
                    processing_time_ms INTEGER NOT NULL,
                    predictions_json TEXT NOT NULL,
                    exif_gps_present INTEGER NOT NULL,
                    success INTEGER NOT NULL DEFAULT 1,
                    error_message TEXT,
                    created_at TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON audit_log(timestamp);
                CREATE INDEX IF NOT EXISTS idx_audit_user ON audit_log(windows_user);
                CREATE INDEX IF NOT EXISTS idx_audit_hash ON audit_log(image_hash);
                CREATE INDEX IF NOT EXISTS idx_audit_created ON audit_log(created_at);
            ";

            await using var command = new SqliteCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync();

            Debug.WriteLine($"Audit log database initialized at: {_dbPath}");
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Logs a processing operation to the audit database.
    /// </summary>
    public async Task LogProcessingOperationAsync(AuditLogEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Serialize predictions to JSON
            var predictionsJson = JsonSerializer.Serialize(entry.Predictions, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            var insertSql = @"
                INSERT INTO audit_log
                (timestamp, filename, filepath, image_hash, windows_user, processing_time_ms,
                 predictions_json, exif_gps_present, success, error_message)
                VALUES
                (@timestamp, @filename, @filepath, @imageHash, @windowsUser, @processingTimeMs,
                 @predictionsJson, @exifGpsPresent, @success, @errorMessage)
            ";

            await using var command = new SqliteCommand(insertSql, connection);
            command.Parameters.AddWithValue("@timestamp", entry.Timestamp.ToString("O")); // ISO 8601
            command.Parameters.AddWithValue("@filename", entry.Filename);
            command.Parameters.AddWithValue("@filepath", entry.Filepath);
            command.Parameters.AddWithValue("@imageHash", entry.ImageHash);
            command.Parameters.AddWithValue("@windowsUser", entry.WindowsUser);
            command.Parameters.AddWithValue("@processingTimeMs", entry.ProcessingTimeMs);
            command.Parameters.AddWithValue("@predictionsJson", predictionsJson);
            command.Parameters.AddWithValue("@exifGpsPresent", entry.ExifGpsPresent ? 1 : 0);
            command.Parameters.AddWithValue("@success", entry.Success ? 1 : 0);
            command.Parameters.AddWithValue("@errorMessage", (object?)entry.ErrorMessage ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();

            Debug.WriteLine($"Audit logged: {entry.Filename} by {entry.WindowsUser} in {entry.ProcessingTimeMs}ms");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write audit log: {ex.Message}");
            // Don't throw - audit logging should never break the main flow
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Gets all audit log entries.
    /// </summary>
    public async Task<List<AuditLogEntry>> GetAllEntriesAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var selectSql = "SELECT * FROM audit_log ORDER BY timestamp DESC";
            await using var command = new SqliteCommand(selectSql, connection);
            await using var reader = await command.ExecuteReaderAsync();

            var entries = new List<AuditLogEntry>();
            while (await reader.ReadAsync())
            {
                entries.Add(ReadEntryFromReader(reader));
            }

            return entries;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Gets audit log entries within a date range.
    /// </summary>
    public async Task<List<AuditLogEntry>> GetEntriesByDateRangeAsync(DateTime start, DateTime end)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var selectSql = @"
                SELECT * FROM audit_log
                WHERE timestamp BETWEEN @start AND @end
                ORDER BY timestamp DESC
            ";

            await using var command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@start", start.ToString("O"));
            command.Parameters.AddWithValue("@end", end.ToString("O"));

            await using var reader = await command.ExecuteReaderAsync();

            var entries = new List<AuditLogEntry>();
            while (await reader.ReadAsync())
            {
                entries.Add(ReadEntryFromReader(reader));
            }

            return entries;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Gets audit log entries for a specific Windows user.
    /// </summary>
    public async Task<List<AuditLogEntry>> GetEntriesByUserAsync(string username)
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var selectSql = @"
                SELECT * FROM audit_log
                WHERE windows_user = @username
                ORDER BY timestamp DESC
            ";

            await using var command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@username", username);

            await using var reader = await command.ExecuteReaderAsync();

            var entries = new List<AuditLogEntry>();
            while (await reader.ReadAsync())
            {
                entries.Add(ReadEntryFromReader(reader));
            }

            return entries;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Gets the total count of audit log entries.
    /// </summary>
    public async Task<int> GetTotalCountAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var countSql = "SELECT COUNT(*) FROM audit_log";
            await using var command = new SqliteCommand(countSql, connection);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Gets the timestamp of the oldest audit log entry.
    /// </summary>
    public async Task<DateTime?> GetOldestEntryDateAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var minSql = "SELECT MIN(timestamp) FROM audit_log";
            await using var command = new SqliteCommand(minSql, connection);

            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                return null;

            return DateTime.Parse(result.ToString()!);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Clears all audit log entries (requires user confirmation in UI).
    /// </summary>
    public async Task ClearAllEntriesAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var deleteSql = "DELETE FROM audit_log";
            await using var command = new SqliteCommand(deleteSql, connection);
            await command.ExecuteNonQueryAsync();

            // Vacuum to reclaim space
            var vacuumSql = "VACUUM";
            await using var vacuumCommand = new SqliteCommand(vacuumSql, connection);
            await vacuumCommand.ExecuteNonQueryAsync();

            Debug.WriteLine("Audit log cleared");
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Exports all audit log entries to CSV format.
    /// </summary>
    public async Task ExportToCsvAsync(string outputPath)
    {
        var entries = await GetAllEntriesAsync();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        await using var writer = new StreamWriter(outputPath);
        await using var csv = new CsvWriter(writer, config);

        // Write header
        csv.WriteField("Timestamp");
        csv.WriteField("Filename");
        csv.WriteField("Filepath");
        csv.WriteField("ImageHash");
        csv.WriteField("WindowsUser");
        csv.WriteField("ProcessingTimeMs");
        csv.WriteField("ExifGpsPresent");
        csv.WriteField("Success");
        csv.WriteField("ErrorMessage");
        csv.WriteField("PredictionCount");
        csv.WriteField("TopPredictionLat");
        csv.WriteField("TopPredictionLon");
        csv.WriteField("TopPredictionLocation");
        csv.WriteField("TopPredictionProbability");
        await csv.NextRecordAsync();

        // Write data
        foreach (var entry in entries)
        {
            csv.WriteField(entry.Timestamp.ToString("O"));
            csv.WriteField(entry.Filename);
            csv.WriteField(entry.Filepath);
            csv.WriteField(entry.ImageHash);
            csv.WriteField(entry.WindowsUser);
            csv.WriteField(entry.ProcessingTimeMs);
            csv.WriteField(entry.ExifGpsPresent);
            csv.WriteField(entry.Success);
            csv.WriteField(entry.ErrorMessage ?? "");

            if (entry.Predictions.Any())
            {
                var topPred = entry.Predictions.First();
                csv.WriteField(entry.Predictions.Count);
                csv.WriteField(topPred.Latitude);
                csv.WriteField(topPred.Longitude);
                csv.WriteField(topPred.LocationName ?? "");
                csv.WriteField(topPred.AdjustedProbability);
            }
            else
            {
                csv.WriteField(0);
                csv.WriteField("");
                csv.WriteField("");
                csv.WriteField("");
                csv.WriteField("");
            }

            await csv.NextRecordAsync();
        }

        Debug.WriteLine($"Audit log exported to CSV: {outputPath}");
    }

    /// <summary>
    /// Exports all audit log entries to JSON format.
    /// </summary>
    public async Task ExportToJsonAsync(string outputPath)
    {
        var entries = await GetAllEntriesAsync();

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(outputPath, json);

        Debug.WriteLine($"Audit log exported to JSON: {outputPath}");
    }

    /// <summary>
    /// Exports all audit log entries to PDF format.
    /// </summary>
    public async Task ExportToPdfAsync(string outputPath)
    {
        var entries = await GetAllEntriesAsync();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);

                page.Header().Text("GeoLens Audit Log")
                    .FontSize(20)
                    .Bold()
                    .AlignCenter();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Timestamp
                        columns.RelativeColumn(2); // Filename
                        columns.RelativeColumn(1); // User
                        columns.RelativeColumn(1); // Time (ms)
                        columns.RelativeColumn(1); // Success
                        columns.RelativeColumn(1); // Predictions
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Timestamp");
                        header.Cell().Element(CellStyle).Text("Filename");
                        header.Cell().Element(CellStyle).Text("User");
                        header.Cell().Element(CellStyle).Text("Time (ms)");
                        header.Cell().Element(CellStyle).Text("Success");
                        header.Cell().Element(CellStyle).Text("Predictions");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container
                                .Border(1)
                                .BorderColor(Colors.Grey.Medium)
                                .Background(Colors.Grey.Lighten3)
                                .Padding(5)
                                .AlignCenter();
                        }
                    });

                    // Data rows
                    foreach (var entry in entries)
                    {
                        table.Cell().Element(CellStyle).Text(entry.Timestamp.ToLocalTime().ToString("g"));
                        table.Cell().Element(CellStyle).Text(entry.Filename);
                        table.Cell().Element(CellStyle).Text(entry.WindowsUser);
                        table.Cell().Element(CellStyle).Text(entry.ProcessingTimeMs.ToString());
                        table.Cell().Element(CellStyle).Text(entry.Success ? "✓" : "✗");
                        table.Cell().Element(CellStyle).Text(entry.Predictions.Count.ToString());

                        static IContainer CellStyle(IContainer container)
                        {
                            return container
                                .Border(1)
                                .BorderColor(Colors.Grey.Medium)
                                .Padding(3);
                        }
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Generated on ");
                        text.Span(DateTime.Now.ToString("g"));
                        text.Span(" • Total entries: ");
                        text.Span(entries.Count.ToString());
                    });
            });
        });

        document.GeneratePdf(outputPath);

        Debug.WriteLine($"Audit log exported to PDF: {outputPath}");
    }

    /// <summary>
    /// Reads an AuditLogEntry from a SqliteDataReader.
    /// </summary>
    private AuditLogEntry ReadEntryFromReader(SqliteDataReader reader)
    {
        var predictionsJson = reader.GetString(reader.GetOrdinal("predictions_json"));
        var predictions = JsonSerializer.Deserialize<List<PredictionResult>>(predictionsJson) ?? new();

        return new AuditLogEntry
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("timestamp"))),
            Filename = reader.GetString(reader.GetOrdinal("filename")),
            Filepath = reader.GetString(reader.GetOrdinal("filepath")),
            ImageHash = reader.GetString(reader.GetOrdinal("image_hash")),
            WindowsUser = reader.GetString(reader.GetOrdinal("windows_user")),
            ProcessingTimeMs = reader.GetInt32(reader.GetOrdinal("processing_time_ms")),
            Predictions = predictions,
            ExifGpsPresent = reader.GetInt32(reader.GetOrdinal("exif_gps_present")) == 1,
            Success = reader.GetInt32(reader.GetOrdinal("success")) == 1,
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("error_message"))
                ? null
                : reader.GetString(reader.GetOrdinal("error_message")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at")))
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _dbLock.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
