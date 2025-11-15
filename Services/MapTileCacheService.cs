using System;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace GeoLens.Services
{
    /// <summary>
    /// Map tile cache service with HTTP cache header support and WebView2 integration.
    /// Caches Leaflet map tiles locally for offline viewing and improved performance.
    /// </summary>
    public class MapTileCacheService : IDisposable
    {
        private readonly string _cacheDirectory;
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly ConcurrentDictionary<string, CachedTile> _memoryCache;
        private readonly SemaphoreSlim _dbLock;
        private readonly HttpClient _httpClient;
        private bool _isDisposed;
        private bool _isInitialized;

        // Configuration
        private readonly int _defaultExpirationDays;
        private readonly long _maxCacheSizeBytes;

        // Statistics tracking
        private long _cacheHits;
        private long _cacheMisses;
        private long _networkRequests;
        private long _bytesDownloaded;

        // Supported tile providers
        private readonly string[] _supportedProviders = new[]
        {
            "tile.openstreetmap.org",
            "a.tile.openstreetmap.org",
            "b.tile.openstreetmap.org",
            "c.tile.openstreetmap.org",
            "cartodb-basemaps",
            "stamen-tiles",
            "api.mapbox.com",
            "api.maptiler.com"
        };

        public MapTileCacheService(int defaultExpirationDays = 30, int maxCacheSizeMB = 500)
        {
            _defaultExpirationDays = defaultExpirationDays;
            _maxCacheSizeBytes = maxCacheSizeMB * 1024L * 1024L;

            // Cache location: AppData/Local/GeoLens/MapTiles/
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheDirectory = Path.Combine(appDataPath, "GeoLens", "MapTiles");
            _dbPath = Path.Combine(_cacheDirectory, "tiles.db");

            _connectionString = $"Data Source={_dbPath};Version=3;Journal Mode=WAL;Pooling=True;Max Pool Size=10;";
            _memoryCache = new ConcurrentDictionary<string, CachedTile>();
            _dbLock = new SemaphoreSlim(1, 1);

            // Configure HTTP client with headers for tile servers
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GeoLens/2.4.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            Debug.WriteLine($"[MapTileCacheService] Initialized with {defaultExpirationDays} day expiration, max size {maxCacheSizeMB}MB");
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
                        CREATE TABLE IF NOT EXISTS tiles (
                            url_hash TEXT PRIMARY KEY NOT NULL,
                            tile_url TEXT NOT NULL,
                            tile_path TEXT NOT NULL,
                            provider TEXT NOT NULL,
                            file_size INTEGER NOT NULL,
                            etag TEXT,
                            last_modified TEXT,
                            cached_at TEXT NOT NULL,
                            accessed_at TEXT NOT NULL,
                            expires_at TEXT NOT NULL,
                            access_count INTEGER DEFAULT 1
                        );

                        CREATE INDEX IF NOT EXISTS idx_provider ON tiles(provider);
                        CREATE INDEX IF NOT EXISTS idx_accessed_at ON tiles(accessed_at);
                        CREATE INDEX IF NOT EXISTS idx_expires_at ON tiles(expires_at);
                        CREATE INDEX IF NOT EXISTS idx_access_count ON tiles(access_count DESC);
                    ";

                    await command.ExecuteNonQueryAsync();
                    _isInitialized = true;

                    Debug.WriteLine($"[MapTileCacheService] Database initialized at: {_dbPath}");
                }
                finally
                {
                    _dbLock.Release();
                }

                // Cleanup expired tiles on startup
                await CleanupExpiredTilesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Failed to initialize database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Register WebView2 interception for tile requests
        /// </summary>
        public void RegisterWebViewInterception(CoreWebView2 webView)
        {
            webView.WebResourceRequested += WebView_WebResourceRequested;

            // Add filter for tile URLs
            foreach (var provider in _supportedProviders)
            {
                webView.AddWebResourceRequestedFilter(
                    $"*{provider}*",
                    CoreWebView2WebResourceContext.Image);
            }

            Debug.WriteLine("[MapTileCacheService] WebView2 interception registered");
        }

        /// <summary>
        /// Unregister WebView2 interception
        /// </summary>
        public void UnregisterWebViewInterception(CoreWebView2 webView)
        {
            webView.WebResourceRequested -= WebView_WebResourceRequested;

            foreach (var provider in _supportedProviders)
            {
                webView.RemoveWebResourceRequestedFilter(
                    $"*{provider}*",
                    CoreWebView2WebResourceContext.Image);
            }

            Debug.WriteLine("[MapTileCacheService] WebView2 interception unregistered");
        }

        /// <summary>
        /// Handle WebView2 resource requests for map tiles
        /// </summary>
        private async void WebView_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                var requestUri = args.Request.Uri;

                // Only handle supported tile providers
                if (!IsTileRequest(requestUri))
                {
                    return;
                }

                Debug.WriteLine($"[MapTileCacheService] Intercepted tile request: {requestUri}");

                // Try to get from cache
                var cachedTile = await GetCachedTileAsync(requestUri);

                if (cachedTile != null)
                {
                    // Serve from cache
                    Debug.WriteLine($"[MapTileCacheService] Serving from cache: {requestUri}");
                    args.Response = sender.Environment.CreateWebResourceResponse(
                        File.OpenRead(cachedTile.TilePath),
                        200,
                        "OK",
                        $"Content-Type: image/png\nCache-Control: max-age=2592000");
                    Interlocked.Increment(ref _cacheHits);
                }
                else
                {
                    // Download and cache
                    Debug.WriteLine($"[MapTileCacheService] Downloading tile: {requestUri}");
                    var tileData = await DownloadTileAsync(requestUri);

                    if (tileData != null)
                    {
                        // Cache the tile
                        await CacheTileAsync(requestUri, tileData.Data, tileData.ETag, tileData.LastModified);

                        // Serve the downloaded tile
                        args.Response = sender.Environment.CreateWebResourceResponse(
                            new MemoryStream(tileData.Data),
                            200,
                            "OK",
                            $"Content-Type: image/png\nCache-Control: max-age=2592000");

                        Interlocked.Increment(ref _cacheMisses);
                        Interlocked.Increment(ref _networkRequests);
                        Interlocked.Add(ref _bytesDownloaded, tileData.Data.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error handling tile request: {ex.Message}");
            }
            finally
            {
                deferral.Complete();
            }
        }

        /// <summary>
        /// Check if a URL is a tile request
        /// </summary>
        private bool IsTileRequest(string url)
        {
            return _supportedProviders.Any(provider => url.Contains(provider)) &&
                   (url.EndsWith(".png") || url.Contains("/tiles/"));
        }

        /// <summary>
        /// Get provider name from URL
        /// </summary>
        private string GetProviderFromUrl(string url)
        {
            foreach (var provider in _supportedProviders)
            {
                if (url.Contains(provider))
                {
                    return provider;
                }
            }
            return "unknown";
        }

        /// <summary>
        /// Compute hash for tile URL
        /// </summary>
        private string ComputeUrlHash(string url)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Get cached tile if available and not expired
        /// </summary>
        private async Task<CachedTile?> GetCachedTileAsync(string tileUrl)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                var urlHash = ComputeUrlHash(tileUrl);

                // Check memory cache first
                if (_memoryCache.TryGetValue(urlHash, out var cachedTile))
                {
                    if (cachedTile.ExpiresAt > DateTime.UtcNow && File.Exists(cachedTile.TilePath))
                    {
                        _ = UpdateAccessTimeAsync(urlHash); // Fire and forget
                        return cachedTile;
                    }
                    else
                    {
                        _memoryCache.TryRemove(urlHash, out _);
                    }
                }

                // Check database
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT tile_path, provider, file_size, etag, last_modified, cached_at, accessed_at, expires_at, access_count
                        FROM tiles
                        WHERE url_hash = @hash AND datetime(expires_at) > datetime('now')
                    ";
                    command.Parameters.AddWithValue("@hash", urlHash);

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var tilePath = reader.GetString(0);

                        if (File.Exists(tilePath))
                        {
                            var tile = new CachedTile
                            {
                                UrlHash = urlHash,
                                TileUrl = tileUrl,
                                TilePath = tilePath,
                                Provider = reader.GetString(1),
                                FileSize = reader.GetInt64(2),
                                ETag = reader.IsDBNull(3) ? null : reader.GetString(3),
                                LastModified = reader.IsDBNull(4) ? null : reader.GetString(4),
                                CachedAt = DateTime.Parse(reader.GetString(5)),
                                AccessedAt = DateTime.Parse(reader.GetString(6)),
                                ExpiresAt = DateTime.Parse(reader.GetString(7)),
                                AccessCount = reader.GetInt32(8)
                            };

                            _memoryCache.TryAdd(urlHash, tile);
                            _ = UpdateAccessTimeAsync(urlHash); // Fire and forget
                            return tile;
                        }
                        else
                        {
                            // Tile file deleted, remove from database
                            await RemoveFromDatabaseAsync(urlHash);
                        }
                    }
                }
                finally
                {
                    _dbLock.Release();
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error getting cached tile: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Download a tile from the network
        /// </summary>
        private async Task<TileDownloadResult?> DownloadTileAsync(string tileUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(tileUrl);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsByteArrayAsync();

                    string? etag = null;
                    string? lastModified = null;

                    if (response.Headers.ETag != null)
                    {
                        etag = response.Headers.ETag.Tag;
                    }

                    if (response.Content.Headers.LastModified.HasValue)
                    {
                        lastModified = response.Content.Headers.LastModified.Value.ToString("R");
                    }

                    return new TileDownloadResult
                    {
                        Data = data,
                        ETag = etag,
                        LastModified = lastModified
                    };
                }
                else
                {
                    Debug.WriteLine($"[MapTileCacheService] Failed to download tile: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error downloading tile: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cache a downloaded tile
        /// </summary>
        private async Task CacheTileAsync(string tileUrl, byte[] tileData, string? etag, string? lastModified)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                var urlHash = ComputeUrlHash(tileUrl);
                var provider = GetProviderFromUrl(tileUrl);
                var tilePath = Path.Combine(_cacheDirectory, $"{urlHash}.png");

                // Save tile to disk
                await File.WriteAllBytesAsync(tilePath, tileData);

                var now = DateTime.UtcNow;
                var expiresAt = now.AddDays(_defaultExpirationDays);

                // Store in database
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                        INSERT OR REPLACE INTO tiles
                        (url_hash, tile_url, tile_path, provider, file_size, etag, last_modified, cached_at, accessed_at, expires_at, access_count)
                        VALUES (@hash, @url, @path, @provider, @size, @etag, @modified, @cached, @accessed, @expires, 1)
                    ";
                    command.Parameters.AddWithValue("@hash", urlHash);
                    command.Parameters.AddWithValue("@url", tileUrl);
                    command.Parameters.AddWithValue("@path", tilePath);
                    command.Parameters.AddWithValue("@provider", provider);
                    command.Parameters.AddWithValue("@size", tileData.Length);
                    command.Parameters.AddWithValue("@etag", (object?)etag ?? DBNull.Value);
                    command.Parameters.AddWithValue("@modified", (object?)lastModified ?? DBNull.Value);
                    command.Parameters.AddWithValue("@cached", now.ToString("o"));
                    command.Parameters.AddWithValue("@accessed", now.ToString("o"));
                    command.Parameters.AddWithValue("@expires", expiresAt.ToString("o"));

                    await command.ExecuteNonQueryAsync();

                    // Add to memory cache
                    var tile = new CachedTile
                    {
                        UrlHash = urlHash,
                        TileUrl = tileUrl,
                        TilePath = tilePath,
                        Provider = provider,
                        FileSize = tileData.Length,
                        ETag = etag,
                        LastModified = lastModified,
                        CachedAt = now,
                        AccessedAt = now,
                        ExpiresAt = expiresAt,
                        AccessCount = 1
                    };
                    _memoryCache.AddOrUpdate(urlHash, tile, (_, _) => tile);

                    Debug.WriteLine($"[MapTileCacheService] Cached tile: {provider}, {tileData.Length} bytes");
                }
                finally
                {
                    _dbLock.Release();
                }

                // Enforce size limit
                await EnforceSizeLimitAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error caching tile: {ex.Message}");
            }
        }

        /// <summary>
        /// Update access time and increment access count
        /// </summary>
        private async Task UpdateAccessTimeAsync(string urlHash)
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
                        UPDATE tiles
                        SET accessed_at = @accessed,
                            access_count = access_count + 1
                        WHERE url_hash = @hash
                    ";
                    command.Parameters.AddWithValue("@accessed", DateTime.UtcNow.ToString("o"));
                    command.Parameters.AddWithValue("@hash", urlHash);

                    await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error updating access time: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove a tile from the database
        /// </summary>
        private async Task RemoveFromDatabaseAsync(string urlHash)
        {
            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = "DELETE FROM tiles WHERE url_hash = @hash";
                    command.Parameters.AddWithValue("@hash", urlHash);

                    await command.ExecuteNonQueryAsync();
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error removing from database: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleanup expired tiles
        /// </summary>
        private async Task CleanupExpiredTilesAsync()
        {
            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    // Get expired tiles
                    using var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = @"
                        SELECT url_hash, tile_path
                        FROM tiles
                        WHERE datetime(expires_at) < datetime('now')
                    ";

                    var toDelete = new System.Collections.Generic.List<(string Hash, string Path)>();
                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            toDelete.Add((reader.GetString(0), reader.GetString(1)));
                        }
                    }

                    // Delete expired tiles
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
                            deleteCommand.CommandText = "DELETE FROM tiles WHERE url_hash = @hash";
                            deleteCommand.Parameters.AddWithValue("@hash", hash);
                            await deleteCommand.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MapTileCacheService] Error deleting expired tile {path}: {ex.Message}");
                        }
                    }

                    if (toDelete.Count > 0)
                    {
                        Debug.WriteLine($"[MapTileCacheService] Cleaned up {toDelete.Count} expired tiles");
                    }
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error cleaning up expired tiles: {ex.Message}");
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

                Debug.WriteLine($"[MapTileCacheService] Cache size {currentSize / (1024.0 * 1024.0):F1}MB exceeds limit, performing LRU eviction");

                await _dbLock.WaitAsync();
                try
                {
                    using var connection = new SQLiteConnection(_connectionString);
                    await connection.OpenAsync();

                    // Get least recently used tiles
                    using var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = @"
                        SELECT url_hash, tile_path, file_size
                        FROM tiles
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

                    // Delete selected tiles
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
                            deleteCommand.CommandText = "DELETE FROM tiles WHERE url_hash = @hash";
                            deleteCommand.Parameters.AddWithValue("@hash", hash);
                            await deleteCommand.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MapTileCacheService] Error deleting tile {path}: {ex.Message}");
                        }
                    }

                    Debug.WriteLine($"[MapTileCacheService] Evicted {toDelete.Count} tiles, freed {toDelete.Sum(t => t.Size) / (1024.0 * 1024.0):F1}MB");
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error enforcing size limit: {ex.Message}");
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
                    command.CommandText = "SELECT COALESCE(SUM(file_size), 0) FROM tiles";

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
                Debug.WriteLine($"[MapTileCacheService] Error getting cache size: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public async Task<MapTileCacheStatistics> GetStatisticsAsync()
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
                            COUNT(DISTINCT provider) as provider_count
                        FROM tiles
                    ";

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var stats = new MapTileCacheStatistics
                        {
                            TotalTiles = reader.GetInt32(0),
                            TotalSizeBytes = reader.GetInt64(1),
                            UniqueProviders = reader.GetInt32(2),
                            CacheHits = _cacheHits,
                            CacheMisses = _cacheMisses,
                            NetworkRequests = _networkRequests,
                            BytesDownloaded = _bytesDownloaded,
                            MemoryCacheSize = _memoryCache.Count
                        };

                        return stats;
                    }
                }
                finally
                {
                    _dbLock.Release();
                }

                return new MapTileCacheStatistics();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error getting statistics: {ex.Message}");
                return new MapTileCacheStatistics();
            }
        }

        /// <summary>
        /// Clear all cached tiles
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

                    // Get all tile paths
                    using var selectCommand = connection.CreateCommand();
                    selectCommand.CommandText = "SELECT tile_path FROM tiles";

                    var tilePaths = new System.Collections.Generic.List<string>();
                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tilePaths.Add(reader.GetString(0));
                        }
                    }

                    // Delete all tile files
                    foreach (var path in tilePaths)
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
                            Debug.WriteLine($"[MapTileCacheService] Error deleting tile {path}: {ex.Message}");
                        }
                    }

                    // Clear database
                    using var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM tiles";
                    await deleteCommand.ExecuteNonQueryAsync();

                    // Clear memory cache
                    _memoryCache.Clear();

                    Debug.WriteLine($"[MapTileCacheService] Cleared {tilePaths.Count} tiles");
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
                Debug.WriteLine($"[MapTileCacheService] Error clearing cache: {ex.Message}");
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

                    Debug.WriteLine("[MapTileCacheService] Database vacuumed successfully");
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MapTileCacheService] Error vacuuming database: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _httpClient?.Dispose();
            _dbLock?.Dispose();
            _memoryCache?.Clear();
            _isDisposed = true;

            SQLiteConnection.ClearAllPools();
            GC.SuppressFinalize(this);

            Debug.WriteLine("[MapTileCacheService] Disposed");
        }
    }

    /// <summary>
    /// Cached tile entry
    /// </summary>
    public class CachedTile
    {
        public string UrlHash { get; set; } = string.Empty;
        public string TileUrl { get; set; } = string.Empty;
        public string TilePath { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? ETag { get; set; }
        public string? LastModified { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime AccessedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int AccessCount { get; set; }
    }

    /// <summary>
    /// Tile download result
    /// </summary>
    internal class TileDownloadResult
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string? ETag { get; set; }
        public string? LastModified { get; set; }
    }

    /// <summary>
    /// Map tile cache statistics
    /// </summary>
    public class MapTileCacheStatistics
    {
        public int TotalTiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public int UniqueProviders { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public long NetworkRequests { get; set; }
        public long BytesDownloaded { get; set; }
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

        public string BytesDownloadedFormatted
        {
            get
            {
                if (BytesDownloaded < 1024)
                    return $"{BytesDownloaded} B";
                if (BytesDownloaded < 1024 * 1024)
                    return $"{BytesDownloaded / 1024.0:F1} KB";
                return $"{BytesDownloaded / (1024.0 * 1024.0):F1} MB";
            }
        }
    }
}
