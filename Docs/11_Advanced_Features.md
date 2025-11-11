# Advanced Features

## Overview

This document covers advanced features planned for GeoLens beyond the MVP, including batch processing, accuracy validation, video support, and advanced visualizations.

---

## 1. Batch Processing Mode

### 1.1 Watch Folder Functionality

Automatically process new images dropped into a monitored folder.

```csharp
// Services/FolderWatcherService.cs
using System.IO;

public class FolderWatcherService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly PredictionProcessor _processor;
    private readonly string _watchPath;
    private readonly bool _includeSubdirectories;

    public event EventHandler<ImageProcessedEventArgs>? ImageProcessed;
    public event EventHandler<ErrorEventArgs>? ProcessingError;

    public FolderWatcherService(
        string watchPath,
        PredictionProcessor processor,
        bool includeSubdirectories = false)
    {
        _watchPath = watchPath;
        _processor = processor;
        _includeSubdirectories = includeSubdirectories;

        _watcher = new FileSystemWatcher(watchPath)
        {
            Filter = "*.*",
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        _watcher.Created += OnImageAdded;
        _watcher.Changed += OnImageChanged;
        _watcher.Error += OnWatcherError;
    }

    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
    }

    private async void OnImageAdded(object sender, FileSystemEventArgs e)
    {
        if (!IsImageFile(e.FullPath))
            return;

        // Wait for file to be fully written
        await WaitForFileAccessAsync(e.FullPath);

        try
        {
            var result = await _processor.ProcessImageAsync(e.FullPath);
            ImageProcessed?.Invoke(this, new ImageProcessedEventArgs
            {
                ImagePath = e.FullPath,
                Result = result,
                ProcessedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            ProcessingError?.Invoke(this, new ErrorEventArgs(ex));
        }
    }

    private async Task WaitForFileAccessAsync(string path, int maxWaitMs = 5000)
    {
        var deadline = DateTime.Now.AddMilliseconds(maxWaitMs);

        while (DateTime.Now < deadline)
        {
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return; // File is accessible
            }
            catch (IOException)
            {
                await Task.Delay(100);
            }
        }
    }

    private bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".heic" or ".bmp";
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}

public class ImageProcessedEventArgs : EventArgs
{
    public string ImagePath { get; init; } = "";
    public EnhancedPredictionResult Result { get; init; } = new();
    public DateTime ProcessedAt { get; init; }
}
```

### 1.2 Command-Line Interface

