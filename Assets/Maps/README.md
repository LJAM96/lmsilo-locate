# GeoLens Maps - Hybrid Tile System

## 🗺️ Overview

GeoLens uses a **hybrid tile system** that combines the best of both worlds:

- **Online tiles** (zoom 0-19): High-quality CartoDB Dark Matter tiles streamed when internet is available
- **Offline tiles** (zoom 0-8): Minimal bundled tiles as fallback when offline

### How It Works

1. **By default**: Map loads online tiles (high quality, all zoom levels)
2. **When offline**: Automatically falls back to bundled tiles (zoom 0-8)
3. **Visual indicator**: Orange badge appears when using offline tiles

## 📦 What's Included

### Out of the Box (Without Setup)
- ✅ Online mode works immediately
- ✅ Hybrid mode infrastructure ready
- ⚠️ Offline tiles not included (need to download)

### After Running Setup
- ✅ Minimal offline tiles (zoom 0-8, ~100-500MB)
- ✅ Works completely offline for world/country/region view
- ✅ Seamlessly switches to online for city/street detail

## 🚀 Setup Instructions

### Option 1: Quick Setup (Recommended)

Run the download script from project root:

```bash
# Download minimal offline tiles (zoom 0-8, ~100-500MB)
python scripts/download_offline_tiles.py

# Or with Python3
python3 scripts/download_offline_tiles.py
```

**Estimated time**: 5-15 minutes (depending on connection speed)

**Result**: Creates `Assets/Maps/tiles/` directory with offline tiles

### Option 2: Custom Setup

Download more detail for larger offline coverage:

```bash
# More detail (zoom 0-10, ~1-2GB)
python scripts/download_offline_tiles.py --max-zoom 10

# Maximum detail (zoom 0-12, ~5-10GB)
python scripts/download_offline_tiles.py --max-zoom 12
```

### Option 3: Manual Download

Use MOBAC, TileMill, or other tile downloading tools to create tiles in this structure:

```
Assets/Maps/tiles/
├── 0/0/0.png
├── 1/0/0.png, 1/0/1.png, 1/1/0.png, 1/1/1.png
├── 2/...
└── 8/...
```

## 📊 Zoom Level Reference

| Zoom | Coverage | Tiles | Size | Use Case |
|------|----------|-------|------|----------|
| 0-2  | World/Continent | 21 | <1MB | Global overview |
| 0-5  | Country/State | 1,365 | ~20MB | Country-level |
| 0-8  | Region/City | 87,381 | ~100-500MB | **Default** |
| 0-10 | Neighborhood | ~1.4M | ~1-2GB | Detailed offline |
| 0-12 | Street | ~22M | ~5-10GB | Maximum offline |

## 🎯 Distribution Strategy

### For Development
Keep using online mode (current default). No tiles needed.

### For Production Installer

**Option A: Minimal Bundle (Recommended)**
- Include zoom 0-8 tiles (~100-500MB)
- Installer size: ~150-600MB
- Works offline for 90% of use cases

**Option B: Standard Bundle**
- Include zoom 0-10 tiles (~1-2GB)
- Installer size: ~1-2GB
- Works offline for street-level detail

**Option C: No Bundle (Smallest)**
- Don't include tiles
- Installer size: ~50MB
- Requires internet (online mode only)

## 🔧 Modes Explained

### Hybrid Mode (Default)
```javascript
mode: 'hybrid'
```
- Tries online tiles first
- Falls back to offline when network fails
- Orange indicator shows when offline
- **Recommended for production**

### Online Mode
```javascript
mode: 'online'
```
- Only uses online tiles
- No fallback
- Smallest installer
- Requires internet

### Offline Mode
```javascript
mode: 'offline'
```
- Only uses bundled tiles
- No online streaming
- Limited to bundled zoom levels
- For air-gapped systems

## 📝 Verification

After running the download script, verify tiles exist:

```bash
# Check if tiles directory was created
ls -la Assets/Maps/tiles/0/0/0.png

# Should see: 0.png file exists

# Check tile count
find Assets/Maps/tiles -name "*.png" | wc -l

# Should see: ~87,381 tiles for zoom 0-8
```

## 🐛 Troubleshooting

### "No tiles directory found"
**Solution**: Run `python scripts/download_offline_tiles.py`

### "Map shows gray squares offline"
**Cause**: Tiles not downloaded or wrong path
**Solution**: Verify tiles exist in `Assets/Maps/tiles/`

### "Orange indicator never appears"
**Cause**: Still connected to internet (hybrid mode works!)
**Solution**: Disable network to test offline mode

### "Download script fails"
**Cause**: Network issues or rate limiting
**Solution**: Run script again, it resumes automatically

## 📈 Performance

### Load Times
- **Online tiles**: ~100-300ms per tile
- **Offline tiles**: ~10-50ms per tile (10x faster!)
- **Hybrid mode**: Best of both (online when available, offline when not)

### Memory Usage
- **Tile cache**: ~50-100MB
- **Total**: ~150-200MB with tiles loaded

### Network Usage
- **Online only**: ~1-5MB per session
- **Hybrid mode**: <1MB per session (only higher zooms)
- **Offline mode**: 0MB (no network)

## 🌍 Coverage

### Included in Minimal Bundle (Zoom 0-8)
- ✅ World overview
- ✅ Continents
- ✅ Countries
- ✅ States/Provinces
- ✅ Large cities
- ⚠️ Street-level details require online

### Available Online (Zoom 9-19)
- City streets
- Neighborhoods
- Buildings
- Parks and landmarks

## 🔄 Updating Tiles

Tiles don't change frequently, but to update:

```bash
# Re-download all tiles
python scripts/download_offline_tiles.py --no-skip-existing

# Or just delete and re-download
rm -rf Assets/Maps/tiles
python scripts/download_offline_tiles.py
```

## 📄 License

Tiles are from CartoDB Dark Matter / OpenStreetMap:
- Map data: © OpenStreetMap contributors (ODbL)
- Map tiles: © CARTO (CC BY 3.0)

Attribution is automatically included in the map interface.

## 💡 Tips

1. **For most users**: Run the default script (zoom 0-8)
2. **For demos**: Use online mode (no setup)
3. **For enterprise**: Bundle zoom 0-10 with installer
4. **For air-gapped**: Download zoom 0-12 and use offline mode
5. **To save bandwidth**: Bundle tiles with installer, reduce online streaming

---

**Need help?** See `Docs/14_Offline_Maps_Guide.md` for detailed setup instructions.
