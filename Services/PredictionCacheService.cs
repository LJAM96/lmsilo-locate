using GeoLens.Models;
using GeoLens.Services.DTOs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GeoLens.Services
{
    /// <summary>
    /// Prediction cache service using SQLite and XXHash64 for fast image fingerprinting.
    /// Implements two-tier caching: in-memory for hot data, SQLite for persistence.
    /// </summary>
    public class PredictionCacheService : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly ConcurrentDictionary<string, CachedPredictionEntry> _memoryCache;
        private readonly SemaphoreSlim _dbLock;
        private bool _isDisposed;
        private bool _isInitialized;

        // Statistics tracking
        private long _cacheHits;
        private long _cacheMisses;

        public PredictionCacheService(string? customDbPath = null)
        {
            // Database location: AppData/Local/GeoLens/cache.db
            _dbPath = customDbPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GeoLens",
                "cache.db"
            );

            _connectionString = $"Data Source={_dbPath};Version=3;Journal Mode=WAL;Pooling=True;Max Pool Size=10;";
            _memoryCache = new ConcurrentDictionary<string, CachedPredictionEntry>();
            _dbLock = new SemaphoreSlim(1, 1);
            _isInitialized = false;
        }

        /// <summary>
        /// Initialize the database schema if it doesn't exist
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                // Ensure the directory exists
                var directory = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS predictions (
                            image_hash TEXT PRIMARY KEY NOT NULL,
                            file_path TEXT NOT NULL,
                            predictions_json TEXT NOT NULL,
                            exif_gps_json TEXT,
                            cached_at TEXT NOT NULL,
                            accessed_at TEXT NOT NULL,
                            access_count INTEGER DEFAULT 1
                        );

                        CREATE INDEX IF NOT EXISTS idx_cached_at ON predictions(cached_at);
                        CREATE INDEX IF NOT EXISTS idx_accessed_at ON predictions(accessed_at);
                        CREATE INDEX IF NOT EXISTS idx_access_count ON predictions(access_count DESC);
                    ";

                    await command.ExecuteNonQueryAsync();
                    _isInitialized = true;

                    Debug.WriteLine($"[PredictionCacheService] Database initialized at: {_dbPath}");
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Failed to initialize database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Compute XXHash64 fingerprint of an image file
        /// </summary>
        private async Task<string> ComputeImageHashAsync(string imagePath)
        {
            try
            {
                using var stream = File.OpenRead(imagePath);
                var hashBytes = await XxHash64.HashAsync(stream);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Failed to compute hash for {imagePath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get cached prediction for an image if it exists
        /// </summary>
        public async Task<CachedPredictionEntry?> GetCachedPredictionAsync(string imagePath)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            if (!File.Exists(imagePath))
            {
                return null;
            }

            try
            {
                var imageHash = await ComputeImageHashAsync(imagePath);

                // Check memory cache first (hot path)
                if (_memoryCache.TryGetValue(imageHash, out var cachedEntry))
                {
                    Interlocked.Increment(ref _cacheHits);
                    await UpdateAccessTimeAsync(imageHash);
                    Debug.WriteLine($"[PredictionCacheService] Memory cache hit for: {Path.GetFileName(imagePath)}");
                    return cachedEntry;
                }

                // Check SQLite database
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT file_path, predictions_json, exif_gps_json, cached_at, accessed_at, access_count
                        FROM predictions
                        WHERE image_hash = @hash
                    ";
                    command.Parameters.AddWithValue("@hash", imageHash);

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var filePath = reader.GetString(0);
                        var predictionsJson = reader.GetString(1);
                        var exifGpsJson = reader.IsDBNull(2) ? null : reader.GetString(2);
                        var cachedAt = DateTime.Parse(reader.GetString(3));
                        var accessedAt = DateTime.Parse(reader.GetString(4));
                        var accessCount = reader.GetInt32(5);

                        // Deserialize predictions
                        var predictions = JsonSerializer.Deserialize<List<PredictionCandidate>>(predictionsJson) ?? new List<PredictionCandidate>();

                        // Deserialize EXIF GPS data if present
                        ExifGpsData? exifGps = null;
                        if (!string.IsNullOrEmpty(exifGpsJson))
                        {
                            exifGps = JsonSerializer.Deserialize<ExifGpsData>(exifGpsJson);
                        }

                        var entry = new CachedPredictionEntry
                        {
                            ImageHash = imageHash,
                            FilePath = filePath,
                            Predictions = predictions,
                            ExifGps = exifGps,
                            CachedAt = cachedAt,
                            AccessedAt = accessedAt,
                            AccessCount = accessCount
                        };

                        // Add to memory cache for future lookups
                        _memoryCache.TryAdd(imageHash, entry);

                        Interlocked.Increment(ref _cacheHits);
                        Debug.WriteLine($"[PredictionCacheService] Database cache hit for: {Path.GetFileName(imagePath)}");

                        // Update access time asynchronously (fire and forget)
                        _ = UpdateAccessTimeAsync(imageHash);

                        return entry;
                    }
                }
                finally
                {
                    _dbLock.Release();
                }

                // Cache miss
                Interlocked.Increment(ref _cacheMisses);
                Debug.WriteLine($"[PredictionCacheService] Cache miss for: {Path.GetFileName(imagePath)}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Error getting cached prediction: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Store prediction results in cache
        /// </summary>
        public async Task StorePredictionAsync(
            string imagePath,
            List<PredictionCandidate> predictions,
            ExifGpsData? exifGps = null)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file not found", imagePath);
            }

            try
            {
                var imageHash = await ComputeImageHashAsync(imagePath);
                var predictionsJson = JsonSerializer.Serialize(predictions);
                var exifGpsJson = exifGps != null ? JsonSerializer.Serialize(exifGps) : null;
                var now = DateTime.UtcNow;

                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT OR REPLACE INTO predictions
                        (image_hash, file_path, predictions_json, exif_gps_json, cached_at, accessed_at, access_count)
                        VALUES (@hash, @path, @predictions, @exif, @cached_at, @accessed_at, 1)
                    ";
                    command.Parameters.AddWithValue("@hash", imageHash);
                    command.Parameters.AddWithValue("@path", imagePath);
                    command.Parameters.AddWithValue("@predictions", predictionsJson);
                    command.Parameters.AddWithValue("@exif", (object?)exifGpsJson ?? DBNull.Value);
                    command.Parameters.AddWithValue("@cached_at", now.ToString("o"));
                    command.Parameters.AddWithValue("@accessed_at", now.ToString("o"));

                    await command.ExecuteNonQueryAsync();

                    // Add to memory cache
                    var entry = new CachedPredictionEntry
                    {
                        ImageHash = imageHash,
                        FilePath = imagePath,
                        Predictions = predictions,
                        ExifGps = exifGps,
                        CachedAt = now,
                        AccessedAt = now,
                        AccessCount = 1
                    };
                    _memoryCache.AddOrUpdate(imageHash, entry, (_, _) => entry);

                    Debug.WriteLine($"[PredictionCacheService] Stored prediction for: {Path.GetFileName(imagePath)}");
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Error storing prediction: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Update the access time and increment access count for a cached entry
        /// </summary>
        private async Task UpdateAccessTimeAsync(string imageHash)
        {
            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        UPDATE predictions
                        SET accessed_at = @accessed_at,
                            access_count = access_count + 1
                        WHERE image_hash = @hash
                    ";
                    command.Parameters.AddWithValue("@accessed_at", DateTime.UtcNow.ToString("o"));
                    command.Parameters.AddWithValue("@hash", imageHash);

                    await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Error updating access time: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear cache entries older than the specified number of days
        /// </summary>
        public async Task ClearExpiredAsync(int expirationDays = 90)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        DELETE FROM predictions
                        WHERE datetime(cached_at) < datetime('now', '-' || @days || ' days')
                    ";
                    command.Parameters.AddWithValue("@days", expirationDays);

                    var deletedCount = await command.ExecuteNonQueryAsync();

                    // Clear memory cache to ensure consistency
                    _memoryCache.Clear();

                    Debug.WriteLine($"[PredictionCacheService] Cleared {deletedCount} expired entries (>{expirationDays} days old)");
                }
                finally
                {
                    _dbLock.Release();
                }

                // Vacuum database to reclaim space
                await VacuumDatabaseAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Error clearing expired entries: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get cache statistics including hit rate and entry counts
        /// </summary>
        public async Task<CacheStatistics> GetCacheStatisticsAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT
                            COUNT(*) as total_entries,
                            SUM(access_count) as total_accesses,
                            AVG(access_count) as avg_accesses,
                            MIN(datetime(cached_at)) as oldest_entry,
                            MAX(datetime(accessed_at)) as newest_access
                        FROM predictions
                    ";

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var totalEntries = reader.GetInt32(0);
                        var totalAccesses = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                        var avgAccesses = reader.IsDBNull(2) ? 0.0 : reader.GetDouble(2);

                        DateTime? oldestEntry = null;
                        DateTime? newestAccess = null;

                        if (!reader.IsDBNull(3))
                        {
                            oldestEntry = DateTime.Parse(reader.GetString(3));
                        }

                        if (!reader.IsDBNull(4))
                        {
                            newestAccess = DateTime.Parse(reader.GetString(4));
                        }

                        // Calculate database size
                        var dbSizeBytes = new FileInfo(_dbPath).Length;

                        var stats = new CacheStatistics
                        {
                            TotalEntries = totalEntries,
                            TotalAccesses = totalAccesses,
                            AverageAccesses = avgAccesses,
                            CacheHits = _cacheHits,
                            CacheMisses = _cacheMisses,
                            MemoryCacheSize = _memoryCache.Count,
                            DatabaseSizeBytes = dbSizeBytes,
                            OldestEntryDate = oldestEntry,
                            NewestAccessDate = newestAccess
                        };

                        Debug.WriteLine($"[PredictionCacheService] Statistics: {stats.TotalEntries} entries, {stats.HitRate:P1} hit rate");
                        return stats;
                    }
                }
                finally
                {
                    _dbLock.Release();
                }

                return new CacheStatistics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Error getting statistics: {ex.Message}");
                return new CacheStatistics();
            }
        }

        /// <summary>
        /// Clear all cache entries and reset statistics
        /// </summary>
        public async Task ClearAllAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM predictions";
                    var deletedCount = await command.ExecuteNonQueryAsync();

                    _memoryCache.Clear();
                    Interlocked.Exchange(ref _cacheHits, 0);
                    Interlocked.Exchange(ref _cacheMisses, 0);

                    Debug.WriteLine($"[PredictionCacheService] Cleared all {deletedCount} cache entries");
                }
                finally
                {
                    _dbLock.Release();
                }

                await VacuumDatabaseAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Error clearing all entries: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Vacuum the database to reclaim space after deletions
        /// </summary>
        private async Task VacuumDatabaseAsync()
        {
            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = "VACUUM";
                    await command.ExecuteNonQueryAsync();

                    Debug.WriteLine("[PredictionCacheService] Database vacuumed successfully");
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionCacheService] Error vacuuming database: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _dbLock?.Dispose();
            _memoryCache?.Clear();
            _isDisposed = true;

            SQLiteConnection.ClearAllPools();
            GC.SuppressFinalize(this);

            Debug.WriteLine("[PredictionCacheService] Disposed");
        }
    }

    /// <summary>
    /// Cached prediction entry with metadata
    /// </summary>
    public class CachedPredictionEntry
    {
        public string ImageHash { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public List<PredictionCandidate> Predictions { get; set; } = new();
        public ExifGpsData? ExifGps { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime AccessedAt { get; set; }
        public int AccessCount { get; set; }
    }

    /// <summary>
    /// Cache statistics and performance metrics
    /// </summary>
    public class CacheStatistics
    {
        public int TotalEntries { get; set; }
        public long TotalAccesses { get; set; }
        public double AverageAccesses { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public int MemoryCacheSize { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public DateTime? OldestEntryDate { get; set; }
        public DateTime? NewestAccessDate { get; set; }

        /// <summary>
        /// Cache hit rate (0.0 to 1.0)
        /// </summary>
        public double HitRate
        {
            get
            {
                var totalRequests = CacheHits + CacheMisses;
                return totalRequests > 0 ? (double)CacheHits / totalRequests : 0.0;
            }
        }

        /// <summary>
        /// Human-readable database size
        /// </summary>
        public string DatabaseSizeFormatted
        {
            get
            {
                if (DatabaseSizeBytes < 1024)
                    return $"{DatabaseSizeBytes} B";
                if (DatabaseSizeBytes < 1024 * 1024)
                    return $"{DatabaseSizeBytes / 1024.0:F1} KB";
                if (DatabaseSizeBytes < 1024 * 1024 * 1024)
                    return $"{DatabaseSizeBytes / (1024.0 * 1024.0):F1} MB";
                return $"{DatabaseSizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }
    }
}
