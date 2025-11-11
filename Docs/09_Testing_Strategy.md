# Testing Strategy

## Overview

This document outlines the comprehensive testing approach for GeoLens, covering unit tests, integration tests, UI automation, and performance benchmarking.

---

## 1. Testing Framework Setup

### C# Testing Stack

```xml
<!-- Add to GeoLens.csproj for test project -->
<ItemGroup>
  <PackageReference Include="MSTest.TestAdapter" Version="3.1.1" />
  <PackageReference Include="MSTest.TestFramework" Version="3.1.1" />
  <PackageReference Include="Moq" Version="4.20.70" />
  <PackageReference Include="FluentAssertions" Version="6.12.0" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
  <PackageReference Include="WinAppDriver" Version="1.2.99" /> <!-- UI Testing -->
</ItemGroup>
```

### Project Structure

```
GeoLens.Tests/
├── Unit/
│   ├── Services/
│   │   ├── HardwareDetectionServiceTests.cs
│   │   ├── PredictionCacheServiceTests.cs
│   │   ├── GeographicClusterAnalyzerTests.cs
│   │   └── ExifMetadataExtractorTests.cs
│   ├── Models/
│   │   └── ConfidenceHelperTests.cs
│   └── Utilities/
│       └── HashingUtilsTests.cs
├── Integration/
│   ├── ApiIntegrationTests.cs
│   ├── PythonServiceTests.cs
│   └── EndToEndPipelineTests.cs
├── UI/
│   ├── MainPageTests.cs
│   └── SettingsPageTests.cs
├── Performance/
│   ├── CacheBenchmarks.cs
│   ├── HeatmapGenerationBenchmarks.cs
│   └── PredictionPipelineBenchmarks.cs
└── TestData/
    ├── Images/
    │   ├── with_exif_gps/
    │   ├── without_exif/
    │   └── corrupted/
    └── MockResponses/
        └── api_responses.json
```

---

## 2. Unit Testing

### Service Layer Tests

```csharp
// Unit/Services/HardwareDetectionServiceTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;

[TestClass]
public class HardwareDetectionServiceTests
{
    private HardwareDetectionService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = new HardwareDetectionService();
    }

    [TestMethod]
    public void DetectHardware_ShouldReturnConsistentResults()
    {
        // Act
        var result1 = _service.DetectHardware();
        var result2 = _service.DetectHardware();

        // Assert
        result1.Should().Be(result2);
    }

    [TestMethod]
    public void DetectHardware_ShouldReturnValidType()
    {
        // Act
        var result = _service.DetectHardware();

        // Assert
        result.Should().BeOneOf(HardwareType.CPU, HardwareType.CUDA, HardwareType.ROCM);
    }

    [TestMethod]
    public void GetHardwareDisplayName_ShouldReturnReadableString()
    {
        // Act
        var name = _service.GetHardwareDisplayName(HardwareType.CUDA);

        // Assert
        name.Should().Contain("NVIDIA");
    }
}
```

### Cache Service Tests

