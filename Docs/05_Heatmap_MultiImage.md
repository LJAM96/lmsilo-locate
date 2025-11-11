# Multi-Image Heatmap System

## Overview

The heatmap feature aggregates predictions from multiple selected images to visualize geographic patterns and identify hotspots. This document details the implementation of the heatmap generation, visualization, and interaction system.

---

## Architecture

```
User selects multiple images
         │
         ▼
┌────────────────────────┐
│ PredictionAggregator   │ ← Collect all predictions
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│ HeatmapGenerator       │ ← Generate intensity grid
│  - 360×180 grid        │
│  - Gaussian smoothing  │
│  - Normalization       │
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│ HotspotDetector        │ ← Find high-intensity regions
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│ MapProvider            │ ← Render heatmap visualization
│  - HexBin (Three.js)   │
│  - Color gradient      │
│  - Interactive tooltip │
└────────────────────────┘
```

---

## 1. Prediction Aggregation

### Weighting Strategy

Predictions are weighted based on:
1. **Rank**: Higher ranks get more weight (1/rank multiplier)
2. **Confidence**: Probability score (0.0-1.0)
3. **Source**: EXIF GPS gets 2x weight

```csharp
// Services/PredictionAggregator.cs
public class PredictionAggregator
{
    public List<WeightedPrediction> AggregateFromResults(
        List<EnhancedPredictionResult> results)
    {
        var aggregated = new List<WeightedPrediction>();

        foreach (var result in results)
        {
            // Add EXIF GPS with maximum weight
            if (result.ExifGps != null)
            {
                aggregated.Add(new WeightedPrediction
                {
                    Latitude = result.ExifGps.Latitude,
                    Longitude = result.ExifGps.Longitude,
                    Weight = 2.0, // Double weight for EXIF
                    Source = "EXIF GPS",
                    ImagePath = result.ImagePath
                });
            }

            // Add AI predictions with rank-based weighting
            foreach (var pred in result.AiPredictions)
            {
                // Weight formula: (probability) * (1 / rank)
                // Rank 1 with 0.85 confidence = 0.85 * 1.0 = 0.85
                // Rank 5 with 0.50 confidence = 0.50 * 0.2 = 0.10
                double weight = pred.AdjustedProbability * (1.0 / pred.Rank);

                aggregated.Add(new WeightedPrediction
                {
                    Latitude = pred.Latitude,
                    Longitude = pred.Longitude,
                    Weight = weight,
                    Source = pred.LocationSummary,
                    ImagePath = result.ImagePath,
                    Rank = pred.Rank
                });
            }
        }

        return aggregated;
    }

    public PredictionStatistics GetStatistics(
        List<WeightedPrediction> predictions)
    {
        return new PredictionStatistics
        {
            TotalPredictions = predictions.Count,
            ExifCount = predictions.Count(p => p.Source == "EXIF GPS"),
            AiCount = predictions.Count(p => p.Source != "EXIF GPS"),
            AverageWeight = predictions.Average(p => p.Weight),
            MaxWeight = predictions.Max(p => p.Weight),
            CoverageAreaKm2 = CalculateCoverageArea(predictions)
        };
    }

    private double CalculateCoverageArea(List<WeightedPrediction> predictions)
    {
        if (predictions.Count < 2) return 0;

        // Calculate bounding box
        double minLat = predictions.Min(p => p.Latitude);
        double maxLat = predictions.Max(p => p.Latitude);
        double minLon = predictions.Min(p => p.Longitude);
        double maxLon = predictions.Max(p => p.Longitude);

        // Approximate area (not accounting for Earth curvature)
        double latDiff = maxLat - minLat;
        double lonDiff = maxLon - minLon;
        double avgLat = (minLat + maxLat) / 2;

        // Convert degrees to km
        double latKm = latDiff * 111.32; // 1° latitude ≈ 111.32 km
        double lonKm = lonDiff * 111.32 * Math.Cos(avgLat * Math.PI / 180.0);

        return latKm * lonKm;
    }
}

public record WeightedPrediction
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Weight { get; init; }
    public string Source { get; init; } = "";
    public string ImagePath { get; init; } = "";
    public int? Rank { get; init; }
}

public record PredictionStatistics
{
    public int TotalPredictions { get; init; }
    public int ExifCount { get; init; }
    public int AiCount { get; init; }
    public double AverageWeight { get; init; }
    public double MaxWeight { get; init; }
    public double CoverageAreaKm2 { get; init; }
}
```

