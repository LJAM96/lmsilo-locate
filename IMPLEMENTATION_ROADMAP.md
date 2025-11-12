# GeoLens Implementation Roadmap

**Status**: 50% Complete | **Target MVP**: 3-4 weeks | **Target Production**: 6-8 weeks

---

## 📊 Visual Progress Overview

```
Phase 0: Project Setup          ████████████████████ 100% ✅
Phase 1: UI Shell              ████████████████████ 100% ✅
Phase 2: Service Foundation    ████████████████████ 100% ✅
Phase 3: Backend Integration   ██████░░░░░░░░░░░░░░  30% ⚠️
Phase 4: 3D Globe             ████████████████████ 100% ✅
Phase 5: Core Services        ░░░░░░░░░░░░░░░░░░░░   0% ❌
Phase 6: Heatmap & Export     ░░░░░░░░░░░░░░░░░░░░   0% ❌
Phase 7: Testing & Polish     ░░░░░░░░░░░░░░░░░░░░   0% ❌
Phase 8: Deployment           ░░░░░░░░░░░░░░░░░░░░   0% ❌

Overall Progress: ████████████░░░░░░░░ 50%
```

---

## 🔥 CRITICAL PATH - Must Do First (Week 1)

### 🐛 Bug Fixes (Priority 1) - 12 hours
**Why**: These break production functionality or cause resource leaks

- [ ] **Memory leak in MainPage** (2 hours)
  - File: `Views/MainPage.xaml.cs`
  - Add: Event handler cleanup in Unloaded event
  - Add: Dispose pattern for page
  - Test: Memory profiler over 1 hour session

- [ ] **Globe initialization error handling** (2 hours)
  - File: `Views/MainPage.xaml.cs:132-139`
  - Add: Error message UI in loading overlay
  - Add: Retry button
  - Test: Unplug internet, verify error message

- [ ] **Race condition in ProcessImages_Click** (3 hours)
  - File: `Views/MainPage.xaml.cs:355-418`
  - Add: `CancellationTokenSource` field
  - Add: Button disable logic during processing
  - Add: Cancel operation on second click
  - Test: Rapid button clicking

- [ ] **GeoCLIPApiClient disposal** (1 hour)
  - File: `App.xaml.cs`
  - Add: `protected override void OnExit()` handler
  - Dispose: `PythonManager`, `ApiClient`
  - Test: Resource Monitor shows cleanup

- [ ] **HardwareDetectionService logic bug** (1 hour)
  - File: `Services/HardwareDetectionService.cs:115-117`
  - Fix: Change `||` to `&&` in GPU detection
  - Add: Filter "Basic Display Adapter"
  - Test: On Intel + NVIDIA system

- [ ] **Python process zombie prevention** (3 hours)
  - File: `Services/PythonRuntimeManager.cs`
  - Add: Windows Job Object implementation
  - Add: Automatic cleanup on app crash
  - Test: Kill app via Task Manager, verify Python stops

**Acceptance Criteria**:
- No memory leaks after 1-hour session
- Globe errors show actionable message
- Process button can be cancelled
- Resources cleaned up on app exit
- Python never orphaned

---

### 🔌 Complete Backend Integration (Priority 2) - 9 hours
**Why**: UI currently shows fake data, not real AI predictions

- [ ] **Remove mock data loading** (1 hour)
  - File: `Views/MainPage.xaml.cs:148-285`
  - Remove: `LoadMockData()` method
  - Remove: Mock data generation
  - Keep: Real file picker (already implemented)

- [ ] **Wire API calls to UI events** (3 hours)
  - File: `Views/MainPage.xaml.cs:355-418`
  - Update: `ProcessImages_Click` to use real API
  - Add: Progress reporting UI
  - Add: Status updates in image queue
  - Test: Process 5 real images

- [ ] **Implement error boundaries** (3 hours)
  - File: `Views/MainPage.xaml.cs`
  - Add: `ShowErrorDialog` method
  - Add: Retry logic for network failures
  - Add: Per-image error display
  - Test: Unplug network, corrupt image file

- [ ] **Add user feedback during processing** (2 hours)
  - File: `Views/MainPage.xaml`
  - Add: Progress bar with percentage
  - Add: "Processing image 3 of 10" text
  - Add: Estimated time remaining
  - Test: Process 20 images

**Acceptance Criteria**:
- Selecting images → processing → predictions works end-to-end
- Errors shown with retry option
- User sees progress during inference
- No fake data in production code

