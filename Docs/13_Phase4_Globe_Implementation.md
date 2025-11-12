# Phase 4: 3D Globe Visualization - Implementation Guide

## Overview

Phase 4 implements a **3D interactive globe** using WebView2 + Three.js/Globe.GL with support for **both offline and online modes**. This allows GeoLens to visualize location predictions on a dark-themed globe with confidence-based pin colors.

---

## Implementation Status

### ✅ Completed

1. **HTML Globe Interface** (`Assets/Globe/globe_dark.html`)
   - Three.js + Globe.GL integration
   - Auto-detection of online/offline mode
   - CDN fallback to local assets
   - Dark space theme with atmosphere
   - Pin rendering with confidence colors
   - Rotation and zoom controls
   - JavaScript API for C# integration

2. **WebView2 Provider** (`Services/MapProviders/WebView2GlobeProvider.cs`)
   - IMapProvider interface implementation
   - WebView2 control wrapper
   - JavaScript interop layer
   - Offline mode support
   - Error handling and logging

3. **IMapProvider Interface** (`Services/MapProviders/IMapProvider.cs`)
   - Standard map provider contract
   - Methods: AddPin, ClearPins, RotateToLocation, SetHeatmapMode
   - IsReady property for initialization checking

4. **Project Configuration**
   - WebView2 NuGet package added (v1.0.2792.45)
   - Assets/Globe/ folder configured for output copy
   - Ready for integration into MainPage

### ⏳ Remaining Work

1. **MainPage Integration**
   - Replace globe placeholder with WebView2 control in XAML
   - Initialize WebView2GlobeProvider in MainPage.xaml.cs
   - Wire up DisplayPredictions() to add pins to globe
   - Handle globe initialization events

2. **Asset Download**
   - Download Three.js (v0.150.0) → `Assets/Globe/lib/three.min.js`
   - Download Globe.GL (v2.24.0) → `Assets/Globe/lib/globe.gl.min.js`
   - Download NASA Black Marble → `Assets/Globe/textures/earth-night.jpg`
   - Download night sky background → `Assets/Globe/textures/night-sky.png`

3. **Testing**
   - Test online mode (CDN loading)
   - Test offline mode (local assets)
   - Test pin rendering with various confidence levels
   - Test rotation animations
   - Test on different screen sizes

---

## Architecture

### Online vs. Offline Modes

| Feature | Online Mode | Offline Mode |
|---------|-------------|--------------|
| **Three.js Source** | CDN (`unpkg.com`) | Local (`./lib/three.min.js`) |
| **Globe.GL Source** | CDN (`unpkg.com`) | Local (`./lib/globe.gl.min.js`) |
| **Earth Texture** | CDN (`unpkg.com`) | Local (`./textures/earth-night.jpg`) |
| **Background** | CDN | Local (`./textures/night-sky.png`) |
| **Internet Required** | Yes | No |
| **First Load** | ~5 seconds | ~1 second |
| **Best For** | Development | Production/Air-gapped |

### Mode Selection Logic

```javascript
// In globe_dark.html
const config = {
    mode: 'auto', // 'online', 'offline', or 'auto'
    textureSource: 'auto'
};

function isOfflineMode() {
    return config.mode === 'offline' ||
           (config.mode === 'auto' && !navigator.onLine);
}
```

**Auto Mode** (default):
- Detects if `navigator.onLine` is false → uses offline assets
- Detects if `navigator.onLine` is true → tries CDN, falls back to local on error

**C# can override**:
```csharp
// Force offline mode
var provider = new WebView2GlobeProvider(webView, offlineMode: true);
```

---

## Integration Steps

### Step 1: Add WebView2 to MainPage.xaml

Replace the globe placeholder in MainPage.xaml (around line 295):

