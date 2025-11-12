# GeoLens Deployment Strategy

## Overview

GeoLens is distributed as a **standalone Windows desktop application** with **embedded Python runtimes**. All AI inference runs locally with no cloud dependency. The app automatically detects hardware and uses the appropriate Python runtime (CPU, CUDA, or ROCm).

---

## Distribution Architecture

### Components

```
GeoLens Installer (~3-4 GB compressed, ~8 GB installed)
├── GeoLens.exe (WinUI3 application, ~50 MB)
├── Runtimes/
│   ├── python_cpu/ (~800 MB)
│   │   ├── python.exe
│   │   ├── Core/ (FastAPI service + GeoCLIP wrapper)
│   │   └── Lib/site-packages/ (PyTorch CPU, FastAPI, GeoCLIP, etc.)
│   ├── python_cuda/ (~3 GB)
│   │   ├── python.exe
│   │   ├── Core/
│   │   └── Lib/site-packages/ (PyTorch CUDA, CUDA libs)
│   └── python_rocm/ (~2.5 GB)
│       ├── python.exe
│       ├── Core/
│       └── Lib/site-packages/ (PyTorch ROCm, ROCm libs)
├── Models/
│   └── geoclip_cache/ (~500 MB - GeoCLIP model weights)
└── Assets/
    └── Globe/ (NASA textures, Three.js, ~45 MB)
```

---

## Build Process

### Prerequisites

- Windows 10/11 x64
- PowerShell 5.1+
- .NET 9 SDK
- Internet connection (for downloading Python + packages)
- ~20 GB free disk space (temp files during build)

### Step 1: Prepare Embedded Python Runtimes

```powershell
# Navigate to project root
cd C:\Projects\GeoLens

# Run runtime preparation script (takes 30-60 minutes)
.\Scripts\PrepareRuntimes.ps1

# What it does:
# 1. Downloads Python 3.11.8 embeddable package
# 2. Creates 3 separate Python environments
# 3. Installs pip in each environment
# 4. Installs dependencies from requirements-cpu/cuda/rocm.txt
# 5. Copies Core/ module into each runtime
```

**Output:**
```
Runtimes/
├── python_cpu/ (800 MB)
├── python_cuda/ (3 GB)
└── python_rocm/ (2.5 GB)
```

### Step 2: Download GeoCLIP Model

```powershell
# Download and cache the GeoCLIP model
.\Scripts\DownloadModels.ps1 -Runtime cpu

# What it does:
# 1. Uses python_cpu runtime to download model
# 2. Saves to Models/geoclip_cache/ (~500 MB)
# 3. Model is now ready for offline use
```

**Output:**
```
Models/
└── geoclip_cache/
    ├── models--MVRL--geoclip/
    └── transformers/ (model weights and config)
```

### Step 3: Test Runtimes (Optional)

```powershell
# Test CPU runtime
.\Scripts\TestRuntime.ps1 -Runtime cpu

# Test CUDA runtime (requires NVIDIA GPU)
.\Scripts\TestRuntime.ps1 -Runtime cuda

# Test ROCm runtime (requires AMD GPU)
.\Scripts\TestRuntime.ps1 -Runtime rocm
```

### Step 4: Build C# Application

```powershell
# Build in Release mode
dotnet build GeoLens.sln -c Release

# Output: bin/Release/net9.0-windows10.0.19041.0/
```

### Step 5: Create Installer (Future - Inno Setup)

```powershell
# Using Inno Setup (to be implemented)
.\Scripts\BuildInstaller.ps1

# What it does:
# 1. Compresses runtimes with LZMA2
# 2. Bundles C# app + runtimes + models + assets
# 3. Creates GeoLens-Setup-v2.4.0.exe (~3-4 GB)
```

---

## Runtime Selection Logic

### At Application Startup

```csharp
// In App.xaml.cs OnLaunched()

// 1. Detect hardware
var hardware = HardwareDetectionService.DetectHardware();
Console.WriteLine($"Detected: {hardware.Description}");

// 2. Select runtime
string runtimePath = hardware.Type switch
{
    HardwareType.NvidiaGpu => Path.Combine(AppDir, "Runtimes", "python_cuda"),
    HardwareType.AmdGpu => Path.Combine(AppDir, "Runtimes", "python_rocm"),
    _ => Path.Combine(AppDir, "Runtimes", "python_cpu")
};

// 3. Start Python service
var pythonManager = new PythonRuntimeManager(runtimePath, port: 8899);
await pythonManager.StartAsync(hardware.DeviceChoice);

// 4. Wait for health check
if (!await pythonManager.WaitForHealthyAsync(TimeoutSeconds: 30))
{
    // Show error to user
    ShowErrorDialog("Failed to start AI service. Please check logs.");
}
```

### Hardware Detection Rules

| Hardware | Detection Method | Runtime Selected |
|----------|------------------|------------------|
| NVIDIA GPU (RTX, GTX) | WMI: `Win32_VideoController` | `python_cuda` |
| AMD GPU (Radeon, RX) | WMI: `Win32_VideoController` | `python_rocm` |
| No GPU / Unknown | Fallback | `python_cpu` |

---

## Installer Requirements

### Minimum System Requirements

```
OS: Windows 10 version 1809 (build 17763) or later
Processor: x64 64-bit processor
RAM: 8 GB (16 GB recommended)
Disk Space: 10 GB free space
Graphics: DirectX 12 capable GPU (optional, for 3D globe)
Runtime: WebView2 (bundled with Windows 11, auto-installed on Windows 10)
```