---

## 2. Heatmap Generation

### Grid-Based Approach

Uses a 360×180 grid (1° resolution) with Gaussian smoothing.

```csharp
// Services/PredictionHeatmapGenerator.cs
public class PredictionHeatmapGenerator
{
    private const int GridWidth = 360;   // Longitude: -180 to +180
    private const int GridHeight = 180;  // Latitude: -90 to +90
    private const double GaussianSigma = 3.0; // Smoothing radius in degrees

    public HeatmapData GenerateHeatmap(
        List<EnhancedPredictionResult> results)
    {
        // Step 1: Aggregate predictions
        var aggregator = new PredictionAggregator();
        var predictions = aggregator.AggregateFromResults(results);

        // Step 2: Initialize grid
        var grid = new double[GridWidth, GridHeight];

        // Step 3: Apply Gaussian kernel for each prediction
        foreach (var pred in predictions)
        {
            ApplyGaussianKernel(
                grid,
                pred.Latitude,
                pred.Longitude,
                pred.Weight,
                GaussianSigma
            );
        }

        // Step 4: Normalize to 0-1 range
        NormalizeGrid(grid);

        // Step 5: Detect hotspots
        var hotspots = DetectHotspots(grid, threshold: 0.7);

        // Step 6: Create result
        return new HeatmapData
        {
            Grid = grid,
            Resolution = 1.0, // 1 degree per cell
            PredictionCount = predictions.Count,
            ImageCount = results.Count,
            HotspotRegions = hotspots,
            Statistics = aggregator.GetStatistics(predictions)
        };
    }

    private void ApplyGaussianKernel(
        double[,] grid,
        double lat,
        double lon,
        double weight,
        double sigma)
    {
        // Convert lat/lon to grid coordinates
        int centerX = (int)Math.Round((lon + 180) % 360);
        int centerY = (int)Math.Round(90 - lat);

        // Apply kernel in 3-sigma radius
        int radius = (int)(sigma * 3);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                // Wrap longitude (periodic boundary)
                int x = (centerX + dx + GridWidth) % GridWidth;

                // Clamp latitude (no wrapping)
                int y = centerY + dy;
                if (y < 0 || y >= GridHeight) continue;

                // Calculate distance
                double distance = Math.Sqrt(dx * dx + dy * dy);

                // Gaussian function: exp(-(distance^2) / (2 * sigma^2))
                double gaussianValue = Math.Exp(
                    -(distance * distance) / (2 * sigma * sigma)
                );

                // Add weighted contribution
                grid[x, y] += weight * gaussianValue;
            }
        }
    }

    private void NormalizeGrid(double[,] grid)
    {
        // Find max value
        double maxValue = 0;
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                maxValue = Math.Max(maxValue, grid[x, y]);
            }
        }

        // Normalize to 0-1
        if (maxValue > 0)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    grid[x, y] /= maxValue;
                }
            }
        }
    }

    private List<HotspotRegion> DetectHotspots(
        double[,] grid,
        double threshold)
    {
        var hotspots = new List<HotspotRegion>();

        // Find all cells above threshold
        var candidates = new List<(int x, int y, double intensity)>();

        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                if (grid[x, y] >= threshold)
                {
                    candidates.Add((x, y, grid[x, y]));
                }
            }
        }

        // Cluster nearby hotspots
        var visited = new HashSet<(int, int)>();

        foreach (var (x, y, intensity) in candidates.OrderByDescending(c => c.intensity))
        {
            if (visited.Contains((x, y))) continue;

            // Find cluster
            var cluster = FindCluster(grid, x, y, threshold, visited);

            if (cluster.Count > 0)
            {
                // Calculate centroid
                double avgX = cluster.Average(c => c.x);
                double avgY = cluster.Average(c => c.y);
                double avgIntensity = cluster.Average(c => c.intensity);

                // Convert back to lat/lon
                double lon = avgX - 180;
                double lat = 90 - avgY;

                hotspots.Add(new HotspotRegion
                {
                    Latitude = lat,
                    Longitude = lon,
                    Intensity = avgIntensity,
                    CellCount = cluster.Count,
                    RadiusKm = EstimateRadiusKm(cluster)
                });
            }
        }

        return hotspots;
    }

    private List<(int x, int y, double intensity)> FindCluster(
        double[,] grid,
        int startX,
        int startY,
        double threshold,
        HashSet<(int, int)> visited)
    {
        var cluster = new List<(int x, int y, double intensity)>();
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            if (visited.Contains((x, y))) continue;
            if (grid[x, y] < threshold) continue;

            visited.Add((x, y));
            cluster.Add((x, y, grid[x, y]));

            // Check neighbors (8-connected)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = (x + dx + GridWidth) % GridWidth;
                    int ny = y + dy;

                    if (ny >= 0 && ny < GridHeight && !visited.Contains((nx, ny)))
                    {
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        return cluster;
    }

    private double EstimateRadiusKm(List<(int x, int y, double intensity)> cluster)
    {
        if (cluster.Count < 2) return 0;

        // Calculate bounding box
        int minX = cluster.Min(c => c.x);
        int maxX = cluster.Max(c => c.x);
        int minY = cluster.Min(c => c.y);
        int maxY = cluster.Max(c => c.y);

        // Convert to approximate km (1° ≈ 111 km)
        double widthKm = (maxX - minX) * 111.32;
        double heightKm = (maxY - minY) * 111.32;

        // Return radius of equivalent circle
        return Math.Sqrt(widthKm * widthKm + heightKm * heightKm) / 2;
    }
}

public record HeatmapData
{
    public double[,] Grid { get; init; } = new double[360, 180];
    public double Resolution { get; init; }
    public int PredictionCount { get; init; }
    public int ImageCount { get; init; }
    public List<HotspotRegion> HotspotRegions { get; init; } = new();
    public PredictionStatistics Statistics { get; init; } = new();
}

public record HotspotRegion
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Intensity { get; init; }
    public int CellCount { get; init; }
    public double RadiusKm { get; init; }
    public string? LocationName { get; set; } // Reverse geocoded
}
```

