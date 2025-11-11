# GeoLens Implementation Roadmap

## Overview

This document provides a detailed, phased implementation plan for building GeoLens from the ground up. Each phase includes specific tasks, dependencies, and acceptance criteria.

---

## Phase 1: Foundation (Week 1)

### Goal
Establish core infrastructure: hardware detection, Python runtime management, and basic API communication.

### Tasks

#### 1.1 Project Setup
- [ ] Create solution structure with proper folders
  - `Services/`
  - `Models/`
  - `ViewModels/`
  - `Views/`
  - `Assets/Maps/`
  - `Assets/Globe/`
- [ ] Add required NuGet packages
- [ ] Configure build system for Release mode optimization fix
- [ ] Set up Git .gitignore (exclude large assets during development)

**Deliverable**: Clean solution that builds successfully

---

#### 1.2 Hardware Detection Service

**File**: `Services/HardwareDetectionService.cs`

```csharp
✓ Implement GPU detection via WMI
✓ Implement nvidia-smi fallback
✓ Implement AMD GPU detection
✓ Add result caching
✓ Create unit tests
```

**Acceptance Criteria**:
- Correctly detects NVIDIA, AMD, or CPU-only
- Returns consistent results across multiple calls
- Handles exceptions gracefully

**Testing**:
```csharp
[TestMethod]
public void DetectHardware_NvidiaGPU_ReturnsCUDA() { }

[TestMethod]
public void DetectHardware_AMDGPU_ReturnsROCM() { }

[TestMethod]
public void DetectHardware_NoGPU_ReturnsCPU() { }
```

---

#### 1.3 Python Runtime Manager

**File**: `Services/PythonRuntimeManager.cs`

```csharp
✓ Implement runtime path selection
✓ Implement service startup (FastAPI/uvicorn)
✓ Implement health check polling
✓ Implement graceful shutdown
✓ Add environment variable configuration
✓ Handle startup failures
```

**Acceptance Criteria**:
- Launches Python service on correct port (8899)
- Service responds to /health within 30 seconds
- Cleans up process on app shutdown
- Logs errors to Debug output

**Testing**:
```csharp
[TestMethod]
public async Task StartServiceAsync_ValidRuntime_ReturnsTrue() { }

[TestMethod]
public async Task StartServiceAsync_MissingRuntime_ReturnsFalse() { }
```

---

#### 1.4 GeoCLIP API Client

**File**: `Services/GeoCLIPApiClient.cs`

```csharp
✓ Implement HTTP client with proper configuration
✓ Create request/response DTOs
✓ Implement /infer endpoint
✓ Implement /health endpoint
✓ Add timeout handling
✓ Add error handling
```

**Acceptance Criteria**:
- Successfully calls Python API
- Deserializes responses correctly
- Handles network errors gracefully
- Supports batch predictions

**Testing**:
```csharp
[TestMethod]
public async Task PredictAsync_ValidImage_ReturnsResults() { }

[TestMethod]
public async Task PredictAsync_ServiceDown_ThrowsException() { }
```

---

#### 1.5 Integration Test

**File**: `IntegrationTests/ApiIntegrationTests.cs`

```csharp
✓ Start Python service
✓ Wait for health check
✓ Send test image prediction
✓ Verify response format
✓ Stop service
```

**Acceptance Criteria**:
- End-to-end flow works from C# → Python → C#
- Response contains valid lat/lon predictions
- No memory leaks or hanging processes

---

### Phase 1 Deliverables

✅ Hardware detection working
✅ Python service starts and responds
✅ API client communicates successfully
✅ All unit tests passing

**Estimated Time**: 5-7 days

---

## Phase 2: UI Foundation (Week 2)

### Goal
Build the basic UI shell with image queue, map placeholder, and results panel.

### Tasks

#### 2.1 Main Window Layout

**File**: `Views/MainPage.xaml`

```xml
✓ Create 3-column layout (left: 300px, center: *, right: 400px)
✓ Add placeholder panels
✓ Configure dark theme
✓ Test responsive resizing
```

