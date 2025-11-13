# download_minimal_tiles.ps1
# Downloads minimal map tiles (zoom 0-5) for offline fallback
# Total: ~2,700 tiles, ~50-80 MB

param(
    [string]$OutputDir = "../Assets/Maps/tiles",
    [int]$MaxZoom = 5,
    [string]$TileServer = "https://a.basemaps.cartocdn.com/dark_all"
)

$ErrorActionPreference = "Stop"

Write-Host "=== GeoLens Minimal Tile Downloader ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script downloads minimal map tiles for offline fallback."
Write-Host "Zoom Levels: 0-$MaxZoom"
Write-Host "Tile Server: $TileServer"
Write-Host "Output: $OutputDir"
Write-Host ""

# Calculate total tiles
$totalTiles = 0
for ($z = 0; $z -le $MaxZoom; $z++) {
    $tilesAtZoom = [Math]::Pow(2, $z) * [Math]::Pow(2, $z)
    $totalTiles += $tilesAtZoom
    Write-Host "  Zoom $z : $tilesAtZoom tiles"
}
Write-Host ""
Write-Host "Total tiles to download: $totalTiles" -ForegroundColor Yellow
Write-Host "Estimated size: 50-80 MB" -ForegroundColor Yellow
Write-Host "Estimated time: 5-10 minutes" -ForegroundColor Yellow
Write-Host ""

# Confirm before proceeding
$confirm = Read-Host "Continue? (Y/N)"
if ($confirm -ne "Y" -and $confirm -ne "y") {
    Write-Host "Aborted." -ForegroundColor Red
    exit 1
}

# Create output directory
$tilesPath = Join-Path $PSScriptRoot $OutputDir
Write-Host ""
Write-Host "Creating directory: $tilesPath" -ForegroundColor Green
New-Item -ItemType Directory -Force -Path $tilesPath | Out-Null

# Download tiles
$downloaded = 0
$failed = 0
$startTime = Get-Date

for ($z = 0; $z -le $MaxZoom; $z++) {
    $maxTile = [Math]::Pow(2, $z)

    Write-Host ""
    Write-Host "Downloading zoom level $z ($maxTile x $maxTile tiles)..." -ForegroundColor Cyan

    for ($x = 0; $x -lt $maxTile; $x++) {
        # Create directory for this zoom/x
        $zoomDir = Join-Path $tilesPath "$z\$x"
        New-Item -ItemType Directory -Force -Path $zoomDir | Out-Null

        for ($y = 0; $y -lt $maxTile; $y++) {
            $tileUrl = "$TileServer/$z/$x/$y.png"
            $outputFile = Join-Path $zoomDir "$y.png"

            # Skip if already exists
            if (Test-Path $outputFile) {
                $downloaded++
                Write-Progress -Activity "Downloading Tiles" -Status "Zoom $z ($x,$y)" `
                    -PercentComplete (($downloaded / $totalTiles) * 100)
                continue
            }

            # Download with retry
            $retries = 3
            $success = $false

            for ($retry = 0; $retry -lt $retries; $retry++) {
                try {
                    Invoke-WebRequest -Uri $tileUrl -OutFile $outputFile -TimeoutSec 10 -UseBasicParsing | Out-Null
                    $downloaded++
                    $success = $true
                    break
                }
                catch {
                    if ($retry -eq $retries - 1) {
                        Write-Host "  Failed: $z/$x/$y" -ForegroundColor Red
                        $failed++
                    }
                    else {
                        Start-Sleep -Milliseconds 500
                    }
                }
            }

            # Update progress
            Write-Progress -Activity "Downloading Tiles" -Status "Zoom $z ($x,$y) - $downloaded/$totalTiles" `
                -PercentComplete (($downloaded / $totalTiles) * 100)

            # Rate limiting (be nice to the tile server)
            Start-Sleep -Milliseconds 50
        }
    }

    Write-Host "  Completed zoom level $z" -ForegroundColor Green
}

Write-Progress -Activity "Downloading Tiles" -Completed

$endTime = Get-Date
$elapsed = $endTime - $startTime

Write-Host ""
Write-Host "=== Download Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Downloaded: $downloaded tiles" -ForegroundColor Green
Write-Host "Failed: $failed tiles" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host "Time elapsed: $($elapsed.ToString('mm\:ss'))" -ForegroundColor Cyan
Write-Host ""

# Calculate actual size
$totalSize = 0
Get-ChildItem -Path $tilesPath -Recurse -File | ForEach-Object {
    $totalSize += $_.Length
}
$sizeMB = [Math]::Round($totalSize / 1MB, 2)

Write-Host "Total size: $sizeMB MB" -ForegroundColor Yellow
Write-Host "Location: $tilesPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "The tiles are now ready for offline use!" -ForegroundColor Green
Write-Host ""
Write-Host "Note: These are minimal tiles (zoom 0-$MaxZoom)." -ForegroundColor Yellow
Write-Host "When online, the app will stream higher quality tiles (zoom 0-19)." -ForegroundColor Yellow
