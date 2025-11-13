# Offline Map Tiles

## Overview

GeoLens uses a **hybrid online/offline map system**:
- **Online (default)**: Streams high-quality tiles (zoom 0-19) from CartoDB Dark Matter
- **Offline fallback**: Uses bundled low-resolution tiles (zoom 0-5) when network is unavailable
- **Automatic switching**: App detects connection issues and seamlessly falls back to offline tiles

## Directory Structure

```
Assets/Maps/
├── leaflet_dark.html         # Leaflet map with hybrid tile support
├── tiles/                     # Offline tile cache (gitignored)
│   ├── 0/                     # Zoom level 0 (1 tile, world view)
│   ├── 1/                     # Zoom level 1 (4 tiles)
│   ├── 2/                     # Zoom level 2 (16 tiles)
│   ├── 3/                     # Zoom level 3 (64 tiles)
│   ├── 4/                     # Zoom level 4 (256 tiles)
│   └── 5/                     # Zoom level 5 (1,024 tiles)
└── OFFLINE_TILES_README.md    # This file
```

## For Development

### Download Tiles Locally

```powershell
cd scripts
.\download_minimal_tiles.ps1
```

This downloads ~2,700 tiles (~50-80 MB) for offline fallback during development.

**Options:**
- `-MaxZoom 5` (default): Download zoom 0-5 (recommended)
- `-MaxZoom 8`: Download zoom 0-8 (~22,000 tiles, ~200 MB, better quality)
- `-OutputDir`: Custom output directory

### Testing Offline Mode

1. Download tiles using the script above
2. Run the app
3. Disconnect from the internet or block network access
4. The app will automatically use offline tiles and show an indicator

## For Distribution

### Bundling Tiles with Installer

When creating the installer, bundle minimal tiles for offline use:

1. **Download tiles** (one time, before building installer):
   ```powershell
   .\scripts\download_minimal_tiles.ps1 -MaxZoom 5
   ```

2. **Include in installer**:
   - Add `Assets/Maps/tiles/` to installer file list
   - Total size: ~50-80 MB (zoom 0-5)
   - Optional: Use higher zoom (0-8) for ~200 MB

3. **Installer structure**:
   ```
   C:\Program Files\GeoLens\
   ├── GeoLens.exe
   ├── Assets\
   │   └── Maps\
   │       ├── leaflet_dark.html
   │       └── tiles\          # Bundled offline tiles
   │           ├── 0\
   │           ├── 1\
   │           ...
   ```

## How It Works

### Tile Loading Strategy

```javascript
// leaflet_dark.html configuration
const config = {
    mode: 'hybrid',             // Default mode
    offlineFallbackZoom: 8      // Max zoom for offline tiles
};
```

1. **Try online first**: Fetch tile from CartoDB Dark Matter
2. **On error**: Check if zoom ≤ offlineFallbackZoom
3. **Fallback to offline**: Load from `./tiles/{z}/{x}/{y}.png`
4. **Show indicator**: Display "Using Offline Tiles" badge

### User Experience

| Scenario | Behavior |
|----------|----------|
| **Online** | High-quality tiles (zoom 0-19), smooth experience |
| **Offline** | Low-res tiles (zoom 0-5), visible "Offline" indicator |
| **Intermittent** | Seamless switching between online/offline |
| **No tiles** | Grey background, map controls still work, pins visible |

## Tile Providers

### Online (Primary)

- **Provider**: CartoDB Dark Matter
- **URL**: `https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png`
- **License**: Free (with attribution), no API key required
- **Zoom**: 0-19
- **Style**: Dark theme matching GeoLens UI

### Offline (Fallback)

- **Source**: Pre-downloaded CartoDB tiles
- **Zoom**: 0-5 (configurable up to 8)
- **Size**: ~50-80 MB (zoom 0-5), ~200 MB (zoom 0-8)
- **Update**: Bundled with installer, no auto-update

## Customization

### Change Tile Provider

Edit `Assets/Maps/leaflet_dark.html`:

```javascript
const tileProviders = {
    online: {
        url: 'https://your-tile-server/{z}/{x}/{y}.png',
        attribution: 'Your attribution',
        maxZoom: 19
    },
    offline: {
        url: './tiles/{z}/{x}/{y}.png',
        maxZoom: 5
    }
};
```

### Adjust Offline Zoom Levels

```javascript
const config = {
    offlineFallbackZoom: 8  // Change from 5 to 8 for better offline quality
};
```

**Trade-off**:
- Zoom 0-5: ~2,700 tiles, ~50-80 MB
- Zoom 0-6: ~5,400 tiles, ~100 MB
- Zoom 0-7: ~10,900 tiles, ~150 MB
- Zoom 0-8: ~21,800 tiles, ~200 MB

### Force Offline Mode

For air-gapped environments, edit `leaflet_dark.html`:

```javascript
const config = {
    mode: 'offline',  // Forces offline-only mode
    ...
};
```

## Performance

### Load Times

| Scenario | Initial Load | Tile Load (avg) |
|----------|--------------|-----------------|
| **Online** | ~500ms | 50-100ms/tile |
| **Offline** | ~200ms | 5-10ms/tile |
| **Hybrid** | ~500ms | 50-100ms (online) → 5-10ms (fallback) |

### Caching

- **Browser cache**: Tiles cached in WebView2 (Chrome cache)
- **Lifetime**: Until browser cache cleared
- **Bundled tiles**: Never cleared, always available

## Troubleshooting

### Tiles Not Loading Offline

1. **Check tile directory**:
   ```powershell
   ls Assets/Maps/tiles/
   ```
   Should see folders: 0, 1, 2, 3, 4, 5

2. **Verify tile files**:
   ```powershell
   ls Assets/Maps/tiles/0/0/
   ```
   Should see `0.png`

3. **Check permissions**: Ensure app can read from tiles directory

### "Using Offline Tiles" Indicator Always Visible

- **Cause**: Network issues or tile server blocked
- **Fix**: Check internet connection, firewall, or proxy settings

### Grey Tiles at High Zoom

- **Expected**: Offline tiles only cover zoom 0-5
- **Solution**: Zoom out or wait for network connection
- **Alternative**: Download more tiles (zoom 0-8) for better coverage

## Maintenance

### Update Tiles

Offline tiles don't auto-update. To refresh:

1. Delete old tiles:
   ```powershell
   rm -r Assets/Maps/tiles/*
   ```

2. Re-download:
   ```powershell
   .\scripts\download_minimal_tiles.ps1
   ```

3. Rebuild installer with new tiles

### Tile Server Changes

If CartoDB changes URLs or shuts down:

1. Update `tileProviders.online.url` in `leaflet_dark.html`
2. Download new tiles from alternate provider
3. Test both online and offline modes

## License & Attribution

### CartoDB Tiles

- **License**: Free for open-source and commercial use
- **Attribution**: Required (auto-included in map)
- **Terms**: https://carto.com/attributions

### OpenStreetMap Data

- **License**: ODbL (Open Database License)
- **Attribution**: Required (auto-included in map)
- **Terms**: https://www.openstreetmap.org/copyright

## Further Reading

- **Leaflet Documentation**: https://leafletjs.com/reference.html
- **CartoDB Tile Service**: https://carto.com/basemaps/
- **OSM Tile Usage Policy**: https://operations.osmfoundation.org/policies/tiles/

---

**Last Updated**: 2025-01-12
**Version**: 1.0
