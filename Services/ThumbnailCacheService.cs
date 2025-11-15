using System;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace GeoLens.Services
{
    /// <summary>
    /// Thumbnail cache service using SQLite and XXHash64 for fast image fingerprinting.
    /// Pre-generates thumbnails asynchronously with LRU eviction when cache exceeds size limit.
    /// </summary>
    public class ThumbnailCacheService : IDisposable
    {
        private readonly string _cacheDirectory;
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly ConcurrentDictionary<string, ThumbnailCacheEntry> _memoryCache;
        private readonly SemaphoreSlim _dbLock;
        private readonly ConcurrentQueue<ThumbnailGenerationRequest> _generationQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _backgroundWorker;
        private bool _isDisposed;
        private bool _isInitialized;

        // Configuration
        private readonly int _thumbnailWidth;
        private readonly long _maxCacheSizeBytes;

        // Statistics tracking
        private long _cacheHits;
        private long _cacheMisses;
        private long _thumbnailsGenerated;

        public ThumbnailCacheService(int thumbnailWidth = 150, int maxCacheSizeMB = 100)
        {
            _thumbnailWidth = thumbnailWidth;
            _maxCacheSizeBytes = maxCacheSizeMB * 1024L * 1024L;

            // Cache location: AppData/Local/GeoLens/Thumbnails/
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheDirectory = Path.Combine(appDataPath, "GeoLens", "Thumbnails");
            _dbPath = Path.Combine(_cacheDirectory, "thumbnails.db");

            _connectionString = $"Data Source={_dbPath};Version=3;Journal Mode=WAL;Pooling=True;Max Pool Size=10;";
            _memoryCache = new ConcurrentDictionary<string, ThumbnailCacheEntry>();
            _dbLock = new SemaphoreSlim(1, 1);
            _generationQueue = new ConcurrentQueue<ThumbnailGenerationRequest>();
            _cancellationTokenSource = new CancellationTokenSource();
            _isInitialized = false;

            // Start background worker for thumbnail generation
            _backgroundWorker = Task.Run(() => BackgroundThumbnailGeneratorAsync(_cancellationTokenSource.Token));

            Debug.WriteLine($"[ThumbnailCacheService] Initialized with {thumbnailWidth}px thumbnails, max size {maxCacheSizeMB}MB");
        }

        /// <summary>
        /// Initialize the database schema and cache directory
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
                return;

            try
            {
                // Ensure cache directory exists
                Directory.CreateDirectory(_cacheDirectory);

                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS thumbnails (
                            image_hash TEXT PRIMARY KEY NOT NULL,
                            original_path TEXT NOT NULL,
                            thumbnail_path TEXT NOT NULL,
                            file_size INTEGER NOT NULL,
                            created_at TEXT NOT NULL,
                            accessed_at TEXT NOT NULL,
                            access_count INTEGER DEFAULT 1
                        );

                        CREATE INDEX IF NOT EXISTS idx_accessed_at ON thumbnails(accessed_at);
                        CREATE INDEX IF NOT EXISTS idx_access_count ON thumbnails(access_count DESC);
                        CREATE INDEX IF NOT EXISTS idx_file_size ON thumbnails(file_size);
                    ";

                    await command.ExecuteNonQueryAsync();
                    _isInitialized = true;

                    Debug.WriteLine($"[ThumbnailCacheService] Database initialized at: {_dbPath}");
                }
                finally
                {
                    _dbLock.Release();
                }

                // Perform cleanup on startup
                await EvictExpiredThumbnailsAsync();
                await EnforceSizeLimitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Failed to initialize database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Compute XXHash64 fingerprint of an image file
        /// </summary>
        public async Task<string> ComputeImageHashAsync(string imagePath)
        {
            try
            {
                await using var stream = File.OpenRead(imagePath);
                var hashBytes = await XxHash64.HashAsync(stream);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Failed to compute hash for {imagePath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Get cached thumbnail for an image, or enqueue for generation if not cached
        /// </summary>
        public async Task<BitmapImage?> GetThumbnailAsync(string imagePath, bool generateIfMissing = true)
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

                    if (File.Exists(cachedEntry.ThumbnailPath))
                    {
                        _ = UpdateAccessTimeAsync(imageHash); // Fire and forget
                        return await LoadThumbnailFromPathAsync(cachedEntry.ThumbnailPath);
                    }
                    else
                    {
                        // Thumbnail file deleted, remove from cache
                        _memoryCache.TryRemove(imageHash, out _);
                        await RemoveFromDatabaseAsync(imageHash);
                    }
                }

                // Check SQLite database
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT thumbnail_path, file_size, created_at, accessed_at, access_count
                        FROM thumbnails
                        WHERE image_hash = @hash
                    ";
                    command.Parameters.AddWithValue("@hash", imageHash);

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var thumbnailPath = reader.GetString(0);
                        var fileSize = reader.GetInt64(1);
                        var createdAt = DateTime.Parse(reader.GetString(2));
                        var accessedAt = DateTime.Parse(reader.GetString(3));
                        var accessCount = reader.GetInt32(4);

                        if (File.Exists(thumbnailPath))
                        {
                            var entry = new ThumbnailCacheEntry
                            {
                                ImageHash = imageHash,
                                OriginalPath = imagePath,
                                ThumbnailPath = thumbnailPath,
                                FileSize = fileSize,
                                CreatedAt = createdAt,
                                AccessedAt = accessedAt,
                                AccessCount = accessCount
                            };

                            _memoryCache.TryAdd(imageHash, entry);
                            Interlocked.Increment(ref _cacheHits);

                            _ = UpdateAccessTimeAsync(imageHash); // Fire and forget
                            return await LoadThumbnailFromPathAsync(thumbnailPath);
                        }
                        else
                        {
                            // Thumbnail file deleted, remove from database
                            await RemoveFromDatabaseAsync(imageHash);
                        }
                    }
                }
                finally
                {
                    _dbLock.Release();
                }

                // Cache miss - enqueue for generation if requested
                Interlocked.Increment(ref _cacheMisses);

                if (generateIfMissing)
                {
                    EnqueueThumbnailGeneration(imagePath, imageHash);
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error getting thumbnail: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Enqueue a thumbnail generation request
        /// </summary>
        private void EnqueueThumbnailGeneration(string imagePath, string imageHash)
        {
            var request = new ThumbnailGenerationRequest
            {
                ImagePath = imagePath,
                ImageHash = imageHash,
                EnqueuedAt = DateTime.UtcNow
            };

            _generationQueue.Enqueue(request);
            Debug.WriteLine($"[ThumbnailCacheService] Enqueued thumbnail generation: {Path.GetFileName(imagePath)}");
        }

        /// <summary>
        /// Background worker that processes thumbnail generation queue
        /// </summary>
        private async Task BackgroundThumbnailGeneratorAsync(CancellationToken cancellationToken)
        {
            Debug.WriteLine("[ThumbnailCacheService] Background worker started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_generationQueue.TryDequeue(out var request))
                    {
                        await GenerateThumbnailAsync(request.ImagePath, request.ImageHash);
                        Interlocked.Increment(ref _thumbnailsGenerated);
                    }
                    else
                    {
                        // No work to do, sleep briefly
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ThumbnailCacheService] Background worker error: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Back off on error
                }
            }

            Debug.WriteLine("[ThumbnailCacheService] Background worker stopped");
        }

        /// <summary>
        /// Generate a thumbnail for the specified image
        /// </summary>
        private async Task GenerateThumbnailAsync(string imagePath, string imageHash)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                Debug.WriteLine($"[ThumbnailCacheService] Generating thumbnail: {Path.GetFileName(imagePath)}");

                var thumbnailPath = Path.Combine(_cacheDirectory, $"{imageHash}.jpg");

                // Generate thumbnail using Windows Imaging
                var file = await StorageFile.GetFileFromPathAsync(imagePath);
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                var decoder = await BitmapDecoder.CreateAsync(stream);

                // Calculate thumbnail dimensions maintaining aspect ratio
                var originalWidth = decoder.PixelWidth;
                var originalHeight = decoder.PixelHeight;
                var aspectRatio = (double)originalWidth / originalHeight;

                uint thumbnailHeight = (uint)(_thumbnailWidth / aspectRatio);

                // Create thumbnail
                using var thumbnailStream = await decoder.GetThumbnailAsync();
                var transform = new BitmapTransform
                {
                    ScaledWidth = (uint)_thumbnailWidth,
                    ScaledHeight = thumbnailHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant // High quality
                };

                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb);

                // Save thumbnail as JPEG
                var thumbnailFile = await StorageFile.GetFileFromPathAsync(
                    Path.GetDirectoryName(thumbnailPath)!);
                var outputFile = await thumbnailFile.CreateFileAsync(
                    Path.GetFileName(thumbnailPath),
                    CreationCollisionOption.ReplaceExisting);

                using var outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);

                encoder.SetPixelData(
                    BitmapPixelFormat.Rgba8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)_thumbnailWidth,
                    thumbnailHeight,
                    decoder.DpiX,
                    decoder.DpiY,
                    pixelData.DetachPixelData());

                await encoder.FlushAsync();

                // Get file size
                var fileInfo = new FileInfo(thumbnailPath);
                var fileSize = fileInfo.Length;

                // Store in database
                await StoreThumbnailMetadataAsync(imageHash, imagePath, thumbnailPath, fileSize);

                Debug.WriteLine($"[ThumbnailCacheService] Generated thumbnail: {fileSize} bytes");

                // Enforce size limit after generating new thumbnail
                await EnforceSizeLimitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Failed to generate thumbnail for {imagePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Store thumbnail metadata in database
        /// </summary>
        private async Task StoreThumbnailMetadataAsync(string imageHash, string originalPath, string thumbnailPath, long fileSize)
        {
            try
            {
                var now = DateTime.UtcNow;

                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT OR REPLACE INTO thumbnails
                        (image_hash, original_path, thumbnail_path, file_size, created_at, accessed_at, access_count)
                        VALUES (@hash, @original, @thumbnail, @size, @created, @accessed, 1)
                    ";
                    command.Parameters.AddWithValue("@hash", imageHash);
                    command.Parameters.AddWithValue("@original", originalPath);
                    command.Parameters.AddWithValue("@thumbnail", thumbnailPath);
                    command.Parameters.AddWithValue("@size", fileSize);
                    command.Parameters.AddWithValue("@created", now.ToString("o"));
                    command.Parameters.AddWithValue("@accessed", now.ToString("o"));

                    await command.ExecuteNonQueryAsync();

                    // Add to memory cache
                    var entry = new ThumbnailCacheEntry
                    {
                        ImageHash = imageHash,
                        OriginalPath = originalPath,
                        ThumbnailPath = thumbnailPath,
                        FileSize = fileSize,
                        CreatedAt = now,
                        AccessedAt = now,
                        AccessCount = 1
                    };
                    _memoryCache.AddOrUpdate(imageHash, entry, (_, _) => entry);
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error storing thumbnail metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Update access time and increment access count
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
                        UPDATE thumbnails
                        SET accessed_at = @accessed,
                            access_count = access_count + 1
                        WHERE image_hash = @hash
                    ";
                    command.Parameters.AddWithValue("@accessed", DateTime.UtcNow.ToString("o"));
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
                Debug.WriteLine($"[ThumbnailCacheService] Error updating access time: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove a thumbnail from the database
        /// </summary>
        private async Task RemoveFromDatabaseAsync(string imageHash)
        {
            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM thumbnails WHERE image_hash = @hash";
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
                Debug.WriteLine($"[ThumbnailCacheService] Error removing from database: {ex.Message}");
            }
        }

        /// <summary>
        /// Load a thumbnail from disk
        /// </summary>
        private async Task<BitmapImage?> LoadThumbnailFromPathAsync(string thumbnailPath)
        {
            try
            {
                var bitmap = new BitmapImage();
                var file = await StorageFile.GetFileFromPathAsync(thumbnailPath);
                using var stream = await file.OpenAsync(FileAccessMode.Read);
                await bitmap.SetSourceAsync(stream);
                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Failed to load thumbnail from {thumbnailPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Enforce cache size limit using LRU eviction
        /// </summary>
        private async Task EnforceSizeLimitAsync()
        {
            try
            {
                var currentSize = await GetTotalCacheSizeAsync();

                if (currentSize <= _maxCacheSizeBytes)
                    return;

                Debug.WriteLine($"[ThumbnailCacheService] Cache size {currentSize / (1024.0 * 1024.0):F1}MB exceeds limit, performing LRU eviction");

                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    // Get least recently used thumbnails
                    using var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = @"
                        SELECT image_hash, thumbnail_path, file_size
                        FROM thumbnails
                        ORDER BY accessed_at ASC
                    ";

                    var toDelete = new System.Collections.Generic.List<(string Hash, string Path, long Size)>();
                    long sizeToFree = currentSize - (_maxCacheSizeBytes * 80 / 100); // Free to 80% capacity

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        long freedSize = 0;
                        while (await reader.ReadAsync() && freedSize < sizeToFree)
                        {
                            var hash = reader.GetString(0);
                            var path = reader.GetString(1);
                            var size = reader.GetInt64(2);

                            toDelete.Add((hash, path, size));
                            freedSize += size;
                        }
                    }

                    // Delete selected thumbnails
                    foreach (var (hash, path, size) in toDelete)
                    {
                        try
                        {
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }

                            _memoryCache.TryRemove(hash, out _);

                            using var deleteCommand = connection.CreateCommand();
                            deleteCommand.CommandText = "DELETE FROM thumbnails WHERE image_hash = @hash";
                            deleteCommand.Parameters.AddWithValue("@hash", hash);
                            await deleteCommand.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ThumbnailCacheService] Error deleting thumbnail {path}: {ex.Message}");
                        }
                    }

                    Debug.WriteLine($"[ThumbnailCacheService] Evicted {toDelete.Count} thumbnails, freed {toDelete.Sum(t => t.Size) / (1024.0 * 1024.0):F1}MB");
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error enforcing size limit: {ex.Message}");
            }
        }

        /// <summary>
        /// Evict thumbnails older than 90 days
        /// </summary>
        private async Task EvictExpiredThumbnailsAsync()
        {
            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    // Get expired thumbnails
                    using var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = @"
                        SELECT image_hash, thumbnail_path
                        FROM thumbnails
                        WHERE datetime(created_at) < datetime('now', '-90 days')
                    ";

                    var toDelete = new System.Collections.Generic.List<(string Hash, string Path)>();
                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            toDelete.Add((reader.GetString(0), reader.GetString(1)));
                        }
                    }

                    // Delete expired thumbnails
                    foreach (var (hash, path) in toDelete)
                    {
                        try
                        {
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }

                            _memoryCache.TryRemove(hash, out _);

                            using var deleteCommand = connection.CreateCommand();
                            deleteCommand.CommandText = "DELETE FROM thumbnails WHERE image_hash = @hash";
                            deleteCommand.Parameters.AddWithValue("@hash", hash);
                            await deleteCommand.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ThumbnailCacheService] Error deleting expired thumbnail {path}: {ex.Message}");
                        }
                    }

                    if (toDelete.Count > 0)
                    {
                        Debug.WriteLine($"[ThumbnailCacheService] Evicted {toDelete.Count} expired thumbnails (>90 days)");
                    }
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error evicting expired thumbnails: {ex.Message}");
            }
        }

        /// <summary>
        /// Get total cache size in bytes
        /// </summary>
        private async Task<long> GetTotalCacheSizeAsync()
        {
            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT COALESCE(SUM(file_size), 0) FROM thumbnails";

                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt64(result);
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error getting cache size: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public async Task<ThumbnailCacheStatistics> GetStatisticsAsync()
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
                            COUNT(*) as total_count,
                            COALESCE(SUM(file_size), 0) as total_size,
                            COALESCE(AVG(file_size), 0) as avg_size
                        FROM thumbnails
                    ";

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var stats = new ThumbnailCacheStatistics
                        {
                            TotalThumbnails = reader.GetInt32(0),
                            TotalSizeBytes = reader.GetInt64(1),
                            AverageSizeBytes = (long)reader.GetDouble(2),
                            CacheHits = _cacheHits,
                            CacheMisses = _cacheMisses,
                            ThumbnailsGenerated = _thumbnailsGenerated,
                            QueuedRequests = _generationQueue.Count,
                            MemoryCacheSize = _memoryCache.Count
                        };

                        return stats;
                    }
                }
                finally
                {
                    _dbLock.Release();
                }

                return new ThumbnailCacheStatistics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error getting statistics: {ex.Message}");
                return new ThumbnailCacheStatistics();
            }
        }

        /// <summary>
        /// Clear all thumbnails from cache
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

                    // Get all thumbnail paths
                    using var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = "SELECT thumbnail_path FROM thumbnails";

                    var thumbnailPaths = new System.Collections.Generic.List<string>();
                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            thumbnailPaths.Add(reader.GetString(0));
                        }
                    }

                    // Delete all thumbnail files
                    foreach (var path in thumbnailPaths)
                    {
                        try
                        {
                            if (File.Exists(path))
                            {
                                File.Delete(path);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ThumbnailCacheService] Error deleting thumbnail {path}: {ex.Message}");
                        }
                    }

                    // Clear database
                    using var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM thumbnails";
                    await deleteCommand.ExecuteNonQueryAsync();

                    // Clear memory cache
                    _memoryCache.Clear();

                    Debug.WriteLine($"[ThumbnailCacheService] Cleared {thumbnailPaths.Count} thumbnails");
                }
                finally
                {
                    _dbLock.Release();
                }

                // Vacuum database
                await VacuumDatabaseAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error clearing cache: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Vacuum the database to reclaim space
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

                    Debug.WriteLine("[ThumbnailCacheService] Database vacuumed successfully");
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error vacuuming database: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            // Stop background worker
            _cancellationTokenSource.Cancel();
            try
            {
                _backgroundWorker.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThumbnailCacheService] Error waiting for background worker: {ex.Message}");
            }

            _cancellationTokenSource.Dispose();
            _dbLock?.Dispose();
            _memoryCache?.Clear();
            _isDisposed = true;

            SQLiteConnection.ClearAllPools();
            GC.SuppressFinalize(this);

            Debug.WriteLine("[ThumbnailCacheService] Disposed");
        }
    }

    /// <summary>
    /// Thumbnail cache entry
    /// </summary>
    public class ThumbnailCacheEntry
    {
        public string ImageHash { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime AccessedAt { get; set; }
        public int AccessCount { get; set; }
    }

    /// <summary>
    /// Thumbnail generation request
    /// </summary>
    internal class ThumbnailGenerationRequest
    {
        public string ImagePath { get; set; } = string.Empty;
        public string ImageHash { get; set; } = string.Empty;
        public DateTime EnqueuedAt { get; set; }
    }

    /// <summary>
    /// Thumbnail cache statistics
    /// </summary>
    public class ThumbnailCacheStatistics
    {
        public int TotalThumbnails { get; set; }
        public long TotalSizeBytes { get; set; }
        public long AverageSizeBytes { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public long ThumbnailsGenerated { get; set; }
        public int QueuedRequests { get; set; }
        public int MemoryCacheSize { get; set; }

        public double HitRate
        {
            get
            {
                var total = CacheHits + CacheMisses;
                return total > 0 ? (double)CacheHits / total : 0.0;
            }
        }

        public string TotalSizeFormatted
        {
            get
            {
                if (TotalSizeBytes < 1024)
                    return $"{TotalSizeBytes} B";
                if (TotalSizeBytes < 1024 * 1024)
                    return $"{TotalSizeBytes / 1024.0:F1} KB";
                return $"{TotalSizeBytes / (1024.0 * 1024.0):F1} MB";
            }
        }
    }
}
