# Service Layer Implementation Guide

## Overview

This document details the implementation of all C# services required for GeoLens.

---

## 1. Hardware Detection Service

### Purpose
Detect available GPU hardware to select the appropriate Python runtime (CPU/CUDA/ROCm).

### Implementation

```csharp
// Services/HardwareDetectionService.cs
using System.Management;

public enum HardwareType { CPU, CUDA, ROCM }

public class HardwareDetectionService
{
    private HardwareType? _cachedResult;

    public HardwareType DetectHardware()
    {
        if (_cachedResult.HasValue)
            return _cachedResult.Value;

        _cachedResult = DetectGPU();
        return _cachedResult.Value;
    }

    private HardwareType DetectGPU()
    {
        try
        {
            if (HasNvidiaGPU())
                return HardwareType.CUDA;

            if (HasAMDGPU())
                return HardwareType.ROCM;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GPU detection failed: {ex.Message}");
        }

        return HardwareType.CPU;
    }

    private bool HasNvidiaGPU()
    {
        // Method 1: Check nvidia-smi
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                process.WaitForExit(3000);
                return process.ExitCode == 0;
            }
        }
        catch { }

        // Method 2: WMI Query
        return QueryVideoController("NVIDIA");
    }

    private bool HasAMDGPU()
    {
        // Check for AMD Radeon GPUs
        return QueryVideoController("AMD") || QueryVideoController("Radeon");
    }

    private bool QueryVideoController(string manufacturer)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_VideoController");

            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                var desc = obj["Description"]?.ToString() ?? "";

                if (name.Contains(manufacturer, StringComparison.OrdinalIgnoreCase) ||
                    desc.Contains(manufacturer, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    public string GetHardwareDisplayName(HardwareType hardware) => hardware switch
    {
        HardwareType.CUDA => "NVIDIA GPU (CUDA)",
        HardwareType.ROCM => "AMD GPU (ROCm)",
        HardwareType.CPU => "CPU Only",
        _ => "Unknown"
    };
}
```

---

## 2. Python Runtime Manager

### Purpose
Launch and manage the embedded Python FastAPI service.

### Implementation

```csharp
// Services/PythonRuntimeManager.cs
public class PythonRuntimeManager : IDisposable
{
    private Process? _apiProcess;
    private readonly string _runtimePath;
    private readonly int _port = 8899;
    private readonly HardwareType _hardware;

    public bool IsRunning { get; private set; }
    public string ApiBaseUrl => $"http://localhost:{_port}";

    public PythonRuntimeManager(HardwareType hardware)
    {
        _hardware = hardware;
        _runtimePath = GetRuntimePath(hardware);
    }

    private string GetRuntimePath(HardwareType hardware)
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "runtime");

        return hardware switch
        {
            HardwareType.CUDA => Path.Combine(basePath, "python_cuda"),
            HardwareType.ROCM => Path.Combine(basePath, "python_rocm"),
            _ => Path.Combine(basePath, "python_cpu")
        };
    }

    public async Task<bool> StartServiceAsync()
    {
        if (IsRunning)
            return true;

        try
        {
            var pythonExe = Path.Combine(_runtimePath, "python.exe");
            var serviceScript = Path.Combine(AppContext.BaseDirectory, "core", "api_service.py");

            if (!File.Exists(pythonExe))
                throw new FileNotFoundException($"Python runtime not found: {pythonExe}");

            if (!File.Exists(serviceScript))
                throw new FileNotFoundException($"Service script not found: {serviceScript}");

            // Set environment variables
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"-m uvicorn api_service:app --host 127.0.0.1 --port {_port}",
                WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "core"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Add Python paths
            var pythonPath = Path.Combine(_runtimePath, "Lib", "site-packages");
            startInfo.EnvironmentVariables["PYTHONPATH"] = pythonPath;
            startInfo.EnvironmentVariables["PYTHONHOME"] = _runtimePath;

            // Set HuggingFace cache to local models
            var modelsPath = Path.Combine(AppContext.BaseDirectory, "models", "geoclip_cache");
            startInfo.EnvironmentVariables["HF_HOME"] = modelsPath;
            startInfo.EnvironmentVariables["TRANSFORMERS_CACHE"] = Path.Combine(modelsPath, "transformers");
            startInfo.EnvironmentVariables["HUGGINGFACE_HUB_CACHE"] = Path.Combine(modelsPath, "hub");

            _apiProcess = Process.Start(startInfo);

            if (_apiProcess == null)
                return false;

            // Wire up output handlers
            _apiProcess.OutputDataReceived += (s, e) => Debug.WriteLine($"[API] {e.Data}");
            _apiProcess.ErrorDataReceived += (s, e) => Debug.WriteLine($"[API ERROR] {e.Data}");
            _apiProcess.BeginOutputReadLine();
            _apiProcess.BeginErrorReadLine();

            // Wait for service to be ready
            IsRunning = await WaitForHealthAsync(TimeSpan.FromSeconds(30));
            return IsRunning;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start Python service: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> WaitForHealthAsync(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.Now + timeout;

        while (DateTime.Now < deadline)
        {
            try
            {
                var response = await client.GetAsync($"{ApiBaseUrl}/health");
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch { }

            await Task.Delay(500);
        }

        return false;
    }

    public async Task StopServiceAsync()
    {
        if (_apiProcess == null || !IsRunning)
            return;

        try
        {
            // Graceful shutdown
            if (!_apiProcess.HasExited)
            {
                _apiProcess.Kill();
                await _apiProcess.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping service: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _apiProcess?.Dispose();
            _apiProcess = null;
        }
    }

    public void Dispose()
    {
        StopServiceAsync().Wait();
    }
}
```