```csharp
// Unit/Services/PredictionCacheServiceTests.cs
[TestClass]
public class PredictionCacheServiceTests
{
    private PredictionCacheService _cacheService;
    private string _testDbPath;

    [TestInitialize]
    public void Setup()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_cache_{Guid.NewGuid()}.db");
        _cacheService = new PredictionCacheService(_testDbPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _cacheService?.Dispose();
        if (File.Exists(_testDbPath))
            File.Delete(_testDbPath);
    }

    [TestMethod]
    public async Task ComputeHash_SameImage_ShouldReturnSameHash()
    {
        // Arrange
        var imagePath = TestData.GetSampleImagePath();

        // Act
        var hash1 = await _cacheService.ComputeHashAsync(imagePath);
        var hash2 = await _cacheService.ComputeHashAsync(imagePath);

        // Assert
        hash1.Should().Be(hash2);
    }

    [TestMethod]
    public async Task GetCachedPrediction_NotCached_ShouldReturnNull()
    {
        // Arrange
        var imagePath = TestData.GetSampleImagePath();

        // Act
        var result = await _cacheService.GetCachedPredictionAsync(imagePath);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public async Task SaveAndRetrieve_ShouldPersistData()
    {
        // Arrange
        var imagePath = TestData.GetSampleImagePath();
        var predictions = TestData.GetMockPredictions();

        // Act
        await _cacheService.SavePredictionAsync(imagePath, predictions, "cuda");
        var cached = await _cacheService.GetCachedPredictionAsync(imagePath);

        // Assert
        cached.Should().NotBeNull();
        cached!.Predictions.Should().HaveCount(predictions.Count);
        cached.Device.Should().Be("cuda");
    }

    [TestMethod]
    public async Task GetStatistics_ShouldReturnAccurateCounts()
    {
        // Arrange
        await _cacheService.SavePredictionAsync(
            TestData.GetSampleImagePath(),
            TestData.GetMockPredictions(),
            "cpu"
        );

        // Act
        var stats = await _cacheService.GetStatisticsAsync();

        // Assert
        stats.TotalEntries.Should().Be(1);
        stats.HitRate.Should().Be(0); // No hits yet
    }
}
```

### Geographic Clustering Tests

```csharp
// Unit/Services/GeographicClusterAnalyzerTests.cs
[TestClass]
public class GeographicClusterAnalyzerTests
{
    private GeographicClusterAnalyzer _analyzer;

    [TestInitialize]
    public void Setup()
    {
        _analyzer = new GeographicClusterAnalyzer();
    }

    [TestMethod]
    public void HaversineDistance_KnownLocations_ShouldReturnCorrectDistance()
    {
        // Arrange: Tokyo to Osaka (approximately 400km)
        double lat1 = 35.6762, lon1 = 139.6503; // Tokyo
        double lat2 = 34.6937, lon2 = 135.5023; // Osaka

        // Act
        var distance = _analyzer.CalculateHaversineDistance(lat1, lon1, lat2, lon2);

        // Assert
        distance.Should().BeApproximately(400, 50); // Within 50km tolerance
    }

    [TestMethod]
    public void AnalyzePredictions_ClusteredLocations_ShouldDetectCluster()
    {
        // Arrange: 3 predictions within 50km of each other
        var predictions = new List<LocationPrediction>
        {
            new() { Rank = 1, Latitude = 35.6762, Longitude = 139.6503, Probability = 0.8 },
            new() { Rank = 2, Latitude = 35.6895, Longitude = 139.6917, Probability = 0.7 }, // ~5km away
            new() { Rank = 3, Latitude = 35.6590, Longitude = 139.7004, Probability = 0.6 }  // ~6km away
        };

        // Act
        var result = _analyzer.AnalyzePredictions(predictions);

        // Assert
        result.IsClustered.Should().BeTrue();
        result.ClusterRadius.Should().BeLessThan(100);
        result.ConfidenceBoost.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void AnalyzePredictions_DispersedLocations_ShouldNotDetectCluster()
    {
        // Arrange: Predictions on different continents
        var predictions = new List<LocationPrediction>
        {
            new() { Rank = 1, Latitude = 35.6762, Longitude = 139.6503, Probability = 0.8 }, // Tokyo
            new() { Rank = 2, Latitude = 40.7128, Longitude = -74.0060, Probability = 0.7 }, // New York
            new() { Rank = 3, Latitude = 51.5074, Longitude = -0.1278, Probability = 0.6 }   // London
        };

        // Act
        var result = _analyzer.AnalyzePredictions(predictions);

        // Assert
        result.IsClustered.Should().BeFalse();
        result.ConfidenceBoost.Should().Be(0);
    }
}
```

---

## 3. Integration Testing

### Python Service Integration

