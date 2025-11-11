# GeoLens - Architecture Overview

## Project Summary

GeoLens is a WinUI3 desktop application that provides AI-powered image geolocation using the GeoCLIP model. The application features a dark-themed interface with 3D globe visualization, intelligent caching, and multi-format export capabilities.

## Core Components

### 1. Python Backend (Core Directory)

- **GeoCLIP AI Model**: Predicts geographic locations from images
- **FastAPI Service** (`api_service.py`): HTTP API exposing inference endpoints
- **Hardware Flexibility**: Auto-detects and uses CPU/CUDA/ROCm
- **Offline Support**: Can use local HuggingFace cache for air-gapped environments
- **Batch Processing**: Handles multiple images with progress tracking
- **Reverse Geocoding**: Converts coordinates to human-readable locations

### 2. C# WinUI3 Frontend

- **Image Queue Management**: Drag-and-drop, thumbnails, multi-selection
- **Interactive Visualization**: 3D globe or 2D map with dark theme
- **Prediction Display**: Confidence levels, clustering analysis, EXIF GPS
- **Export Functionality**: CSV, PDF, KML formats
- **Caching System**: Hash-based instant recall of previous predictions

### 3. Service Layer Architecture

```
┌─────────────────────────────────────────┐
│    WinUI3 Frontend (GeoLens.csproj)   │
│  - Image queue management               │
│  - Interactive globe/map visualization  │
│  - Drag-and-drop image support          │
│  - Settings UI (online/offline mode)    │
└─────────────────┬───────────────────────┘
                  │ HTTP/REST
┌─────────────────▼───────────────────────┐
│   C# Service Layer (New)                │
│  - PythonRuntimeManager                 │
│  - HardwareDetectionService             │
│  - GeoCLIPApiClient (HTTP client)       │
│  - MapProviderService (online/offline)  │
│  - PredictionCacheService (SQLite)      │
│  - ExifGpsExtractor                     │
│  - GeographicClusterAnalyzer            │
│  - PredictionHeatmapGenerator           │
└─────────────────┬───────────────────────┘
                  │ Process Management
┌─────────────────▼───────────────────────┐
│  Embedded Python Runtime (embeddable)   │
│  - FastAPI service (api_service.py)     │
│  - GeoCLIP model + dependencies         │
│  - Bundled with correct torch variant   │
└─────────────────────────────────────────┘
```

## Hardware Detection & Runtime Selection

### Detection Methods

1. **WMI Queries**: Use `Win32_VideoController` to enumerate GPUs
2. **Fallback Detection**: Run `nvidia-smi` or check registry keys
3. **Cache Result**: Store in settings to avoid repeated checks

### Runtime Distribution

```
runtime/
├── python_cpu/         # CPU-only (smallest ~800MB)
│   ├── python.exe
│   ├── Lib/
│   └── site-packages/  # torch-cpu, geoclip, etc.
├── python_cuda/        # NVIDIA GPU (~3GB)
│   └── site-packages/  # torch-cuda
└── python_rocm/        # AMD GPU (~2.5GB)
    └── site-packages/  # torch-rocm
```

## Key Features

### 1. Confidence Level System

- **4-Level Classification**: Very High (EXIF), High, Medium, Low
- **Geographic Clustering**: Boost confidence when predictions cluster within 100km
- **Visual Indicators**: Color-coded badges (Green → Yellow → Red)

### 2. EXIF GPS Priority

- **Automatic Extraction**: Reads GPS from JPEG/HEIC metadata
- **Always First**: Shown in highlighted panel above AI predictions
- **Very High Confidence**: Marked as most reliable source

### 3. Intelligent Caching

- **Hash Algorithm**: XXHash64 for fast, collision-resistant hashing
- **Storage**: SQLite database + in-memory cache
- **Instant Recall**: O(1) lookup by image hash
- **Auto-Cleanup**: Configurable expiration (default 90 days)

### 4. Dark Mode Visualization