```csharp
// CLI/Program.cs
using System.CommandLine;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("GeoLens CLI - AI-powered image geolocation");

        // Predict command
        var predictCommand = new Command("predict", "Predict location for images");
        var inputOption = new Option<string[]>(
            aliases: new[] { "--input", "-i" },
            description: "Input image paths or directory"
        ) { IsRequired = true, AllowMultipleArgumentsPerToken = true };
        var outputOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Output file (CSV/JSON/PDF)"
        );
        var formatOption = new Option<ExportFormat>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => ExportFormat.CSV,
            description: "Export format"
        );
        var topKOption = new Option<int>(
            aliases: new[] { "--top-k", "-k" },
            getDefaultValue: () => 5,
            description: "Number of predictions per image"
        );
        var deviceOption = new Option<string>(
            aliases: new[] { "--device", "-d" },
            getDefaultValue: () => "auto",
            description: "Device: auto, cpu, cuda, rocm"
        );

        predictCommand.AddOption(inputOption);
        predictCommand.AddOption(outputOption);
        predictCommand.AddOption(formatOption);
        predictCommand.AddOption(topKOption);
        predictCommand.AddOption(deviceOption);

        predictCommand.SetHandler(async (string[] inputs, string? output, ExportFormat format, int topK, string device) =>
        {
            await PredictAsync(inputs, output, format, topK, device);
        }, inputOption, outputOption, formatOption, topKOption, deviceOption);

        rootCommand.AddCommand(predictCommand);

        // Watch command
        var watchCommand = new Command("watch", "Watch folder for new images");
        var folderOption = new Option<string>(
            aliases: new[] { "--folder", "-f" },
            description: "Folder to watch"
        ) { IsRequired = true };
        var recursiveOption = new Option<bool>(
            aliases: new[] { "--recursive", "-r" },
            getDefaultValue: () => false,
            description: "Include subdirectories"
        );

        watchCommand.AddOption(folderOption);
        watchCommand.AddOption(recursiveOption);

        watchCommand.SetHandler(async (string folder, bool recursive) =>
        {
            await WatchFolderAsync(folder, recursive);
        }, folderOption, recursiveOption);

        rootCommand.AddCommand(watchCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task PredictAsync(
        string[] inputs,
        string? output,
        ExportFormat format,
        int topK,
        string device)
    {
        Console.WriteLine($"GeoLens CLI - Predicting locations...");

        // Collect all image files
        var imageFiles = new List<string>();
        foreach (var input in inputs)
        {
            if (File.Exists(input))
            {
                imageFiles.Add(input);
            }
            else if (Directory.Exists(input))
            {
                imageFiles.AddRange(Directory.GetFiles(input, "*.*", SearchOption.AllDirectories)
                    .Where(f => IsImageFile(f)));
            }
        }

        Console.WriteLine($"Found {imageFiles.Count} images");

        // Start Python service
        var hardwareType = device.ToLower() switch
        {
            "cuda" => HardwareType.CUDA,
            "rocm" => HardwareType.ROCM,
            "cpu" => HardwareType.CPU,
            _ => new HardwareDetectionService().DetectHardware()
        };

        var runtimeManager = new PythonRuntimeManager(hardwareType);
        Console.WriteLine("Starting Python service...");
        var started = await runtimeManager.StartServiceAsync();
        if (!started)
        {
            Console.Error.WriteLine("Failed to start Python service");
            return;
        }

        Console.WriteLine($"Using device: {hardwareType}");

        // Process images
        var apiClient = new GeoCLIPApiClient(runtimeManager.ApiBaseUrl);
        var cacheService = new PredictionCacheService();
        var processor = new PredictionProcessor(apiClient, cacheService);

        var results = new List<EnhancedPredictionResult>();

        for (int i = 0; i < imageFiles.Count; i++)
        {
            Console.Write($"\rProcessing {i + 1}/{imageFiles.Count}: {Path.GetFileName(imageFiles[i])}");

            try
            {
                var result = await processor.ProcessImageAsync(imageFiles[i]);
                results.Add(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nError processing {imageFiles[i]}: {ex.Message}");
            }
        }

        Console.WriteLine($"\n\nProcessed {results.Count} images successfully");

        // Export results
        if (!string.IsNullOrEmpty(output))
        {
            var exporter = new ExportService();
            await ExportResultsAsync(exporter, results, output, format);
            Console.WriteLine($"Results exported to: {output}");
        }
        else
        {
            // Print to console
            PrintResults(results);
        }

        await runtimeManager.StopServiceAsync();
    }

    private static async Task WatchFolderAsync(string folder, bool recursive)
    {
        Console.WriteLine($"Watching folder: {folder}");
        Console.WriteLine($"Recursive: {recursive}");
        Console.WriteLine("Press Ctrl+C to stop\n");

        // Setup services
        var hardware = new HardwareDetectionService().DetectHardware();
        var runtimeManager = new PythonRuntimeManager(hardware);
        await runtimeManager.StartServiceAsync();

        var apiClient = new GeoCLIPApiClient(runtimeManager.ApiBaseUrl);
        var cacheService = new PredictionCacheService();
        var processor = new PredictionProcessor(apiClient, cacheService);

        var watcher = new FolderWatcherService(folder, processor, recursive);

        watcher.ImageProcessed += (s, e) =>
        {
            Console.WriteLine($"[{e.ProcessedAt:HH:mm:ss}] Processed: {Path.GetFileName(e.ImagePath)}");
            if (e.Result.AiPredictions.Any())
            {
                var top = e.Result.AiPredictions[0];
                Console.WriteLine($"  Top prediction: {top.LocationSummary} ({top.Probability:P1})");
            }
        };

        watcher.ProcessingError += (s, e) =>
        {
            Console.Error.WriteLine($"Error: {e.GetException().Message}");
        };

        watcher.Start();

        // Wait for Ctrl+C
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await Task.Delay(Timeout.Infinite, cts.Token);

        watcher.Stop();
        await runtimeManager.StopServiceAsync();
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".heic" or ".bmp";
    }
}
```

### 1.3 Windows Explorer Context Menu

```csharp
// Installer addition to setup.iss
[Registry]
Root: HKCR; Subkey: "*\shell\GeoLens"; ValueType: string; ValueName: ""; ValueData: "Predict Location with GeoLens"; Flags: uninsdeletekey
Root: HKCR; Subkey: "*\shell\GeoLens"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\GeoLens.exe,0"
Root: HKCR; Subkey: "*\shell\GeoLens\command"; ValueType: string; ValueName: ""; ValueData: """{app}\GeoLens.exe"" ""%1"""
```

---

## 2. Accuracy Validation System

### 2.1 Ground Truth Comparison

When EXIF GPS data exists, compare AI predictions against ground truth.