---

## 🎯 MVP Features (Week 1-2)

### 📦 PredictionCacheService (Priority 3) - 4 hours
**Why**: Instant recall saves API calls and improves UX

- [ ] **Create SQLite schema** (1 hour)
  - File: `Services/PredictionCacheService.cs` (new)
  - Schema:
    ```sql
    CREATE TABLE predictions (
      image_hash TEXT PRIMARY KEY,
      file_path TEXT,
      predictions_json TEXT,
      cached_at INTEGER
    );
    ```
  - Location: `%LOCALAPPDATA%\GeoLens\cache.db`

- [ ] **Implement XXHash64 hashing** (1 hour)
  - Package: Already installed (`System.IO.Hashing`)
  - Method: `ComputeHash(Stream imageStream)`
  - Return: Base64 hash string

- [ ] **Cache hit/miss logic** (1 hour)
  - Method: `TryGetCached(string filePath)` → `EnhancedPredictionResult?`
  - Method: `SaveToCache(string filePath, EnhancedPredictionResult result)`
  - Logic: Return cached if file hash matches

- [ ] **Integrate into MainPage** (1 hour)
  - Update: `ProcessImages_Click` to check cache first
  - Update: Status badge to show "CACHED" icon
  - Test: Process same image twice, verify instant recall

**Acceptance Criteria**:
- First inference: 5 seconds
- Second inference (same image): <100ms
- Cache survives app restart
- Old cache entries auto-expire after 30 days

---

### 📷 ExifMetadataExtractor (Priority 4) - 3 hours
**Why**: GPS data from photos provides "Very High" confidence predictions

- [ ] **Create extraction service** (2 hours)
  - File: `Services/ExifMetadataExtractor.cs` (new)
  - API: `Windows.Graphics.Imaging.BitmapDecoder`
  - Method: `ExtractAsync(string filePath)` → `ExifGpsData?`
  - Extract: GPS lat/lon, camera make/model, capture time

- [ ] **Handle missing EXIF gracefully** (30 min)
  - Return: `null` if no GPS data
  - Log: Warning if file can't be read
  - Test: JPEG with GPS, JPEG without GPS, PNG (no EXIF)

- [ ] **Display in UI** (30 min)
  - File: `Views/MainPage.xaml.cs`
  - Update: `DisplayPredictions` to check EXIF first
  - Show: EXIF panel when GPS data found
  - Add: EXIF pin to globe (cyan color)

**Acceptance Criteria**:
- iPhone photos show GPS location in EXIF panel
- Non-GPS photos don't crash, proceed to AI
- EXIF pin displayed first on globe

---

### 🌍 GeographicClusterAnalyzer (Priority 5) - 3 hours
**Why**: Boosts confidence when multiple predictions agree on region

- [ ] **Haversine distance calculation** (1 hour)
  - File: `Services/GeographicClusterAnalyzer.cs` (new)
  - Method: `CalculateDistance(lat1, lon1, lat2, lon2)` → km
  - Formula: Standard Haversine (Earth radius = 6371 km)

- [ ] **Cluster detection logic** (1 hour)
  - Method: `AnalyzeClusters(List<LocationPrediction>)` → `ClusterAnalysis`
  - Logic: Find predictions within 100km radius
  - Output: Cluster center, member count, boost factor

- [ ] **Confidence boosting** (1 hour)
  - Method: `ApplyClusterBoost(List<EnhancedLocationPrediction>)`
  - Boost: Add 0.15 to adjusted probability for cluster members
  - Update: `ConfidenceLevel` based on adjusted probability
  - UI: Show cluster badge on cards

**Acceptance Criteria**:
- 3 predictions in Paris cluster → all boosted to "High"
- Outlier predictions not affected
- Reliability message shows cluster info

---

### 🔄 PredictionProcessor (Priority 6) - 4 hours
**Why**: Orchestrates cache → EXIF → API → clustering pipeline

- [ ] **Create orchestration service** (2 hours)
  - File: `Services/PredictionProcessor.cs` (new)
  - Method: `ProcessImageAsync(string filePath)` → `EnhancedPredictionResult`
  - Pipeline:
    1. Check cache (instant return if hit)
    2. Extract EXIF GPS (instant return if found)
    3. Call GeoCLIP API (5-10 seconds)
    4. Run cluster analysis
    5. Save to cache
    6. Return result