```csharp
// Integration/PythonServiceTests.cs
[TestClass]
public class PythonServiceIntegrationTests
{
    private PythonRuntimeManager? _runtimeManager;
    private GeoCLIPApiClient? _apiClient;

    [TestInitialize]
    public async Task Setup()
    {
        var hardwareService = new HardwareDetectionService();
        var hardware = hardwareService.DetectHardware();

        _runtimeManager = new PythonRuntimeManager(hardware);
        var started = await _runtimeManager.StartServiceAsync();

        if (!started)
            Assert.Inconclusive("Python service failed to start");

        _apiClient = new GeoCLIPApiClient(_runtimeManager.ApiBaseUrl);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_runtimeManager != null)
            await _runtimeManager.StopServiceAsync();
    }

    [TestMethod]
    public async Task HealthCheck_ServiceRunning_ShouldReturnTrue()
    {
        // Act
        var isHealthy = await _apiClient!.CheckHealthAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }

    [TestMethod]
    public async Task Predict_ValidImage_ShouldReturnPredictions()
    {
        // Arrange
        var imagePath = TestData.GetSampleImagePath();

        // Act
        var response = await _apiClient!.PredictAsync(new[] { imagePath }, topK: 5);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().HaveCount(1);
        response.Results[0].Predictions.Should().HaveCount(5);
        response.Results[0].Error.Should().BeNull();
    }

    [TestMethod]
    public async Task Predict_MissingImage_ShouldReturnError()
    {
        // Arrange
        var imagePath = "/nonexistent/image.jpg";

        // Act
        var response = await _apiClient!.PredictAsync(
            new[] { imagePath },
            skipMissing: false
        );

        // Assert
        response.Results[0].Error.Should().NotBeNull();
    }
}
```

### End-to-End Pipeline Tests

```csharp
// Integration/EndToEndPipelineTests.cs
[TestClass]
public class EndToEndPipelineTests
{
    private PredictionProcessor? _processor;
    private PythonRuntimeManager? _runtimeManager;

    [TestInitialize]
    public async Task Setup()
    {
        var hardware = new HardwareDetectionService().DetectHardware();
        _runtimeManager = new PythonRuntimeManager(hardware);
        await _runtimeManager.StartServiceAsync();

        var apiClient = new GeoCLIPApiClient(_runtimeManager.ApiBaseUrl);
        var cacheService = new PredictionCacheService(":memory:");

        _processor = new PredictionProcessor(apiClient, cacheService);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_runtimeManager != null)
            await _runtimeManager.StopServiceAsync();
    }

    [TestMethod]
    public async Task ProcessImage_WithExifGPS_ShouldPrioritizeExif()
    {
        // Arrange
        var imagePath = TestData.GetImageWithExifGPS();

        // Act
        var result = await _processor!.ProcessImageAsync(imagePath);

        // Assert
        result.ExifGps.Should().NotBeNull();
        result.ExifGps!.ConfidenceLevel.Should().Be(ConfidenceLevel.VeryHigh);
    }

    [TestMethod]
    public async Task ProcessImage_SecondCall_ShouldUseCacheAndBeInstant()
    {
        // Arrange
        var imagePath = TestData.GetSampleImagePath();

        // Act
        var sw = Stopwatch.StartNew();
        await _processor!.ProcessImageAsync(imagePath); // First call
        sw.Stop();
        var firstCallTime = sw.ElapsedMilliseconds;

        sw.Restart();
        await _processor!.ProcessImageAsync(imagePath); // Second call (cached)
        sw.Stop();
        var secondCallTime = sw.ElapsedMilliseconds;

        // Assert
        secondCallTime.Should().BeLessThan(100); // Cache hit should be < 100ms
        secondCallTime.Should().BeLessThan(firstCallTime / 10); // At least 10x faster
    }
}
```

---

## 4. UI Automation Testing

### WinAppDriver Setup

```bash
# Install WinAppDriver
# https://github.com/Microsoft/WinAppDriver/releases

# Run WinAppDriver on test machine
WinAppDriver.exe
```

### UI Test Example

