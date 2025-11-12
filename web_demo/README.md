# GeoLens Map Library Comparison Demo

A comprehensive Flask web demo comparing **5 different mapping libraries** for the GeoLens WinUI3 application. See predictions from GeoCLIP visualized on each platform to make an informed technology choice.

## 🗺️ The 5 Map Libraries

### Tab 1: Globe.GL + NASA (51MB)
- WebGL-based 3D globe using NASA Blue Marble imagery
- Auto-rotating with smooth animations
- Smaller bundle size
- **Pros**: Beautiful visuals, good performance
- **Cons**: No offline tile caching

### Tab 2: Leaflet 2D (500MB) ⭐ **RECOMMENDED**
- Traditional 2D mapping with dark CartoDB tiles
- Full offline capability with MBTiles
- Lightweight and battle-tested
- **Pros**: Most reliable, offline support, easy dark theme
- **Cons**: Larger tile storage for offline mode

### Tab 3: MapLibre 2D (Vector)
- Modern vector-based 2D mapping
- Smooth zoom and rotation
- Smaller storage with vector tiles
- **Pros**: Smooth UX, GPU-accelerated, modern
- **Cons**: Requires GPU, more complex

### Tab 4: MapLibre 3D Globe
- Same as MapLibre 2D but with globe projection
- 3D terrain support
- **Pros**: Beautiful globe view, vector benefits
- **Cons**: Higher GPU requirements

### Tab 5: Cesium (150MB)
- Professional 3D geospatial platform
- Photorealistic globe with terrain
- Time-based animations
- **Pros**: Most advanced features, stunning visuals
- **Cons**: Larger bundle, steeper learning curve

## 🚀 Quick Start

### Option 1: Demo Mode (No Backend)

```powershell
cd web_demo
python app.py
```

Open http://localhost:5000 and click **"Load Demo Data"** to see sample predictions on all 5 map types!

### Option 2: Full Mode (With GeoCLIP Backend)

**Terminal 1** - Start FastAPI backend:
```powershell
cd C:\Users\Luke\git\geolens
# Try port 8000 if 8899 is blocked:
uvicorn Core.api_service:app --reload --port 8000
```

**Terminal 2** - Start Flask:
```powershell
cd C:\Users\Luke\git\geolens\web_demo
python app.py
```

Open http://localhost:5000 and upload an image to get real predictions!

## ✨ Features

- **5 Mapping Libraries** - Side-by-side comparison with detailed pros/cons
- **Tabbed Interface** - Easy switching between map types
- **Dark Theme** - All maps styled for dark mode
- **Confidence Color Coding**
  - 🟢 Green: High (>10%)
  - 🟡 Yellow: Medium (5-10%)
  - 🔴 Red: Low (<5%)
- **Interactive**
  - Click predictions to focus
  - Hover for details
  - Drag to rotate/pan
- **Image Upload** - Drag & drop or file picker
- **Demo Mode** - Works without backend

## 📊 Comparison Summary

| Library | Bundle Size | Offline | 3D | GPU Required | Complexity |
|---------|-------------|---------|----|--------------| -----------|
| **Globe.GL** | 51MB | ❌ | ✅ | ⚠️ Optional | Low |
| **Leaflet** ⭐ | 500MB | ✅ | ❌ | ❌ | Low |
| **MapLibre 2D** | ~50MB | ✅ | ❌ | ✅ | Medium |
| **MapLibre 3D** | ~50MB | ✅ | ✅ | ✅ | Medium |
| **Cesium** | 150MB | ⚠️ Partial | ✅ | ✅ | High |

## 🎯 Recommendation

**For GeoLens Desktop App: Leaflet 2D**

Why:
1. **Reliability** - Most mature and stable for desktop WebView2 integration
2. **Offline First** - Full offline with MBTiles (critical for no-cloud requirement)
3. **Dark Theme** - Easy to implement with CartoDB Dark Matter tiles
4. **Lightweight** - Simple API, low memory footprint
5. **Wide Adoption** - Extensive documentation and community support

**Alternative**: MapLibre 2D if you want vector tiles and smoother zooming (requires GPU acceleration check).

## 🛠️ Troubleshooting

### Backend Port Issues (Windows Error 10013)

If port 8899 is blocked, try port 8000:
```powershell
uvicorn Core.api_service:app --reload --port 8000
```

Then update `web_demo/app.py` line 17:
```python
BACKEND_URL = 'http://localhost:8000'  # Changed from 8899
```

### Maps Not Loading

Check browser console (F12 → Console):
- **"Globe.gl library not loaded"** - CDN blocked, check firewall
- **"MapLibre GL JS not loaded"** - CDN blocked
- **"Cesium not loaded"** - CDN blocked

Solution: Ensure internet connection for CDN resources.

### Only One Map Type Shows

Click the **tab buttons** at the top of the map area. Active tab has cyan/green gradient.

## 📁 File Structure

```
web_demo/
├── app.py                 # Flask server with upload endpoints
├── requirements.txt       # Flask, requests, Pillow
├── templates/
│   └── index.html        # 5-tab comparison interface
├── static/
│   ├── css/
│   │   └── style.css     # Dark theme + tab styles
│   ├── js/
│   │   └── app.js        # 5 map initializers + predictions
│   └── uploads/          # Uploaded images
└── README.md             # This file
```

## 🔧 Technologies Used

- **Backend**: Flask 3.0+
- **Map Libraries**:
  - Leaflet 1.9.4
  - Globe.GL 2.31.0 + Three.js 0.160.0
  - MapLibre GL JS 3.6.2
  - Cesium 1.111
- **Tiles**: CartoDB Dark Matter (Leaflet), MapLibre demo tiles
- **Styling**: Custom dark theme CSS

## 🎨 Integration into WinUI3

Once you choose a library:

1. **Leaflet** → Use WebView2 with local HTML file, bundle MBTiles in `maps/` folder
2. **Globe.GL** → WebView2 + bundle NASA textures (51MB)
3. **MapLibre** → WebView2 + bundle vector style JSON + tiles
4. **Cesium** → WebView2 + Cesium ion token + 150MB assets

All approaches use WebView2 component in WinUI3 with JavaScript interop for C# ↔ Map communication.

## 📝 Next Steps

1. **Try all 5 tabs** to see visual differences
2. **Upload test images** to see how predictions look
3. **Check performance** on your hardware (especially 3D views)
4. **Pick your favorite** for the desktop app
5. **Report back** which one you prefer!

---

**Current Status**: Demo works in standalone mode with sample data. Backend integration ready for real GeoCLIP predictions.
