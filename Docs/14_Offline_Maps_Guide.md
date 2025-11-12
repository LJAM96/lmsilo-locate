# Offline Maps Guide for GeoLens

## Overview

GeoLens uses Leaflet.js with **dark mode tile layers** for map visualization. The map can work in two modes:

1. **Online Mode** (Default) - Uses free CartoDB Dark Matter tiles from the internet
2. **Offline Mode** - Uses locally bundled map tiles (requires setup)

---

## Current Status: Online by Default

**Does it work offline?**
- ✅ **Infrastructure is ready** - Code supports offline tiles
- ⚠️ **Tiles not bundled** - You need to download/generate tiles
- 🌐 **Default mode** - Currently requires internet for tiles

---

## Quick Answer

**Online Mode (Current)**
```csharp
// MainPage.xaml.cs line 102
_mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: false);
```
- ✅ Works immediately
- ✅ No setup required
- ✅ High quality dark tiles
- ❌ Requires internet connection

**Offline Mode (Requires Setup)**
```csharp
// MainPage.xaml.cs line 102
_mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: true);
```
- ✅ Works without internet
- ✅ Perfect for air-gapped systems
- ❌ Requires downloading ~5-50GB of tiles
- ❌ Requires tile generation tools

---

## Setting Up Offline Maps

### Step 1: Choose a Tile Provider

#### **Option A: CartoDB Dark Matter (Recommended)**
- **Source**: https://carto.com/attributions
- **Style**: Dark mode (matches GeoLens theme)
- **License**: Free with attribution
- **Quality**: High

#### **Option B: OpenStreetMap Standard**
- **Source**: https://www.openstreetmap.org/
- **Style**: Light mode (would need CSS inversion)
- **License**: ODbL
- **Quality**: High

#### **Option C: Custom Tiles**
- Generate your own from offline mapping tools
- Full control over styling
- Can create true dark mode tiles

---

### Step 2: Download Tiles

#### **Method 1: Using MOBAC (Mobile Atlas Creator)**

**Download MOBAC:**
```bash
# Download from: https://mobac.sourceforge.io/
# Or use wget:
wget https://sourceforge.net/projects/mobac/files/latest/download -O MOBAC.zip
unzip MOBAC.zip
```

**Configure MOBAC for CartoDB Dark:**
1. Open MOBAC
2. Go to Settings → Map Sources
3. Add custom source:
```xml
<customMapSource>
    <name>CartoDB Dark Matter</name>
    <minZoom>0</minZoom>
    <maxZoom>19</maxZoom>
    <tileType>PNG</tileType>
    <url>https://{$serverpart}.basemaps.cartocdn.com/dark_all/{$z}/{$x}/{$y}.png</url>
    <serverParts>a b c d</serverParts>
</customMapSource>
```

**Download tiles:**
1. Select region (draw box on map)
2. Choose zoom levels (recommend 0-12 for world, 0-16 for cities)
3. Select output format: "Tile Store" → "PNG files"
4. Click "Add Selection" → "Create Atlas"

**Size estimates:**
- Zoom 0-8: ~500MB (world overview)
- Zoom 0-12: ~5GB (city-level detail)
- Zoom 0-16: ~50GB (street-level detail)

#### **Method 2: Using TileMill (For Custom Styling)**

**Install TileMill:**
```bash
# Ubuntu/Debian:
sudo apt-get install nodejs npm
npm install -g @mapbox/tilemill

# macOS:
brew install tilemill

# Windows:
# Download from: https://github.com/tilemill-project/tilemill/releases
```

**Create custom dark tiles:**
1. Import OSM data
2. Apply dark stylesheet (CartoCSS)
3. Export to MBTiles format
4. Extract PNG tiles

#### **Method 3: Using TileDownloader Scripts**