```csharp
// Services/AccuracyValidator.cs
public class AccuracyValidator
{
    private readonly GeographicClusterAnalyzer _clusterAnalyzer;

    public AccuracyValidator()
    {
        _clusterAnalyzer = new GeographicClusterAnalyzer();
    }

    public AccuracyReport ValidatePredictions(
        EnhancedPredictionResult result)
    {
        if (result.ExifGps == null)
        {
            return new AccuracyReport
            {
                HasGroundTruth = false,
                Message = "No EXIF GPS data available for validation"
            };
        }

        var groundTruthLat = result.ExifGps.Latitude;
        var groundTruthLon = result.ExifGps.Longitude;

        var report = new AccuracyReport
        {
            HasGroundTruth = true,
            GroundTruthLocation = new Location
            {
                Latitude = groundTruthLat,
                Longitude = groundTruthLon,
                LocationName = result.ExifGps.LocationName ?? "Unknown"
            },
            PredictionAccuracies = new List<PredictionAccuracy>()
        };

        foreach (var prediction in result.AiPredictions)
        {
            var distance = _clusterAnalyzer.CalculateHaversineDistance(
                groundTruthLat,
                groundTruthLon,
                prediction.Latitude,
                prediction.Longitude
            );

            var accuracy = new PredictionAccuracy
            {
                Rank = prediction.Rank,
                LocationName = prediction.LocationSummary,
                Confidence = prediction.Probability,
                ErrorDistanceKm = distance,
                ErrorCategory = ClassifyError(distance)
            };

            report.PredictionAccuracies.Add(accuracy);
        }

        // Overall assessment
        var topPredictionError = report.PredictionAccuracies[0].ErrorDistanceKm;
        report.TopPredictionErrorKm = topPredictionError;
        report.AccuracyLevel = ClassifyAccuracyLevel(topPredictionError);
        report.IsTopPredictionCorrect = topPredictionError < 100; // Within 100km

        return report;
    }

    private ErrorCategory ClassifyError(double distanceKm)
    {
        return distanceKm switch
        {
            < 1 => ErrorCategory.Excellent,      // < 1km
            < 10 => ErrorCategory.VeryGood,      // 1-10km
            < 100 => ErrorCategory.Good,         // 10-100km
            < 500 => ErrorCategory.Fair,         // 100-500km
            < 1000 => ErrorCategory.Poor,        // 500-1000km
            _ => ErrorCategory.VeryPoor          // > 1000km
        };
    }

    private string ClassifyAccuracyLevel(double distanceKm)
    {
        return distanceKm switch
        {
            < 1 => "Pinpoint accuracy",
            < 10 => "City-level accuracy",
            < 100 => "Regional accuracy",
            < 500 => "Country-level accuracy",
            < 1000 => "Continental accuracy",
            _ => "Global-level guess"
        };
    }
}

public record AccuracyReport
{
    public bool HasGroundTruth { get; init; }
    public Location? GroundTruthLocation { get; init; }
    public List<PredictionAccuracy> PredictionAccuracies { get; init; } = new();
    public double TopPredictionErrorKm { get; init; }
    public string AccuracyLevel { get; init; } = "";
    public bool IsTopPredictionCorrect { get; init; }
    public string Message { get; init; } = "";
}

public record PredictionAccuracy
{
    public int Rank { get; init; }
    public string LocationName { get; init; } = "";
    public double Confidence { get; init; }
    public double ErrorDistanceKm { get; init; }
    public ErrorCategory ErrorCategory { get; init; }
}

public enum ErrorCategory
{
    Excellent,
    VeryGood,
    Good,
    Fair,
    Poor,
    VeryPoor
}
```

### 2.2 Accuracy UI Display

```xml
<!-- Views/AccuracyPanel.xaml -->
<Border Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
        CornerRadius="8"
        Padding="16"
        Visibility="{x:Bind ViewModel.AccuracyReport.HasGroundTruth, Mode=OneWay}">
    <StackPanel Spacing="12">
        <TextBlock Text="Accuracy Validation"
                   FontSize="16"
                   FontWeight="SemiBold"/>

        <StackPanel Orientation="Horizontal" Spacing="8">
            <FontIcon Glyph="&#xE930;"
                      FontSize="16"
                      Foreground="{ThemeResource SystemAccentColor}"/>
            <TextBlock>
                <Run Text="Ground Truth:"/>
                <Run Text="{x:Bind ViewModel.AccuracyReport.GroundTruthLocation.LocationName, Mode=OneWay}"
                     FontWeight="SemiBold"/>
            </TextBlock>
        </StackPanel>

        <Border Background="{ThemeResource LayerFillColorDefaultBrush}"
                CornerRadius="4"
                Padding="12">
            <StackPanel Spacing="8">
                <TextBlock Text="{x:Bind ViewModel.AccuracyReport.AccuracyLevel, Mode=OneWay}"
                           FontWeight="SemiBold"/>
                <TextBlock>
                    <Run Text="Top prediction error:"/>
                    <Run Text="{x:Bind ViewModel.AccuracyReport.TopPredictionErrorKm, Mode=OneWay, Converter={StaticResource DistanceFormatter}}"/>
                    <Run Text="km"/>
                </TextBlock>
            </StackPanel>
        </Border>

        <ItemsControl ItemsSource="{x:Bind ViewModel.AccuracyReport.PredictionAccuracies, Mode=OneWay}">
            <ItemsControl.ItemTemplate>
                <DataTemplate x:DataType="local:PredictionAccuracy">
                    <Grid ColumnDefinitions="Auto,*,Auto" Padding="0,4">
                        <TextBlock Grid.Column="0"
                                   Text="{x:Bind Rank}"
                                   Width="24"
                                   Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                        <TextBlock Grid.Column="1"
                                   Text="{x:Bind LocationName}"/>
                        <TextBlock Grid.Column="2"
                                   Foreground="{x:Bind ErrorCategory, Converter={StaticResource ErrorColorConverter}}">
                            <Run Text="{x:Bind ErrorDistanceKm, Converter={StaticResource DistanceFormatter}}"/>
                            <Run Text="km"/>
                        </TextBlock>
                    </Grid>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </StackPanel>
</Border>
```