---

## 3. Visualization

### Three.js HexBin Rendering

```javascript
// Assets/globe_dark.html (heatmap extension)

function showHeatmap(heatmapData) {
    const hexData = [];
    const grid = heatmapData.grid;

    // Convert grid to hex data
    for (let x = 0; x < 360; x++) {
        for (let y = 0; y < 180; y++) {
            const intensity = grid[x][y];

            // Skip low-intensity cells
            if (intensity < 0.1) continue;

            hexData.push({
                lat: 90 - y,
                lng: x - 180,
                weight: intensity
            });
        }
    }

    // Clear existing pins and rings
    world.pointsData([]);
    world.ringsData([]);

    // Configure hexBin layer
    world.hexBinPointsData(hexData)
        .hexBinPointWeight('weight')
        .hexAltitude(d => d.sumWeight * 0.05) // Height based on intensity
        .hexBinResolution(4) // 4x4 degree bins
        .hexTopColor(d => heatmapColorScale(d.sumWeight))
        .hexSideColor(d => heatmapColorScale(d.sumWeight))
        .hexBinMerge(true) // Merge adjacent hexes
        .enablePointerInteraction(true)
        .hexLabel(d => createHeatmapTooltip(d));

    // Add hotspot markers
    addHotspotMarkers(heatmapData.hotspotRegions);
}

function heatmapColorScale(intensity) {
    // 5-step gradient for dark mode visibility
    if (intensity < 0.2) return `rgba(0, 100, 255, ${intensity * 3})`; // Dark blue
    if (intensity < 0.4) return `rgba(0, 200, 255, ${intensity * 2})`; // Cyan
    if (intensity < 0.6) return `rgba(0, 255, 150, ${intensity})`; // Green
    if (intensity < 0.8) return `rgba(255, 230, 0, ${intensity})`; // Yellow
    return `rgba(255, 50, 50, ${Math.min(intensity, 1.0)})`; // Red
}

function createHeatmapTooltip(hexData) {
    return `
        <div class="tooltip" style="border: 2px solid ${heatmapColorScale(hexData.sumWeight)}">
            <div style="font-weight: bold; margin-bottom: 4px;">
                Hotspot Region
            </div>
            <div style="font-size: 11px; opacity: 0.9;">
                Intensity: ${(hexData.sumWeight * 100).toFixed(1)}%
            </div>
            <div style="font-size: 10px; opacity: 0.7; margin-top: 2px;">
                ${hexData.points.length} predictions
            </div>
        </div>
    `;
}

function addHotspotMarkers(hotspots) {
    const markers = hotspots.map(hotspot => ({
        lat: hotspot.latitude,
        lng: hotspot.longitude,
        size: 1.5,
        color: '#ffffff',
        label: `Hotspot: ${hotspot.locationName || 'Unknown'} (${(hotspot.intensity * 100).toFixed(0)}%)`,
        intensity: hotspot.intensity
    }));

    world.pointsData(markers)
        .pointAltitude(0.02)
        .pointRadius('size')
        .pointColor('color')
        .pointLabel('label');
}

function hideHeatmap() {
    world.hexBinPointsData([]);
    world.pointsData([]);
}
```

