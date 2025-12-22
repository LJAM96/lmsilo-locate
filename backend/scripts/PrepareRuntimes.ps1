# GeoLens - Prepare Embedded Python Runtimes
# This script creates three embedded Python environments for distribution
# Run this ONCE before creating the installer

param(
    [string]$OutputDir = ".\Runtimes",
    [string]$PythonVersion = "3.11.8",
    [switch]$SkipDownload,
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"

Write-Host "GeoLens Runtime Preparation Script" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$pythonEmbedUrl = "https://www.python.org/ftp/python/$PythonVersion/python-$PythonVersion-embed-amd64.zip"
$getpipUrl = "https://bootstrap.pypa.io/get-pip.py"

$runtimes = @{
    "cpu" = @{
        "Name" = "CPU-Only Runtime"
        "Dir" = Join-Path $OutputDir "python_cpu"
        "Requirements" = ".\Core\requirements-cpu.txt"
        "ExpectedSize" = "~800 MB"
    }
    "cuda" = @{
        "Name" = "NVIDIA CUDA Runtime"
        "Dir" = Join-Path $OutputDir "python_cuda"
        "Requirements" = ".\Core\requirements-cuda.txt"
        "ExpectedSize" = "~3 GB"
    }
    "rocm" = @{
        "Name" = "AMD ROCm Runtime"
        "Dir" = Join-Path $OutputDir "python_rocm"
        "Requirements" = ".\Core\requirements-rocm.txt"
        "ExpectedSize" = "~2.5 GB"
    }
}

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    Write-Host "Created output directory: $OutputDir" -ForegroundColor Green
}

$tempDir = Join-Path $OutputDir "temp"
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir | Out-Null
}

# Download Python embeddable package
$pythonZip = Join-Path $tempDir "python-embed.zip"
if (-not $SkipDownload) {
    if (-not (Test-Path $pythonZip)) {
        Write-Host "Downloading Python $PythonVersion embeddable package..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $pythonEmbedUrl -OutFile $pythonZip
        Write-Host "Downloaded: $pythonZip" -ForegroundColor Green
    } else {
        Write-Host "Python package already downloaded: $pythonZip" -ForegroundColor Gray
    }
}

# Download get-pip.py
$getPipScript = Join-Path $tempDir "get-pip.py"
if (-not $SkipDownload) {
    if (-not (Test-Path $getPipScript)) {
        Write-Host "Downloading get-pip.py..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri $getpipUrl -OutFile $getPipScript
        Write-Host "Downloaded: $getPipScript" -ForegroundColor Green
    } else {
        Write-Host "get-pip.py already downloaded" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Creating embedded Python runtimes..." -ForegroundColor Cyan
Write-Host ""

foreach ($key in $runtimes.Keys) {
    $runtime = $runtimes[$key]
    Write-Host "[$($runtime.Name)]" -ForegroundColor Magenta
    Write-Host "  Target: $($runtime.Dir)" -ForegroundColor Gray
    Write-Host "  Expected Size: $($runtime.ExpectedSize)" -ForegroundColor Gray

    # Create runtime directory
    if (Test-Path $runtime.Dir) {
        Write-Host "  Cleaning existing runtime..." -ForegroundColor Yellow
        Remove-Item -Recurse -Force $runtime.Dir
    }
    New-Item -ItemType Directory -Path $runtime.Dir | Out-Null

    if (-not $SkipInstall) {
        # Extract Python
        Write-Host "  Extracting Python..." -ForegroundColor Yellow
        Expand-Archive -Path $pythonZip -DestinationPath $runtime.Dir -Force

        # Enable site-packages (required for pip)
        $pthFile = Get-ChildItem -Path $runtime.Dir -Filter "python*._pth" | Select-Object -First 1
        if ($pthFile) {
            $content = Get-Content $pthFile.FullName
            $content = $content -replace "#import site", "import site"
            Set-Content -Path $pthFile.FullName -Value $content
            Write-Host "  Enabled site-packages" -ForegroundColor Green
        }

        # Install pip
        Write-Host "  Installing pip..." -ForegroundColor Yellow
        $pythonExe = Join-Path $runtime.Dir "python.exe"
        & $pythonExe $getPipScript --no-warn-script-location 2>&1 | Out-Null

        # Upgrade pip
        & $pythonExe -m pip install --upgrade pip --quiet 2>&1 | Out-Null
        Write-Host "  Pip installed" -ForegroundColor Green

        # Install requirements
        Write-Host "  Installing Python packages (this may take several minutes)..." -ForegroundColor Yellow
        $reqFile = $runtime.Requirements
        if (Test-Path $reqFile) {
            & $pythonExe -m pip install -r $reqFile --no-warn-script-location
            Write-Host "  Packages installed from $reqFile" -ForegroundColor Green
        } else {
            Write-Host "  WARNING: Requirements file not found: $reqFile" -ForegroundColor Red
        }

        # Copy Core module
        Write-Host "  Copying Core module..." -ForegroundColor Yellow
        $coreSource = ".\Core"
        $coreTarget = Join-Path $runtime.Dir "Core"
        if (Test-Path $coreSource) {
            Copy-Item -Path $coreSource -Destination $coreTarget -Recurse -Force
            Write-Host "  Core module copied" -ForegroundColor Green
        }

        # Get runtime size
        $size = (Get-ChildItem $runtime.Dir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Host "  Actual Size: $([math]::Round($size, 2)) MB" -ForegroundColor Cyan
    }

    Write-Host "  ✓ Complete" -ForegroundColor Green
    Write-Host ""
}

# Cleanup
if (Test-Path $tempDir) {
    Remove-Item -Recurse -Force $tempDir
    Write-Host "Cleaned up temporary files" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Runtime preparation complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test each runtime: .\Scripts\TestRuntime.ps1 -Runtime cpu" -ForegroundColor Gray
Write-Host "  2. Download models: .\Scripts\DownloadModels.ps1" -ForegroundColor Gray
Write-Host "  3. Build installer: .\Scripts\BuildInstaller.ps1" -ForegroundColor Gray
Write-Host ""
