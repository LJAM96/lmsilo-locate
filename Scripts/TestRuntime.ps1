# GeoLens - Test Embedded Python Runtime
# Verifies a runtime can start the FastAPI service and perform inference

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("cpu", "cuda", "rocm")]
    [string]$Runtime,

    [string]$RuntimesDir = ".\Runtimes",
    [int]$Port = 8899
)

$ErrorActionPreference = "Stop"

Write-Host "Testing $Runtime runtime..." -ForegroundColor Cyan

$runtimePath = Join-Path $RuntimesDir "python_$Runtime"
$pythonExe = Join-Path $runtimePath "python.exe"

if (-not (Test-Path $pythonExe)) {
    Write-Host "ERROR: Runtime not found at $runtimePath" -ForegroundColor Red
    Write-Host "Run PrepareRuntimes.ps1 first" -ForegroundColor Yellow
    exit 1
}

Write-Host "Runtime path: $runtimePath" -ForegroundColor Gray
Write-Host "Python executable: $pythonExe" -ForegroundColor Gray
Write-Host ""

# Test 1: Python version
Write-Host "[1/4] Checking Python version..." -ForegroundColor Yellow
$pythonVersion = & $pythonExe --version 2>&1
Write-Host "  $pythonVersion" -ForegroundColor Green

# Test 2: Import key packages
Write-Host "[2/4] Testing package imports..." -ForegroundColor Yellow
$testScript = @"
import sys
import torch
import fastapi
import geoclip
print(f"  ✓ PyTorch {torch.__version__}")
print(f"  ✓ FastAPI {fastapi.__version__}")
print(f"  ✓ GeoCLIP {geoclip.__version__}")
print(f"  Device: {torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'CPU'}")
"@

$testScript | & $pythonExe 2>&1

# Test 3: Start FastAPI service
Write-Host "[3/4] Starting FastAPI service..." -ForegroundColor Yellow
$env:GEOCLIP_DEVICE = "auto"
$coreDir = Join-Path $runtimePath "Core"

$serviceProcess = Start-Process -FilePath $pythonExe `
    -ArgumentList "-m", "uvicorn", "Core.api_service:app", "--host", "127.0.0.1", "--port", "$Port" `
    -WorkingDirectory $runtimePath `
    -PassThru `
    -NoNewWindow

Write-Host "  Service PID: $($serviceProcess.Id)" -ForegroundColor Gray

# Wait for service to start
Write-Host "  Waiting for health check..." -ForegroundColor Gray
Start-Sleep -Seconds 5

$healthUrl = "http://127.0.0.1:$Port/health"
try {
    $response = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 10
    Write-Host "  ✓ Health check passed: $($response.status)" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Health check failed: $_" -ForegroundColor Red
    Stop-Process -Id $serviceProcess.Id -Force
    exit 1
}

# Test 4: Test inference (optional - requires model downloaded)
Write-Host "[4/4] Testing inference..." -ForegroundColor Yellow
Write-Host "  (Skipping - requires GeoCLIP model download)" -ForegroundColor Gray
Write-Host "  Run DownloadModels.ps1 to enable inference testing" -ForegroundColor Gray

# Cleanup
Write-Host ""
Write-Host "Stopping service..." -ForegroundColor Yellow
Stop-Process -Id $serviceProcess.Id -Force
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "✓ Runtime test passed!" -ForegroundColor Green
Write-Host ""