**Python script example:**
```python
#!/usr/bin/env python3
import os
import requests
from pathlib import Path

def download_tiles(min_zoom, max_zoom, bbox):
    """Download tiles for offline use"""
    base_url = "https://a.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png"
    output_dir = Path("Assets/Maps/tiles")

    for z in range(min_zoom, max_zoom + 1):
        # Calculate tile coordinates from bbox
        # (implement lat/lon to tile conversion)
        for x in tile_x_range:
            for y in tile_y_range:
                tile_dir = output_dir / str(z) / str(x)
                tile_dir.mkdir(parents=True, exist_ok=True)

                tile_path = tile_dir / f"{y}.png"
                if not tile_path.exists():
                    url = base_url.format(z=z, x=x, y=y)
                    response = requests.get(url)
                    if response.status_code == 200:
                        with open(tile_path, 'wb') as f:
                            f.write(response.content)
                        print(f"Downloaded: {z}/{x}/{y}")

# Example: Download world overview (zoom 0-8)
download_tiles(0, 8, bbox=(-180, -85, 180, 85))
```

---

### Step 3: Organize Tiles in Project

**Required directory structure:**
```
GeoLens/
└── Assets/
    └── Maps/
        ├── leaflet_dark.html (already exists)
        └── tiles/
            ├── 0/
            │   └── 0/
            │       └── 0.png
            ├── 1/
            │   ├── 0/
            │   │   ├── 0.png
            │   │   └── 1.png
            │   └── 1/
            │       ├── 0.png
            │       └── 1.png
            ├── 2/
            │   └── ... (4x4 tiles)
            └── ... (up to zoom level 16-18)
```

**Create the directory:**
```bash
cd /home/user/geolens
mkdir -p Assets/Maps/tiles
```

**Copy your downloaded tiles:**
```bash
# If using MOBAC output:
cp -r /path/to/mobac/output/* Assets/Maps/tiles/

# Verify structure:
ls -la Assets/Maps/tiles/0/0/0.png  # Should exist
```

---

### Step 4: Enable Offline Mode in Code

**Edit MainPage.xaml.cs (line 102):**
```csharp
// Change from:
_mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: false);

// To:
_mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: true);
```

**Build and test:**
```bash
dotnet build
dotnet run --project GeoLens.csproj
```

**Disconnect from internet and verify:**
- Map should load tiles from local disk
- No network requests for tiles
- May see console message: "Loading offline tiles"

---

## Hybrid Mode (Recommended for Most Users)

**Best of both worlds:**
```csharp
// Auto-detect based on network connectivity
bool isOffline = !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
_mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: isOffline);
```

**Fallback strategy:**
```csharp
// Try online first, fallback to offline if network fails
_mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: false);
await _mapProvider.InitializeAsync();

// If initialization fails due to network, retry offline:
if (!_mapProvider.IsReady)
{
    _mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: true);
    await _mapProvider.InitializeAsync();
}
```

---

## Alternative: Using MBTiles Format

**MBTiles is a single-file tile database (SQLite)**

### Advantages:
- Single file instead of thousands of PNGs
- Faster disk I/O
- Easier distribution
- Smaller compressed size

### Setup:

**1. Generate MBTiles:**
```bash
# Using MOBAC: Select "MBTiles SQLite" as output format
# Or use mb-util to convert PNG tiles:
pip install mbutil
mb-util --image_format=png tiles/ world-dark.mbtiles
```

**2. Update HTML to use MBTiles:**

Edit `Assets/Maps/leaflet_dark.html` line 139:
```javascript
// Add MBTiles plugin
<script src="https://unpkg.com/leaflet.mbtiles@latest/dist/Leaflet.MBTiles.js"></script>

// In initMap function, replace offline tile provider:
if (config.mode === 'offline') {
    L.tileLayer.mbTiles('./world-dark.mbtiles', {
        attribution: 'Offline Map Data',
        maxZoom: 18
    }).addTo(map);
}
```

**3. Copy MBTiles file:**
```bash
cp world-dark.mbtiles Assets/Maps/
```

---

## Testing Offline Mode

### Verify Tile Loading

**Check browser console (F12):**
```javascript
// Should see:
[LeafletMap] Loading offline tiles from: ./tiles/
// Not:
[LeafletMap] Fetching tiles from: https://...
```

**Monitor network tab:**
- Should see NO requests to `cartocdn.com` or `stadiamaps.com`
- Should see local file:// requests

### Test Scenarios