---

## 3. Video Support

### 3.1 Frame Extraction

```csharp
// Services/VideoFrameExtractor.cs
using System.Diagnostics;

public class VideoFrameExtractor
{
    private readonly string _ffmpegPath;

    public VideoFrameExtractor()
    {
        // Bundled ffmpeg or system installation
        _ffmpegPath = FindFFmpegPath();
    }

    public async Task<List<string>> ExtractFramesAsync(
        string videoPath,
        int intervalSeconds = 5,
        string outputDirectory = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        outputDirectory ??= Path.Combine(Path.GetTempPath(), $"geolens_frames_{Guid.NewGuid()}");
        Directory.CreateDirectory(outputDirectory);

        // Get video duration
        var duration = await GetVideoDurationAsync(videoPath);
        var frameCount = (int)(duration / intervalSeconds);

        var outputPattern = Path.Combine(outputDirectory, "frame_%04d.jpg");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-i \"{videoPath}\" -vf \"fps=1/{intervalSeconds}\" \"{outputPattern}\" -hide_banner",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Monitor progress
        _ = Task.Run(async () =>
        {
            while (!process.HasExited)
            {
                await Task.Delay(500, cancellationToken);
                var extractedFrames = Directory.GetFiles(outputDirectory, "*.jpg").Length;
                progress?.Report((int)((double)extractedFrames / frameCount * 100));
            }
        }, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"FFmpeg failed: {error}");
        }

        return Directory.GetFiles(outputDirectory, "*.jpg")
            .OrderBy(f => f)
            .ToList();
    }

    private async Task<double> GetVideoDurationAsync(string videoPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-i \"{videoPath}\" -hide_banner",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Parse duration from output: "Duration: 00:01:30.00"
        var match = System.Text.RegularExpressions.Regex.Match(output, @"Duration: (\d{2}):(\d{2}):(\d{2})\.(\d{2})");
        if (match.Success)
        {
            var hours = int.Parse(match.Groups[1].Value);
            var minutes = int.Parse(match.Groups[2].Value);
            var seconds = int.Parse(match.Groups[3].Value);
            return hours * 3600 + minutes * 60 + seconds;
        }

        return 0;
    }

    private string FindFFmpegPath()
    {
        // Check bundled version
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(bundledPath))
            return bundledPath;

        // Check system PATH
        return "ffmpeg"; // Will fail if not in PATH
    }
}
```

### 3.2 Travel Route Generation

```csharp
// Services/TravelRouteGenerator.cs
public class TravelRouteGenerator
{
    public TravelRoute GenerateRoute(List<VideoFramePrediction> framePredictions)
    {
        // Sort by timestamp
        var sortedFrames = framePredictions
            .OrderBy(f => f.FrameNumber)
            .ToList();

        var route = new TravelRoute
        {
            TotalFrames = sortedFrames.Count,
            RoutePoints = new List<RoutePoint>()
        };

        RoutePoint? lastPoint = null;

        foreach (var frame in sortedFrames)
        {
            if (!frame.Predictions.Any())
                continue;

            var topPrediction = frame.Predictions[0];

            // Detect location change (> 10km movement)
            if (lastPoint != null)
            {
                var distance = CalculateDistance(
                    lastPoint.Latitude,
                    lastPoint.Longitude,
                    topPrediction.Latitude,
                    topPrediction.Longitude
                );

                if (distance < 10) // Same location, skip
                    continue;
            }

            var point = new RoutePoint
            {
                FrameNumber = frame.FrameNumber,
                Latitude = topPrediction.Latitude,
                Longitude = topPrediction.Longitude,
                LocationName = topPrediction.LocationSummary,
                Confidence = topPrediction.Probability
            };

            if (lastPoint != null)
            {
                point.DistanceFromPreviousKm = CalculateDistance(
                    lastPoint.Latitude,
                    lastPoint.Longitude,
                    point.Latitude,
                    point.Longitude
                );
            }

            route.RoutePoints.Add(point);
            lastPoint = point;
        }

        route.TotalDistanceKm = route.RoutePoints.Sum(p => p.DistanceFromPreviousKm);

        return route;
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Haversine formula (same as GeographicClusterAnalyzer)
        const double R = 6371; // Earth radius in km
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}

public record VideoFramePrediction
{
    public int FrameNumber { get; init; }
    public string FramePath { get; init; } = "";
    public List<LocationPrediction> Predictions { get; init; } = new();
}

public record TravelRoute
{
    public int TotalFrames { get; init; }
    public List<RoutePoint> RoutePoints { get; init; } = new();
    public double TotalDistanceKm { get; init; }
}

public record RoutePoint
{
    public int FrameNumber { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string LocationName { get; init; } = "";
    public double Confidence { get; init; }
    public double DistanceFromPreviousKm { get; init; }
}
```

