# Dark Mode Map Visualization Guide

## Overview

All map visualizations in GeoLens use dark themes to maintain interface consistency. This document covers implementation details for each visualization mode.

---

## Color Palette

### Background Colors
- **App Background**: `#0a0a0a` (RGB: 10, 10, 10)
- **Map Background**: `#0a0a0a` to `#1a1a1a`
- **Atmosphere Glow**: `#1a1a2e` (Dark blue)
- **Card Backgrounds**: `#1e1e1e`

### Pin Colors (Enhanced for Dark Mode)
| Confidence Level | Color | Hex | RGB |
|-----------------|-------|-----|-----|
| Very High (EXIF) | Cyan | `#00ffff` | 0, 255, 255 |
| High (0.85+) | Cyan-Green | `#00ff88` | 0, 255, 136 |
| Medium (0.50-0.85) | Bright Yellow | `#ffdd00` | 255, 221, 0 |
| Low (<0.50) | Bright Red | `#ff6666` | 255, 102, 102 |

### Heatmap Gradient
- **0-20%**: Dark Blue `rgba(0, 100, 255, 0.4)`
- **20-40%**: Cyan `rgba(0, 200, 255, 0.6)`
- **40-60%**: Green `rgba(0, 255, 150, 0.7)`
- **60-80%**: Yellow `rgba(255, 230, 0, 0.8)`
- **80-100%**: Red `rgba(255, 50, 50, 1.0)`

---

## 1. WebView2 Globe (Online) - Three.js

### Dark Globe HTML

