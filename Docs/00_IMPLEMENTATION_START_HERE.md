# 🚀 GeoLens Implementation Guide - START HERE

## 📋 Quick Reference

This is your master implementation document. Follow the phases in order for a smooth build process.

---

## 📚 Documentation Structure

| Document | Purpose | Read When |
|----------|---------|-----------|
| **00_IMPLEMENTATION_START_HERE.md** | **You are here** - Master checklist and order | Start of project |
| 01_Architecture_Overview.md | High-level system design | Planning phase |
| 02_Service_Implementations.md | Detailed service code | Implementing services |
| 03_Dark_Mode_Maps.md | Map visualization specs | Implementing map view |
| 04_UI_Wireframes.md | Layout and design specs | Implementing UI |
| 05_Heatmap_MultiImage.md | Heatmap system design | Implementing heatmap |
| 06_Implementation_Roadmap.md | Phased timeline (8-10 weeks) | Project planning |
| 07_Fluent_UI_Design.md | **Windows 11 Fluent Design** | Implementing UI |
| 08_EXIF_Metadata_System.md | EXIF extraction and display | Implementing metadata |

---

## 🎯 Implementation Order

### **Phase 0: Project Setup (Day 1)**

#### ✅ Checklist
- [ ] Create new WinUI 3 project (net9.0-windows10.0.19041.0)
- [ ] Add required NuGet packages
- [ ] Create folder structure
- [ ] Configure Release mode fix (Optimize=false)
- [ ] Set up Git .gitignore
- [ ] Test build

#### 📦 NuGet Packages

```xml
<ItemGroup>
  <!-- Core -->
  <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.250916003" />
  <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.*" />
  <PackageReference Include="System.Management" Version="9.0.0" />

  <!-- Database -->
  <PackageReference Include="System.Data.SQLite.Core" Version="1.0.118" />

  <!-- Export -->
  <PackageReference Include="CsvHelper" Version="30.0.1" />
  <PackageReference Include="QuestPDF" Version="2024.3.0" />

  <!-- Hashing -->
  <PackageReference Include="System.IO.Hashing" Version="9.0.0" />

  <!-- 3D Rendering -->
  <PackageReference Include="Microsoft.Graphics.Win2D" Version="1.2.0" />

  <!-- JSON -->
  <PackageReference Include="System.Net.Http.Json" Version="9.0.0" />
</ItemGroup>
```

#### 📁 Folder Structure

```
GeoLens/
├── Services/
│   ├── HardwareDetectionService.cs
│   ├── PythonRuntimeManager.cs
│   ├── GeoCLIPApiClient.cs
│   ├── PredictionCacheService.cs
│   ├── ExifMetadataExtractor.cs
│   ├── GeographicClusterAnalyzer.cs
│   ├── PredictionProcessor.cs
│   ├── ExportService.cs
│   ├── PredictionHeatmapGenerator.cs
│   └── MapProviders/
│       ├── IMapProvider.cs
│       ├── WebGlobe3DProvider.cs
│       └── Win2DGlobe3DProvider.cs
├── Models/
│   ├── ExifMetadata.cs
│   ├── LocationPrediction.cs
│   ├── EnhancedPredictionResult.cs
│   ├── HeatmapData.cs
│   ├── ConfidenceLevel.cs
│   └── UserSettings.cs
├── ViewModels/
│   ├── ImageQueueItem.cs
│   └── MainPageViewModel.cs
├── Views/
│   ├── MainPage.xaml
│   ├── MainPage.xaml.cs
│   ├── SettingsPage.xaml
│   ├── SettingsPage.xaml.cs
│   └── ExifMetadataPanel.xaml
├── Assets/
│   ├── Globe/
│   │   ├── globe_dark.html
│   │   ├── three.min.js
│   │   └── globe.gl.min.js
│   └── Maps/
│       └── (map tiles - added later)
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml
└── MainWindow.xaml.cs
```

#### ⚙️ Project Configuration

**GeoLens.csproj** - Add these fixes:

```xml
<PropertyGroup>
  <!-- Fix Release mode COM activation -->
  <Optimize Condition="'$(Configuration)' == 'Release'">false</Optimize>
  <DebugType Condition="'$(Configuration)' == 'Release'">portable</DebugType>
  <WindowsAppSDKSelfContained>false</WindowsAppSDKSelfContained>
</PropertyGroup>
```

---