---

## 4. Advanced Visualization

### 4.1 3D Terrain Overlay

```javascript
// Assets/globe_dark.html - Add terrain layer
function enableTerrainOverlay() {
    // Add 3D terrain using Mapbox Terrain-DEM v1
    world.globeMaterial(new THREE.MeshPhongMaterial({
        map: earthTexture,
        bumpMap: terrainBumpTexture, // Height map
        bumpScale: 0.05,
        specular: new THREE.Color('#0a0a0a'),
        shininess: 5
    }));
}

// Bundled terrain bump map
const terrainBumpTexture = new THREE.TextureLoader().load('earth_bump_8k.jpg');
```

### 4.2 Street-Level Preview Integration

#### Mapillary Integration (2D Street Images)

```csharp
// Services/StreetViewProvider.cs
public class StreetViewProvider
{
    private const string MapillaryApiUrl = "https://graph.mapillary.com/images";
    private readonly HttpClient _http;

    public async Task<StreetViewImage?> GetNearestStreetViewAsync(
        double lat,
        double lon,
        int radiusMeters = 100)
    {
        // Query Mapillary API for nearby images
        var bbox = CalculateBoundingBox(lat, lon, radiusMeters);

        var url = $"{MapillaryApiUrl}?bbox={bbox}&fields=id,thumb_256_url,geometry";

        try
        {
            var response = await _http.GetFromJsonAsync<MapillaryResponse>(url);

            if (response?.Data?.Any() == true)
            {
                var nearest = response.Data
                    .OrderBy(img => CalculateDistance(lat, lon, img.Geometry.Coordinates[1], img.Geometry.Coordinates[0]))
                    .First();

                return new StreetViewImage
                {
                    ThumbnailUrl = nearest.Thumb256Url,
                    Latitude = nearest.Geometry.Coordinates[1],
                    Longitude = nearest.Geometry.Coordinates[0],
                    ViewUrl = $"https://www.mapillary.com/app/?pKey={nearest.Id}"
                };
            }
        }
        catch { }

        return null;
    }

    private string CalculateBoundingBox(double lat, double lon, int radiusMeters)
    {
        // Simple approximation (not accurate at poles)
        double latOffset = radiusMeters / 111320.0;
        double lonOffset = radiusMeters / (111320.0 * Math.Cos(lat * Math.PI / 180));

        return $"{lon - lonOffset},{lat - latOffset},{lon + lonOffset},{lat + latOffset}";
    }
}

public record StreetViewImage
{
    public string ThumbnailUrl { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string ViewUrl { get; init; } = "";
}
```

#### 3D Street View Integration

Full 3D immersive street view using local data caches or online services.