---

## 3. GeoCLIP API Client

### Purpose
HTTP client for communicating with the Python FastAPI service.

### Implementation

```csharp
// Services/GeoCLIPApiClient.cs
public class GeoCLIPApiClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public GeoCLIPApiClient(string baseUrl = "http://localhost:8899")
    {
        _baseUrl = baseUrl;
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<InferenceResponse> PredictAsync(
        IEnumerable<string> imagePaths,
        int topK = 5,
        bool skipMissing = true)
    {
        var items = imagePaths.Select(path => new InferenceItem
        {
            Path = path,
            Md5 = null
        }).ToList();

        var request = new InferenceRequest
        {
            Items = items,
            TopK = topK,
            Device = "auto",
            SkipMissing = skipMissing,
            HfCache = null
        };

        var response = await _http.PostAsJsonAsync("/infer", request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<InferenceResponse>()
            ?? throw new Exception("Failed to deserialize response");
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _http.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// DTOs matching Python Pydantic models
public record InferenceItem
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("md5")]
    public string? Md5 { get; init; }
}

public record InferenceRequest
{
    [JsonPropertyName("items")]
    public List<InferenceItem> Items { get; init; } = new();

    [JsonPropertyName("top_k")]
    public int TopK { get; init; } = 5;

    [JsonPropertyName("device")]
    public string Device { get; init; } = "auto";

    [JsonPropertyName("skip_missing")]
    public bool SkipMissing { get; init; } = true;

    [JsonPropertyName("hf_cache")]
    public string? HfCache { get; init; }
}

public record InferenceResponse
{
    [JsonPropertyName("device")]
    public string Device { get; init; } = "";

    [JsonPropertyName("results")]
    public List<PredictionResultResponse> Results { get; init; } = new();
}

public record PredictionResultResponse
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("md5")]
    public string? Md5 { get; init; }

    [JsonPropertyName("predictions")]
    public List<LocationPrediction> Predictions { get; init; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; init; } = new();

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

public record LocationPrediction
{
    [JsonPropertyName("rank")]
    public int Rank { get; init; }

    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }

    [JsonPropertyName("probability")]
    public double Probability { get; init; }

    [JsonPropertyName("city")]
    public string City { get; init; } = "";

    [JsonPropertyName("state")]
    public string State { get; init; } = "";

    [JsonPropertyName("county")]
    public string County { get; init; } = "";

    [JsonPropertyName("country")]
    public string Country { get; init; } = "";

    [JsonPropertyName("location_summary")]
    public string LocationSummary { get; init; } = "";
}
```

---

## 4. Prediction Cache Service

### Purpose
Store and retrieve prediction results using image hash for instant recall.

### Implementation