### **Phase 1: Foundation - Backend Services (Week 1)**

**Reference**: `02_Service_Implementations.md`

#### Task 1.1: Hardware Detection (Day 1-2)
- [ ] Create `Services/HardwareDetectionService.cs`
- [ ] Implement WMI GPU detection
- [ ] Implement nvidia-smi fallback
- [ ] Test on NVIDIA, AMD, and CPU-only machines
- [ ] Create unit tests

**File**: `Services/HardwareDetectionService.cs` (see doc 02, section 1)

#### Task 1.2: Python Runtime Manager (Day 2-3)
- [ ] Create `Services/PythonRuntimeManager.cs`
- [ ] Implement service startup (FastAPI/uvicorn)
- [ ] Implement health check polling
- [ ] Implement graceful shutdown
- [ ] Test with mock Python script

**File**: `Services/PythonRuntimeManager.cs` (see doc 02, section 2)

#### Task 1.3: API Client (Day 3-4)
- [ ] Create DTOs for API request/response
- [ ] Create `Services/GeoCLIPApiClient.cs`
- [ ] Implement /health endpoint
- [ ] Implement /infer endpoint
- [ ] Test with Python service running

**File**: `Services/GeoCLIPApiClient.cs` (see doc 02, section 3)

#### Task 1.4: Integration Test (Day 4-5)
- [ ] Start Python service from C#
- [ ] Send test image to API
- [ ] Verify response format
- [ ] Test error handling
- [ ] Document any issues

**Acceptance Criteria:**
- ✅ Hardware detection returns correct type
- ✅ Python service starts and responds to /health
- ✅ API client can send images and receive predictions
- ✅ All unit tests passing

---

### **Phase 2: UI Foundation - Fluent Design (Week 2)**

**Reference**: `07_Fluent_UI_Design.md` + `04_UI_Wireframes.md`

#### Task 2.1: Main Window with Mica (Day 1)
- [ ] Update `App.xaml` with Fluent theme resources
- [ ] Update `MainWindow.xaml` with Mica backdrop
- [ ] Create custom title bar with Acrylic
- [ ] Add CommandBar with Segoe Fluent Icons
- [ ] Test dark theme

**File**: `MainWindow.xaml` (see doc 07, Main Window section)

#### Task 2.2: 3-Panel Layout (Day 2)
- [ ] Create `Views/MainPage.xaml`
- [ ] Implement 3-column Grid (320px | * | 400px)
- [ ] Add Acrylic to left and right panels
- [ ] Add placeholder content
- [ ] Test responsive resizing

**File**: `Views/MainPage.xaml` (see doc 04, Main Window Layout)

#### Task 2.3: Image Queue Panel (Day 3-4)
- [ ] Create `ViewModels/ImageQueueItem.cs`
- [ ] Implement GridView with card template
- [ ] Add drag-and-drop support
- [ ] Implement thumbnail generation
- [ ] Add multi-selection UI
- [ ] Add status badges with glyphs

**File**: `Views/MainPage.xaml` (see doc 07, Left Panel section)

#### Task 2.4: Results Panel (Day 4-5)
- [ ] Create prediction card template
- [ ] Add Expander for collapsible sections
- [ ] Add confidence badges
- [ ] Add export buttons with glyphs
- [ ] Test with mock data

**File**: `Views/MainPage.xaml` (see doc 07, Right Panel section)

#### Task 2.5: Wire Up Prediction Flow (Day 5)
- [ ] Connect Add Images button to file picker
- [ ] Implement ProcessImageAsync method
- [ ] Call API client and display results
- [ ] Add error handling with InfoBar
- [ ] Test end-to-end flow

**Acceptance Criteria:**
- ✅ Fluent Design with Mica/Acrylic renders correctly
- ✅ All icons use Segoe Fluent Icons glyphs
- ✅ Image queue displays thumbnails
- ✅ Real predictions from API show in results panel
- ✅ UI is fully responsive

---

### **Phase 3: Visualization - 3D Globe (Week 3)**

**Reference**: `03_Dark_Mode_Maps.md` + `07_Fluent_UI_Design.md`

#### Task 3.1: Download Assets (Day 1)
- [ ] Download NASA Black Marble 8K (45MB)
- [ ] Download three.js and globe.gl
- [ ] Save to `Assets/Globe/`
- [ ] Test file loading

