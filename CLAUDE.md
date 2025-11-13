# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

GeoLens is a WinUI3 desktop application that provides AI-powered image geolocation using the GeoCLIP deep learning model. The app features a dark-themed interface with 2D map visualization (Leaflet with dark theme), intelligent caching, EXIF metadata extraction, and multi-format export capabilities. All AI inference runs locally (no cloud dependency).

**Current Status:** 85% feature complete. Core services fully implemented, UI functional, ready for alpha testing.

**Key Technologies:**
- Frontend: WinUI3 (Windows App SDK 1.8), C# 12, .NET 9
- Backend: Python 3.11+, FastAPI, GeoCLIP 1.2.0, PyTorch 2.6.0
- Architecture: C# desktop app launches embedded Python FastAPI service, communicates via HTTP

**Supported Image Formats:**
- `.jpg`/`.jpeg` - JPEG images (EXIF GPS supported)
- `.png` - PNG images
- `.bmp` - Bitmap images
- `.gif` - GIF images
- `.heic` - HEIC images (iPhone photos, EXIF GPS supported)
- `.webp` - WebP images

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
│  - Image queue with drag-and-drop  ✅   │
│  - 2D map visualization (Leaflet)  ✅   │
│  - Prediction display with confidence ✅│
│  - EXIF metadata panel ✅               │
│  - Multi-image heatmap ✅               │
└─────────────────┬───────────────────────┘
                  │ HTTP/REST (localhost:8899)
┌─────────────────▼───────────────────────┐
│   C# Service Layer (IMPLEMENTED) ✅     │
│  - PythonRuntimeManager ✅              │
│  - HardwareDetectionService ✅          │
│  - GeoCLIPApiClient ✅                  │
│  - PredictionCacheService (SQLite) ✅   │
│  - ExifMetadataExtractor ✅             │
│  - GeographicClusterAnalyzer ✅         │
│  - PredictionProcessor ✅               │
│  - ExportService (CSV/JSON/PDF/KML) ✅  │
│  - LeafletMapProvider ✅                │
│  - PredictionHeatmapGenerator ✅        │
│  - UserSettingsService ✅               │
└─────────────────┬───────────────────────┘
                  │ Process Management
┌─────────────────▼───────────────────────┐
│  Python FastAPI Service ✅              │
│  - api_service.py (HTTP endpoints) ✅   │
│  - llocale.predictor (GeoCLIP wrapper)✅│
│  - Automatic hardware detection ✅      │
│  - Reverse geocoding ✅                 │
│  - Batch processing ✅                  │
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

The C# app launches the Python service as a subprocess using `PythonRuntimeManager`. The service:
- Runs on `localhost:8899` by default
- Auto-detects GPU hardware (NVIDIA/AMD/Intel Arc/CPU) and loads appropriate PyTorch variant
- Supports offline mode via `hf_cache` parameter for air-gapped environments
- Uses `lru_cache` to keep predictor in memory across requests (maxsize=3)
- LRU cache warms 3 device configurations for instant switching

## Implementation Status

### ✅ FULLY IMPLEMENTED Services

All core services are production-ready with comprehensive error handling:

1. **HardwareDetectionService** (137 lines): WMI-based GPU detection for NVIDIA/AMD/Intel Arc
2. **PythonRuntimeManager** (268 lines): Process lifecycle, health checks, progress reporting
3. **GeoCLIPApiClient** (190 lines): HTTP client with batch processing and MD5 hashing
4. **PredictionCacheService** (613 lines): Two-tier caching (memory + SQLite), XXHash64 fingerprinting
5. **ExifMetadataExtractor** (472 lines): GPS, camera settings, capture metadata extraction
6. **GeographicClusterAnalyzer** (295 lines): Haversine distance, confidence boosting, clustering
7. **PredictionProcessor** (373 lines): Complete pipeline orchestration (cache → EXIF → API → cluster)
8. **ExportService** (769 lines): CSV (CsvHelper), JSON, PDF (QuestPDF), KML (Google Earth)
9. **LeafletMapProvider** (374 lines): WebView2 integration with dark theme, heatmap support
10. **PredictionHeatmapGenerator** (374 lines): Gaussian smoothing, hotspot detection
11. **UserSettingsService** (169 lines): JSON persistence with debounced saves

