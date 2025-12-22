# GeoLens - Download Globe Assets
# Downloads Three.js, Globe.GL, and NASA textures for offline globe rendering

$ErrorActionPreference = "Stop"

Write-Host "GeoLens Globe Assets Download Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$assetsDir = ".\Assets\Globe"
$libDir = "$assetsDir\lib"
$texturesDir = "$assetsDir\textures"

# Create directories
New-Item -ItemType Directory -Force -Path $libDir | Out-Null
New-Item -ItemType Directory -Force -Path $texturesDir | Out-Null

Write-Host "Directories created:" -ForegroundColor Gray
Write-Host "  - $libDir" -ForegroundColor Gray
Write-Host "  - $texturesDir" -ForegroundColor Gray
Write-Host ""

# Download Three.js
Write-Host "[1/4] Downloading Three.js v0.150.0..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "https://unpkg.com/three@0.150.0/build/three.min.js" `
        -OutFile "$libDir\three.min.js" `
        -UseBasicParsing

    $size = (Get-Item "$libDir\three.min.js").Length / 1KB
    Write-Host "  ✓ Three.js downloaded ($([math]::Round($size, 1)) KB)" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed to download Three.js: $_" -ForegroundColor Red
    exit 1
}

# Download Globe.GL
Write-Host "[2/4] Downloading Globe.GL v2.24.0..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "https://unpkg.com/globe.gl@2.24.0/dist/globe.gl.min.js" `
        -OutFile "$libDir\globe.gl.min.js" `
        -UseBasicParsing

    $size = (Get-Item "$libDir\globe.gl.min.js").Length / 1KB
    Write-Host "  ✓ Globe.GL downloaded ($([math]::Round($size, 1)) KB)" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed to download Globe.GL: $_" -ForegroundColor Red
    exit 1
}

# Download NASA Black Marble texture
Write-Host "[3/4] Downloading NASA Black Marble texture..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "https://unpkg.com/three-globe@2.24.0/example/img/earth-night.jpg" `
        -OutFile "$texturesDir\earth-night.jpg" `
        -UseBasicParsing

    $size = (Get-Item "$texturesDir\earth-night.jpg").Length / 1MB
    Write-Host "  ✓ Earth texture downloaded ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed to download Earth texture: $_" -ForegroundColor Red
    exit 1
}

# Download night sky background
Write-Host "[4/4] Downloading night sky background..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri "https://unpkg.com/three-globe@2.24.0/example/img/night-sky.png" `
        -OutFile "$texturesDir\night-sky.png" `
        -UseBasicParsing

    $size = (Get-Item "$texturesDir\night-sky.png").Length / 1MB
    Write-Host "  ✓ Night sky downloaded ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed to download night sky: $_" -ForegroundColor Red
    exit 1
}

# Calculate total size
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "All assets downloaded successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$totalSize = (Get-ChildItem "$libDir\*", "$texturesDir\*" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "Total size: $([math]::Round($totalSize, 2)) MB" -ForegroundColor Cyan
Write-Host ""

Write-Host "Files downloaded:" -ForegroundColor Gray
Write-Host "  - $libDir\three.min.js" -ForegroundColor Gray
Write-Host "  - $libDir\globe.gl.min.js" -ForegroundColor Gray
Write-Host "  - $texturesDir\earth-night.jpg" -ForegroundColor Gray
Write-Host "  - $texturesDir\night-sky.png" -ForegroundColor Gray
Write-Host ""

Write-Host "The globe is now ready for offline use!" -ForegroundColor Green
Write-Host "Build the project to copy these assets to the output directory." -ForegroundColor Yellow