**Acceptance Criteria**:
- Layout renders correctly at 1920×1080 and 1280×720
- Dark theme applied consistently
- No white flashes on load

---

#### 2.2 Image Queue Panel

**File**: `Views/MainPage.xaml` + `ViewModels/ImageQueueItem.cs`

```csharp
✓ Create ImageQueueItem model
✓ Implement ObservableCollection binding
✓ Add drag-and-drop support
✓ Implement thumbnail generation
✓ Add status badges
✓ Implement multi-selection
```

**Acceptance Criteria**:
- Drag-and-drop adds images to queue
- Thumbnails load asynchronously
- Status updates (Queued → Processing → Done)
- Multi-selection works with checkboxes

---

#### 2.3 Map Placeholder

**File**: `Views/MainPage.xaml`

```xml
✓ Add WebView2 control
✓ Load placeholder HTML with dark background
✓ Test control initialization
```

**Acceptance Criteria**:
- WebView2 initializes without errors
- Dark background displays (no white flash)
- Ready for globe.gl integration

---

#### 2.4 Results Panel

**File**: `Views/MainPage.xaml`

```xml
✓ Create prediction card template
✓ Add scrollable ItemsRepeater
✓ Add EXIF GPS display section
✓ Add export button placeholders
```

**Acceptance Criteria**:
- Displays mock prediction data
- Scrolls smoothly with many results
- EXIF section shows/hides correctly

---

#### 2.5 Prediction Pipeline Integration

**File**: `Views/MainPage.xaml.cs`

```csharp
✓ Wire AddImages button to file picker
✓ Implement ProcessImageAsync method
✓ Call API client for predictions
✓ Update UI with results
✓ Handle errors gracefully
```

**Acceptance Criteria**:
- Clicking "Process" sends images to API
- Results display in right panel
- Status updates show progress
- Errors display in InfoBar

---

### Phase 2 Deliverables

✅ Full UI shell with 3 panels
✅ Image queue with thumbnails
✅ Real predictions from API display in UI
✅ Basic error handling

**Estimated Time**: 7-10 days

---

## Phase 3: Visualization (Week 3)

### Goal
Implement dark-themed 3D globe with pin rendering.

### Tasks

#### 3.1 Download Dark Globe Assets

```bash
✓ Download NASA Black Marble 8K texture
✓ Download night sky background
✓ Save to Assets/Globe/
✓ Test file loading
```

**Assets**:
- `earth_night_8k.jpg` (45 MB)
- `night_sky.png` (2 MB)

---

#### 3.2 Three.js Globe Implementation

**File**: `Assets/globe_dark.html`

```javascript
✓ Implement globe initialization
✓ Load dark textures
✓ Implement addPin function
✓ Implement rotateToPin function
✓ Add tooltip with dark theme
✓ Add pulse rings for top predictions
```

**Acceptance Criteria**:
- Globe renders with dark textures
- Pins are visible with glow effects
- Tooltips display on hover
- Camera rotates smoothly

---

#### 3.3 C# Map Provider

**File**: `Services/WebGlobe3DProvider.cs`

```csharp
✓ Implement IMapProvider interface
✓ Initialize WebView2 with HTML
✓ Implement AddPinAsync (call JS)
✓ Implement RotateToPinAsync
✓ Implement ClearPinsAsync
```

**Acceptance Criteria**:
- C# calls execute JavaScript successfully
- Pins appear on globe after API prediction
- Rotation animation works
- No JavaScript errors in console

---

#### 3.4 Integration with Predictions

**File**: `Views/MainPage.xaml.cs`

```csharp
✓ Initialize map provider on page load
✓ Add pins after prediction complete
✓ Color pins by confidence level
✓ Add EXIF GPS pins with special styling
✓ Implement "View on Map" button
```

**Acceptance Criteria**:
- Predictions automatically display on globe
- Confidence colors match design (green/yellow/red)
- EXIF pins are cyan with larger size
- Clicking prediction rotates to location

---

### Phase 3 Deliverables