```csharp
// Services/StreetView3DProvider.cs
public class StreetView3DProvider
{
    private readonly HttpClient _http;
    private readonly string _cacheDirectory;

    public StreetView3DProvider()
    {
        _http = new HttpClient();
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GeoLens",
            "StreetViewCache"
        );
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<StreetView3DData?> Get3DStreetViewAsync(
        double lat,
        double lon,
        int heading = 0,
        int pitch = 0,
        int fov = 90)
    {
        // Option 1: Google Street View Static API (requires API key)
        var googleApiKey = GetGoogleApiKey(); // From settings
        if (!string.IsNullOrEmpty(googleApiKey))
        {
            return await GetGoogleStreetView3DAsync(lat, lon, heading, pitch, fov, googleApiKey);
        }

        // Option 2: Mapillary 360° images
        return await GetMapillary360Async(lat, lon, heading);
    }

    private async Task<StreetView3DData?> GetGoogleStreetView3DAsync(
        double lat,
        double lon,
        int heading,
        int pitch,
        int fov,
        string apiKey)
    {
        // First, check if Street View is available at this location
        var metadataUrl = $"https://maps.googleapis.com/maps/api/streetview/metadata?" +
                         $"location={lat},{lon}&key={apiKey}";

        var metadataResponse = await _http.GetFromJsonAsync<GoogleStreetViewMetadata>(metadataUrl);

        if (metadataResponse?.Status != "OK")
        {
            return null; // No street view available
        }

        // Download panorama images (6 faces for cubemap or equirectangular)
        var panoramaUrl = $"https://maps.googleapis.com/maps/api/streetview?" +
                         $"size=640x640&location={lat},{lon}&heading={heading}&pitch={pitch}" +
                         $"&fov={fov}&key={apiKey}";

        var imageBytes = await _http.GetByteArrayAsync(panoramaUrl);

        // Cache the image
        var cacheKey = $"{lat:F6}_{lon:F6}_{heading}_{pitch}.jpg";
        var cachePath = Path.Combine(_cacheDirectory, cacheKey);
        await File.WriteAllBytesAsync(cachePath, imageBytes);

        return new StreetView3DData
        {
            Latitude = metadataResponse.Location.Lat,
            Longitude = metadataResponse.Location.Lng,
            Heading = heading,
            Pitch = pitch,
            ImagePath = cachePath,
            PanoId = metadataResponse.PanoId,
            CaptureDate = metadataResponse.Date,
            Provider = "Google Street View"
        };
    }

    private async Task<StreetView3DData?> GetMapillary360Async(
        double lat,
        double lon,
        int heading)
    {
        // Query Mapillary for 360° panoramic images
        var bbox = CalculateBoundingBox(lat, lon, 50);
        var url = $"https://graph.mapillary.com/images?bbox={bbox}" +
                 $"&fields=id,thumb_original_url,geometry,camera_type,captured_at" +
                 $"&is_pano=true"; // Only 360° images

        try
        {
            var response = await _http.GetFromJsonAsync<MapillaryResponse>(url);

            if (response?.Data?.Any() == true)
            {
                var nearest = response.Data.First();

                // Download the panoramic image
                var imageBytes = await _http.GetByteArrayAsync(nearest.ThumbOriginalUrl);

                var cacheKey = $"mapillary_{nearest.Id}.jpg";
                var cachePath = Path.Combine(_cacheDirectory, cacheKey);
                await File.WriteAllBytesAsync(cachePath, imageBytes);

                return new StreetView3DData
                {
                    Latitude = nearest.Geometry.Coordinates[1],
                    Longitude = nearest.Geometry.Coordinates[0],
                    Heading = heading,
                    ImagePath = cachePath,
                    PanoId = nearest.Id,
                    CaptureDate = nearest.CapturedAt,
                    Provider = "Mapillary",
                    Is360 = true
                };
            }
        }
        catch { }

        return null;
    }

    private string CalculateBoundingBox(double lat, double lon, int radiusMeters)
    {
        double latOffset = radiusMeters / 111320.0;
        double lonOffset = radiusMeters / (111320.0 * Math.Cos(lat * Math.PI / 180));
        return $"{lon - lonOffset},{lat - latOffset},{lon + lonOffset},{lat + latOffset}";
    }

    private string? GetGoogleApiKey()
    {
        // Read from user settings or environment variable
        return Environment.GetEnvironmentVariable("GOOGLE_MAPS_API_KEY");
    }
}

public record StreetView3DData
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int Heading { get; init; }
    public int Pitch { get; init; }
    public string ImagePath { get; init; } = "";
    public string PanoId { get; init; } = "";
    public string CaptureDate { get; init; } = "";
    public string Provider { get; init; } = "";
    public bool Is360 { get; init; }
}

public record GoogleStreetViewMetadata
{
    public string Status { get; init; } = "";
    public string PanoId { get; init; } = "";
    public Location Location { get; init; } = new();
    public string Date { get; init; } = "";
}

public record Location
{
    public double Lat { get; init; }
    public double Lng { get; init; }
}
```

#### 3D Street View Viewer (WebView2 or Win2D)

