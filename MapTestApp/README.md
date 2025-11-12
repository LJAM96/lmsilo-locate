# GeoLens Map Comparison Tool

A standalone test application to compare different map implementations for GeoLens.

## What This Does

Shows 5 different map options side-by-side in tabs so you can see exactly what each looks like before implementing in GeoLens.

## How to Run

```bash
cd MapTestApp
dotnet restore
dotnet run
```

## What You'll See

### Tab 1: Globe.GL + NASA (51MB)
- 3D spinning globe
- NASA Black Marble satellite imagery
- Beautiful but NO labels or street names
- Best for: Quick visual fix, satellite view lovers

### Tab 2: Leaflet 2D (500MB) ⭐ RECOMMENDED
- Traditional 2D street map
- CartoDB Dark Matter style
- City/country labels visible
- Roads and streets shown
- Best for: Most users, practical geolocation tool

### Tab 3: MapLibre 2D (Vector)
- 2D map with vector tiles
- Infinite zoom quality
- All labels and streets
- Best for: Highest quality 2D

### Tab 4: MapLibre 3D Globe
- 3D globe with vector tiles
- Streets wrapped on sphere
- Can toggle 2D/3D
- Best for: Want both options

### Tab 5: Cesium 3D (150MB)
- True 3D globe with terrain
- NASA-grade visualization
- Professional satellite view
- Best for: Scientific feel, 3D lovers

## Test Features

Each map shows 5 sample location pins:
- Paris (red/green based on confidence)
- London
- New York
- Tokyo
- Sydney

Click tabs to switch between implementations. Each loads independently.

## Notes

- All maps use online CDN resources for demo (no local bundles)
- In production, you'd bundle tiles/libraries locally
- File sizes shown are approximate for offline bundles
- This lets you see visual quality before committing

## Making Your Choice

After testing, consider:
1. **Visual preference** - Which looks best to you?
2. **Labels needed?** - Do you need street/city names?
3. **2D vs 3D** - Which feels more usable?
4. **File size** - Is 500MB acceptable for better features?

**My recommendation after testing**: Leaflet 2D (Tab 2)
- Clear labels help users understand predictions
- Familiar map interface
- 500MB is reasonable for desktop app
- Professional appearance

## Next Steps

Once you choose, I can implement that option in the main GeoLens app.