**Scenario 1: Full offline (no internet)**
```bash
# Linux: Disable network
sudo ifconfig eth0 down

# Windows: Disable network adapter
# Then run app and verify map loads
```

**Scenario 2: Firewall blocking**
```bash
# Block tile servers in hosts file
echo "0.0.0.0 a.basemaps.cartocdn.com" >> /etc/hosts
echo "0.0.0.0 tiles.stadiamaps.com" >> /etc/hosts
```

**Scenario 3: Slow network**
```bash
# Simulate slow network, offline should be instant
# Use network throttling in browser DevTools
```

---

## Disk Space Considerations

### Recommended Zoom Levels

**Minimal (1-2 GB):**
- Zoom 0-10: Good for country/state level
- Use case: Quick geolocation overview

**Standard (5-10 GB):**
- Zoom 0-12: City-level detail
- Use case: Most GeoLens users

**Detailed (20-50 GB):**
- Zoom 0-14: Street-level detail
- Use case: Precision analysis

**Maximum (100+ GB):**
- Zoom 0-16+: Building-level detail
- Use case: Professional/enterprise

### Compression

**Compress tiles for distribution:**
```bash
# Using 7zip
7z a -t7z -m0=lzma -mx=9 geolens-tiles.7z Assets/Maps/tiles/

# Typical compression: 30-50% size reduction
# Example: 10GB → 5GB compressed
```

---

## Distribution Strategy

### For Installers

**Option 1: Separate download**
- Ship app without tiles (~50MB)
- Provide tile download link
- User downloads tiles post-install

**Option 2: Full bundle**
- Ship app with tiles (~1-10GB depending on zoom)
- Single installer, works offline immediately
- Large download size

**Option 3: Installer variants**
```
geolens-setup-lite.exe    (50MB - online only)
geolens-setup-standard.exe (2GB - includes zoom 0-10)
geolens-setup-full.exe     (10GB - includes zoom 0-14)
```

---

## Updating Tiles

**Tiles don't change frequently, but to update:**

```bash
# Download new tiles (monthly or quarterly)
python download_tiles.py --update

# Or use MOBAC with "resume download" option

# Replace old tiles
rm -rf Assets/Maps/tiles/*
cp -r new-tiles/* Assets/Maps/tiles/
```

**Automated update script:**
```csharp
// In app, check for tile updates
public async Task CheckForTileUpdatesAsync()
{
    var updateUrl = "https://yourdomain.com/tiles/version.json";
    // Download if newer version available
}
```

---

## Troubleshooting

### "Map shows gray tiles"
**Cause:** Tiles not found or wrong path
**Fix:**
1. Check `Assets/Maps/tiles/0/0/0.png` exists
2. Verify .csproj includes tiles in output
3. Check console for 404 errors

### "Map loads slowly offline"
**Cause:** Too many small files (PNG tiles)
**Fix:** Convert to MBTiles format (single file)

### "Tiles don't match dark theme"
**Cause:** Using light tiles instead of dark
**Fix:** Download CartoDB Dark Matter or Stadia Dark tiles

### "Missing tiles at certain zoom levels"
**Cause:** Partial download or zoom level limit
**Fix:** Download tiles for all required zoom levels

---

## Current Recommendation

**For Development:**
Keep using online mode (current default) - fastest to work with

**For Production/Enterprise:**
Bundle offline tiles with installer
- Download zoom 0-12 (~5GB)
- Use MBTiles format
- Enable offline mode in code

**For General Users:**
Provide both options:
- Default: Online (free, always up to date)
- Optional: Download offline pack

---

## Summary

| Feature | Online Mode | Offline Mode |
|---------|-------------|--------------|
| **Setup Time** | 0 minutes | 1-4 hours |
| **Internet Required** | Yes | No |
| **Disk Space** | ~10MB | 1-50GB |
| **Performance** | Good | Excellent |
| **Tile Quality** | High | High |
| **Updates** | Automatic | Manual |
| **Best For** | Development, Demos | Production, Air-gapped |

**Current Status:** ✅ Online mode ready to use now
**Offline Status:** 🔧 Infrastructure ready, tiles need setup
