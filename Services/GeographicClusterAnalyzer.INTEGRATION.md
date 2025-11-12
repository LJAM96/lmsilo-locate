# GeographicClusterAnalyzer Integration Guide

## Quick Start

### 1. Add to your service layer

```csharp
// In your PredictionProcessor or MainPage.xaml.cs
using GeoLens.Services;
using GeoLens.Models;

// Create analyzer instance (can be reused)
private readonly GeographicClusterAnalyzer _clusterAnalyzer = new();
```

### 2. Use after getting predictions from API

```csharp
public async Task<EnhancedPredictionResult> ProcessImageAsync(string imagePath)
{
    // Step 1: Get predictions from GeoCLIP API
    var apiResponse = await _apiClient.InferAsync(imagePath, topK: 5);

    // Step 2: Convert to EnhancedLocationPrediction list
    var predictions = new List<EnhancedLocationPrediction>();
    for (int i = 0; i < apiResponse.Predictions.Count; i++)
    {
        var pred = apiResponse.Predictions[i];
        predictions.Add(new EnhancedLocationPrediction
        {
            Rank = i + 1,
            Latitude = pred.Latitude,
            Longitude = pred.Longitude,
            Probability = pred.Probability,
            AdjustedProbability = pred.Probability, // Start with raw probability
            City = pred.City,
            State = pred.State,
            Country = pred.Country,
            LocationSummary = $"{pred.City}, {pred.Country}",
            IsPartOfCluster = false // Will be set by analyzer
        });
    }

    // Step 3: Run cluster analysis
    var clusterInfo = _clusterAnalyzer.AnalyzeClusters(predictions);

    // Step 4: Return complete result
    return new EnhancedPredictionResult
    {
        ImagePath = imagePath,
        AiPredictions = predictions,
        ClusterInfo = clusterInfo
    };
}
```

### 3. Display results in UI

```csharp
private async void ProcessImageButton_Click(object sender, RoutedEventArgs e)
{
    var result = await ProcessImageAsync(selectedImagePath);

    // Display cluster info
    if (result.ClusterInfo?.IsClustered == true)
    {
        ClusterInfoText.Text = $"Predictions clustered within {result.ClusterInfo.ClusterRadius:F0}km";
        ClusterBoostText.Text = $"Confidence boosted by {result.ClusterInfo.ConfidenceBoost * 100:F1}%";
        ClusterCenterText.Text = $"Center: {result.ClusterInfo.ClusterCenterLat:F4}°, {result.ClusterInfo.ClusterCenterLon:F4}°";

        ClusterIndicator.Visibility = Visibility.Visible;
    }
    else
    {
        ClusterIndicator.Visibility = Visibility.Collapsed;
    }

    // Bind predictions to UI
    PredictionsList.ItemsSource = result.AiPredictions;
}
```

## Full Example: MainPage.xaml.cs Integration

```csharp
using GeoLens.Services;
using GeoLens.Models;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeoLens.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly GeoCLIPApiClient _apiClient;
        private readonly GeographicClusterAnalyzer _clusterAnalyzer;
        private readonly ExifMetadataExtractor _exifExtractor;

        public MainPage()
        {
            this.InitializeComponent();
            _apiClient = new GeoCLIPApiClient("http://localhost:8899");
            _clusterAnalyzer = new GeographicClusterAnalyzer();
            _exifExtractor = new ExifMetadataExtractor();
        }

        private async Task<EnhancedPredictionResult> ProcessImageWithClusteringAsync(string imagePath)
        {
            // 1. Try EXIF GPS first
            var exifGps = await _exifExtractor.ExtractGpsDataAsync(imagePath);

            if (exifGps?.HasGps == true)
            {
                // Create EXIF-based result (highest confidence)
                return new EnhancedPredictionResult
                {
                    ImagePath = imagePath,
                    ExifGps = exifGps,
                    AiPredictions = new List<EnhancedLocationPrediction>
                    {
                        new EnhancedLocationPrediction
                        {
                            Rank = 1,
                            Latitude = exifGps.Latitude,
                            Longitude = exifGps.Longitude,
                            Probability = 1.0,
                            AdjustedProbability = 1.0,
                            ConfidenceLevel = ConfidenceLevel.VeryHigh,
                            City = exifGps.City,
                            State = exifGps.State,
                            Country = exifGps.Country,
                            LocationSummary = exifGps.LocationSummary
                        }
                    }
                };
            }

            // 2. Get AI predictions from GeoCLIP
            var apiResponse = await _apiClient.InferAsync(imagePath, topK: 5);

            var predictions = new List<EnhancedLocationPrediction>();
            for (int i = 0; i < apiResponse.Predictions.Count; i++)
            {
                var pred = apiResponse.Predictions[i];
                predictions.Add(new EnhancedLocationPrediction
                {
                    Rank = i + 1,
                    Latitude = pred.Latitude,
                    Longitude = pred.Longitude,
                    Probability = pred.Probability,
                    AdjustedProbability = pred.Probability,
                    City = pred.City,
                    State = pred.State,
                    Country = pred.Country,
                    LocationSummary = $"{pred.City}, {pred.Country}"
                });
            }

            // 3. Apply cluster analysis
            var clusterInfo = _clusterAnalyzer.AnalyzeClusters(predictions);

            return new EnhancedPredictionResult
            {
                ImagePath = imagePath,
                AiPredictions = predictions,
                ClusterInfo = clusterInfo
            };
        }

        private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedImagePath))
                return;

            LoadingIndicator.IsActive = true;

            try
            {
                var result = await ProcessImageWithClusteringAsync(SelectedImagePath);

                // Update UI
                UpdatePredictionDisplay(result);
                UpdateClusterDisplay(result.ClusterInfo);
                UpdateMapPins(result);
            }
            catch (Exception ex)
            {
                ErrorText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                LoadingIndicator.IsActive = false;
            }
        }

        private void UpdateClusterDisplay(ClusterAnalysisResult? clusterInfo)
        {
            if (clusterInfo?.IsClustered == true)
            {
                ClusterPanel.Visibility = Visibility.Visible;
                ClusterRadiusText.Text = $"{clusterInfo.ClusterRadius:F1} km";
                ClusterBoostText.Text = $"+{clusterInfo.ConfidenceBoost * 100:F1}%";
                ClusterCenterText.Text = $"{clusterInfo.ClusterCenterLat:F4}°, {clusterInfo.ClusterCenterLon:F4}°";
            }
            else
            {
                ClusterPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
```