✅ Dark 3D globe renders correctly
✅ Predictions display as pins on globe
✅ Smooth animations and interactions
✅ EXIF GPS pins are highlighted

**Estimated Time**: 7-10 days

---

## Phase 4: Advanced Features (Week 4)

### Goal
Add caching, EXIF extraction, clustering, and confidence system.

### Tasks

#### 4.1 Prediction Cache Service

**File**: `Services/PredictionCacheService.cs`

```csharp
✓ Create SQLite database
✓ Implement hash computation (XXHash64)
✓ Implement cache lookup
✓ Implement cache save
✓ Implement statistics
✓ Implement cleanup
```

**Acceptance Criteria**:
- Hash computation is fast (<10ms)
- Cache hit returns result instantly (<100ms)
- Database grows properly
- Cleanup removes old entries

---

#### 4.2 EXIF GPS Extractor

**File**: `Services/ExifGpsExtractor.cs`

```csharp
✓ Read EXIF properties
✓ Parse GPS coordinates
✓ Handle missing EXIF data
✓ Test with various image formats
```

**Acceptance Criteria**:
- Correctly extracts GPS from JPEG with EXIF
- Returns null for images without GPS
- Handles corrupt EXIF gracefully

---

#### 4.3 Geographic Cluster Analyzer

**File**: `Services/GeographicClusterAnalyzer.cs`

```csharp
✓ Implement Haversine distance calculation
✓ Implement clustering detection
✓ Calculate confidence boost
✓ Find cluster center
```

**Acceptance Criteria**:
- Detects when top 3 predictions are within 100km
- Boosts confidence by up to +0.15
- Calculates accurate centroid

---

#### 4.4 Confidence Classification

**File**: `Models/ConfidenceHelper.cs`

```csharp
✓ Implement confidence level classification
✓ Add color mapping
✓ Add icon mapping
✓ Add display text
```

**Acceptance Criteria**:
- Confidence levels match design spec
- Colors are visible on dark background
- Icons display correctly

---

#### 4.5 Enhanced Prediction Processor

**File**: `Services/PredictionProcessor.cs`

```csharp
✓ Integrate cache check
✓ Integrate EXIF extraction
✓ Integrate clustering analysis
✓ Create EnhancedPredictionResult
✓ Save to cache
```

**Acceptance Criteria**:
- Full pipeline runs correctly
- Cache hits bypass API
- EXIF GPS shown first
- Clustering boosts confidence

---

#### 4.6 UI Updates

**File**: `Views/MainPage.xaml`

```xml
✓ Update prediction cards with confidence badges
✓ Add EXIF GPS special panel
✓ Add clustering indicators
✓ Add reliability assessment InfoBar
```

**Acceptance Criteria**:
- Confidence badges color-coded correctly
- EXIF GPS panel highlighted in cyan
- Clustered predictions show icon
- Reliability text updates dynamically

---

### Phase 4 Deliverables

✅ Caching system working (instant recall)
✅ EXIF GPS detected and prioritized
✅ Clustering boosts confidence
✅ UI displays all enhancements

**Estimated Time**: 10-12 days

---

## Phase 5: Multi-Image Heatmap (Week 5)

### Goal
Implement heatmap visualization for multiple selected images.

### Tasks

#### 5.1 Heatmap Generator

**File**: `Services/PredictionHeatmapGenerator.cs`

```csharp
✓ Implement grid initialization
✓ Implement Gaussian kernel
✓ Implement normalization
✓ Implement hotspot detection
✓ Implement clustering
```

**Acceptance Criteria**:
- Generates heatmap in <500ms for 100 predictions
- Hotspots detected correctly
- Grid normalized to 0-1

---

#### 5.2 Heatmap Visualization

**File**: `Assets/globe_dark.html`

```javascript
✓ Implement hexBin layer
✓ Implement color gradient
✓ Implement tooltips
✓ Implement toggle between pins/heatmap
```

**Acceptance Criteria**:
- Heatmap renders with gradient colors
- Toggle switches smoothly
- Tooltips show intensity