### Optional GPU Acceleration

```
NVIDIA GPU: GTX 1060 or newer, CUDA 11.8+ drivers
AMD GPU: RX 5000 series or newer, ROCm-compatible drivers
```

---

## Installation Flow

### First-Time Installation

```
1. User runs GeoLens-Setup-v2.4.0.exe
2. Installer extracts files to C:\Program Files\GeoLens\
   - Progress bar shows extraction (~3-4 GB)
3. Creates Start Menu shortcut
4. Creates desktop shortcut (optional)
5. Registers file type associations (.jpg, .png for "Open with GeoLens")
6. Completes in 5-10 minutes
```

### First Launch

```
1. User launches GeoLens
2. App detects hardware (GPU or CPU)
3. Shows splash screen: "Initializing AI service..."
4. Starts appropriate Python runtime
5. Waits for FastAPI service health check
6. Main window appears (~10-15 seconds total)
```

---

## Update Strategy

### Minor Updates (Patches)

For small bug fixes or UI updates:

```
1. User downloads GeoLens-Update-v2.4.1.exe (small, ~50 MB)
2. Replaces only GeoLens.exe and modified DLLs
3. Keeps existing runtimes and models
4. Fast update (~30 seconds)
```

### Major Updates (New Models)

For new GeoCLIP versions or major features:

```
1. User downloads full installer GeoLens-Setup-v2.5.0.exe (~3-4 GB)
2. Uninstalls old version (keeps user settings)
3. Installs new version with updated runtimes/models
4. Imports saved settings from %LOCALAPPDATA%\GeoLens\
```

---

## Offline Capability

### Air-Gapped Deployment

GeoLens can run **100% offline** after installation:

1. ✅ All Python dependencies pre-installed
2. ✅ GeoCLIP model cached locally
3. ✅ Offline map tiles bundled (optional)
4. ✅ No telemetry or internet communication

### Model Cache Location

```
Installation directory:
C:\Program Files\GeoLens\Models\geoclip_cache\

The app sets environment variables:
HF_HOME=C:\Program Files\GeoLens\Models\geoclip_cache
TRANSFORMERS_CACHE=C:\Program Files\GeoLens\Models\geoclip_cache

This ensures GeoCLIP loads from local cache without internet.
```

---

## Disk Space Management

### Installer Size Breakdown

| Component | Compressed | Installed |
|-----------|------------|-----------|
| C# Application | 50 MB | 50 MB |
| CPU Runtime | 250 MB | 800 MB |
| CUDA Runtime | 900 MB | 3 GB |
| ROCm Runtime | 700 MB | 2.5 GB |
| GeoCLIP Model | 150 MB | 500 MB |
| Assets (Globe) | 15 MB | 45 MB |
| **TOTAL** | **~3-4 GB** | **~8 GB** |

### Optional Components

Users can choose to install only what they need:

```
✓ Core Application (required) - 50 MB
✓ CPU Runtime (required) - 800 MB
✓ GeoCLIP Model (required) - 500 MB
□ CUDA Runtime (optional) - 3 GB
□ ROCm Runtime (optional) - 2.5 GB
□ Offline Maps (optional) - 500 MB
```

Minimal install: **~1.5 GB** (CPU-only)
Full install: **~8 GB** (all runtimes + offline maps)

---

## Development vs. Production

### Development Mode

```
# Use local Python environment
python -m uvicorn Core.api_service:app --reload --port 8899

# App connects to localhost:8899
# Hot-reload enabled for Python code
# Easy debugging
```

### Production Mode

```
# App uses embedded runtime
C:\Program Files\GeoLens\Runtimes\python_cuda\python.exe ^
    -m uvicorn Core.api_service:app --port 8899

# No reload, production optimized
# Logs to %LOCALAPPDATA%\GeoLens\logs\
```

---

## Troubleshooting

### Common Issues

**Issue: "Python service failed to start"**
```
Solution:
1. Check Windows Firewall isn't blocking port 8899
2. Verify runtime exists: C:\Program Files\GeoLens\Runtimes\python_cpu\
3. Check logs: %LOCALAPPDATA%\GeoLens\logs\api_service.log
```

**Issue: "CUDA runtime not found" (despite having NVIDIA GPU)**
```
Solution:
1. Install NVIDIA drivers (latest)
2. Verify CUDA 11.8+ installed
3. Reinstall with CUDA runtime selected
```

**Issue: "Model download failed"**
```
Solution:
1. Already cached locally - no internet needed
2. If corrupted, reinstall GeoLens
3. Model at: C:\Program Files\GeoLens\Models\geoclip_cache\
```

---

## Security Considerations

### Code Signing

```
# Sign the installer (requires certificate)
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com GeoLens-Setup.exe

# Sign the main executable
signtool sign /f certificate.pfx /p password GeoLens.exe
```

### User Data

```
User settings: %LOCALAPPDATA%\GeoLens\settings.json
Prediction cache: %LOCALAPPDATA%\GeoLens\cache.db
Logs: %LOCALAPPDATA%\GeoLens\logs\

No data leaves the user's machine.
```

---

## Future Enhancements

1. **Auto-Update System**: Check for updates on startup, download in background
2. **Differential Updates**: Only download changed files
3. **GPU Runtime Downloader**: Ship with CPU, offer GPU download post-install
4. **Portable Version**: Zip file with no installer for USB deployment
5. **Docker Container**: For server/batch processing scenarios

---

This deployment strategy ensures GeoLens can be distributed as a professional, self-contained Windows application with all dependencies bundled.