```html
<!-- Assets/globe_dark.html -->
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <title>GeoLens Dark Globe</title>
    <script src="three.min.js"></script>
    <script src="globe.gl.min.js"></script>
    <style>
        body {
            margin: 0;
            background: #0a0a0a;
            overflow: hidden;
        }
        #globeViz {
            width: 100vw;
            height: 100vh;
        }
        .tooltip {
            background: rgba(20, 20, 20, 0.95);
            color: #ffffff;
            padding: 8px 12px;
            border-radius: 6px;
            font-family: 'Segoe UI', sans-serif;
            font-size: 12px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.5);
        }
    </style>
</head>
<body>
    <div id="globeViz"></div>
    <script>
        // Initialize dark globe
        const world = Globe()
            .globeImageUrl('earth-night.jpg')  // NASA Black Marble
            .backgroundImageUrl('night-sky.png')
            .atmosphereColor('#1a1a2e')
            .atmosphereAltitude(0.15)
            .showAtmosphere(true)
            (document.getElementById('globeViz'));

        // Disable auto-rotate by default
        world.controls().autoRotate = false;
        world.pointOfView({ lat: 0, lng: 0, altitude: 2.5 });

        // Pin management
        let currentPins = [];
        let currentHeatmap = null;

        function addPin(lat, lon, label, confidence, rank, isExif = false) {
            const color = isExif ? '#00ffff' : getConfidenceColor(confidence);
            const size = rank === 1 ? 0.8 : 0.5;

            currentPins.push({
                lat: lat,
                lng: lon,
                size: size,
                color: color,
                label: label,
                confidence: confidence,
                rank: rank,
                isExif: isExif
            });

            updatePins();

            // Add pulse ring for top prediction or EXIF
            if (rank === 1 || isExif) {
                addPulseRing(lat, lon, color);
            }

            // Auto-rotate to first pin
            if (rank === 1) {
                rotateToPin(lat, lon, 2000);
            }
        }

        function updatePins() {
            world.pointsData(currentPins)
                .pointAltitude(0.01)
                .pointRadius('size')
                .pointColor('color')
                .pointLabel(d => createTooltip(d));
        }

        function createTooltip(pin) {
            const confLabel = pin.isExif ? 'EXIF GPS' : `${(pin.confidence * 100).toFixed(1)}%`;
            return `
                <div class="tooltip" style="border: 2px solid ${pin.color}">
                    <div style="font-weight: bold; margin-bottom: 4px;">
                        ${pin.isExif ? '📍' : `Rank ${pin.rank}:`} ${pin.label}
                    </div>
                    <div style="font-size: 11px; opacity: 0.9;">
                        Confidence: ${confLabel}
                    </div>
                    <div style="font-size: 10px; opacity: 0.7; margin-top: 2px;">
                        ${pin.lat.toFixed(4)}°, ${pin.lng.toFixed(4)}°
                    </div>
                </div>
            `;
        }

        function addPulseRing(lat, lon, color) {
            const rings = world.ringsData();
            rings.push({
                lat: lat,
                lng: lon,
                maxR: 3,
                propagationSpeed: 2,
                repeatPeriod: 1000,
                color: color
            });

            world.ringsData(rings)
                .ringColor(d => `${d.color}80`)  // 50% opacity
                .ringMaxRadius('maxR')
                .ringPropagationSpeed('propagationSpeed')
                .ringRepeatPeriod('repeatPeriod');
        }

        function getConfidenceColor(confidence) {
            if (confidence >= 0.85) return '#00ff88';
            if (confidence >= 0.70) return '#00dd66';
            if (confidence >= 0.50) return '#ffdd00';
            return '#ff6666';
        }

        function rotateToPin(lat, lon, duration = 2000) {
            world.pointOfView({ lat: lat, lng: lon, altitude: 2.0 }, duration);
        }

        function clearPins() {
            currentPins = [];
            world.pointsData([]);
            world.ringsData([]);
        }

        function showHeatmap(heatmapData) {
            // Convert grid to hexBin data
            const hexData = [];
            const grid = heatmapData.grid;

            for (let x = 0; x < 360; x++) {
                for (let y = 0; y < 180; y++) {
                    const intensity = grid[x][y];
                    if (intensity > 0.1) {
                        hexData.push({
                            lat: 90 - y,
                            lng: x - 180,
                            weight: intensity
                        });
                    }
                }
            }

            // Hide pins, show heatmap
            world.pointsData([]);
            world.ringsData([]);

            world.hexBinPointsData(hexData)
                .hexBinPointWeight('weight')
                .hexAltitude(d => d.sumWeight * 0.05)
                .hexBinResolution(4)
                .hexTopColor(d => heatmapColorScale(d.sumWeight))
                .hexSideColor(d => heatmapColorScale(d.sumWeight))
                .hexBinMerge(true)
                .enablePointerInteraction(true)
                .hexLabel(d => `
                    <div class="tooltip">
                        <div style="font-weight: bold;">Hotspot</div>
                        <div>Intensity: ${(d.sumWeight * 100).toFixed(1)}%</div>
                    </div>
                `);

            currentHeatmap = hexData;
        }

        function heatmapColorScale(intensity) {
            if (intensity < 0.2) return `rgba(0, 100, 255, ${intensity * 2})`;
            if (intensity < 0.4) return `rgba(0, 200, 255, ${intensity * 2})`;
            if (intensity < 0.6) return `rgba(0, 255, 150, ${intensity})`;
            if (intensity < 0.8) return `rgba(255, 230, 0, ${intensity})`;
            return `rgba(255, 50, 50, ${Math.min(intensity, 1.0)})`;
        }

        function hideHeatmap() {
            world.hexBinPointsData([]);
            updatePins();
        }

        function setAutoRotate(enabled) {
            world.controls().autoRotate = enabled;
        }
    </script>
</body>
</html>
```

### C# Integration

```csharp
// Services/WebGlobe3DProvider.cs
public class WebGlobe3DProvider : IMapProvider
{
    private readonly WebView2 _webView;
    private bool _isInitialized;

    public WebGlobe3DProvider(WebView2 webView, bool darkMode = true)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _webView.EnsureCoreWebView2Async();

        var htmlPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "globe_dark.html");

        _webView.Source = new Uri(htmlPath);
        _isInitialized = true;

        // Wait for page load
        await Task.Delay(2000);
    }

    public async Task AddPinAsync(
        double lat, double lon, string label,
        double confidence, int rank, bool isExif = false)
    {
        var escapedLabel = label.Replace("'", "\\'");
        await _webView.ExecuteScriptAsync(
            $"addPin({lat}, {lon}, '{escapedLabel}', {confidence}, {rank}, {isExif.ToString().ToLower()})");
    }

    public async Task RotateToPinAsync(double lat, double lon, TimeSpan duration)
    {
        await _webView.ExecuteScriptAsync(
            $"rotateToPin({lat}, {lon}, {duration.TotalMilliseconds})");
    }

    public async Task ClearPinsAsync()
    {
        await _webView.ExecuteScriptAsync("clearPins()");
    }

    public async Task ShowHeatmapAsync(HeatmapData heatmapData)
    {
        // Serialize heatmap data to JSON
        var json = JsonSerializer.Serialize(new
        {
            grid = heatmapData.Grid,
            resolution = heatmapData.Resolution
        });

        await _webView.ExecuteScriptAsync($"showHeatmap({json})");
    }

    public void SetDarkMode(bool enabled)
    {
        // Always dark mode for GeoLens
    }
}
```