```csharp
// Services/PredictionCacheService.cs
using System.Data.SQLite;
using System.IO.Hashing;

public class PredictionCacheService : IDisposable
{
    private readonly SQLiteConnection _connection;
    private readonly Dictionary<string, CachedPrediction> _memoryCache;
    private readonly string _dbPath;

    public PredictionCacheService(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GeoLens",
            "cache.db"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

        _connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
        _connection.Open();
        _memoryCache = new Dictionary<string, CachedPrediction>();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS predictions (
                image_hash TEXT PRIMARY KEY,
                image_path TEXT NOT NULL,
                predictions_json TEXT NOT NULL,
                device TEXT NOT NULL,
                cached_at TEXT NOT NULL,
                hit_count INTEGER DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_cached_at ON predictions(cached_at);
        ";
        command.ExecuteNonQuery();
    }

    public async Task<string> ComputeHashAsync(string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        var hashBytes = await XxHash64.HashAsync(stream);
        return Convert.ToHexString(hashBytes);
    }

    public async Task<CachedPrediction?> GetCachedPredictionAsync(string imagePath)
    {
        var hash = await ComputeHashAsync(imagePath);

        // Check memory cache first
        if (_memoryCache.TryGetValue(hash, out var cached))
        {
            await IncrementHitCountAsync(hash);
            return cached;
        }

        // Check database
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT predictions_json, device, cached_at
            FROM predictions
            WHERE image_hash = @hash
        ";
        command.Parameters.AddWithValue("@hash", hash);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var predictionsJson = reader.GetString(0);
            var device = reader.GetString(1);
            var cachedAt = DateTime.Parse(reader.GetString(2));

            var predictions = JsonSerializer.Deserialize<List<EnhancedLocationPrediction>>(predictionsJson);

            var result = new CachedPrediction
            {
                ImagePath = imagePath,
                Predictions = predictions ?? new(),
                Device = device,
                CachedAt = cachedAt
            };

            // Add to memory cache
            _memoryCache[hash] = result;

            await IncrementHitCountAsync(hash);
            return result;
        }

        return null;
    }

    public async Task SavePredictionAsync(
        string imagePath,
        List<EnhancedLocationPrediction> predictions,
        string device)
    {
        var hash = await ComputeHashAsync(imagePath);
        var predictionsJson = JsonSerializer.Serialize(predictions);

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO predictions
            (image_hash, image_path, predictions_json, device, cached_at, hit_count)
            VALUES (@hash, @path, @json, @device, @cached_at, 0)
        ";
        command.Parameters.AddWithValue("@hash", hash);
        command.Parameters.AddWithValue("@path", imagePath);
        command.Parameters.AddWithValue("@json", predictionsJson);
        command.Parameters.AddWithValue("@device", device);
        command.Parameters.AddWithValue("@cached_at", DateTime.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync();

        // Add to memory cache
        _memoryCache[hash] = new CachedPrediction
        {
            ImagePath = imagePath,
            Predictions = predictions,
            Device = device,
            CachedAt = DateTime.UtcNow
        };
    }

    private async Task IncrementHitCountAsync(string hash)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            UPDATE predictions
            SET hit_count = hit_count + 1
            WHERE image_hash = @hash
        ";
        command.Parameters.AddWithValue("@hash", hash);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT
                COUNT(*) as total_entries,
                SUM(hit_count) as total_hits,
                AVG(hit_count) as avg_hits
            FROM predictions
        ";

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new CacheStatistics
            {
                TotalEntries = reader.GetInt32(0),
                TotalHits = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                AverageHits = reader.IsDBNull(2) ? 0 : reader.GetDouble(2)
            };
        }

        return new CacheStatistics();
    }

    public async Task CleanupOldEntriesAsync(int daysOld = 90)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM predictions
            WHERE datetime(cached_at) < datetime('now', '-' || @days || ' days')
        ";
        command.Parameters.AddWithValue("@days", daysOld);

        var deleted = await command.ExecuteNonQueryAsync();
        Debug.WriteLine($"Deleted {deleted} old cache entries");

        // Clear memory cache
        _memoryCache.Clear();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

public record CachedPrediction
{
    public string ImagePath { get; init; } = "";
    public List<EnhancedLocationPrediction> Predictions { get; init; } = new();
    public string Device { get; init; } = "";
    public DateTime CachedAt { get; init; }
}

public record CacheStatistics
{
    public int TotalEntries { get; init; }
    public long TotalHits { get; init; }
    public double AverageHits { get; init; }
    public double HitRate => TotalEntries > 0 ? (double)TotalHits / TotalEntries : 0;
}
```

**File**: `Services/PredictionCacheService.cs`

---

## 5. EXIF GPS Extractor

### Purpose
Extract GPS coordinates from image EXIF metadata.

### Implementation

