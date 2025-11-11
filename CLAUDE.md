# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GeoLens is a WinUI3 desktop application that provides AI-powered image geolocation using the GeoCLIP deep learning model. The app features a dark-themed interface with 3D globe visualization, intelligent caching, EXIF metadata extraction, and multi-format export capabilities. All AI inference runs locally (no cloud dependency).

**Key Technologies:**
- Frontend: WinUI3 (Windows App SDK 1.8), C# 12, .NET 9
- Backend: Python 3.11+, FastAPI, GeoCLIP 1.2.0, PyTorch 2.6.0
- Architecture: C# desktop app launches embedded Python FastAPI service, communicates via HTTP

## Build and Development Commands

### C# Frontend

```bash
# Restore NuGet dependencies
dotnet restore GeoLens.sln

# Build the application
dotnet build GeoLens.sln

# Run the desktop application (requires Python service to be running)
dotnet run --project GeoLens.csproj

# Format C# code
dotnet format
```

### Python Backend

```bash
# Install Python dependencies (choose variant based on hardware)
python -m pip install -r Core/requirements.txt          # Base dependencies
python -m pip install -r Core/requirements-cpu.txt      # CPU-only PyTorch
python -m pip install -r Core/requirements-cuda.txt     # NVIDIA GPU
python -m pip install -r Core/requirements-rocm.txt     # AMD GPU

# Start the FastAPI service for development
uvicorn Core.api_service:app --reload --port 8899

# Run smoke test to verify GeoCLIP model functionality
python -m Core.smoke_test --device auto
```

## Architecture

### High-Level System Design

```
┌─────────────────────────────────────────┐
│    WinUI3 Frontend (GeoLens.csproj)    │
│  - Image queue with drag-and-drop       │
│  - 3D globe/2D map visualization        │
│  - Prediction display with confidence   │
│  - EXIF metadata extraction             │
└─────────────────┬───────────────────────┘
                  │ HTTP/REST (localhost:8899)
┌─────────────────▼───────────────────────┐
│   C# Service Layer (Planned)            │
│  - PythonRuntimeManager                 │
│  - HardwareDetectionService             │
│  - GeoCLIPApiClient                     │
│  - PredictionCacheService (SQLite)      │
│  - ExifMetadataExtractor                │
│  - GeographicClusterAnalyzer            │
│  - MapProviders (online/offline)        │
└─────────────────┬───────────────────────┘
                  │ Process Management
┌─────────────────▼───────────────────────┐
│  Python FastAPI Service                 │
│  - api_service.py (HTTP endpoints)      │
│  - llocale.predictor (GeoCLIP wrapper)  │
│  - Automatic hardware detection         │
│  - Reverse geocoding                    │
└─────────────────────────────────────────┘
```

### Python Backend (`Core/`)

The Python backend (`Core/api_service.py`) exposes a FastAPI service with two main endpoints:
- `GET /health`: Health check endpoint
- `POST /infer`: Accepts images, returns top-k location predictions with reverse geocoding

The predictor logic is in `Core/predictor/`, packaged as `llocale`. It handles:
- GeoCLIP model loading and inference
- Hardware detection (auto-selects CPU/CUDA/ROCm)
- Reverse geocoding (lat/lon → city/state/country)
- Batch processing with progress tracking

### C# Frontend Structure

- `App.xaml(.cs)`: Application entry point, dark theme configuration, settings window management
- `Views/MainPage.xaml(.cs)`: Main UI with image queue, prediction list, and globe preview
- `Views/SettingsPage.xaml(.cs)`: Settings interface (placeholder for future configuration)
- `Assets/`: Static resources (icons, future globe textures)
- `Properties/`: App manifest and launch settings
- `Docs/`: Comprehensive architecture documentation (see below)

## Important Implementation Details

### WinUI3 Release Mode Configuration

The `.csproj` file contains critical fixes for WinUI3 COM activation issues in Release builds:

```xml
<Optimize Condition="'$(Configuration)' == 'Release'">false</Optimize>
<WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>
```

**Never remove these settings** - they prevent runtime crashes on startup.

### Python Service Communication

The C# app will launch the Python service as a subprocess using `PythonRuntimeManager` (to be implemented). The service:
- Runs on `localhost:8899` by default
- Auto-detects GPU hardware (NVIDIA/AMD/CPU) and loads appropriate PyTorch variant
- Supports offline mode via `hf_cache` parameter for air-gapped environments
- Uses `lru_cache` to keep predictor in memory across requests

### Planned Service Implementations

See `Docs/02_Service_Implementations.md` for detailed code examples. Key services to implement:

1. **HardwareDetectionService**: WMI-based GPU detection (NVIDIA/AMD/CPU)
2. **PythonRuntimeManager**: Launch/manage embedded Python runtime, health checks
3. **GeoCLIPApiClient**: HTTP client for `/health` and `/infer` endpoints
4. **PredictionCacheService**: SQLite + XXHash64 for instant recall of previous predictions
5. **ExifMetadataExtractor**: Extract GPS, camera info, capture settings from images
6. **GeographicClusterAnalyzer**: Boost confidence when predictions cluster within 100km
7. **PredictionProcessor**: Orchestrates cache → EXIF → API → clustering pipeline
8. **MapProviders**: WebView2 with Three.js/globe.gl for 3D visualization, dark theme support
9. **ExportService**: CSV, PDF (QuestPDF), KML export formats

### Confidence Level System

