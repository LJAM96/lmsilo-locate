#!/usr/bin/env python3
"""
Download minimal offline tiles for GeoLens hybrid map mode.

This script downloads CartoDB Dark Matter tiles for zoom levels 0-8,
which provides world to country-level detail for offline fallback.

Total download: ~100-500MB depending on coverage
"""

import argparse
import os
import sys
import time
from pathlib import Path
from urllib.request import urlopen, Request
from urllib.error import HTTPError, URLError

# Tile server configuration
TILE_SERVER = "https://a.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png"
USER_AGENT = "GeoLens/2.4 (Offline Tile Downloader)"

# Default configuration
DEFAULT_MIN_ZOOM = 0
DEFAULT_MAX_ZOOM = 8
DEFAULT_OUTPUT_DIR = Path(__file__).parent.parent / "Assets" / "Maps" / "tiles"

def num_tiles_for_zoom(zoom):
    """Calculate number of tiles at a given zoom level."""
    return 2 ** (zoom * 2)

def total_tiles(min_zoom, max_zoom):
    """Calculate total number of tiles across zoom levels."""
    return sum(num_tiles_for_zoom(z) for z in range(min_zoom, max_zoom + 1))

def format_size(bytes):
    """Format bytes as human-readable size."""
    for unit in ['B', 'KB', 'MB', 'GB']:
        if bytes < 1024:
            return f"{bytes:.2f} {unit}"
        bytes /= 1024
    return f"{bytes:.2f} TB"

def download_tile(z, x, y, output_dir, retry=3):
    """Download a single tile with retry logic."""
    # Create directory structure
    tile_dir = output_dir / str(z) / str(x)
    tile_dir.mkdir(parents=True, exist_ok=True)

    tile_path = tile_dir / f"{y}.png"

    # Skip if already downloaded
    if tile_path.exists():
        return True, 0

    # Build URL
    url = TILE_SERVER.format(z=z, x=x, y=y)

    # Download with retries
    for attempt in range(retry):
        try:
            request = Request(url, headers={'User-Agent': USER_AGENT})
            with urlopen(request, timeout=10) as response:
                tile_data = response.read()

            # Save tile
            with open(tile_path, 'wb') as f:
                f.write(tile_data)

            return True, len(tile_data)

        except (HTTPError, URLError) as e:
            if attempt < retry - 1:
                time.sleep(1 * (attempt + 1))  # Exponential backoff
                continue
            else:
                print(f"  ✗ Failed {z}/{x}/{y}: {e}", file=sys.stderr)
                return False, 0

    return False, 0

def download_tiles(min_zoom, max_zoom, output_dir, skip_existing=True):
    """Download all tiles for specified zoom levels."""
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Calculate total
    total = total_tiles(min_zoom, max_zoom)
    print(f"📦 Downloading {total:,} tiles (zoom {min_zoom}-{max_zoom})")
    print(f"📁 Output directory: {output_dir.absolute()}")
    print(f"🌐 Tile server: {TILE_SERVER.split('/')[2]}")
    print()

    downloaded = 0
    skipped = 0
    failed = 0
    total_bytes = 0
    start_time = time.time()

    for z in range(min_zoom, max_zoom + 1):
        tiles_at_zoom = 2 ** z
        print(f"📍 Zoom {z}: {num_tiles_for_zoom(z):,} tiles ({tiles_at_zoom}x{tiles_at_zoom} grid)")

        zoom_downloaded = 0
        zoom_bytes = 0

        for x in range(tiles_at_zoom):
            for y in range(tiles_at_zoom):
                success, size = download_tile(z, x, y, output_dir)

                if success:
                    if size > 0:
                        downloaded += 1
                        zoom_downloaded += 1
                        total_bytes += size
                        zoom_bytes += size
                    else:
                        skipped += 1
                else:
                    failed += 1

                # Progress indicator
                current = sum(num_tiles_for_zoom(zz) for zz in range(min_zoom, z)) + (x * tiles_at_zoom) + y + 1
                if current % 100 == 0 or current == total:
                    elapsed = time.time() - start_time
                    percent = (current / total) * 100
                    rate = current / elapsed if elapsed > 0 else 0
                    eta = (total - current) / rate if rate > 0 else 0

                    print(f"  Progress: {current:,}/{total:,} ({percent:.1f}%) | "
                          f"{rate:.1f} tiles/s | ETA: {eta:.0f}s | "
                          f"Downloaded: {format_size(total_bytes)}", end='\r')

        print(f"  ✓ Zoom {z} complete: {zoom_downloaded:,} new, {format_size(zoom_bytes)}")

    # Summary
    elapsed = time.time() - start_time
    print()
    print("=" * 70)
    print(f"✅ Download complete!")
    print(f"   Downloaded: {downloaded:,} tiles ({format_size(total_bytes)})")
    print(f"   Skipped: {skipped:,} (already existed)")
    print(f"   Failed: {failed:,}")
    print(f"   Time: {elapsed:.1f}s ({downloaded/elapsed:.1f} tiles/s)")
    print(f"   Average tile size: {format_size(total_bytes/downloaded) if downloaded > 0 else '0 B'}")
    print("=" * 70)