**Links**:
- NASA: https://visibleearth.nasa.gov/images/144898
- Three.js: https://threejs.org/build/three.min.js
- Globe.gl: https://unpkg.com/globe.gl/dist/globe.gl.min.js

#### Task 3.2: Globe HTML (Day 2-3)
- [ ] Create `Assets/Globe/globe_dark.html`
- [ ] Implement globe initialization with dark textures
- [ ] Implement addPin JavaScript function
- [ ] Implement rotateToPin function
- [ ] Add tooltips with dark theme
- [ ] Test in browser standalone

**File**: `Assets/Globe/globe_dark.html` (see doc 03, section 1)

#### Task 3.3: C# Map Provider (Day 3-4)
- [ ] Create `Services/MapProviders/IMapProvider.cs`
- [ ] Create `Services/MapProviders/WebGlobe3DProvider.cs`
- [ ] Implement InitializeAsync (load HTML in WebView2)
- [ ] Implement AddPinAsync (execute JavaScript)
- [ ] Implement RotateToPinAsync
- [ ] Test C# ↔ JavaScript communication

**File**: `Services/MapProviders/WebGlobe3DProvider.cs` (see doc 03, section 1)

#### Task 3.4: Integration (Day 4-5)
- [ ] Add WebView2 to center panel
- [ ] Initialize map provider on page load
- [ ] Add pins after prediction complete
- [ ] Color pins by confidence level
- [ ] Add EXIF GPS pins (cyan, larger)
- [ ] Implement "View on Map" button

**Acceptance Criteria:**
- ✅ Dark globe renders with NASA textures
- ✅ Pins appear after API prediction
- ✅ Confidence colors match design (green/yellow/red/cyan)
- ✅ Smooth rotation animations work
- ✅ No JavaScript errors in console

---

### **Phase 4: Advanced Features (Week 4)**

**Reference**: `02_Service_Implementations.md` + `08_EXIF_Metadata_System.md`

#### Task 4.1: EXIF Metadata Extractor (Day 1-2)
- [ ] Create `Services/ExifMetadataExtractor.cs`
- [ ] Implement GPS extraction
- [ ] Implement camera info extraction
- [ ] Implement capture settings extraction
- [ ] Implement image details extraction
- [ ] Test with various image formats

**File**: `Services/ExifMetadataExtractor.cs` (see doc 08, Complete EXIF Extractor)

#### Task 4.2: EXIF Display Panel (Day 2-3)
- [ ] Create `Views/ExifMetadataPanel.xaml`
- [ ] Design collapsible sections
- [ ] Add icons for each metadata type
- [ ] Implement visibility logic
- [ ] Test with images with/without EXIF

**File**: `Views/ExifMetadataPanel.xaml` (see doc 08, UI Display Component)

#### Task 4.3: Prediction Cache (Day 3-4)
- [ ] Create `Services/PredictionCacheService.cs`
- [ ] Implement SQLite database
- [ ] Implement XXHash64 computation
- [ ] Implement cache lookup/save
- [ ] Implement statistics and cleanup
- [ ] Test cache hit/miss performance

**File**: `Services/PredictionCacheService.cs` (see doc 02, section 4)

#### Task 4.4: Geographic Clustering (Day 4)
- [ ] Create `Services/GeographicClusterAnalyzer.cs`
- [ ] Implement Haversine distance calculation
- [ ] Implement clustering detection (100km radius)
- [ ] Calculate confidence boost
- [ ] Find cluster center
- [ ] Test with clustered predictions

**File**: `Services/GeographicClusterAnalyzer.cs` (see doc 02, section 6)

#### Task 4.5: Confidence System (Day 5)
- [ ] Create `Models/ConfidenceLevel.cs`
- [ ] Implement confidence classification
- [ ] Add color/icon mappings
- [ ] Update prediction cards with confidence badges
- [ ] Test all confidence levels

#### Task 4.6: Enhanced Processor (Day 5)
- [ ] Create `Services/PredictionProcessor.cs`
- [ ] Integrate cache check
- [ ] Integrate EXIF extraction
- [ ] Integrate clustering analysis
- [ ] Save results to cache
- [ ] Test full pipeline

**File**: `Services/PredictionProcessor.cs` (see doc 02, section 7)

**Acceptance Criteria:**
- ✅ EXIF metadata displays correctly
- ✅ Cache provides instant recall (<100ms)
- ✅ Clustering boosts confidence
- ✅ Confidence badges show correct colors
- ✅ Full pipeline works end-to-end