```csharp
// UI/MainPageTests.cs
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

[TestClass]
public class MainPageUITests
{
    private WindowsDriver<WindowsElement>? _session;

    [TestInitialize]
    public void Setup()
    {
        var appiumOptions = new AppiumOptions();
        appiumOptions.AddAdditionalCapability("app", "GeoLens.exe");
        appiumOptions.AddAdditionalCapability("deviceName", "WindowsPC");

        _session = new WindowsDriver<WindowsElement>(
            new Uri("http://127.0.0.1:4723"),
            appiumOptions
        );

        _session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _session?.Quit();
    }

    [TestMethod]
    public void AddImageButton_Click_ShouldOpenFilePicker()
    {
        // Arrange
        var addButton = _session!.FindElementByName("Add Image(s)");

        // Act
        addButton.Click();

        // Assert
        var fileDialog = _session.FindElementByClassName("#32770"); // File dialog class
        fileDialog.Should().NotBeNull();
    }

    [TestMethod]
    public void SettingsButton_Click_ShouldOpenSettingsWindow()
    {
        // Arrange
        var settingsButton = _session!.FindElementByName("Settings");

        // Act
        settingsButton.Click();
        Thread.Sleep(1000); // Wait for window

        // Assert
        var settingsWindow = _session.FindElementByName("GeoLens Settings");
        settingsWindow.Should().NotBeNull();
    }
}
```

---

## 5. Performance Benchmarking

### BenchmarkDotNet Setup

```xml
<PackageReference Include="BenchmarkDotNet" Version="0.13.10" />
```

### Cache Performance Benchmark

```csharp
// Performance/CacheBenchmarks.cs
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
public class CacheBenchmarks
{
    private PredictionCacheService _cache;
    private string _imagePath;
    private List<EnhancedLocationPrediction> _predictions;

    [GlobalSetup]
    public void Setup()
    {
        _cache = new PredictionCacheService(":memory:");
        _imagePath = TestData.GetSampleImagePath();
        _predictions = TestData.GetMockPredictions();
    }

    [Benchmark]
    public async Task<string> ComputeHash()
    {
        return await _cache.ComputeHashAsync(_imagePath);
    }

    [Benchmark]
    public async Task SavePrediction()
    {
        await _cache.SavePredictionAsync(_imagePath, _predictions, "cpu");
    }

    [Benchmark]
    public async Task<CachedPrediction?> GetCachedPrediction()
    {
        return await _cache.GetCachedPredictionAsync(_imagePath);
    }
}

// Run with: dotnet run -c Release --project GeoLens.Benchmarks
```

### Heatmap Generation Benchmark

```csharp
// Performance/HeatmapGenerationBenchmarks.cs
[MemoryDiagnoser]
public class HeatmapGenerationBenchmarks
{
    private List<EnhancedPredictionResult> _results10;
    private List<EnhancedPredictionResult> _results50;
    private List<EnhancedPredictionResult> _results100;
    private PredictionHeatmapGenerator _generator;

    [GlobalSetup]
    public void Setup()
    {
        _generator = new PredictionHeatmapGenerator();
        _results10 = TestData.GenerateMockResults(10);
        _results50 = TestData.GenerateMockResults(50);
        _results100 = TestData.GenerateMockResults(100);
    }

    [Benchmark]
    public HeatmapData GenerateHeatmap_10Images()
    {
        return _generator.GenerateHeatmap(_results10);
    }

    [Benchmark]
    public HeatmapData GenerateHeatmap_50Images()
    {
        return _generator.GenerateHeatmap(_results50);
    }

    [Benchmark]
    public HeatmapData GenerateHeatmap_100Images()
    {
        return _generator.GenerateHeatmap(_results100);
    }
}
```

---

## 6. Test Data Management

### Mock Data Generation