- [ ] **Progress reporting** (1 hour)
  - Parameter: `IProgress<ProcessingStage>`
  - Stages: "Checking cache", "Extracting EXIF", "Running AI", etc.
  - UI: Update status text for each stage

- [ ] **Batch processing** (1 hour)
  - Method: `ProcessBatchAsync(List<string> filePaths)`
  - Logic: Process images in parallel (max 3 concurrent)
  - Return: Results as they complete

**Acceptance Criteria**:
- Single image: Cache → EXIF → API → Cluster works
- Batch: 10 images processed in ~20 seconds (parallelized)
- Progress shown for each stage
- Errors don't stop other images

---

### 💾 ExportService (Priority 7) - 6 hours
**Why**: Users need to save results for reports/analysis

#### CSV Export (2 hours)
- [ ] **Install CsvHelper** (5 min)
  - Package: `CsvHelper` v30.0.1+
  - NuGet: `dotnet add package CsvHelper`

- [ ] **Implement CSV export** (1.5 hours)
  - File: `Services/ExportService.cs` (new)
  - Method: `ExportToCsvAsync(List<EnhancedPredictionResult>)`
  - Columns: Image path, Rank, Lat, Lon, City, State, Country, Probability, Confidence
  - Save: Via `FileSavePicker`

- [ ] **Wire to UI** (30 min)
  - Update: `ExportSelection_Click` in MainPage
  - Show: Success message after export
  - Test: Export 5 predictions, open in Excel

#### PDF Export (4 hours)
- [ ] **Install QuestPDF** (5 min)
  - Package: `QuestPDF` v2024.3.0+
  - NuGet: `dotnet add package QuestPDF`

- [ ] **Design PDF layout** (1 hour)
  - Template: Header (logo, title), table, map thumbnails
  - Style: Dark theme matching app
  - Fonts: Segoe UI

- [ ] **Implement PDF export** (2 hours)
  - Method: `ExportToPdfAsync(List<EnhancedPredictionResult>)`
  - Content: Same as CSV but formatted nicely
  - Images: Embed thumbnails, globe screenshot

- [ ] **Wire to UI** (1 hour)
  - Add: "Export PDF" button
  - Show: PDF preview before save
  - Test: Generate PDF, verify formatting

**Acceptance Criteria**:
- CSV opens correctly in Excel/Google Sheets
- PDF looks professional, matches dark theme
- Export buttons functional, not placeholders

---

## 🚀 Advanced Features (Week 3)

### 🔥 PredictionHeatmapGenerator (Priority 8) - 5 hours
**When**: After multi-image processing works

- [ ] **360×180 grid generation** (2 hours)
  - File: `Services/PredictionHeatmapGenerator.cs` (new)
  - Method: `GenerateHeatmap(List<EnhancedPredictionResult>)` → `HeatmapData`
  - Grid: 1° resolution, lat -90 to 90, lon -180 to 180

- [ ] **Gaussian kernel smoothing** (2 hours)
  - Kernel: 5×5 Gaussian blur
  - Weight: By prediction probability
  - Normalize: Scale to 0.0-1.0 range

- [ ] **Hotspot detection** (1 hour)
  - Threshold: >80% of max intensity
  - Output: List of hotspot centers with radius

**Acceptance Criteria**:
- 50 images → smooth heatmap
- Hotspots detected correctly
- Export heatmap as JSON for globe rendering

---

### 🌐 Globe Heatmap Rendering (Priority 9) - 4 hours
**When**: After heatmap generator complete

- [ ] **Update globe_dark.html** (2 hours)
  - File: `Assets/Globe/globe_dark.html`
  - Add: `addHeatmapLayer(heatmapData)` function
  - Library: Globe.GL hexBin layer
  - Colors: Red (hot) → Blue (cold)

- [ ] **Heatmap toggle UI** (1 hour)
  - File: `Views/MainPage.xaml`
  - Add: Toggle switch "Show Heatmap"
  - Action: Switch between pins and heatmap

- [ ] **Wire to WebView2** (1 hour)
  - Method: `IMapProvider.SetHeatmapAsync(HeatmapData)`
  - Call: JavaScript interop to update layer
  - Test: 50+ predictions, toggle on/off

**Acceptance Criteria**:
- Heatmap renders smoothly on globe
- Toggle switches without flickering
- Hotspots highlighted

---

## ✅ Testing & Quality (Week 3-4)