---

### **Phase 5: Multi-Image Heatmap (Week 5)**

**Reference**: `05_Heatmap_MultiImage.md`

#### Task 5.1: Heatmap Generator (Day 1-2)
- [ ] Create `Services/PredictionHeatmapGenerator.cs`
- [ ] Implement 360×180 grid initialization
- [ ] Implement Gaussian kernel smoothing
- [ ] Implement normalization
- [ ] Implement hotspot detection
- [ ] Test with 100 predictions

**File**: `Services/PredictionHeatmapGenerator.cs` (see doc 05, section 2)

#### Task 5.2: Heatmap Visualization (Day 2-3)
- [ ] Add hexBin functions to `globe_dark.html`
- [ ] Implement color gradient
- [ ] Implement tooltips
- [ ] Implement toggle between pins/heatmap
- [ ] Test visualization

**File**: `Assets/Globe/globe_dark.html` (see doc 05, section 3)

#### Task 5.3: Multi-Selection UI (Day 3-4)
- [ ] Add selection toolbar to image queue
- [ ] Add "Select All" checkbox
- [ ] Add heatmap toggle button
- [ ] Add "Overlay All" button
- [ ] Update status bar with counts

**File**: `Views/MainPage.xaml` (see doc 05, section 4)

#### Task 5.4: Integration (Day 4-5)
- [ ] Implement multi-selection handling
- [ ] Generate heatmap on selection change
- [ ] Send heatmap data to map provider
- [ ] Display hotspot info
- [ ] Test with multiple images

**Acceptance Criteria:**
- ✅ Heatmap generates in <500ms
- ✅ Toggle switches smoothly between pins/heatmap
- ✅ Hotspots detected correctly
- ✅ Multi-selection UI works
- ✅ Heatmap updates in real-time

---

### **Phase 6: Export & Settings (Week 6)**

**Reference**: `02_Service_Implementations.md` + `07_Fluent_UI_Design.md`

#### Task 6.1: Export Service (Day 1-3)
- [ ] Create `Services/ExportService.cs`
- [ ] Implement CSV export (CsvHelper)
- [ ] Implement PDF export (QuestPDF)
- [ ] Implement KML export (XML)
- [ ] Add file save dialogs
- [ ] Test all export formats

**File**: `Services/ExportService.cs` (see doc 02, section 9)

#### Task 6.2: Settings Page (Day 3-4)
- [ ] Create `Views/SettingsPage.xaml`
- [ ] Add cache settings section
- [ ] Add map mode selector
- [ ] Add prediction settings
- [ ] Add thumbnail size selector
- [ ] Implement settings persistence (JSON)

**File**: `Views/SettingsPage.xaml` (see doc 07, Settings section)

#### Task 6.3: Performance Optimization (Day 4-5)
- [ ] Add thumbnail caching
- [ ] Virtualize image queue (ItemsRepeater)
- [ ] Lazy-load map tiles
- [ ] Debounce search inputs
- [ ] Profile memory usage
- [ ] Optimize database queries

#### Task 6.4: Error Handling (Day 5)
- [ ] Add try-catch blocks everywhere
- [ ] Display user-friendly error messages
- [ ] Log errors to file
- [ ] Handle Python service crashes
- [ ] Test error scenarios

**Acceptance Criteria:**
- ✅ All export formats work correctly
- ✅ Settings persist across sessions
- ✅ App launches in <3 seconds
- ✅ Memory usage <500MB
- ✅ No unhandled exceptions

---

### **Phase 7: Distribution (Week 7-8)**

**Reference**: `01_Architecture_Overview.md` + `06_Implementation_Roadmap.md`

#### Task 7.1: Prepare Python Runtimes (Day 1-2)
- [ ] Download Python embeddable 3.11
- [ ] Create 3 runtime folders (cpu/cuda/rocm)
- [ ] Install dependencies in each
- [ ] Test each runtime standalone
- [ ] Document sizes

**Commands**:
```bash
# CPU
pip install -r core/requirements-cpu.txt --target runtime/python_cpu/Lib/site-packages

# CUDA
pip install -r core/requirements-cuda.txt --target runtime/python_cuda/Lib/site-packages

# ROCm
pip install -r core/requirements-rocm.txt --target runtime/python_rocm/Lib/site-packages
```