---

#### 5.3 Multi-Selection UI

**File**: `Views/MainPage.xaml`

```xml
✓ Add selection toolbar
✓ Add "Select All" checkbox
✓ Add heatmap toggle button
✓ Add "Overlay All" button
✓ Update status bar
```

**Acceptance Criteria**:
- Select all works
- Selected count updates
- Heatmap toggle triggers visualization

---

#### 5.4 Integration

**File**: `Views/MainPage.xaml.cs`

```csharp
✓ Implement multi-selection handling
✓ Generate heatmap on selection change
✓ Send heatmap data to map provider
✓ Update UI with hotspot info
```

**Acceptance Criteria**:
- Selecting 2+ images enables heatmap
- Heatmap updates in real-time
- Hotspot info displays correctly

---

### Phase 5 Deliverables

✅ Heatmap generation working
✅ Multi-selection UI functional
✅ Toggle between pins and heatmap
✅ Hotspot detection and display

**Estimated Time**: 7-10 days

---

## Phase 6: Export & Polish (Week 6)

### Goal
Add export functionality and final polish.

### Tasks

#### 6.1 Export Service

**File**: `Services/ExportService.cs`

```csharp
✓ Implement CSV export
✓ Implement PDF export (QuestPDF)
✓ Implement KML export
✓ Add file save dialogs
```

**Acceptance Criteria**:
- CSV exports all predictions
- PDF includes thumbnails and tables
- KML opens in Google Earth

---

#### 6.2 Settings Service

**File**: `Services/SettingsService.cs`

```csharp
✓ Create UserSettings model
✓ Implement Load/Save to JSON
✓ Add default values
```

**File**: `Views/SettingsPage.xaml`

```xml
✓ Create settings UI
✓ Add cache controls
✓ Add map mode selector
✓ Add thumbnail size selector
```

**Acceptance Criteria**:
- Settings persist across sessions
- All toggles work correctly
- Hardware info displays

---

#### 6.3 Performance Optimization

```csharp
✓ Add thumbnail caching
✓ Virtualize image queue
✓ Lazy-load map tiles
✓ Debounce search inputs
✓ Profile memory usage
```

**Acceptance Criteria**:
- App launches in <3 seconds
- Scrolling image queue is smooth
- Memory usage stays below 500MB

---

#### 6.4 Error Handling

```csharp
✓ Add try-catch blocks everywhere
✓ Display user-friendly error messages
✓ Log errors to file
✓ Handle Python service crashes
```

**Acceptance Criteria**:
- No unhandled exceptions
- Errors don't crash app
- User gets actionable error messages

---

#### 6.5 Final Testing

```
✓ Test on NVIDIA GPU machine
✓ Test on AMD GPU machine
✓ Test on CPU-only machine
✓ Test offline mode (no internet)
✓ Test with large image batches (100+)
✓ Test cache cleanup
✓ Test all export formats
```

**Acceptance Criteria**:
- Works on all hardware types
- No regressions
- All features functional

---

### Phase 6 Deliverables

✅ Export working (CSV/PDF/KML)
✅ Settings page functional
✅ Performance optimized
✅ Comprehensive error handling
✅ All tests passing

**Estimated Time**: 10-12 days

---

## Phase 7: Distribution (Week 7)

### Goal
Create installer with embedded Python runtimes and assets.

### Tasks

#### 7.1 Prepare Python Runtimes

```bash
✓ Download Python embeddable 3.11 for Windows x64
✓ Install CPU dependencies: pip install -r requirements-cpu.txt --target site-packages
✓ Install CUDA dependencies: pip install -r requirements-cuda.txt --target site-packages
✓ Install ROCm dependencies: pip install -r requirements-rocm.txt --target site-packages
✓ Test each runtime standalone
```

**Output**:
- `runtime/python_cpu/` (~800MB)
- `runtime/python_cuda/` (~3GB)
- `runtime/python_rocm/` (~2.5GB)

---

#### 7.2 Download Models