```csharp
// Services/ExifGpsExtractor.cs
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Graphics.Imaging;

public class ExifGpsExtractor
{
    public async Task<ExifGpsData?> ExtractGpsDataAsync(string imagePath)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            using var stream = await file.OpenAsync(FileAccessMode.Read);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            var properties = await decoder.BitmapProperties.GetPropertiesAsync(new[]
            {
                "/app1/ifd/gps/{ushort=1}",  // GPSLatitudeRef (N/S)
                "/app1/ifd/gps/{ushort=2}",  // GPSLatitude
                "/app1/ifd/gps/{ushort=3}",  // GPSLongitudeRef (E/W)
                "/app1/ifd/gps/{ushort=4}",  // GPSLongitude
                "/app1/ifd/gps/{ushort=5}",  // GPSAltitudeRef
                "/app1/ifd/gps/{ushort=6}",  // GPSAltitude
            });

            if (!properties.Any())
                return null;

            var latRef = GetPropertyValue<string>(properties, "/app1/ifd/gps/{ushort=1}");
            var latData = GetPropertyValue<object>(properties, "/app1/ifd/gps/{ushort=2}");
            var lonRef = GetPropertyValue<string>(properties, "/app1/ifd/gps/{ushort=3}");
            var lonData = GetPropertyValue<object>(properties, "/app1/ifd/gps/{ushort=4}");

            if (latData == null || lonData == null)
                return null;

            var latitude = ParseGpsCoordinate(latData, latRef);
            var longitude = ParseGpsCoordinate(lonData, lonRef);

            if (latitude == 0 && longitude == 0)
                return null; // Invalid GPS data

            return new ExifGpsData
            {
                Latitude = latitude,
                Longitude = longitude,
                HasGps = true
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to extract EXIF GPS: {ex.Message}");
            return null;
        }
    }

    private double ParseGpsCoordinate(object data, string? reference)
    {
        // GPS coordinates are stored as rational arrays: [degrees, minutes, seconds]
        if (data is not IList<object> parts || parts.Count < 3)
            return 0;

        var degrees = ParseRational(parts[0]);
        var minutes = ParseRational(parts[1]);
        var seconds = ParseRational(parts[2]);

        var coordinate = degrees + (minutes / 60.0) + (seconds / 3600.0);

        // Apply hemisphere (S and W are negative)
        if (reference == "S" || reference == "W")
            coordinate = -coordinate;

        return coordinate;
    }

    private double ParseRational(object rational)
    {
        if (rational is IDictionary<string, object> dict)
        {
            if (dict.TryGetValue("Numerator", out var num) &&
                dict.TryGetValue("Denominator", out var denom))
            {
                var numerator = Convert.ToDouble(num);
                var denominator = Convert.ToDouble(denom);
                return denominator != 0 ? numerator / denominator : 0;
            }
        }

        return 0;
    }

    private T? GetPropertyValue<T>(IDictionary<string, BitmapTypedValue> properties, string key)
    {
        if (properties.TryGetValue(key, out var value))
        {
            return (T?)value.Value;
        }
        return default;
    }
}

public record ExifGpsData
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public bool HasGps { get; init; }
    public string? LocationName { get; set; } // Populated via reverse geocoding
}
```

**File**: `Services/ExifGpsExtractor.cs`

---

## 6. Geographic Cluster Analyzer

### Purpose
Detect when predictions are geographically clustered to boost confidence.

### Implementation

```csharp
// Services/GeographicClusterAnalyzer.cs
public class GeographicClusterAnalyzer
{
    private const double ClusterRadiusKm = 100.0;
    private const double EarthRadiusKm = 6371.0;

    public ClusterAnalysisResult AnalyzePredictions(List<LocationPrediction> predictions)
    {
        if (predictions.Count < 2)
        {
            return new ClusterAnalysisResult
            {
                IsClustered = false,
                ClusterRadius = 0,
                ConfidenceBoost = 0
            };
        }

        // Take top 3 predictions for clustering analysis
        var topPredictions = predictions.Take(3).ToList();

        // Calculate pairwise distances
        var distances = new List<double>();
        for (int i = 0; i < topPredictions.Count; i++)
        {
            for (int j = i + 1; j < topPredictions.Count; j++)
            {
                var distance = CalculateHaversineDistance(
                    topPredictions[i].Latitude,
                    topPredictions[i].Longitude,
                    topPredictions[j].Latitude,
                    topPredictions[j].Longitude
                );
                distances.Add(distance);
            }
        }

        var maxDistance = distances.Max();
        var avgDistance = distances.Average();

        // Clustering criteria: all top 3 predictions within 100km
        var isClustered = maxDistance <= ClusterRadiusKm;

        if (!isClustered)
        {
            return new ClusterAnalysisResult
            {
                IsClustered = false,
                ClusterRadius = maxDistance,
                ConfidenceBoost = 0
            };
        }

        // Calculate confidence boost (inverse relationship with distance)
        // Closer clusters get higher boost (max +0.15)
        var confidenceBoost = Math.Max(0, 0.15 * (1 - (avgDistance / ClusterRadiusKm)));

        // Calculate cluster center (centroid)
        var centerLat = topPredictions.Average(p => p.Latitude);
        var centerLon = topPredictions.Average(p => p.Longitude);

        return new ClusterAnalysisResult
        {
            IsClustered = true,
            ClusterRadius = maxDistance,
            AverageDistance = avgDistance,
            ConfidenceBoost = confidenceBoost,
            ClusterCenterLat = centerLat,
            ClusterCenterLon = centerLon
        };
    }

    public double CalculateHaversineDistance(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusKm * c;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}

public record ClusterAnalysisResult
{
    public bool IsClustered { get; init; }
    public double ClusterRadius { get; init; }
    public double AverageDistance { get; init; }
    public double ConfidenceBoost { get; init; }
    public double ClusterCenterLat { get; init; }
    public double ClusterCenterLon { get; init; }
}
```