### 🧪 Unit Tests (Priority 10) - 12 hours
**When**: After services implemented

- [ ] **Set up test project** (1 hour)
  - Project: `GeoLens.Tests` (xUnit)
  - Packages: xUnit, Moq, FluentAssertions
  - Structure: Mirror main project folders

- [ ] **Service tests** (8 hours)
  - `HardwareDetectionServiceTests.cs` (1 hour)
    - Test: GPU detection on different systems
    - Test: Fallback to CPU on error

  - `PythonRuntimeManagerTests.cs` (2 hours)
    - Test: Start/stop lifecycle
    - Test: Health check timeout
    - Mock: Process creation

  - `GeoCLIPApiClientTests.cs` (2 hours)
    - Test: Single/batch inference
    - Test: Error handling
    - Mock: HTTP responses

  - `PredictionCacheServiceTests.cs` (1 hour)
    - Test: Cache hit/miss
    - Test: XXHash collision handling

  - `ExifMetadataExtractorTests.cs` (1 hour)
    - Test: GPS extraction
    - Test: Missing EXIF handling

  - `GeographicClusterAnalyzerTests.cs` (1 hour)
    - Test: Haversine distance accuracy
    - Test: Cluster detection (within 100km)

- [ ] **Model tests** (2 hours)
  - `ImageQueueItemTests.cs`
  - `EnhancedLocationPredictionTests.cs`
  - Property change notifications

- [ ] **CI integration** (1 hour)
  - GitHub Actions: Run tests on push
  - Fail build if tests fail

**Acceptance Criteria**:
- >80% code coverage for services
- All tests pass on CI
- Mocked external dependencies (no real API calls)

---

### 🔬 Integration Tests (Priority 11) - 6 hours

- [ ] **End-to-end pipeline test** (3 hours)
  - Test: Real image → cache → EXIF → API → cluster → display
  - Setup: Test Python service with mock model
  - Assert: Pipeline completes in <10 seconds

- [ ] **Multi-image batch test** (2 hours)
  - Test: 10 images processed in parallel
  - Assert: All results returned, no crashes

- [ ] **Export integration test** (1 hour)
  - Test: Predictions → CSV/PDF export
  - Assert: Files created, valid format

**Acceptance Criteria**:
- Full pipeline tested with real images
- Integration tests in separate CI job
- Test data checked into repo (small images)

---

### 🎨 Polish & UX (Priority 12) - 10 hours

- [ ] **Loading states** (3 hours)
  - Add: Shimmer effect while loading
  - Add: Skeleton screens for predictions
  - Add: Progress spinners with percentages

- [ ] **Error messages** (2 hours)
  - Replace: Debug.WriteLine with user dialogs
  - Add: Actionable error messages ("Try again", "Check settings")
  - Test: Unplug network, corrupt files, kill Python

- [ ] **Settings page** (5 hours)
  - UI: API port, device override, cache location
  - UI: Model path, offline mode toggle
  - Persistence: JSON settings file
  - Test: Change settings, restart app, verify loaded

**Acceptance Criteria**:
- No "hanging" UI states
- Errors guide user to solution
- Settings persist across sessions

---

## 📦 Deployment (Week 4+)

### 🏗️ CI/CD Pipeline (Priority 13) - 8 hours

- [ ] **Build workflow** (4 hours)
  - File: `.github/workflows/build.yml`
  - Triggers: Push to main, PR
  - Steps:
    1. Checkout code
    2. Setup .NET 9
    3. Restore NuGet packages
    4. Build x64 + ARM64
    5. Upload artifacts

- [ ] **Test workflow** (2 hours)
  - File: `.github/workflows/test.yml`
  - Triggers: On build success
  - Steps:
    1. Run unit tests
    2. Run integration tests
    3. Generate coverage report
    4. Fail if coverage <70%

- [ ] **Release workflow** (2 hours)
  - File: `.github/workflows/release.yml`
  - Triggers: Tag push (v*.*.*)
  - Steps:
    1. Build release
    2. Create MSIX installer
    3. Upload to GitHub Releases
    4. Generate changelog

**Acceptance Criteria**:
- Every push builds on CI
- PRs blocked if tests fail
- Releases automated on tag push

---

### 📥 MSIX Packaging (Priority 14) - 6 hours

- [ ] **Configure packaging** (2 hours)
  - Update: `GeoLens.csproj` with MSIX properties
  - Create: Package.appxmanifest
  - Add: App icons, splash screen