```html
<!-- Assets/StreetView/viewer_360.html -->
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>GeoLens 3D Street View</title>
    <script src="three.min.js"></script>
    <script src="pannellum.min.js"></script>
    <link rel="stylesheet" href="pannellum.css">
    <style>
        body {
            margin: 0;
            background: #0a0a0a;
            overflow: hidden;
        }
        #panorama {
            width: 100vw;
            height: 100vh;
        }
        .controls {
            position: absolute;
            bottom: 20px;
            left: 50%;
            transform: translateX(-50%);
            background: rgba(20, 20, 20, 0.9);
            padding: 12px 20px;
            border-radius: 8px;
            color: white;
            font-family: 'Segoe UI', sans-serif;
        }
    </style>
</head>
<body>
    <div id="panorama"></div>
    <div class="controls">
        <span id="location-info"></span>
    </div>

    <script>
        let viewer;

        function loadPanorama(imagePath, lat, lon, heading, provider, captureDate) {
            viewer = pannellum.viewer('panorama', {
                type: 'equirectangular',
                panorama: imagePath,
                autoLoad: true,
                showControls: true,
                compass: true,
                northOffset: heading,
                hfov: 100,
                pitch: 0,
                yaw: heading,
                minHfov: 50,
                maxHfov: 120,
                backgroundColor: [10, 10, 10]
            });

            // Update location info
            document.getElementById('location-info').textContent =
                `${lat.toFixed(6)}, ${lon.toFixed(6)} | ${provider} | ${captureDate}`;
        }

        function setHeading(heading) {
            if (viewer) {
                viewer.setYaw(heading);
            }
        }

        function setPitch(pitch) {
            if (viewer) {
                viewer.setPitch(pitch);
            }
        }

        function setFov(fov) {
            if (viewer) {
                viewer.setHfov(fov);
            }
        }
    </script>
</body>
</html>
```

#### C# Integration with WebView2

```csharp
// Views/StreetView3DWindow.xaml.cs
public sealed partial class StreetView3DWindow : Window
{
    private readonly StreetView3DProvider _provider;

    public StreetView3DWindow()
    {
        InitializeComponent();
        _provider = new StreetView3DProvider();
    }

    public async Task ShowLocationAsync(double lat, double lon, int heading = 0)
    {
        // Show loading indicator
        LoadingRing.IsActive = true;

        // Fetch 3D street view data
        var streetViewData = await _provider.Get3DStreetViewAsync(lat, lon, heading);

        if (streetViewData == null)
        {
            // No street view available
            ShowError("No street view available for this location");
            LoadingRing.IsActive = false;
            return;
        }

        // Initialize WebView2
        await StreetViewWebView.EnsureCoreWebView2Async();

        // Load the panorama viewer
        var viewerPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "StreetView",
            "viewer_360.html"
        );

        StreetViewWebView.Source = new Uri(viewerPath);

        // Wait for page to load
        await Task.Delay(1000);

        // Load the panorama image
        var script = $"loadPanorama(" +
                    $"'{streetViewData.ImagePath.Replace("\\", "\\\\")}', " +
                    $"{streetViewData.Latitude}, " +
                    $"{streetViewData.Longitude}, " +
                    $"{streetViewData.Heading}, " +
                    $"'{streetViewData.Provider}', " +
                    $"'{streetViewData.CaptureDate}')";

        await StreetViewWebView.ExecuteScriptAsync(script);

        LoadingRing.IsActive = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBar.IsOpen = true;
    }
}
```

#### UI Integration - Street View Button

```xml
<!-- Add to prediction result card -->
<Button Click="ViewIn3DStreetView_Click"
        ToolTipService.ToolTip="View in 3D Street View">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <FontIcon Glyph="&#xE81D;" FontSize="16"/>
        <TextBlock Text="Street View 3D"/>
    </StackPanel>
</Button>
```

```csharp
// MainPage.xaml.cs
private async void ViewIn3DStreetView_Click(object sender, RoutedEventArgs e)
{
    var button = sender as Button;
    var prediction = button?.DataContext as LocationPrediction;

    if (prediction == null) return;

    var streetViewWindow = new StreetView3DWindow();
    await streetViewWindow.ShowLocationAsync(
        prediction.Latitude,
        prediction.Longitude,
        heading: 0
    );

    streetViewWindow.Activate();
}
```

#### Offline 3D Street View Cache

For offline usage, pre-download street view tiles for specific regions:

```csharp
// Services/StreetViewCacheBuilder.cs
public class StreetViewCacheBuilder
{
    public async Task DownloadRegionAsync(
        double centerLat,
        double centerLon,
        double radiusKm,
        IProgress<int>? progress = null)
    {
        // Calculate grid of points to download
        var points = GenerateGridPoints(centerLat, centerLon, radiusKm, spacing: 0.05);

        var downloaded = 0;
        var provider = new StreetView3DProvider();

        foreach (var (lat, lon) in points)
        {
            // Download street view for each point (4 cardinal directions)
            for (int heading = 0; heading < 360; heading += 90)
            {
                var data = await provider.Get3DStreetViewAsync(lat, lon, heading);
                if (data != null)
                {
                    // Image is already cached by provider
                }
            }

            downloaded++;
            progress?.Report((int)((double)downloaded / points.Count * 100));
        }
    }

    private List<(double lat, double lon)> GenerateGridPoints(
        double centerLat,
        double centerLon,
        double radiusKm,
        double spacing)
    {
        var points = new List<(double, double)>();
        double degreeSpacing = spacing / 111.32; // Approximate km to degrees

        for (double lat = centerLat - radiusKm / 111.32;
             lat <= centerLat + radiusKm / 111.32;
             lat += degreeSpacing)
        {
            for (double lon = centerLon - radiusKm / 111.32;
                 lon <= centerLon + radiusKm / 111.32;
                 lon += degreeSpacing)
            {
                // Check if within radius
                var distance = CalculateDistance(centerLat, centerLon, lat, lon);
                if (distance <= radiusKm)
                {
                    points.Add((lat, lon));
                }
            }
        }

        return points;
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
```