**File**: `Services/GeographicClusterAnalyzer.cs`

---

## 7. Prediction Processor

### Purpose
Orchestrate the full prediction pipeline (EXIF → API → Clustering → Enhancement).

### Implementation

```csharp
// Services/PredictionProcessor.cs
public class PredictionProcessor
{
    private readonly GeoCLIPApiClient _apiClient;
    private readonly ExifGpsExtractor _exifExtractor;
    private readonly GeographicClusterAnalyzer _clusterAnalyzer;
    private readonly PredictionCacheService _cacheService;

    public PredictionProcessor(
        GeoCLIPApiClient apiClient,
        PredictionCacheService cacheService)
    {
        _apiClient = apiClient;
        _cacheService = cacheService;
        _exifExtractor = new ExifGpsExtractor();
        _clusterAnalyzer = new GeographicClusterAnalyzer();
    }

    public async Task<EnhancedPredictionResult> ProcessImageAsync(string imagePath)
    {
        // Check cache first
        var cached = await _cacheService.GetCachedPredictionAsync(imagePath);
        if (cached != null)
        {
            return ConvertFromCache(cached);
        }

        var result = new EnhancedPredictionResult { ImagePath = imagePath };

        // Extract EXIF GPS
        var exifGps = _exifExtractor.ExtractGpsData(imagePath);
        if (exifGps != null)
        {
            result.ExifGps = exifGps;
        }

        // Get AI predictions
        var apiResponse = await _apiClient.PredictAsync(new[] { imagePath });
        var predictions = apiResponse.Results[0].Predictions;

        // Analyze clustering
        var clusterInfo = _clusterAnalyzer.AnalyzePredictions(predictions);
        result.ClusterInfo = clusterInfo;

        // Enhance predictions
        var enhanced = predictions.Select(p => new EnhancedLocationPrediction
        {
            Rank = p.Rank,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            Probability = p.Probability,
            City = p.City,
            State = p.State,
            County = p.County,
            Country = p.Country,
            LocationSummary = p.LocationSummary,
            AdjustedProbability = p.Probability + (clusterInfo.IsClustered ? clusterInfo.ConfidenceBoost : 0),
            IsPartOfCluster = clusterInfo.IsClustered && p.Rank <= 3,
            ConfidenceLevel = ConfidenceHelper.ClassifyConfidence(
                p.Probability,
                clusterInfo.IsClustered && p.Rank <= 3)
        }).ToList();

        result.AiPredictions = enhanced;

        // Save to cache
        await _cacheService.SavePredictionAsync(imagePath, enhanced, apiResponse.Device);

        return result;
    }

    private EnhancedPredictionResult ConvertFromCache(CachedPrediction cached)
    {
        // Convert cached data to EnhancedPredictionResult
        // (Implementation omitted for brevity)
        return new EnhancedPredictionResult
        {
            ImagePath = cached.ImagePath,
            AiPredictions = cached.Predictions.Cast<EnhancedLocationPrediction>().ToList()
        };
    }
}
```

---

## 8. Map Provider Factory

### Purpose
Abstract map rendering to support online/offline and different visualization modes.

### Implementation

```csharp
// Services/IMapProvider.cs
public interface IMapProvider
{
    Task InitializeAsync();
    Task AddPinAsync(double lat, double lon, string label, double confidence, int rank, bool isExif = false);
    Task RotateToPinAsync(double lat, double lon, TimeSpan duration);
    Task ClearPinsAsync();
    Task ShowHeatmapAsync(HeatmapData heatmapData);
    void SetDarkMode(bool enabled);
}

// Services/MapProviderFactory.cs
public class MapProviderFactory
{
    public static IMapProvider Create(
        MapRenderMode mode,
        bool offlineMode,
        UIElement container)
    {
        return (mode, offlineMode) switch
        {
            (MapRenderMode.Globe3D, false) =>
                new WebGlobe3DProvider(container as WebView2, darkMode: true),

            (MapRenderMode.Globe3D, true) =>
                new Win2DGlobe3DProvider(container as CanvasControl, darkMode: true),

            (MapRenderMode.FlatMap, false) =>
                new MapBoxDarkProvider(container as WebView2),

            (MapRenderMode.FlatMap, true) =>
                new OfflineDarkMapProvider(container as CanvasControl),

            _ => throw new NotSupportedException(
                $"Mode {mode} with offline={offlineMode} not supported")
        };
    }
}
```