Predictions are classified into 4 levels:
- **Very High**: EXIF GPS data (cyan pin, always shown first)
- **High**: Probability > 0.1 or geographic clustering detected (green)
- **Medium**: Probability 0.05-0.1 (yellow)
- **Low**: Probability < 0.05 (red)

Clustering analysis: If multiple predictions fall within 100km radius, boost confidence and highlight the cluster center.

### Dark Mode Visualization Strategy

- **Online Maps**: MapBox/MapTiler dark themes, NASA Black Marble textures
- **Offline Maps**: Pre-bundled dark tiles in `maps/` directory (planned)
- **Globe**: Three.js with WebView2, dark space background, bright pins with glow effects
- **UI Theme**: All WinUI components use dark theme (`ElementTheme.Dark`)

### Multi-Image Heatmap System

When multiple images are selected:
1. Generate 360×180 grid (1° resolution)
2. Apply Gaussian kernel smoothing to predictions
3. Normalize and render as hexBin layer on globe
4. Detect hotspots (>80% max intensity)
5. Toggle between individual pins and heatmap view

See `Docs/05_Heatmap_MultiImage.md` for implementation details.

## Code Style and Conventions

### C# Code Style
- Four-space indentation
- PascalCase for public types and methods
- camelCase for private fields (prefix with `_` for class fields)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Use `var` for obvious types, explicit types for clarity
- Prefer expression-bodied members for simple properties/methods

### XAML Style
- One attribute per line for readability (except very simple elements)
- Use `x:Name` for elements referenced in code-behind
- Avoid heavy code-behind logic - prefer view models or services
- Use `ThemeResource` for colors to support dark theme

### Python Code Style
- PEP 8 spacing and naming conventions
- Type hints for all function signatures (already in use throughout `Core/`)
- Use Pydantic models for API request/response DTOs
- Use dataclasses for internal data structures
- Keep `Core/predictor` structures in sync with API DTOs

## Testing

### Python Tests
Extend `Core/smoke_test.py` when adding new inference behavior:

```bash
# Test with auto-detected hardware
python -m Core.smoke_test --device auto

# Test specific device
python -m Core.smoke_test --device cuda
```

### C# Tests
Once test suite exists, follow naming pattern: `test_<component>_<behavior>` (e.g., `test_predictor_csv_loading`)

Document manual verification steps in `Docs/` until automated tests are implemented.

## Documentation Structure

The `Docs/` directory contains comprehensive design documents:

| Document | Purpose |
|----------|---------|
| `00_IMPLEMENTATION_START_HERE.md` | Master implementation checklist with 7 phases |
| `01_Architecture_Overview.md` | High-level system design and technology stack |
| `02_Service_Implementations.md` | Detailed C# service code examples |
| `03_Dark_Mode_Maps.md` | Map visualization specifications |
| `04_UI_Wireframes.md` | Layout and design specifications |
| `05_Heatmap_MultiImage.md` | Heatmap system design and implementation |
| `06_Implementation_Roadmap.md` | 8-10 week phased timeline |
| `07_Fluent_UI_Design.md` | Windows 11 Fluent Design guidelines |
| `08_EXIF_Metadata_System.md` | EXIF extraction and display specifications |

Refer to these documents when implementing new features. They contain detailed code examples and specifications.

## Distribution Strategy

The final installer will bundle:
- Three embedded Python runtimes (`runtime/python_cpu`, `python_cuda`, `python_rocm`)
- Pre-downloaded GeoCLIP models (`models/geoclip_cache/`)
- Offline map tiles (`maps/dark_tiles.mbtiles`)
- Total size: ~3-4GB compressed (7-8GB uncompressed)

The app detects hardware at startup and selects the appropriate Python runtime automatically.

## Security and Privacy

- All AI inference runs **locally** (no cloud dependency)
- No telemetry or external data transmission
- Full offline capability once models are downloaded
- Users can clear cache/data at any time
- GeoCLIP model: Open-source, trained on public datasets (OSM + public images)

## Common Issues

### Python service fails to start
- Verify `python.exe` exists in runtime folder
- Check Windows Firewall isn't blocking port 8899
- Check port 8899 isn't already in use
- Review `uvicorn` logs for model loading errors

### WebView2 not rendering globe
- Install WebView2 runtime: https://developer.microsoft.com/microsoft-edge/webview2/
- Verify HTML file path in `MapProvider.InitializeAsync`
- Check browser console for JavaScript errors

### EXIF data not extracting
- Only JPEG/HEIC formats contain EXIF
- Handle missing properties gracefully (not all images have GPS)
- Verify EXIF property IDs match Windows.Graphics.Imaging spec

### High memory usage
- Virtualize image queue with `ItemsRepeater` instead of `ListView`
- Clear old predictions from `ObservableCollection`
- Implement thumbnail caching with size limits
- Profile with Visual Studio Diagnostic Tools

## Development Workflow

1. Start Python service: `uvicorn Core.api_service:app --reload`
2. Build and run C# app: `dotnet run --project GeoLens.csproj`
3. Make changes to either frontend or backend
4. Test integration with real images
5. Run smoke test after dependency changes: `python -m Core.smoke_test --device auto`
6. Format C# code before committing: `dotnet format`

## Commit Message Style

Use conventional commit format:
```
type(scope): short description

Examples:
feat(core): add caching toggle for offline mode
fix(ui): correct pin positioning on dark globe
docs(architecture): update service layer diagram
refactor(predictor): extract reverse geocoding to separate module
```

Include verification steps in commit message or PR description. Add screenshots for UI changes.
