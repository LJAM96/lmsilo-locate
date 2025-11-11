"""
Lightweight smoke test for the GeoCLIP environment.

Creates a temporary synthetic image and ensures GeoCLIP can produce predictions.
Useful after recreating the conda environment on an offline machine.
"""

from __future__ import annotations

import argparse
import tempfile
from pathlib import Path

import numpy as np
import torch
from geoclip import GeoCLIP
from PIL import Image


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run a minimal GeoCLIP sanity check.")
    parser.add_argument(
        "--device",
        choices=["auto", "cpu", "cuda"],
        default="auto",
        help="Device to run the test on (default: auto-detect CUDA).",
    )
    return parser.parse_args()


def select_device(requested: str) -> str:
    if requested == "auto":
        return "cuda" if torch.cuda.is_available() else "cpu"
    if requested == "cuda" and not torch.cuda.is_available():
        raise RuntimeError("CUDA requested but no compatible GPU is available.")
    return requested


def main() -> int:
    args = parse_args()
    device = select_device(args.device)

    with tempfile.TemporaryDirectory() as tmpdir:
        image_path = Path(tmpdir) / "smoke.png"
        dummy_pixels = (np.random.rand(224, 224, 3) * 255).astype("uint8")
        Image.fromarray(dummy_pixels).save(image_path)

        model = GeoCLIP()
        model.to(device)
        gps, prob = model.predict(str(image_path), top_k=3)
        gps = gps.tolist() if hasattr(gps, "tolist") else gps
        prob = prob.tolist() if hasattr(prob, "tolist") else prob

    print(f"Smoke test passed on device '{device}'.")
    for idx, (coords, score) in enumerate(zip(gps, prob), start=1):
        lat, lon = coords
        print(f"  Prediction {idx}: ({lat:.4f}, {lon:.4f}) prob={score:.4f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