---

## 9. Export Service

### Purpose
Export predictions to CSV, PDF, and KML formats.

### Implementation

```csharp
// Services/ExportService.cs
using CsvHelper;
using CsvHelper.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Xml.Linq;

public class ExportService
{
    public async Task ExportToCsvAsync(
        List<EnhancedPredictionResult> results,
        string outputPath)
    {
        var records = new List<CsvExportRecord>();

        foreach (var result in results)
        {
            // Add EXIF GPS if present
            if (result.ExifGps != null)
            {
                records.Add(new CsvExportRecord
                {
                    ImagePath = result.ImagePath,
                    Rank = 0,
                    Source = "EXIF GPS",
                    Latitude = result.ExifGps.Latitude,
                    Longitude = result.ExifGps.Longitude,
                    Confidence = 1.0,
                    LocationName = result.ExifGps.LocationName ?? "",
                    City = "",
                    State = "",
                    Country = ""
                });
            }

            // Add AI predictions
            foreach (var pred in result.AiPredictions)
            {
                records.Add(new CsvExportRecord
                {
                    ImagePath = result.ImagePath,
                    Rank = pred.Rank,
                    Source = "AI Prediction",
                    Latitude = pred.Latitude,
                    Longitude = pred.Longitude,
                    Confidence = pred.AdjustedProbability,
                    LocationName = pred.LocationSummary,
                    City = pred.City,
                    State = pred.State,
                    Country = pred.Country
                });
            }
        }

        using var writer = new StreamWriter(outputPath);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        await csv.WriteRecordsAsync(records);
    }

    public async Task ExportToPdfAsync(
        List<EnhancedPredictionResult> results,
        string outputPath)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Element(ComposeHeader);
                page.Content().Element(content => ComposeContent(content, results));
                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        await Task.Run(() => document.GeneratePdf(outputPath));
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("GeoLens Prediction Report")
                    .FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);

                column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                    .FontSize(10).FontColor(Colors.Grey.Darken2);
            });
        });
    }

    private void ComposeContent(IContainer container, List<EnhancedPredictionResult> results)
    {
        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(15);

            foreach (var result in results)
            {
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Column(item =>
                {
                    item.Item().Text(Path.GetFileName(result.ImagePath))
                        .FontSize(14).SemiBold();

                    if (result.ExifGps != null)
                    {
                        item.Item().PaddingLeft(10).Text(text =>
                        {
                            text.Span("EXIF GPS: ").SemiBold().FontColor(Colors.Green.Darken1);
                            text.Span($"{result.ExifGps.Latitude:F6}, {result.ExifGps.Longitude:F6}");
                        });
                    }

                    item.Item().PaddingLeft(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn();
                            columns.ConstantColumn(100);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Rank").SemiBold();
                            header.Cell().Text("Location").SemiBold();
                            header.Cell().Text("Coordinates").SemiBold();
                            header.Cell().Text("Confidence").SemiBold();
                        });

                        foreach (var pred in result.AiPredictions)
                        {
                            table.Cell().Text(pred.Rank.ToString());
                            table.Cell().Text(pred.LocationSummary);
                            table.Cell().Text($"{pred.Latitude:F4}, {pred.Longitude:F4}");
                            table.Cell().Text($"{pred.AdjustedProbability:P1}");
                        }
                    });
                });
            }
        });
    }

    public async Task ExportToKmlAsync(
        List<EnhancedPredictionResult> results,
        string outputPath)
    {
        var kml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("kml",
                new XAttribute("xmlns", "http://www.opengis.net/kml/2.2"),
                new XElement("Document",
                    new XElement("name", "GeoLens Predictions"),
                    new XElement("description", $"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}"),

                    // Styles
                    CreateKmlStyle("exifStyle", "ff00ffff", 1.2), // Cyan for EXIF
                    CreateKmlStyle("highStyle", "ff00ff00", 1.0), // Green for high confidence
                    CreateKmlStyle("mediumStyle", "ff00ffff", 0.8), // Yellow for medium
                    CreateKmlStyle("lowStyle", "ff0000ff", 0.6), // Red for low

                    // Placemarks
                    from result in results
                    from placemark in CreatePlacemarks(result)
                    select placemark
                )
            )
        );

        using var writer = new StreamWriter(outputPath);
        await writer.WriteAsync(kml.ToString());
    }

    private XElement CreateKmlStyle(string id, string color, double scale)
    {
        return new XElement("Style",
            new XAttribute("id", id),
            new XElement("IconStyle",
                new XElement("color", color),
                new XElement("scale", scale),
                new XElement("Icon",
                    new XElement("href", "http://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png")
                )
            )
        );
    }

    private IEnumerable<XElement> CreatePlacemarks(EnhancedPredictionResult result)
    {
        var placemarks = new List<XElement>();

        // EXIF GPS placemark
        if (result.ExifGps != null)
        {
            placemarks.Add(new XElement("Placemark",
                new XElement("name", $"EXIF GPS - {Path.GetFileName(result.ImagePath)}"),
                new XElement("description", result.ExifGps.LocationName ?? "GPS from image metadata"),
                new XElement("styleUrl", "#exifStyle"),
                new XElement("Point",
                    new XElement("coordinates", $"{result.ExifGps.Longitude},{result.ExifGps.Latitude},0")
                )
            ));
        }

        // AI prediction placemarks
        foreach (var pred in result.AiPredictions)
        {
            var style = pred.ConfidenceLevel switch
            {
                ConfidenceLevel.High => "#highStyle",
                ConfidenceLevel.Medium => "#mediumStyle",
                _ => "#lowStyle"
            };

            placemarks.Add(new XElement("Placemark",
                new XElement("name", $"Rank {pred.Rank}: {pred.LocationSummary}"),
                new XElement("description",
                    $"Confidence: {pred.AdjustedProbability:P1}\n" +
                    $"Image: {Path.GetFileName(result.ImagePath)}"),
                new XElement("styleUrl", style),
                new XElement("Point",
                    new XElement("coordinates", $"{pred.Longitude},{pred.Latitude},0")
                )
            ));
        }

        return placemarks;
    }
}

public record CsvExportRecord
{
    public string ImagePath { get; init; } = "";
    public int Rank { get; init; }
    public string Source { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Confidence { get; init; }
    public string LocationName { get; init; } = "";
    public string City { get; init; } = "";
    public string State { get; init; } = "";
    public string Country { get; init; } = "";
}
```