- [ ] **Code signing** (2 hours)
  - Generate: Self-signed certificate (dev)
  - Setup: GitHub Secrets for production cert
  - Sign: During release build

- [ ] **Installer testing** (2 hours)
  - Test: Install on clean Windows 11 VM
  - Test: Upgrade from previous version
  - Test: Uninstall cleanup

**Acceptance Criteria**:
- MSIX installs without errors
- App appears in Start Menu
- Uninstall removes all files

---

### 🐍 Embed Python Runtimes (Priority 15) - 6 hours

- [ ] **Download runtimes** (2 hours)
  - Script: `Scripts/PrepareRuntimes.ps1` (already exists)
  - Download: Python 3.11 embeddable (CPU/CUDA/ROCm)
  - Size: ~500MB each
  - Location: `Runtimes/python_{cpu,cuda,rocm}/`

- [ ] **Pre-install dependencies** (2 hours)
  - Script: Install packages into each runtime
  - Verify: `pip list` shows all requirements
  - Test: `python -m uvicorn --version`

- [ ] **Bundle with installer** (2 hours)
  - Update: MSIX to include Runtimes folder
  - Compression: Use MSIX compression
  - Test: Installed app finds correct runtime

**Acceptance Criteria**:
- App works offline (no internet needed)
- Runtime selected based on GPU
- Total installer size: ~3-4GB

---

### 🤖 Pre-download Models (Priority 16) - 2 hours

- [ ] **Download GeoCLIP models** (1 hour)
  - Script: `Scripts/DownloadModels.ps1` (already exists)
  - Models: GeoCLIP-ViT-L-14 (~1.2GB)
  - Location: `models/geoclip_cache/`

- [ ] **Bundle with installer** (1 hour)
  - Update: MSIX to include models
  - Update: Python service to use bundled models
  - Test: Offline inference works

**Acceptance Criteria**:
- First launch doesn't download anything
- Models loaded from local cache
- Offline mode fully functional

---

## 📈 Success Metrics

### MVP Definition (Week 2)
- [x] User can drag-drop images
- [ ] AI predicts top 5 locations
- [ ] Results cached instantly on repeat
- [ ] EXIF GPS detected and prioritized
- [ ] Globe visualizes all predictions
- [ ] Export to CSV works
- [ ] No crashes on common errors

### Production Definition (Week 4)
- [ ] All MVP features
- [ ] Export to PDF + KML
- [ ] Heatmap for multi-image analysis
- [ ] Settings page functional
- [ ] >80% test coverage
- [ ] MSIX installer
- [ ] CI/CD pipeline
- [ ] Offline mode (no internet needed)

---

## 🗓️ Sprint Planning

### Sprint 1: Fix & Stabilize (Week 1)
**Goal**: Fix all critical bugs, complete backend integration

- Day 1-2: Bug fixes (memory leaks, race conditions, disposal)
- Day 3-4: Backend integration (remove mocks, wire API)
- Day 5: Testing & validation

**Deliverable**: App processes real images end-to-end without crashes

---

### Sprint 2: Core Services (Week 2)
**Goal**: Implement cache, EXIF, clustering, export

- Day 1: PredictionCacheService
- Day 2: ExifMetadataExtractor + GeographicClusterAnalyzer
- Day 3: PredictionProcessor orchestration
- Day 4-5: ExportService (CSV + PDF)

**Deliverable**: Full prediction pipeline with exports

---

### Sprint 3: Testing & Quality (Week 3)
**Goal**: Automated tests, performance optimization

- Day 1-2: Unit tests for all services
- Day 3: Integration tests
- Day 4: Performance profiling & fixes
- Day 5: UX polish (loading states, errors)

**Deliverable**: >80% test coverage, optimized performance

---

### Sprint 4: Deployment (Week 4)
**Goal**: CI/CD, packaging, production release

- Day 1-2: GitHub Actions workflows
- Day 3: MSIX packaging
- Day 4: Runtime + model embedding
- Day 5: Release v1.0.0

**Deliverable**: Installable production app

---

## 📞 Questions & Support

- **Blockers**: Create GitHub issue with "blocked" label
- **Technical questions**: Refer to `Docs/` folder
- **Architecture decisions**: Review `Docs/01_Architecture_Overview.md`
- **Code examples**: See `Docs/02_Service_Implementations.md`

---

**Last Updated**: 2025-11-12
**Next Review**: After Sprint 1 completion