```xml
<!-- 3D Globe (WebView2) -->
<Grid Grid.Row="1">
    <WebView2 x:Name="GlobeWebView"
             DefaultBackgroundColor="Black"/>

    <!-- Loading Overlay -->
    <Grid x:Name="GlobeLoadingOverlay"
         Background="#0F0F0F"
         Visibility="Visible">
        <StackPanel HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   Spacing="16">
            <ProgressRing IsActive="True"
                         Width="48"
                         Height="48"/>
            <TextBlock Text="Loading 3D Globe..."
                      FontSize="16"
                      Foreground="#FFFFFF"/>
        </StackPanel>
    </Grid>
</Grid>
```

### Step 2: Initialize Globe in MainPage.xaml.cs

Add to MainPage.xaml.cs:

```csharp
using GeoLens.Services.MapProviders;

public sealed partial class MainPage : Page
{
    private IMapProvider? _mapProvider;

    public MainPage()
    {
        InitializeComponent();
        LoadMockData();

        // Wire up selection changed event
        ImageListView.SelectionChanged += ImageListView_SelectionChanged;

        // Initialize globe
        this.Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeGlobeAsync();
    }

    private async Task InitializeGlobeAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainPage] Initializing globe...");

            // Create provider (auto-detect online/offline)
            _mapProvider = new WebView2GlobeProvider(GlobeWebView, offlineMode: false);

            // Initialize
            await _mapProvider.InitializeAsync();

            // Hide loading overlay
            GlobeLoadingOverlay.Visibility = Visibility.Collapsed;

            System.Diagnostics.Debug.WriteLine("[MainPage] Globe ready");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Globe initialization failed: {ex.Message}");

            // Show error message on loading overlay
            GlobeLoadingOverlay.Visibility = Visibility.Visible;
            // TODO: Update overlay to show error instead of loading
        }
    }
}
```

### Step 3: Add Pins After Prediction

Update `DisplayPredictions()` method:

```csharp
private async void DisplayPredictions(Services.DTOs.PredictionResult result)
{
    // Clear existing predictions
    Predictions.Clear();

    // Clear globe pins
    if (_mapProvider != null && _mapProvider.IsReady)
    {
        await _mapProvider.ClearPinsAsync();
    }

    if (result.Predictions != null)
    {
        for (int i = 0; i < result.Predictions.Count; i++)
        {
            var pred = result.Predictions[i];

            var prediction = new EnhancedLocationPrediction
            {
                Rank = i + 1,
                Latitude = pred.Latitude,
                Longitude = pred.Longitude,
                Probability = pred.Probability,
                City = pred.City ?? "",
                State = pred.State ?? "",
                County = pred.County ?? "",
                Country = pred.Country ?? "",
                LocationSummary = BuildLocationSummary(pred),
                IsPartOfCluster = false,
                ConfidenceLevel = ClassifyConfidence(pred.Probability)
            };

            Predictions.Add(prediction);

            // Add pin to globe
            if (_mapProvider != null && _mapProvider.IsReady)
            {
                await _mapProvider.AddPinAsync(
                    prediction.Latitude,
                    prediction.Longitude,
                    prediction.LocationSummary,
                    prediction.Probability,
                    prediction.Rank,
                    isExif: false
                );
            }
        }

        // Rotate to first prediction
        if (Predictions.Count > 0 && _mapProvider != null && _mapProvider.IsReady)
        {
            var first = Predictions[0];
            await _mapProvider.RotateToLocationAsync(first.Latitude, first.Longitude, 1500);
        }

        ReliabilityMessage = $"Showing {Predictions.Count} predictions";
    }
}
```

---

## Downloading Required Assets

### PowerShell Script to Download Assets

Create `Scripts/DownloadGlobeAssets.ps1`:

```powershell
# Download required assets for offline globe
$ErrorActionPreference = "Stop"

$assetsDir = ".\Assets\Globe"
$libDir = "$assetsDir\lib"
$texturesDir = "$assetsDir\textures"

# Create directories
New-Item -ItemType Directory -Force -Path $libDir | Out-Null
New-Item -ItemType Directory -Force -Path $texturesDir | Out-Null

Write-Host "Downloading Three.js..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://unpkg.com/three@0.150.0/build/three.min.js" `
    -OutFile "$libDir\three.min.js"
Write-Host "  ✓ Three.js downloaded (593 KB)" -ForegroundColor Green