**File**: `Services/ExportService.cs`

---

## 10. Heatmap Generator

### Purpose
Aggregate predictions from multiple images into a heatmap visualization.

### Implementation

**See `/Docs/05_Heatmap_MultiImage.md` for complete implementation** (lines 159-412).

Key components:
- **Grid-based approach**: 360×180 grid (1° resolution)
- **Gaussian smoothing**: Apply kernel to each prediction with sigma=3.0
- **Normalization**: Scale all values to 0-1 range
- **Hotspot detection**: Find clusters above 0.7 threshold
- **Clustering algorithm**: Flood-fill to merge adjacent hotspots

**Quick reference**:

```csharp
// Services/PredictionHeatmapGenerator.cs
public class PredictionHeatmapGenerator
{
    private const int GridWidth = 360;
    private const int GridHeight = 180;
    private const double GaussianSigma = 3.0;

    public HeatmapData GenerateHeatmap(List<EnhancedPredictionResult> results)
    {
        var aggregator = new PredictionAggregator();
        var predictions = aggregator.AggregateFromResults(results);

        var grid = new double[GridWidth, GridHeight];

        // Apply Gaussian kernel for each prediction
        foreach (var pred in predictions)
        {
            ApplyGaussianKernel(grid, pred.Latitude, pred.Longitude, pred.Weight, GaussianSigma);
        }

        NormalizeGrid(grid);

        var hotspots = DetectHotspots(grid, threshold: 0.7);

        return new HeatmapData
        {
            Grid = grid,
            Resolution = 1.0,
            PredictionCount = predictions.Count,
            ImageCount = results.Count,
            HotspotRegions = hotspots,
            Statistics = aggregator.GetStatistics(predictions)
        };
    }

    private void ApplyGaussianKernel(double[,] grid, double lat, double lon, double weight, double sigma)
    {
        // Convert lat/lon to grid coordinates
        int centerX = (int)Math.Round((lon + 180) % 360);
        int centerY = (int)Math.Round(90 - lat);
        int radius = (int)(sigma * 3);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = (centerX + dx + GridWidth) % GridWidth;
                int y = centerY + dy;
                if (y < 0 || y >= GridHeight) continue;

                double distance = Math.Sqrt(dx * dx + dy * dy);
                double gaussianValue = Math.Exp(-(distance * distance) / (2 * sigma * sigma));
                grid[x, y] += weight * gaussianValue;
            }
        }
    }

    // See full implementation in /Docs/05_Heatmap_MultiImage.md
}
```

**File**: `Services/PredictionHeatmapGenerator.cs`

---

## Service Dependencies

```
PredictionProcessor
├── GeoCLIPApiClient (HTTP to Python)
├── PredictionCacheService (SQLite)
├── ExifGpsExtractor (System.Drawing)
└── GeographicClusterAnalyzer (Math)

MainPage
├── PredictionProcessor
├── MapProviderFactory → IMapProvider
├── ExportService
└── PredictionHeatmapGenerator

App
├── HardwareDetectionService
└── PythonRuntimeManager
```