---

## 2. Win2D Dark Globe (Offline)

### Implementation

```csharp
// Services/Win2DGlobe3DProvider.cs
public class Win2DGlobe3DProvider : IMapProvider
{
    private readonly CanvasControl _canvas;
    private CanvasBitmap? _earthTexture;
    private readonly List<GlobePin> _pins = new();
    private readonly Compositor _compositor;

    public Win2DGlobe3DProvider(CanvasControl canvas, bool darkMode = true)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _compositor = CompositionTarget.GetCompositorForCurrentThread();
        _canvas.Draw += OnDraw;
    }

    public async Task InitializeAsync()
    {
        // Load NASA Black Marble texture
        var texturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Maps",
            "black_marble_8k.jpg");

        if (File.Exists(texturePath))
        {
            _earthTexture = await CanvasBitmap.LoadAsync(_canvas, texturePath);
        }
        else
        {
            // Fallback: generate dark sphere
            _earthTexture = GenerateDarkSphere(_canvas);
        }
    }

    public Task AddPinAsync(
        double lat, double lon, string label,
        double confidence, int rank, bool isExif = false)
    {
        var pin = new GlobePin
        {
            Latitude = lat,
            Longitude = lon,
            Label = label,
            Confidence = confidence,
            Rank = rank,
            IsExif = isExif,
            Color = GetPinColor(confidence, isExif),
            Size = rank == 1 || isExif ? 8f : 5f,
            PulsePhase = rank == 1 || isExif ? 0f : -1f
        };

        _pins.Add(pin);
        _canvas.Invalidate();

        return Task.CompletedTask;
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;

        // Clear to dark background
        ds.Clear(Color.FromArgb(255, 10, 10, 10));

        if (_earthTexture == null) return;

        // Draw Earth sphere
        DrawEarthSphere(ds);

        // Draw atmosphere glow
        DrawAtmosphereGlow(ds);

        // Draw pins
        foreach (var pin in _pins)
        {
            DrawPinWithGlow(ds, pin);
        }

        // Update pulse animations
        UpdatePulseAnimations();
    }

    private void DrawEarthSphere(CanvasDrawingSession ds)
    {
        var centerX = (float)_canvas.ActualWidth / 2;
        var centerY = (float)_canvas.ActualHeight / 2;
        var radius = Math.Min(centerX, centerY) * 0.7f;

        // Draw textured sphere
        var sphereRect = new Rect(
            centerX - radius,
            centerY - radius,
            radius * 2,
            radius * 2);

        using (var clipGeometry = CanvasGeometry.CreateCircle(
            _canvas, centerX, centerY, radius))
        {
            ds.DrawImage(_earthTexture, sphereRect);
        }
    }

    private void DrawAtmosphereGlow(CanvasDrawingSession ds)
    {
        var centerX = (float)_canvas.ActualWidth / 2;
        var centerY = (float)_canvas.ActualHeight / 2;
        var radius = Math.Min(centerX, centerY) * 0.7f;

        // Create radial gradient
        using (var brush = new CanvasRadialGradientBrush(
            _canvas,
            Color.FromArgb(80, 26, 26, 46),
            Color.FromArgb(0, 0, 0, 0)))
        {
            brush.Center = new Vector2(centerX, centerY);
            brush.RadiusX = radius * 1.15f;
            brush.RadiusY = radius * 1.15f;

            ds.FillCircle(centerX, centerY, radius * 1.15f, brush);
        }
    }

    private void DrawPinWithGlow(CanvasDrawingSession ds, GlobePin pin)
    {
        var centerX = (float)_canvas.ActualWidth / 2;
        var centerY = (float)_canvas.ActualHeight / 2;
        var radius = Math.Min(centerX, centerY) * 0.7f;

        // Convert lat/lon to screen coordinates
        var (x, y, visible) = LatLonToScreen(
            pin.Latitude, pin.Longitude, centerX, centerY, radius);

        if (!visible) return;

        // Draw multi-layer glow
        for (int i = 3; i > 0; i--)
        {
            var glowColor = Color.FromArgb(
                (byte)(40 / i),
                pin.Color.R,
                pin.Color.G,
                pin.Color.B);

            ds.FillCircle(x, y, pin.Size + (i * 6), glowColor);
        }

        // Main pin body
        ds.FillCircle(x, y, pin.Size, pin.Color);

        // White border
        ds.DrawCircle(x, y, pin.Size, Colors.White, 2);

        // Inner highlight
        ds.FillCircle(
            x - pin.Size / 3,
            y - pin.Size / 3,
            pin.Size / 3,
            Color.FromArgb(128, 255, 255, 255));

        // Pulse ring
        if (pin.PulsePhase >= 0)
        {
            var pulseRadius = pin.Size + (pin.PulsePhase * 20);
            var pulseAlpha = (byte)(100 * (1 - pin.PulsePhase));
            var pulseColor = Color.FromArgb(
                pulseAlpha, pin.Color.R, pin.Color.G, pin.Color.B);

            ds.DrawCircle(x, y, pulseRadius, pulseColor, 3);
        }
    }

    private (float x, float y, bool visible) LatLonToScreen(
        double lat, double lon, float centerX, float centerY, float radius)
    {
        // Simple orthographic projection (front hemisphere only)
        double latRad = lat * Math.PI / 180.0;
        double lonRad = lon * Math.PI / 180.0;

        // Rotate globe (simple example, no rotation yet)
        float x = centerX + (float)(Math.Cos(latRad) * Math.Sin(lonRad) * radius);
        float y = centerY - (float)(Math.Sin(latRad) * radius);

        // Check if point is on visible hemisphere
        bool visible = Math.Cos(latRad) * Math.Cos(lonRad) > 0;

        return (x, y, visible);
    }

    private void UpdatePulseAnimations()
    {
        bool needsRedraw = false;

        foreach (var pin in _pins.Where(p => p.PulsePhase >= 0))
        {
            pin.PulsePhase += 0.02f;
            if (pin.PulsePhase > 1.0f)
                pin.PulsePhase = 0f;

            needsRedraw = true;
        }

        if (needsRedraw)
        {
            _canvas.Invalidate();
        }
    }

    private Color GetPinColor(double confidence, bool isExif)
    {
        if (isExif)
            return Color.FromArgb(255, 0, 255, 255); // Cyan

        return confidence switch
        {
            >= 0.85 => Color.FromArgb(255, 0, 255, 136),  // Cyan-green
            >= 0.70 => Color.FromArgb(255, 80, 255, 120), // Green
            >= 0.50 => Color.FromArgb(255, 255, 230, 50), // Yellow
            _ => Color.FromArgb(255, 255, 120, 120)       // Red
        };
    }

    private CanvasBitmap GenerateDarkSphere(CanvasControl canvas)
    {
        // Generate simple dark gradient sphere as fallback
        var size = 2048;
        var renderTarget = new CanvasRenderTarget(
            canvas, size, size, 96);

        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Color.FromArgb(255, 20, 20, 30));

            // Draw gradient to simulate sphere
            using (var brush = new CanvasRadialGradientBrush(
                canvas,
                Color.FromArgb(255, 40, 40, 60),
                Color.FromArgb(255, 10, 10, 15)))
            {
                brush.Center = new Vector2(size / 2, size / 2);
                brush.RadiusX = size / 2;
                brush.RadiusY = size / 2;

                ds.FillCircle(size / 2, size / 2, size / 2, brush);
            }
        }

        return CanvasBitmap.CreateFromBytes(
            canvas,
            renderTarget.GetPixelBytes(),
            size, size,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            96);
    }

    public Task ClearPinsAsync()
    {
        _pins.Clear();
        _canvas.Invalidate();
        return Task.CompletedTask;
    }

    public Task ShowHeatmapAsync(HeatmapData heatmapData)
    {
        // TODO: Implement heatmap overlay for Win2D
        return Task.CompletedTask;
    }

    public void SetDarkMode(bool enabled)
    {
        // Always dark
    }

    public Task RotateToPinAsync(double lat, double lon, TimeSpan duration)
    {
        // TODO: Implement smooth rotation animation
        return Task.CompletedTask;
    }
}

public class GlobePin
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Label { get; set; } = "";
    public double Confidence { get; set; }
    public int Rank { get; set; }
    public bool IsExif { get; set; }
    public Color Color { get; set; }
    public float Size { get; set; }
    public float PulsePhase { get; set; } = -1f;
}
```