Write-Host "Downloading Globe.GL..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://unpkg.com/globe.gl@2.24.0/dist/globe.gl.min.js" `
    -OutFile "$libDir\globe.gl.min.js"
Write-Host "  ✓ Globe.GL downloaded (89 KB)" -ForegroundColor Green

Write-Host "Downloading NASA Black Marble texture..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://unpkg.com/three-globe@2.24.0/example/img/earth-night.jpg" `
    -OutFile "$texturesDir\earth-night.jpg"
Write-Host "  ✓ Earth texture downloaded (4.2 MB)" -ForegroundColor Green

Write-Host "Downloading night sky background..." -ForegroundColor Yellow
Invoke-WebRequest -Uri "https://unpkg.com/three-globe@2.24.0/example/img/night-sky.png" `
    -OutFile "$texturesDir\night-sky.png"
Write-Host "  ✓ Night sky downloaded (1.8 MB)" -ForegroundColor Green

Write-Host ""
Write-Host "All assets downloaded successfully!" -ForegroundColor Cyan
Write-Host "Total size: ~6.7 MB" -ForegroundColor Gray
```

Run it:
```powershell
.\Scripts\DownloadGlobeAssets.ps1
```

---

## Pin Color System

Pins are colored based on confidence level:

| Confidence | Level | Color | Hex |
|------------|-------|-------|-----|
| **EXIF GPS** | Very High | Cyan | `#00ffff` |
| **≥ 0.85** | High | Cyan-Green | `#00ff88` |
| **0.50-0.85** | Medium | Yellow | `#ffdd00` |
| **< 0.50** | Low | Red | `#ff6666` |

Pin sizes:
- **Rank 1**: 1.0 (largest)
- **Rank 2-3**: 0.7 (medium)
- **Rank 4+**: 0.5 (small)

---

## Testing Checklist

### Online Mode Testing

- [ ] Open app with internet connection
- [ ] Verify globe loads from CDN
- [ ] Check browser console for "Globe initialized successfully"
- [ ] Add images and process
- [ ] Verify pins appear on globe
- [ ] Test rotation to first prediction
- [ ] Check pin colors match confidence levels

### Offline Mode Testing

- [ ] Disconnect internet
- [ ] Open app
- [ ] Verify globe loads from local assets
- [ ] Process images
- [ ] Verify pins still work
- [ ] Check fallback worked (check Debug output)

### Error Handling

- [ ] Start app without WebView2 runtime → Show error
- [ ] Delete Assets/Globe folder → Show error message
- [ ] Test with corrupted HTML file → Graceful failure

---

## Troubleshooting

### Issue: "Globe HTML not found"

**Cause**: Assets/Globe/globe_dark.html not copied to output directory

**Solution**:
```bash
# Check if file exists
ls bin/Debug/net9.0-windows10.0.19041.0/Assets/Globe/

# Rebuild project
dotnet clean
dotnet build
```

### Issue: "Failed to load globe libraries"

**Cause**: Internet connection failed and local assets not downloaded

**Solution**:
```powershell
# Download offline assets
.\Scripts\DownloadGlobeAssets.ps1

# Rebuild
dotnet build
```

### Issue: Globe shows black screen

**Cause**: WebView2 runtime not installed or JavaScript error

**Solution**:
1. Install WebView2 runtime: https://developer.microsoft.com/microsoft-edge/webview2/
2. Check Debug output for JavaScript errors
3. Enable DevTools in WebView2 settings (`AreDevToolsEnabled = true`)

---

## Future Enhancements (Post-MVP)

1. **Heatmap Visualization** - Multi-image probability heatmap
2. **Pin Clustering** - Group nearby pins when zoomed out
3. **Custom Textures** - User-selectable globe themes
4. **Offline Map Tiles** - Pre-bundled MBTiles for 100% offline
5. **Animation Controls** - Play/pause auto-rotation
6. **Pin Details Panel** - Click pin to show details in UI

---

This completes the Phase 4 implementation guide. Follow the integration steps above to wire the globe into MainPage.