#### Task 7.2: Download Models (Day 2)
- [ ] Run smoke test to trigger model download
- [ ] Copy models from cache to `models/geoclip_cache/`
- [ ] Test offline loading
- [ ] Document size (~500MB)

#### Task 7.3: Prepare Map Assets (Day 3-4)
- [ ] Download NASA Black Marble
- [ ] Download dark map tiles (zoom 0-8)
- [ ] Convert to MBTiles format
- [ ] Test offline rendering
- [ ] Document size (~500MB)

#### Task 7.4: Create Installer (Day 4-5)
- [ ] Install Inno Setup
- [ ] Create `installer/setup.iss` script
- [ ] Add all files to installer
- [ ] Set compression (LZMA2)
- [ ] Build installer
- [ ] Test on fresh Windows

#### Task 7.5: Final Testing (Day 6-7)
- [ ] Install on clean Windows 10
- [ ] Install on clean Windows 11
- [ ] Test hardware detection
- [ ] Test offline mode
- [ ] Test all features
- [ ] Document any issues

**Acceptance Criteria:**
- ✅ Single installer (.exe) created
- ✅ Size is 3-4GB compressed
- ✅ Installs correctly on fresh Windows
- ✅ All features work after install
- ✅ Uninstaller removes all files

---

## 🧪 Testing Strategy

### Unit Tests (Throughout)
- [ ] HardwareDetectionService tests
- [ ] PredictionCacheService tests
- [ ] GeographicClusterAnalyzer tests
- [ ] ConfidenceHelper tests
- [ ] ExifMetadataExtractor tests

### Integration Tests
- [ ] Full prediction pipeline
- [ ] Python service startup/shutdown
- [ ] Map provider switching
- [ ] Export formats

### Manual Testing
- [ ] Test on NVIDIA GPU machine
- [ ] Test on AMD GPU machine
- [ ] Test on CPU-only machine
- [ ] Test with 100+ images
- [ ] Test offline mode
- [ ] Test all export formats

---

## 📊 Progress Tracking

### Current Phase: ⬜ Not Started

| Phase | Status | Duration | Completion |
|-------|--------|----------|------------|
| Phase 0: Setup | ⬜ | 1 day | 0% |
| Phase 1: Foundation | ⬜ | 5-7 days | 0% |
| Phase 2: UI Foundation | ⬜ | 7-10 days | 0% |
| Phase 3: Visualization | ⬜ | 7-10 days | 0% |
| Phase 4: Advanced Features | ⬜ | 10-12 days | 0% |
| Phase 5: Heatmap | ⬜ | 7-10 days | 0% |
| Phase 6: Export & Settings | ⬜ | 10-12 days | 0% |
| Phase 7: Distribution | ⬜ | 10-14 days | 0% |

### Legend
- ⬜ Not Started
- 🟡 In Progress
- ✅ Complete
- ❌ Blocked

---

## 🚨 Common Issues & Solutions

### Issue: Python service won't start
**Solution**: Check that python.exe exists in runtime folder, check firewall, check port 8899 not in use

### Issue: WebView2 not rendering
**Solution**: Install WebView2 runtime, check HTML file path, check JavaScript console

### Issue: Mica not showing
**Solution**: Check Windows 11 version, ensure SystemBackdrop set, check dark theme enabled

### Issue: EXIF data not extracting
**Solution**: Check image format (JPEG/HEIC), verify EXIF property IDs, handle missing properties

### Issue: Memory usage high
**Solution**: Virtualize image queue, clear old predictions, optimize thumbnail caching

---

## 📞 Support

If you encounter issues:
1. Check relevant documentation file
2. Review code examples in `02_Service_Implementations.md`
3. Check GitHub issues (if available)
4. Review Microsoft WinUI 3 documentation

---

## ✅ Final Checklist

Before considering project complete:

- [ ] All features from architecture overview implemented
- [ ] All unit tests passing
- [ ] Manual testing complete on all hardware types
- [ ] Installer builds successfully
- [ ] Installation tested on fresh Windows
- [ ] Documentation updated with any changes
- [ ] Performance benchmarks met
- [ ] No known critical bugs
- [ ] User guide written (optional)
- [ ] Release notes prepared

---

## 🎉 You're Ready to Start!

**Begin with Phase 0: Project Setup**

1. Create new WinUI 3 project
2. Add NuGet packages
3. Create folder structure
4. Move to Phase 1: Foundation

Good luck! 🚀
