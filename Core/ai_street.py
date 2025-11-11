"""
Cleaned-up CLI for running GeoCLIP predictions on image inputs.

The original executable accepted either a directory of image files or a CSV
listing image paths and (optionally) precomputed MD5 hashes. This module
recreates that behaviour with clearer structure, additional validation, and
CSV export support shared with the desktop runtime.
"""

from __future__ import annotations

import argparse
import os
import sys
import time
from pathlib import Path
from typing import Iterable, List, Optional, Sequence, Tuple

import pandas as pd
from transformers.utils import logging as hf_logging

from llocale import (
    GeoClipPredictor,
    InputRecord,
    PredictionOutcome,
    ensure_output_directory,
    load_records_from_path,
    normalise_extensions,
    set_hf_cache_environment,
)

os.environ.setdefault("HF_HUB_DISABLE_PROGRESS_BARS", "1")
os.environ.setdefault("TRANSFORMERS_VERBOSITY", "error")


def parse_args(argv: Optional[Sequence[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run the GeoCLIP model on either a directory of images or a CSV manifest."
    )
    parser.add_argument(
        "--input", required=True, type=Path, help="Directory, image file, or CSV manifest to process."
    )
    parser.add_argument(
        "--top-k",
        type=int,
        default=5,
        help="Number of predictions to request from GeoCLIP (default: 5).",
    )
    parser.add_argument(
        "--write-csv",
        action="store_true",
        help="Persist predictions to a CSV file.",
    )
    parser.add_argument(
        "--output-csv",
        type=Path,
        default=Path("results.csv"),
        help="Where to write predictions if --write-csv is passed (default: results.csv).",
    )
    parser.add_argument(
        "--delimiter",
        default=",",
        help="Delimiter to use when reading a CSV manifest (default: ',').",
    )
    parser.add_argument(
        "--path-column",
        default="path",
        help=(
            "Column name holding image paths in the CSV manifest (default: path). "
            "If the column is missing, the final column is used."
        ),
    )
    parser.add_argument(
        "--md5-column",
        default="md5",
        help=(
            "Column name holding MD5 hashes in the CSV manifest (default: md5). "
            "If the column is missing, the first column is used when available."
        ),
    )
    parser.add_argument(
        "--skip-missing",
        action="store_true",
        help="Skip files that are missing on disk instead of treating them as errors.",
    )
    parser.add_argument(
        "--recursive",
        action="store_true",
        help="When reading a directory input, include images in subdirectories.",
    )
    parser.add_argument(
        "--extensions",
        default=".jpg,.jpeg,.png,.webp,.bmp,.tif,.tiff",
        help=(
            "Comma-separated list of allowed image extensions for directory inputs "
            "(default: common image formats)."
        ),
    )
    parser.add_argument(
        "--device",
        choices=["auto", "cpu", "cuda", "rocm"],
        default="auto",
        help="Device to run predictions on (default: auto-detect CUDA/ROCm).",
    )
    parser.add_argument(
        "--hf-cache",
        type=Path,
        help="Custom Hugging Face cache directory to support offline workflows.",
    )
    parser.add_argument(
        "--log-file",
        type=Path,
        help="Write prediction tables to this file in addition to console output.",
    )
    parser.add_argument(
        "--progress",
        action="store_true",
        help="Display a compact progress bar while processing multiple inputs.",
    )
    parser.add_argument(
        "--quiet",
        action="store_true",
        help="Suppress per-record tables on stdout (useful together with --log-file).",
    )
    return parser.parse_args(argv)


def prepare_records(args: argparse.Namespace) -> List[InputRecord]:
    return load_records_from_path(
        args.input,
        delimiter=args.delimiter,
        path_column=args.path_column,
        md5_column=args.md5_column,
        recursive=args.recursive,
        allowed_extensions=normalise_extensions(args.extensions),
    )


def write_csv(path: Path, outcomes: Iterable[PredictionOutcome]) -> None:
    rows = []
    for outcome in outcomes:
        if not outcome.predictions:
            continue
        for prediction in outcome.predictions:
            rows.append(
                {
                    "path": str(outcome.record.path),
                    "md5": outcome.record.md5 or "",
                    "prediction_number": prediction.rank,
                    "latitude": prediction.latitude,
                    "longitude": prediction.longitude,
                    "probability": prediction.probability,
                    "city": prediction.city,
                    "state": prediction.state,
                    "county": prediction.county,
                    "country": prediction.country,
                }
            )
    ensure_output_directory(path)
    pd.DataFrame(rows).to_csv(path, index=False)


def print_table(
    outcome: PredictionOutcome,
    *,
    quiet: bool,
    log_handle: Optional[object],
) -> None:
    header_line = "  Rank  Latitude     Longitude    Probability  Location"
    underline = "  ----  --------     ----------   -----------  --------"
    if not quiet:
        print(header_line)
        print(underline)
    if log_handle:
        log_handle.write(header_line + "\n")
        log_handle.write(underline + "\n")

    for prediction in outcome.predictions:
        location_display = prediction.location_summary or "-"
        line = (
            f"  {prediction.rank:>4}  {prediction.latitude:>9.6f}  "
            f"{prediction.longitude:>11.6f}  {prediction.probability:>11.6f}  "
            f"{location_display}"
        )
        if not quiet:
            print(line)
        if log_handle:
            log_handle.write(line + "\n")

    if not quiet:
        print("")
    if log_handle:
        log_handle.write("\n")


def run_predictions(
    records: List[InputRecord],
    *,
    top_k: int,
    skip_missing: bool,
    device: str,
    quiet: bool,
    log_file: Optional[Path],
    show_progress: bool,
) -> Tuple[int, List[PredictionOutcome]]:
    total = len(records)
    if total == 0:
        print("No inputs found. Exiting.", file=sys.stderr)
        return 1

    predictor = GeoClipPredictor(device=device)
    print(f"Using device: {predictor.device_label}", file=sys.stderr)

    log_handle = None
    if log_file:
        ensure_output_directory(log_file)
        log_handle = log_file.open("w", encoding="utf-8")

    progress_bar = None
    if show_progress and total > 1:
        from tqdm import tqdm

        progress_bar = tqdm(total=total, unit="img", desc="Predicting", leave=False)

    failures = 0
    outcomes: List[PredictionOutcome] = []

    for outcome in predictor.predict_records(records, top_k=top_k, skip_missing=skip_missing):
        banner = outcome.record.banner(total)
        if not quiet:
            print(banner)
        if log_handle:
            log_handle.write(banner + "\n")

        for warning in outcome.warnings:
            message = f"  ! {warning}"
            if not quiet:
                print(message, file=sys.stderr)
            if log_handle:
                log_handle.write(message + "\n")

        if outcome.error:
            message = f"  ! {outcome.error}"
            print(message, file=sys.stderr)
            if log_handle:
                log_handle.write(message + "\n")
            failures += 1
        elif outcome.predictions:
            print_table(outcome, quiet=quiet, log_handle=log_handle)
        # Missing files with --skip-missing land here with warnings but no error and no predictions.

        outcomes.append(outcome)

        if progress_bar:
            progress_bar.update(1)

    if log_handle:
        log_handle.close()
    if progress_bar:
        progress_bar.close()

    return_code = 0 if failures == 0 else 1
    return return_code, outcomes


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = parse_args(argv)
    hf_logging.set_verbosity_error()
    hf_logging.disable_progress_bar()

    start_time = time.perf_counter()
    records = prepare_records(args)

    cache_root = args.hf_cache.expanduser().resolve() if args.hf_cache else None
    set_hf_cache_environment(cache_root)

    exit_code, outcomes = run_predictions(
        records,
        top_k=args.top_k,
        skip_missing=args.skip_missing,
        device=args.device,
        quiet=args.quiet,
        log_file=args.log_file,
        show_progress=args.progress,
    )

    if args.write_csv:
        write_csv(args.output_csv, outcomes)
        print(f"Saved predictions to '{args.output_csv}'.")

    duration = time.perf_counter() - start_time
    print(f"Completed in {duration:.2f} seconds with exit code {exit_code}.")
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