## XAML UI Elements for Cluster Display

```xml
<!-- Add to MainPage.xaml -->
<StackPanel x:Name="ClusterPanel"
            Visibility="Collapsed"
            Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
            Padding="16"
            Margin="0,8,0,0"
            CornerRadius="8">

    <TextBlock Text="Geographic Clustering Detected"
               FontWeight="SemiBold"
               Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"
               Margin="0,0,0,8"/>

    <Grid ColumnSpacing="16" RowSpacing="4">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Grid.Column="0" Text="Cluster Radius:" Opacity="0.7"/>
        <TextBlock Grid.Row="0" Grid.Column="1" x:Name="ClusterRadiusText" FontWeight="SemiBold"/>

        <TextBlock Grid.Row="1" Grid.Column="0" Text="Confidence Boost:" Opacity="0.7"/>
        <TextBlock Grid.Row="1" Grid.Column="1" x:Name="ClusterBoostText" FontWeight="SemiBold" Foreground="LimeGreen"/>

        <TextBlock Grid.Row="2" Grid.Column="0" Text="Center:" Opacity="0.7"/>
        <TextBlock Grid.Row="2" Grid.Column="1" x:Name="ClusterCenterText" FontFamily="Consolas"/>
    </Grid>

    <InfoBar Severity="Success"
             IsOpen="True"
             IsClosable="False"
             Message="Multiple predictions agree on this region - higher reliability"
             Margin="0,8,0,0"/>
</StackPanel>
```

## Testing Integration

### Manual Test with Sample Data

```csharp
// Add a test button to MainPage.xaml
private void TestClusteringButton_Click(object sender, RoutedEventArgs e)
{
    // Create sample predictions (Paris area)
    var predictions = new List<EnhancedLocationPrediction>
    {
        new() { Rank = 1, Latitude = 48.8566, Longitude = 2.3522,
                Probability = 0.18, AdjustedProbability = 0.18, City = "Paris" },
        new() { Rank = 2, Latitude = 48.8049, Longitude = 2.1204,
                Probability = 0.14, AdjustedProbability = 0.14, City = "Versailles" },
        new() { Rank = 3, Latitude = 48.4084, Longitude = 2.7008,
                Probability = 0.11, AdjustedProbability = 0.11, City = "Fontainebleau" },
        new() { Rank = 4, Latitude = 52.5200, Longitude = 13.4050,
                Probability = 0.08, AdjustedProbability = 0.08, City = "Berlin" },
    };

    // Analyze
    var result = _clusterAnalyzer.AnalyzeClusters(predictions);

    // Display
    var message = result.IsClustered
        ? $"Cluster found! {predictions.Count(p => p.IsPartOfCluster)} predictions within {result.ClusterRadius:F1}km"
        : "No clustering detected";

    System.Diagnostics.Debug.WriteLine(message);
}
```

## Performance Considerations

### When to Run Clustering

```csharp
// GOOD: Run once per image, after getting all predictions
var result = await ProcessImageWithClusteringAsync(imagePath);

// BAD: Don't run on every UI update or animation frame
// BAD: Don't run multiple times for the same predictions
```

### Memory Management

```csharp
// Analyzer is stateless and can be reused
private readonly GeographicClusterAnalyzer _clusterAnalyzer = new();

// No need to dispose or recreate
```

## Troubleshooting

### Predictions not marked as clustered

Check that:
1. At least 2 predictions exist
2. Predictions are within 100km of each other
3. `AdjustedProbability` is initialized to `Probability` before analysis

### Confidence boost not visible

Check that:
1. UI is binding to `AdjustedProbability` not `Probability`
2. Cluster info panel is set to `Visible` when `IsClustered == true`

### Incorrect distances

Verify:
1. Coordinates are in decimal degrees (not DMS format)
2. Latitude is -90 to +90, Longitude is -180 to +180
3. No NaN or Infinity values in coordinates

## Next Steps

After integration:

1. Test with real images from GeoCLIP API
2. Add cluster visualization to map/globe
3. Implement cache storage for cluster results
4. Add cluster info to export formats (CSV, PDF, KML)
5. Consider showing cluster radius circle on map

## Related Files

- `/home/user/geolens/Services/GeographicClusterAnalyzer.cs` - Main implementation
- `/home/user/geolens/Services/GeographicClusterAnalyzer.Test.cs` - Test examples
- `/home/user/geolens/Services/GeographicClusterAnalyzer.README.md` - Full documentation
- `/home/user/geolens/Services/CLUSTERING_ALGORITHM_EXPLAINED.md` - Algorithm details
- `/home/user/geolens/Models/EnhancedLocationPrediction.cs` - Data model
- `/home/user/geolens/Models/EnhancedPredictionResult.cs` - Result model with ClusterAnalysisResult