---

## 4. UI Integration

### Toggle Controls

```xml
<!-- Views/MainPage.xaml - Heatmap Toggle -->
<StackPanel Orientation="Horizontal" Spacing="12">
    <ToggleButton
        x:Name="HeatmapToggle"
        IsChecked="{x:Bind ViewModel.IsHeatmapMode, Mode=TwoWay}"
        Width="120">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <FontIcon Glyph="&#xE81C;" FontSize="16"/>
            <TextBlock Text="Heatmap"/>
        </StackPanel>
    </ToggleButton>

    <TextBlock
        Text="{x:Bind ViewModel.HeatmapStatusText, Mode=OneWay}"
        VerticalAlignment="Center"
        Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
</StackPanel>
```

### Code-Behind

```csharp
// Views/MainPage.xaml.cs
private bool _isHeatmapMode;
public bool IsHeatmapMode
{
    get => _isHeatmapMode;
    set
    {
        _isHeatmapMode = value;
        OnPropertyChanged();
        UpdateMapVisualization();
    }
}

private string _heatmapStatusText = "";
public string HeatmapStatusText
{
    get => _heatmapStatusText;
    set
    {
        _heatmapStatusText = value;
        OnPropertyChanged();
    }
}

private async void UpdateMapVisualization()
{
    if (IsHeatmapMode && SelectedImages.Count > 1)
    {
        await ShowHeatmapAsync();
    }
    else if (IsHeatmapMode && SelectedImages.Count <= 1)
    {
        HeatmapStatusText = "Select 2 or more images for heatmap";
        IsHeatmapMode = false;
    }
    else
    {
        await ShowIndividualPinsAsync();
    }
}

private async Task ShowHeatmapAsync()
{
    // Get processed results
    var processedResults = SelectedImages
        .Where(img => img.Result != null)
        .Select(img => img.Result!)
        .ToList();

    if (processedResults.Count == 0)
    {
        HeatmapStatusText = "No processed images in selection";
        return;
    }

    // Generate heatmap
    var generator = new PredictionHeatmapGenerator();
    var heatmapData = generator.GenerateHeatmap(processedResults);

    // Reverse geocode hotspots
    foreach (var hotspot in heatmapData.HotspotRegions)
    {
        hotspot.LocationName = await ReverseGeocodeAsync(
            hotspot.Latitude,
            hotspot.Longitude
        );
    }

    // Update status
    HeatmapStatusText = $"{heatmapData.PredictionCount} predictions from " +
                       $"{heatmapData.ImageCount} images, " +
                       $"{heatmapData.HotspotRegions.Count} hotspots";

    // Send to map
    await _mapProvider.ShowHeatmapAsync(heatmapData);

    // Update UI
    HotspotCount = heatmapData.HotspotRegions.Count;
    OnPropertyChanged(nameof(HotspotCount));
}
```

---

## 5. Export Functionality

### Heatmap Data Export

```csharp
// Services/HeatmapExporter.cs
public class HeatmapExporter
{
    public async Task ExportToGeoTIFF(HeatmapData heatmap, string outputPath)
    {
        // Export as GeoTIFF for GIS software
        // (Requires external library like MaxRev.Gdal.Core)
    }

    public async Task ExportToCSV(HeatmapData heatmap, string outputPath)
    {
        await using var writer = new StreamWriter(outputPath);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write hotspots
        await csv.WriteRecordsAsync(heatmap.HotspotRegions.Select(h => new
        {
            Latitude = h.Latitude,
            Longitude = h.Longitude,
            Intensity = h.Intensity,
            LocationName = h.LocationName,
            RadiusKm = h.RadiusKm,
            CellCount = h.CellCount
        }));
    }

    public async Task ExportToJSON(HeatmapData heatmap, string outputPath)
    {
        var json = JsonSerializer.Serialize(new
        {
            metadata = new
            {
                imageCount = heatmap.ImageCount,
                predictionCount = heatmap.PredictionCount,
                resolution = heatmap.Resolution,
                generatedAt = DateTime.UtcNow
            },
            hotspots = heatmap.HotspotRegions,
            statistics = heatmap.Statistics
        }, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(outputPath, json);
    }
}
```