- **Online Maps**: MapBox/MapTiler dark themes, NASA Black Marble
- **Offline Maps**: Pre-bundled dark tiles, custom dark globe
- **Enhanced Visibility**: Bright pins with glow effects, white borders
- **Consistent Theme**: All UI elements use dark palette (#0a0a0a)

### 5. Multi-Image Heatmap

- **Aggregation**: Combines predictions from multiple selected images
- **Gaussian Kernel**: Smooth heatmap with 1° resolution
- **Hotspot Detection**: Identifies high-intensity regions
- **Toggle View**: Switch between individual pins and heatmap

### 6. Multi-Format Export

- **CSV**: Full tabular data with all predictions
- **PDF**: Professional report with thumbnails and tables (QuestPDF)
- **KML**: Google Earth compatible with styled pins

## Technology Stack

### Frontend

- **Framework**: WinUI3 (Windows App SDK 1.8)
- **Language**: C# 12 (.NET 9)
- **3D Rendering**: Win2D or WebView2 with Three.js/globe.gl
- **Database**: SQLite (System.Data.SQLite)

### Backend

- **Language**: Python 3.11+
- **AI Model**: GeoCLIP 1.2.0
- **Web Framework**: FastAPI 0.115.2
- **ML Framework**: PyTorch 2.6.0 (CPU/CUDA/ROCm variants)

### Open-Source Dependencies

All components use MIT, Apache 2.0, or Public Domain licenses:

- **Win2D**: MIT
- **Three.js**: MIT
- **globe.gl**: MIT
- **QuestPDF**: MIT
- **CsvHelper**: MS-PL/Apache 2.0
- **SQLite**: Public Domain
- **NASA Textures**: Public Domain

## Installer Strategy

### Single Installer Approach

- **Size**: ~7-8GB uncompressed, ~3-4GB with LZMA2 compression
- **Contents**: All three Python runtimes, models, offline maps
- **Runtime Selection**: App detects hardware at startup and uses appropriate runtime
- **No User Setup**: Embedded Python, pre-installed dependencies

### Directory Structure

```
GeoLens/
├── GeoLens.exe
├── runtime/
│   ├── python_cpu/
│   ├── python_cuda/
│   └── python_rocm/
├── core/                   # Python scripts
│   ├── api_service.py
│   └── predictor/
├── models/                 # Pre-downloaded models
│   └── geoclip_cache/
└── maps/                   # Offline map tiles
    └── dark_tiles.mbtiles
```

## Development Roadmap

### Phase 1: Foundation (Week 1)

- [ ] Implement HardwareDetectionService
- [ ] Implement PythonRuntimeManager
- [ ] Test API communication

### Phase 2: Integration (Week 2)

- [ ] Create GeoCLIPApiClient
- [ ] Wire up MainPage to use real predictions
- [ ] Implement drag-and-drop image queue

### Phase 3: Visualization (Week 3)

- [ ] Implement online map provider (WebView2 + MapBox)
- [ ] Implement offline map provider (static tiles)
- [ ] Add pin rendering with prediction confidence

### Phase 4: Advanced Features (Week 4)

- [ ] Implement caching system
- [ ] Add EXIF GPS extraction
- [ ] Implement geographic clustering
- [ ] Add multi-image heatmap

### Phase 5: Distribution (Week 5)

- [ ] Create embedded Python packages (3 variants)
- [ ] Download and bundle GeoCLIP models
- [ ] Prepare map tiles for offline mode
- [ ] Create Inno Setup installer script

### Phase 6: Polish (Week 6)

- [ ] Add batch processing UI
- [ ] Implement multi-format export
- [ ] Add settings persistence
- [ ] Performance optimization

## Performance Considerations

### Optimization Strategies

1. **Lazy Loading**: Load map tiles and models on-demand
2. **Background Processing**: Run predictions in separate thread
3. **Thumbnail Generation**: Async loading with caching
4. **Database Indexing**: Hash-based lookups for cache
5. **Memory Management**: Clear old predictions from memory

### Expected Performance

- **First Prediction**: 5-15 seconds (model loading + inference)
- **Subsequent Predictions**: 2-5 seconds (inference only)
- **Cached Results**: <100ms (instant recall)
- **Heatmap Generation**: 1-3 seconds for 100 images

## Security & Privacy

### Data Handling

- **Local Processing**: All AI inference runs locally (no cloud)
- **No Telemetry**: No data sent to external servers
- **Offline Capable**: Full functionality without internet
- **User Control**: Clear cache and data at any time

### Model Provenance

- **GeoCLIP**: Open-source model from VicenteVivan/geoclip
- **Training Data**: OpenStreetMap + public image datasets
- **No PII**: Model trained on publicly available data

## Future Enhancements

### Potential Features

1. **Video Frame Analysis**: Extract frames and predict locations
2. **Batch Export**: Process entire folders automatically
3. **Timeline View**: Show predictions on temporal axis
4. **3D Street View**: Integrate with local street view data
5. **Accuracy Metrics**: Compare with EXIF GPS when available

## References

- [GeoCLIP GitHub](https://github.com/VicenteVivan/geo-clip)
- [WinUI 3 Documentation](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [FastAPI Documentation](https://fastapi.tiangolo.com/)
- [PyTorch Documentation](https://pytorch.org/docs/stable/index.html)
- [NASA Visible Earth](https://visibleearth.nasa.gov/)