### 4.3 Performance Profiling Dashboard

```csharp
// Services/PerformanceProfiler.cs
public class PerformanceProfiler
{
    private readonly Dictionary<string, List<double>> _metrics = new();
    private readonly Stopwatch _stopwatch = new();

    public IDisposable MeasureOperation(string operationName)
    {
        return new OperationMeasurement(this, operationName);
    }

    private void RecordMetric(string name, double milliseconds)
    {
        if (!_metrics.ContainsKey(name))
            _metrics[name] = new List<double>();

        _metrics[name].Add(milliseconds);
    }

    public PerformanceReport GetReport()
    {
        var operations = new List<OperationStats>();

        foreach (var (name, timings) in _metrics)
        {
            operations.Add(new OperationStats
            {
                Name = name,
                Count = timings.Count,
                AverageMs = timings.Average(),
                MinMs = timings.Min(),
                MaxMs = timings.Max(),
                P50Ms = Percentile(timings, 0.5),
                P95Ms = Percentile(timings, 0.95),
                P99Ms = Percentile(timings, 0.99)
            });
        }

        return new PerformanceReport
        {
            Operations = operations,
            GeneratedAt = DateTime.Now
        };
    }

    private double Percentile(List<double> values, double percentile)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int index = (int)Math.Ceiling(sorted.Count * percentile) - 1;
        return sorted[Math.Max(0, index)];
    }

    private class OperationMeasurement : IDisposable
    {
        private readonly PerformanceProfiler _profiler;
        private readonly string _operationName;
        private readonly Stopwatch _sw;

        public OperationMeasurement(PerformanceProfiler profiler, string operationName)
        {
            _profiler = profiler;
            _operationName = operationName;
            _sw = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _sw.Stop();
            _profiler.RecordMetric(_operationName, _sw.Elapsed.TotalMilliseconds);
        }
    }
}

public record PerformanceReport
{
    public List<OperationStats> Operations { get; init; } = new();
    public DateTime GeneratedAt { get; init; }
}

public record OperationStats
{
    public string Name { get; init; } = "";
    public int Count { get; init; }
    public double AverageMs { get; init; }
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double P50Ms { get; init; } // Median
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
}
```

---

## 5. Security and Privacy Features

### 5.1 Secure Cache Deletion

```csharp
// Services/SecureCacheCleaner.cs
public class SecureCacheCleaner
{
    public async Task SecureDeleteCacheAsync(PredictionCacheService cache)
    {
        // Get database path
        var dbPath = cache.GetDatabasePath();

        // Close database connection
        cache.Dispose();

        // Overwrite with random data before deletion
        if (File.Exists(dbPath))
        {
            var fileInfo = new FileInfo(dbPath);
            var fileSize = fileInfo.Length;

            using (var fs = File.OpenWrite(dbPath))
            {
                var buffer = new byte[4096];
                var random = new Random();

                // 3-pass overwrite
                for (int pass = 0; pass < 3; pass++)
                {
                    fs.Position = 0;
                    for (long written = 0; written < fileSize; written += buffer.Length)
                    {
                        random.NextBytes(buffer);
                        var toWrite = (int)Math.Min(buffer.Length, fileSize - written);
                        await fs.WriteAsync(buffer.AsMemory(0, toWrite));
                    }
                    await fs.FlushAsync();
                }
            }

            // Finally delete
            File.Delete(dbPath);
        }

        // Delete associated files
        var directory = Path.GetDirectoryName(dbPath);
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.GetFiles(directory, "*.db-*"))
            {
                File.Delete(file);
            }
        }
    }
}
```

### 5.2 Data Encryption at Rest

```csharp
// Services/EncryptedCacheService.cs
using Microsoft.Data.Sqlite;

public class EncryptedCacheService : PredictionCacheService
{
    public EncryptedCacheService(string password) : base(GetEncryptedConnectionString(password))
    {
    }

    private static string GetEncryptedConnectionString(string password)
    {
        // Use SQLCipher for encrypted SQLite
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GeoLens",
                "cache_encrypted.db"
            ),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = password // SQLCipher encryption
        };

        return builder.ToString();
    }
}
```

---

These advanced features extend GeoLens beyond the MVP while maintaining focus on core geolocation functionality.