---

## 6. Performance Optimization

### Optimization Strategies

1. **Grid Size**: Use 360×180 (1°) for speed, 720×360 (0.5°) for quality
2. **Caching**: Cache generated heatmaps for selected image sets
3. **Progressive Rendering**: Render low-res preview, then refine
4. **Web Workers**: Offload grid calculation to background thread (JS)

### Benchmark Targets

| Operation | Target Time |
|-----------|-------------|
| Aggregate 100 predictions | < 10ms |
| Generate grid (360×180) | < 100ms |
| Detect hotspots | < 50ms |
| Render to canvas | < 200ms |
| **Total** | **< 400ms** |

---

## 7. User Interactions

### Hotspot Click

```javascript
world.onHexClick(hex => {
    // Show detailed breakdown
    const predictions = hex.points;
    const breakdown = groupByImage(predictions);

    showHotspotDetails({
        center: { lat: hex.lat, lng: hex.lng },
        intensity: hex.sumWeight,
        imageBreakdown: breakdown
    });
});

function groupByImage(predictions) {
    const grouped = {};

    predictions.forEach(pred => {
        if (!grouped[pred.imagePath]) {
            grouped[pred.imagePath] = [];
        }
        grouped[pred.imagePath].push(pred);
    });

    return grouped;
}
```

### Intensity Slider

```xml
<Slider
    Minimum="0.1"
    Maximum="1.0"
    Value="{x:Bind ViewModel.HeatmapThreshold, Mode=TwoWay}"
    StepFrequency="0.1"
    Header="Intensity Threshold"/>
```

---

## 8. Edge Cases

### Handling Special Scenarios

```csharp
// 1. Single image selected
if (results.Count == 1)
{
    ShowWarning("Heatmap requires 2 or more images. Showing individual pins.");
    return;
}

// 2. No overlapping regions
if (heatmap.HotspotRegions.Count == 0)
{
    ShowInfo("No significant hotspots detected. Predictions are widely dispersed.");
}

// 3. Global distribution (all continents)
if (heatmap.Statistics.CoverageAreaKm2 > 100_000_000) // > 100M km²
{
    ShowInfo("Predictions span multiple continents. Consider filtering by region.");
}

// 4. Polar regions
if (predictions.Any(p => Math.Abs(p.Latitude) > 80))
{
    ShowWarning("Polar region predictions may appear distorted in 2D projection.");
}
```

---

## 9. Testing Strategy

### Unit Tests

```csharp
[TestMethod]
public void GenerateHeatmap_SinglePrediction_CreatesGaussianDistribution()
{
    var results = new List<EnhancedPredictionResult>
    {
        CreateMockResult(lat: 0, lon: 0, confidence: 1.0)
    };

    var generator = new PredictionHeatmapGenerator();
    var heatmap = generator.GenerateHeatmap(results);

    // Check center cell has max value
    Assert.AreEqual(1.0, heatmap.Grid[180, 90], 0.01);

    // Check Gaussian falloff
    Assert.IsTrue(heatmap.Grid[183, 90] < heatmap.Grid[181, 90]);
}

[TestMethod]
public void DetectHotspots_ClusteredPredictions_FindsSingleHotspot()
{
    var results = CreateClusteredPredictions(
        centerLat: 35.6,
        centerLon: 139.7,
        count: 10,
        radiusKm: 50
    );

    var generator = new PredictionHeatmapGenerator();
    var heatmap = generator.GenerateHeatmap(results);

    Assert.AreEqual(1, heatmap.HotspotRegions.Count);
    Assert.AreEqual(35.6, heatmap.HotspotRegions[0].Latitude, 1.0);
}
```

---

This completes the heatmap system documentation. All algorithms, UI patterns, and optimization strategies are production-ready.