### 🚧 IN PROGRESS

- **MainPage.xaml.cs**: 95% complete, using mock data for development (needs removal)
- **LoadingPage.xaml.cs**: Complete with progress bars and tips

### ❌ PLANNED (Not Yet Implemented)

1. **Hybrid Offline/Online Maps**: Bundle minimal tiles, stream high-quality when online
2. **Installer Creation**: MSI/MSIX with embedded Python runtimes and models
3. **Video Frame Extraction**: See "Future Features" below
4. **Whisper Transcription**: See "Future Features" below

### Confidence Level System

Predictions are classified into 4 levels based on **adjusted probability** (after clustering boost):
- **Very High**: EXIF GPS data (cyan pin, always shown first) - 90% confidence (EXIF can be edited)
- **High**: Adjusted probability ≥ 60% (green) - very confident, more likely correct than wrong
- **Medium**: Adjusted probability ≥ 30% (yellow) - moderate confidence, reasonable possibility
- **Low**: Adjusted probability < 30% (red) - weak confidence, many possibilities

**Clustering Boost**: If multiple predictions fall within 100km radius, a confidence boost (up to 15%) is applied to clustered predictions. The UI and all exports display:
- **Base probability**: Original AI model output
- **Clustering boost**: Percentage increase due to geographic clustering
- **Final probability**: Base + boost (used for confidence classification)

Example: `3.2% base + 8.8% clustering boost = 12.0%` (classified as Low since < 30%)

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

## Future Planned Features

### Video Frame Extraction for Geolocation

**Status**: Planned for Phase 2 (Post-MVP)

**Overview**: Allow users to upload video files and extract multiple frames for GeoCLIP processing, enabling geolocation of video footage.

**Key Features**:
- Support common video formats: `.mp4`, `.mov`, `.avi`, `.mkv`, `.webm`
- Video preview with timeline scrubber
- Frame extraction modes:
  - **Manual Selection**: User clicks timeline to select specific frames
  - **Interval Extraction**: Extract frames every N seconds
  - **Smart Extraction**: Detect scene changes and extract representative frames
- Batch process all selected frames through GeoCLIP pipeline
- Video metadata extraction (duration, resolution, codec, GPS if available)
- Export results with video timestamp references

**Technical Architecture**:
- Use FFmpeg.NET or FFMpegCore for video processing
- Extract frames to temporary directory for processing
- Store video path + timestamp in prediction cache
- Display extracted frames in image queue with timestamp badges
- Enable batch export with video source information

**Implementation Priority**: Medium (after 3D globe and installer)

See `Docs/15_Video_Frame_Extraction.md` (to be created) for detailed specifications.

---

### Whisper AI Transcription Module

**Status**: Planned for Phase 3 (Future Major Feature)

**Overview**: New dedicated screen for AI-powered audio/video transcription using OpenAI's Whisper model, with translation and speaker diarization capabilities.

**Key Features**:
- **Audio/Video Upload**:
  - Audio formats: `.mp3`, `.wav`, `.flac`, `.m4a`, `.ogg`, `.wma`
  - Video formats: `.mp4`, `.mov`, `.avi`, `.mkv`, `.webm` (extract audio)
  - Drag-and-drop support
  - Queue management for batch processing

- **Whisper Model Integration**:
  - Local inference (whisper.cpp or faster-whisper)
  - Multiple model sizes (tiny, base, small, medium, large)
  - Automatic language detection (99 languages)
  - GPU acceleration (CUDA/ROCm) with CPU fallback
  - Real-time progress tracking

- **Transcription Features**:
  - Word-level timestamps
  - Automatic punctuation
  - Speaker diarization (identify different speakers)
  - Confidence scores per segment
  - Export formats: TXT, SRT (subtitles), VTT, JSON

- **Translation**:
  - Translate to English from 99 source languages
  - Preserve timestamps in translated output
  - Side-by-side original/translation view

- **UI Components**:
  - Dedicated "Transcription" tab in NavigationView
  - Audio waveform visualization
  - Editable transcription text with sync to audio
  - Speaker label assignment interface
  - Export options with format selection