---

## 3. Dark Map Tiles (Offline 2D)

### Tile Sources (Open-Source)

1. **CartoDB Dark Matter**: https://cartodb-basemaps.global.ssl.fastly.net/dark_all/{z}/{x}/{y}.png
2. **OpenMapTiles Dark**: https://api.maptiler.com/maps/dark/{z}/{x}/{y}.png
3. **Custom Dark OSM**: Process OSM data with custom dark stylesheet

### Pre-bundling Tiles

```bash
# Download tiles for zoom levels 0-8 (world coverage)
# Using GDAL/ogr2ogr or MBUtil

# Example script
for z in {0..8}; do
    for x in {0..$((2**z-1))}; do
        for y in {0..$((2**z-1))}; do
            curl "https://cartodb-basemaps.global.ssl.fastly.net/dark_all/$z/$x/$y.png" \
                -o "tiles/$z/$x/$y.png"
        done
    done
done

# Convert to MBTiles format (SQLite)
mb-util tiles/ dark_world.mbtiles
```

### Accessing Bundled Tiles

```csharp
// Services/OfflineTileProvider.cs
public class OfflineTileProvider
{
    private readonly string _mbtilesPath;
    private SQLiteConnection? _connection;

    public OfflineTileProvider()
    {
        _mbtilesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Maps",
            "dark_world.mbtiles");
    }

    public async Task<byte[]?> GetTileAsync(int zoom, int x, int y)
    {
        _connection ??= new SQLiteConnection($"Data Source={_mbtilesPath};Read Only=True");

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT tile_data
            FROM tiles
            WHERE zoom_level = @z AND tile_column = @x AND tile_row = @y
        ";
        command.Parameters.AddWithValue("@z", zoom);
        command.Parameters.AddWithValue("@x", x);
        command.Parameters.AddWithValue("@y", y);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader["tile_data"] as byte[];
        }

        return null;
    }
}
```

