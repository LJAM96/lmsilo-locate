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

See previous response for full implementation with:
- SQLite database storage
- XXHash64 hashing
- In-memory cache layer
- Statistics and cleanup methods

**File**: `Services/PredictionCacheService.cs`

---

## 5. EXIF GPS Extractor

### Purpose
Extract GPS coordinates from image EXIF metadata.

### Implementation

See previous response for full implementation with:
- EXIF property reading
- GPS coordinate parsing
- Hemisphere handling (N/S/E/W)

**File**: `Services/ExifGpsExtractor.cs`

---

## 6. Geographic Cluster Analyzer

### Purpose
Detect when predictions are geographically clustered to boost confidence.

### Implementation

See previous response for full implementation with:
- Haversine distance calculation
- Clustering detection (100km radius)
- Confidence boost calculation
- Hotspot identification

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

See previous response for full implementation with:
- CSV export using CsvHelper
- PDF export using QuestPDF
- KML export for Google Earth

**File**: `Services/ExportService.cs`

---

## 10. Heatmap Generator

### Purpose
Aggregate predictions from multiple images into a heatmap visualization.

### Implementation

See previous response for full implementation with:
- 360×180 grid (1° resolution)
- Gaussian kernel smoothing
- Hotspot detection and clustering
- Normalization (0-1 range)

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
