# GeoLens - Download GeoCLIP Models
# Downloads the GeoCLIP model from Hugging Face for offline distribution

param(
    [string]$OutputDir = ".\Models\geoclip_cache",
    [string]$Runtime = "cpu"
)

$ErrorActionPreference = "Stop"

Write-Host "GeoLens Model Download Script" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""

# Create models directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created models directory: $OutputDir" -ForegroundColor Green
}

# Use one of the prepared runtimes to download the model
$runtimePath = ".\Runtimes\python_$Runtime"
$pythonExe = Join-Path $runtimePath "python.exe"

if (-not (Test-Path $pythonExe)) {
    Write-Host "ERROR: Runtime not found: $runtimePath" -ForegroundColor Red
    Write-Host "Run PrepareRuntimes.ps1 first" -ForegroundColor Yellow
    exit 1
}

Write-Host "Using runtime: $runtimePath" -ForegroundColor Gray
Write-Host "Output directory: $OutputDir" -ForegroundColor Gray
Write-Host ""

# Set environment variable for Hugging Face cache
$env:HF_HOME = $OutputDir
$env:TRANSFORMERS_CACHE = $OutputDir

Write-Host "Downloading GeoCLIP model (this may take several minutes)..." -ForegroundColor Yellow
Write-Host "Model size: ~500 MB" -ForegroundColor Gray
Write-Host ""

# Run smoke test which will trigger model download
$downloadScript = @"
import os
os.environ['HF_HOME'] = r'$OutputDir'
os.environ['TRANSFORMERS_CACHE'] = r'$OutputDir'

print("Initializing GeoCLIP model...")
from geoclip import GeoCLIP

model = GeoCLIP(from_pretrained=True)
print("✓ Model downloaded successfully!")
print(f"Cache location: {os.environ['HF_HOME']}")
"@

$downloadScript | & $pythonExe 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Model download complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    # Get cache size
    $size = (Get-ChildItem $OutputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host "Cache size: $([math]::Round($size, 2)) MB" -ForegroundColor Cyan
    Write-Host "Location: $OutputDir" -ForegroundColor Gray
    Write-Host ""
    Write-Host "The model cache is now ready for bundling into the installer." -ForegroundColor Green
} else {
    Write-Host "ERROR: Model download failed" -ForegroundColor Red
    exit 1
}