---

## Asset Bundle Sizes

| Asset | Size (Uncompressed) | Size (LZMA2) |
|-------|---------------------|--------------|
| NASA Black Marble 8K | 45 MB | 12 MB |
| Dark Tiles (Zoom 0-8) | 500 MB | 180 MB |
| Night Sky Background | 2 MB | 0.5 MB |
| **Total** | **547 MB** | **~200 MB** |

---

## Performance Optimization

### Texture Loading
- Use mipmaps for efficient rendering at different zoom levels
- Load textures asynchronously on background thread
- Cache decoded bitmaps in memory

### Tile Caching
- Keep last 100 tiles in memory cache
- Use LRU eviction policy
- Lazy load tiles as user pans/zooms

### Rendering
- Use hardware acceleration (GPU compositing)
- Batch pin rendering
- Throttle redraw to 60 FPS

---

## Accessibility

### High Contrast Mode
- Increase pin border thickness to 4px
- Use solid colors instead of gradients
- Add text labels to all pins (optional toggle)

### Screen Reader Support
- Provide alt text for pins: "Prediction rank 1: Tokyo, Japan, confidence 85%"
- Announce heatmap intensity when hovering

---

## Testing Checklist

- [ ] Globe renders correctly on first load
- [ ] Pins are visible on dark background
- [ ] Colors match design specifications
- [ ] Pulse animations are smooth (60 FPS)
- [ ] Heatmap gradient is correct
- [ ] Tooltips are readable (dark background, light text)
- [ ] Works in offline mode with bundled assets
- [ ] No white flashes during loading