**Technical Architecture**:
```
┌─────────────────────────────────────────┐
│  Transcription UI (TranscriptionPage)   │
│  - Audio queue management               │
│  - Waveform visualization               │
│  - Transcript editor                    │
│  - Speaker diarization UI               │
└─────────────────┬───────────────────────┘
                  │ HTTP/REST (localhost:8900)
┌─────────────────▼───────────────────────┐
│  C# Service Layer                       │
│  - WhisperRuntimeManager                │
│  - WhisperApiClient                     │
│  - AudioExtractionService (FFmpeg)      │
│  - TranscriptionCacheService (SQLite)   │
│  - SpeakerDiarizationService            │
│  - TranscriptExportService              │
└─────────────────┬───────────────────────┘
                  │ Process Management
┌─────────────────▼───────────────────────┐
│  Python Whisper Service (FastAPI)       │
│  - /transcribe endpoint                 │
│  - /translate endpoint                  │
│  - /identify-language endpoint          │
│  - faster-whisper or whisper.cpp        │
│  - pyannote.audio for diarization       │
└─────────────────────────────────────────┘
```

**Implementation Strategy**:
1. **Phase 3.1**: Basic Whisper integration (transcription only)
2. **Phase 3.2**: Add translation support
3. **Phase 3.3**: Add speaker diarization
4. **Phase 3.4**: Advanced UI (waveform, editing, sync playback)

**Model Options**:
- **faster-whisper** (recommended): 4x faster than OpenAI Whisper, lower memory
- **whisper.cpp**: C++ implementation, best for CPU inference
- Bundle multiple model sizes: tiny (75MB), base (142MB), small (466MB)

**Estimated Distribution Impact**:
- Whisper models: +75MB to +1.5GB depending on bundled sizes
- pyannote models: +500MB for speaker diarization
- Total with all features: +2GB to distribution size

**Implementation Priority**: Low (post-MVP, separate major feature)

See `Docs/16_Whisper_Transcription.md` (to be created) for detailed specifications.

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

| Document | Status | Purpose |
|----------|--------|---------|
| `00_IMPLEMENTATION_START_HERE.md` | ✅ Complete | Master implementation checklist with 7 phases (update needed) |
| `01_Architecture_Overview.md` | ✅ Complete | High-level system design and technology stack |
| `02_Service_Implementations.md` | ✅ Complete | Detailed C# service code examples (all implemented) |
| `03_Dark_Mode_Maps.md` | ✅ Complete | Map visualization specifications (Leaflet implemented) |
| `04_UI_Wireframes.md` | ✅ Complete | Layout and design specifications |
| `05_Heatmap_MultiImage.md` | ✅ Complete | Heatmap system design (fully implemented) |
| `06_Implementation_Roadmap.md` | 🔄 Outdated | 8-10 week phased timeline (needs update with current status) |
| `07_Fluent_UI_Design.md` | ✅ Complete | Windows 11 Fluent Design guidelines |
| `08_EXIF_Metadata_System.md` | ✅ Complete | EXIF extraction and display (fully implemented) |
| `09_Testing_Strategy.md` | ✅ Complete | Unit/integration/UI testing, benchmarking, CI workflows |
| `10_Deployment_and_CI.md` | ✅ Complete | GitHub Actions, versioning, installer creation, releases |
| `11_Advanced_Features.md` | ✅ Complete | Post-MVP features: batch processing, video support, accuracy validation |
| `12_Deployment_Strategy.md` | ✅ Complete | Distribution packaging and deployment |
| `13_Phase4_Globe_Implementation.md` | 🚧 Pending | 3D Globe with Three.js/Globe.GL (not yet implemented) |
| `14_Offline_Maps_Guide.md` | ✅ Complete | Offline map tile bundling guide |
| `15_Video_Frame_Extraction.md` | ❌ To Create | Video frame extraction feature specification |
| `16_Whisper_Transcription.md` | ❌ To Create | Whisper AI transcription module specification |

**Legend**:
- ✅ Complete: Document exists and is current
- 🔄 Outdated: Document exists but needs updating
- 🚧 Pending: Document exists but feature not implemented
- ❌ To Create: Planned document for new feature

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