```bash
✓ Run smoke test to trigger model download
✓ Copy models from cache to models/geoclip_cache/
✓ Test offline loading
```

**Output**:
- `models/geoclip_cache/` (~500MB)

---

#### 7.3 Prepare Map Assets

```bash
✓ Download NASA Black Marble
✓ Download dark map tiles (zoom 0-8)
✓ Convert tiles to MBTiles format
✓ Test offline rendering
```

**Output**:
- `Assets/Maps/black_marble_8k.jpg` (45MB)
- `Assets/Maps/dark_tiles.mbtiles` (500MB)

---

#### 7.4 Create Inno Setup Script

**File**: `installer/setup.iss`

```pascal
✓ Define app info
✓ Add all files
✓ Set compression (LZMA2)
✓ Create shortcuts
✓ Set registry keys
✓ Test installer build
```

**Acceptance Criteria**:
- Installer builds successfully
- Size is ~3-4GB compressed
- Installs correctly on fresh Windows

---

#### 7.5 Test Installation

```
✓ Install on clean Windows 10
✓ Install on clean Windows 11
✓ Test hardware detection
✓ Test first-run experience
✓ Test uninstall
```

**Acceptance Criteria**:
- App runs after install
- Correct runtime selected
- Models load successfully
- Uninstall removes all files

---

### Phase 7 Deliverables

✅ Single installer (.exe) created
✅ All runtimes and assets bundled
✅ Installation tested on multiple machines
✅ Uninstaller works correctly

**Estimated Time**: 7-10 days

---

## Total Timeline

| Phase | Duration | Cumulative |
|-------|----------|------------|
| Phase 1: Foundation | 5-7 days | Week 1 |
| Phase 2: UI Foundation | 7-10 days | Week 2-3 |
| Phase 3: Visualization | 7-10 days | Week 3-4 |
| Phase 4: Advanced Features | 10-12 days | Week 4-5 |
| Phase 5: Multi-Image Heatmap | 7-10 days | Week 6 |
| Phase 6: Export & Polish | 10-12 days | Week 7-8 |
| Phase 7: Distribution | 7-10 days | Week 9 |
| **Total** | **53-71 days** | **8-10 weeks** |

---

## Risk Mitigation

### Potential Blockers

1. **Python Service Fails to Start**
   - Mitigation: Extensive error logging, fallback to simpler runtime
   - Test: Create mock API server for development

2. **WebView2 Rendering Issues**
   - Mitigation: Fallback to Win2D offline rendering
   - Test: Test on machines without WebView2 runtime

3. **Large Installer Size**
   - Mitigation: Offer download-on-demand for runtimes
   - Alternative: Create separate installers per GPU type

4. **Performance Issues**
   - Mitigation: Profile early and often
   - Optimization: Use async/await everywhere, virtualize lists

5. **EXIF Parsing Failures**
   - Mitigation: Catch exceptions, log errors, continue without EXIF
   - Test: Collect diverse image formats for testing

---

## Success Criteria

### MVP (Minimum Viable Product)

- [ ] User can drag-and-drop images
- [ ] Predictions display on 3D globe
- [ ] Confidence levels are visible
- [ ] EXIF GPS is extracted and prioritized
- [ ] Results can be exported to CSV
- [ ] Works offline

### Full Release

- [ ] All MVP features
- [ ] Heatmap visualization working
- [ ] PDF and KML export
- [ ] Settings persist
- [ ] Cache provides instant recall
- [ ] Single installer works on all hardware
- [ ] Documentation complete

---

## Post-Launch

### Future Enhancements (Phase 8+)

1. **Video Support**: Extract frames from videos for location prediction
2. **Timeline View**: Show predictions on temporal axis
3. **Batch Mode**: Process entire folders automatically
4. **API Mode**: Expose REST API for external tools
5. **Cloud Sync**: Optional encrypted cloud backup
6. **Multi-Language**: Localize UI and location names
7. **Custom Models**: Allow users to fine-tune GeoCLIP

---

This roadmap is living document. Update task completion status as work progresses.