def main():
    parser = argparse.ArgumentParser(
        description="Download minimal offline tiles for GeoLens",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Download minimal set (zoom 0-8, ~100-500MB)
  python download_offline_tiles.py

  # Download more detail (zoom 0-10, ~1-2GB)
  python download_offline_tiles.py --max-zoom 10

  # Custom output directory
  python download_offline_tiles.py --output ./my-tiles

  # Resume interrupted download
  python download_offline_tiles.py --skip-existing

Zoom level reference:
  0-2:  World/continent level
  3-5:  Country/state level
  6-8:  Region/large city level (default max)
  9-11: City/neighborhood level
  12-14: Street level
  15+:  Building level
        """
    )

    parser.add_argument(
        '--min-zoom',
        type=int,
        default=DEFAULT_MIN_ZOOM,
        help=f"Minimum zoom level (default: {DEFAULT_MIN_ZOOM})"
    )

    parser.add_argument(
        '--max-zoom',
        type=int,
        default=DEFAULT_MAX_ZOOM,
        help=f"Maximum zoom level (default: {DEFAULT_MAX_ZOOM})"
    )

    parser.add_argument(
        '--output',
        type=Path,
        default=DEFAULT_OUTPUT_DIR,
        help=f"Output directory (default: {DEFAULT_OUTPUT_DIR})"
    )

    parser.add_argument(
        '--no-skip-existing',
        action='store_true',
        help="Re-download existing tiles"
    )

    args = parser.parse_args()

    # Validate zoom levels
    if args.min_zoom < 0 or args.min_zoom > 18:
        print(f"Error: min-zoom must be 0-18", file=sys.stderr)
        return 1

    if args.max_zoom < args.min_zoom or args.max_zoom > 18:
        print(f"Error: max-zoom must be {args.min_zoom}-18", file=sys.stderr)
        return 1

    # Estimate size
    total = total_tiles(args.min_zoom, args.max_zoom)
    estimated_size = total * 15000  # ~15KB average per tile

    print("GeoLens Offline Tile Downloader")
    print("=" * 70)
    print(f"Zoom levels: {args.min_zoom}-{args.max_zoom}")
    print(f"Total tiles: {total:,}")
    print(f"Estimated size: {format_size(estimated_size)}")
    print(f"Estimated time: {total/100:.0f}s (at 100 tiles/s)")
    print("=" * 70)

    # Confirm for large downloads
    if total > 10000:
        response = input("\nThis will download a large number of tiles. Continue? [y/N] ")
        if response.lower() not in ['y', 'yes']:
            print("Cancelled.")
            return 0

    print()

    try:
        download_tiles(
            args.min_zoom,
            args.max_zoom,
            args.output,
            skip_existing=not args.no_skip_existing
        )
        return 0
    except KeyboardInterrupt:
        print("\n\n⚠️  Download interrupted by user")
        print("Run the script again to resume from where you left off.")
        return 130
    except Exception as e:
        print(f"\n\n❌ Error: {e}", file=sys.stderr)
        return 1

if __name__ == '__main__':
    sys.exit(main())