```csharp
// TestData/TestDataGenerator.cs
public static class TestData
{
    private static readonly string TestDataPath = Path.Combine(
        AppContext.BaseDirectory, "TestData", "Images"
    );

    public static string GetSampleImagePath()
    {
        return Path.Combine(TestDataPath, "sample_landscape.jpg");
    }

    public static string GetImageWithExifGPS()
    {
        return Path.Combine(TestDataPath, "with_exif_gps", "tokyo_tower.jpg");
    }

    public static string GetImageWithoutExif()
    {
        return Path.Combine(TestDataPath, "without_exif", "screenshot.png");
    }

    public static List<EnhancedLocationPrediction> GetMockPredictions(int count = 5)
    {
        var random = new Random(42); // Deterministic seed
        var predictions = new List<EnhancedLocationPrediction>();

        for (int i = 0; i < count; i++)
        {
            predictions.Add(new EnhancedLocationPrediction
            {
                Rank = i + 1,
                Latitude = random.NextDouble() * 180 - 90,
                Longitude = random.NextDouble() * 360 - 180,
                Probability = 1.0 - (i * 0.15),
                City = $"City{i}",
                State = $"State{i}",
                Country = $"Country{i}",
                LocationSummary = $"Location {i}",
                ConfidenceLevel = ConfidenceLevel.High
            });
        }

        return predictions;
    }

    public static List<EnhancedPredictionResult> GenerateMockResults(int imageCount)
    {
        var results = new List<EnhancedPredictionResult>();

        for (int i = 0; i < imageCount; i++)
        {
            results.Add(new EnhancedPredictionResult
            {
                ImagePath = $"/test/image_{i}.jpg",
                AiPredictions = GetMockPredictions(5)
            });
        }

        return results;
    }
}
```

---

## 7. Continuous Testing

### GitHub Actions Workflow

```yaml
# .github/workflows/test.yml
name: Run Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore --configuration Release

    - name: Run Unit Tests
      run: dotnet test --no-build --configuration Release --filter "Category=Unit"

    - name: Run Integration Tests
      run: dotnet test --no-build --configuration Release --filter "Category=Integration"

    - name: Upload Test Results
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: test-results
        path: '**/TestResults/*.trx'
```

---

## 8. Performance Targets

### Acceptance Criteria

| Component | Target | Measurement |
|-----------|--------|-------------|
| Hash Computation | < 10ms | BenchmarkDotNet |
| Cache Hit Lookup | < 100ms | Integration test |
| Cache Save | < 200ms | Integration test |
| Heatmap Generation (100 images) | < 500ms | BenchmarkDotNet |
| Python Service Startup | < 30 seconds | Integration test |
| API Prediction (GPU) | < 3 seconds | Integration test |
| API Prediction (CPU) | < 10 seconds | Integration test |
| UI Launch Time | < 3 seconds | Manual/UI automation |
| Memory Usage (idle) | < 200 MB | Performance counter |
| Memory Usage (processing 100 images) | < 800 MB | Performance counter |

---

## 9. Test Coverage Goals

- **Unit Tests**: > 80% code coverage for service layer
- **Integration Tests**: All critical paths tested
- **UI Tests**: Smoke tests for all main workflows
- **Performance Tests**: Regression detection for all benchmarks

### Measuring Coverage

```bash
# Install coverage tool
dotnet tool install --global dotnet-coverage

# Run tests with coverage
dotnet-coverage collect "dotnet test" -f xml -o coverage.xml

# Generate HTML report
ReportGenerator -reports:coverage.xml -targetdir:coverage_html
```

---

## 10. Manual Test Checklist

### Pre-Release Testing

**Hardware Configurations:**
- [ ] Test on NVIDIA GPU machine (RTX 3060 or better)
- [ ] Test on AMD GPU machine (RX 6000 series)
- [ ] Test on CPU-only machine (Intel i5 or better)

**Operating Systems:**
- [ ] Windows 11 (22H2 or later)
- [ ] Windows 10 (21H2 or later)

**Image Formats:**
- [ ] JPEG with EXIF GPS
- [ ] JPEG without EXIF
- [ ] PNG
- [ ] HEIC (iPhone photos)
- [ ] Corrupted/invalid images

**Batch Sizes:**
- [ ] Single image
- [ ] 10 images
- [ ] 50 images
- [ ] 100+ images

**Offline Mode:**
- [ ] Disconnect internet
- [ ] Verify predictions still work
- [ ] Verify offline maps render

**Export Formats:**
- [ ] CSV export
- [ ] PDF export
- [ ] KML export (open in Google Earth)

**Error Scenarios:**
- [ ] Python service crash recovery
- [ ] Out of memory handling
- [ ] Disk full scenarios
- [ ] Network interruption (online maps)

---

This testing strategy ensures comprehensive coverage and high-quality releases.