---

## NuGet Package Requirements

```xml
<ItemGroup>
  <!-- Core -->
  <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.250916003" />
  <PackageReference Include="System.Management" Version="9.0.0" />

  <!-- Database -->
  <PackageReference Include="System.Data.SQLite.Core" Version="1.0.118" />

  <!-- Export -->
  <PackageReference Include="CsvHelper" Version="30.0.1" />
  <PackageReference Include="QuestPDF" Version="2024.3.0" />

  <!-- Hashing -->
  <PackageReference Include="System.IO.Hashing" Version="9.0.0" />

  <!-- 3D Rendering -->
  <PackageReference Include="Microsoft.Graphics.Win2D" Version="1.2.0" />

  <!-- JSON -->
  <PackageReference Include="System.Net.Http.Json" Version="9.0.0" />
</ItemGroup>
```

---

## 11. Error Recovery and Resilience

### Purpose
Implement robust error handling, automatic recovery, and graceful degradation.

### Implementation Patterns

```csharp
// Services/ResilientPythonRuntimeManager.cs
public class ResilientPythonRuntimeManager : PythonRuntimeManager
{
    private int _restartAttempts = 0;
    private const int MaxRestartAttempts = 3;
    private readonly TimeSpan _restartDelay = TimeSpan.FromSeconds(5);

    public async Task<bool> EnsureRunningAsync()
    {
        if (IsRunning && await CheckHealthAsync())
            return true;

        // Service crashed or unhealthy
        Debug.WriteLine("Python service is down, attempting restart...");

        while (_restartAttempts < MaxRestartAttempts)
        {
            await StopServiceAsync();
            await Task.Delay(_restartDelay);

            if (await StartServiceAsync())
            {
                _restartAttempts = 0;
                return true;
            }

            _restartAttempts++;
        }

        return false; // Failed to restart
    }

    private async Task<bool> CheckHealthAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await client.GetAsync($"{ApiBaseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
```

### Corrupted Model Handling

```csharp
// Services/ModelIntegrityChecker.cs
public class ModelIntegrityChecker
{
    private readonly string _modelsPath;
    private readonly Dictionary<string, string> _expectedHashes;

    public async Task<bool> VerifyModelsAsync()
    {
        foreach (var (file, expectedHash) in _expectedHashes)
        {
            var filePath = Path.Combine(_modelsPath, file);

            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"Missing model file: {file}");
                return false;
            }

            var actualHash = await ComputeSHA256Async(filePath);
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"Model file corrupted: {file}");
                return false;
            }
        }

        return true;
    }

    private async Task<string> ComputeSHA256Async(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes);
    }
}
```

### Network Interruption Recovery

```csharp
// Services/ResilientMapProvider.cs
public class ResilientMapProvider : IMapProvider
{
    private readonly IMapProvider _onlineProvider;
    private readonly IMapProvider _offlineProvider;
    private bool _useOfflineMode = false;

    public async Task InitializeAsync()
    {
        try
        {
            await _onlineProvider.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Online maps unavailable: {ex.Message}");
            Debug.WriteLine("Falling back to offline maps");
            _useOfflineMode = true;
            await _offlineProvider.InitializeAsync();
        }
    }

    public async Task AddPinAsync(double lat, double lon, string label, double confidence, int rank, bool isExif = false)
    {
        var provider = _useOfflineMode ? _offlineProvider : _onlineProvider;
        await provider.AddPinAsync(lat, lon, label, confidence, rank, isExif);
    }

    // Implement other IMapProvider methods similarly
}
```

---

## 12. Logging Strategy

### Purpose
Comprehensive logging for debugging and diagnostics.

### Implementation

```csharp
// Services/FileLogger.cs
public class FileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _semaphore = new(1);

    public FileLogger(string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        _writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
    }

    public async Task LogAsync(LogLevel level, string category, string message, Exception? exception = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLine = $"[{timestamp}] [{level}] [{category}] {message}";

            await _writer.WriteLineAsync(logLine);

            if (exception != null)
            {
                await _writer.WriteLineAsync($"Exception: {exception}");
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _semaphore?.Dispose();
    }
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Critical
}
```

---

## Testing Strategy

### Unit Tests
- Mock API client for offline testing
- Test confidence calculation logic
- Test geographic distance calculations
- Test cache hit/miss scenarios

### Integration Tests
- Test full prediction pipeline
- Test Python service startup/shutdown
- Test map provider switching
- Test export formats

### Performance Tests
- Benchmark cache lookup speed
- Measure API response times
- Test heatmap generation with 100+ images
- Memory profiling for large batches
