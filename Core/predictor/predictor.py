"""
Reusable GeoCLIP prediction utilities.

The CLI (`ai_street.py`) and the forthcoming FastAPI service both rely on the
helpers defined here so behaviour remains consistent across surfaces.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Iterable, Iterator, List, Optional, Sequence, Tuple, Union

import pandas as pd
import reverse_geocode
import torch
from geoclip import GeoCLIP


@dataclass
class InputRecord:
    """Represents a single prediction request."""

    index: int
    path: Path
    md5: Optional[str] = None

    def banner(self, total: int) -> str:
        base = f"{self.index}/{total}"
        parts = [base]
        if self.md5:
            parts.append(self.md5)
        parts.append(str(self.path))
        return " :: ".join(parts)


@dataclass
class LocationPrediction:
    """Single ranked prediction for a record."""

    rank: int
    latitude: float
    longitude: float
    probability: float
    city: str
    state: str
    county: str
    country: str

    @property
    def location_summary(self) -> str:
        parts = [
            self.city or "",
            self.state or "",
            self.county or "",
            self.country or "",
        ]
        filtered = [part for part in parts if part]
        return ", ".join(filtered)


@dataclass
class PredictionOutcome:
    """Result of attempting to predict a single record."""

    record: InputRecord
    predictions: List[LocationPrediction] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)
    error: Optional[str] = None

    @property
    def success(self) -> bool:
        return self.error is None and bool(self.predictions)


def normalise_extensions(ext_string: str) -> Optional[set[str]]:
    exts = {item.strip().lower() for item in ext_string.split(",") if item.strip()}
    if not exts:
        return None
    return {ext if ext.startswith(".") else f".{ext}" for ext in exts}


def load_records_from_csv(
    csv_path: Path,
    *,
    delimiter: str,
    path_column: str,
    md5_column: str,
) -> List[InputRecord]:
    """Load prediction requests from a CSV manifest."""
    try:
        df = pd.read_csv(csv_path, delimiter=delimiter, on_bad_lines="skip")
    except Exception as exc:  # pragma: no cover - diagnostic path
        raise RuntimeError(f"Failed to read CSV '{csv_path}': {exc}") from exc

    if df.empty:
        return []

    records: List[InputRecord] = []
    for idx, row in df.iterrows():
        path_value = None

        if path_column in df.columns:
            path_value = row[path_column]
        elif len(row) > 0:
            path_value = row.iloc[-1]

        if path_value is None or (isinstance(path_value, float) and pd.isna(path_value)):
            raise ValueError(
                f"Row {idx + 1} in '{csv_path}' is missing a valid path value."
            )

        if md5_column in df.columns:
            md5_candidate = row[md5_column]
        elif len(row) > 1:
            md5_candidate = row.iloc[0]
        else:
            md5_candidate = None

        md5_value: Optional[str]
        if md5_candidate is not None and not (isinstance(md5_candidate, float) and pd.isna(md5_candidate)):
            md5_value = str(md5_candidate)
        else:
            md5_value = None

        resolved_path = Path(str(path_value)).expanduser()
        if not resolved_path.is_absolute():
            resolved_path = (csv_path.parent / resolved_path).resolve()

        records.append(
            InputRecord(
                index=idx + 1,
                path=resolved_path,
                md5=md5_value,
            )
        )

    return records


def load_records_from_directory(
    directory: Path,
    *,
    recursive: bool,
    allowed_extensions: Optional[set[str]],
) -> List[InputRecord]:
    """Load prediction requests from a directory of image files."""
    if not directory.exists():
        raise FileNotFoundError(f"Input directory '{directory}' does not exist.")
    if not directory.is_dir():
        raise NotADirectoryError(f"Input '{directory}' is not a directory.")

    iterator = directory.rglob("*") if recursive else directory.iterdir()
    files = []
    for path in iterator:
        if not path.is_file():
            continue
        if allowed_extensions is None:
            files.append(path)
        elif path.suffix.lower() in allowed_extensions:
            files.append(path)

    files.sort()

    return [
        InputRecord(index=i + 1, path=path)
        for i, path in enumerate(files)
    ]


def load_records_from_path(
    path: Path,
    *,
    delimiter: str,
    path_column: str,
    md5_column: str,
    recursive: bool,
    allowed_extensions: Optional[set[str]],
) -> List[InputRecord]:
    """Route input loading depending on whether the path is a CSV, image file, or directory."""
    resolved = path.expanduser()
    if not resolved.exists():
        raise FileNotFoundError(f"Input path '{resolved}' does not exist.")

    if resolved.is_file():
        if resolved.suffix.lower() == ".csv":
            return load_records_from_csv(
                resolved,
                delimiter=delimiter,
                path_column=path_column,
                md5_column=md5_column,
            )
        return [InputRecord(index=1, path=resolved.resolve())]

    return load_records_from_directory(
        resolved,
        recursive=recursive,
        allowed_extensions=allowed_extensions,
    )


def reverse_lookup(latitude: float, longitude: float) -> Dict[str, str]:
    """Fetch human-readable location details for the given coordinates."""
    try:
        return reverse_geocode.get((latitude, longitude))
    except Exception:
        return {}


def _materialize_predictions(
    gps: Union[Sequence, "torch.Tensor"],  # type: ignore[name-defined]
    probs: Union[Sequence, "torch.Tensor"],  # type: ignore[name-defined]
) -> Tuple[List[Sequence[float]], List[float]]:
    """
    Convert GeoCLIP outputs into plain Python lists regardless of tensor types.

    GeoCLIP may return PyTorch tensors; attempting truthiness on them raises an
    error. Converting early keeps the rest of the pipeline framework-agnostic.
    """

    def _to_list(value: Union[Sequence, "torch.Tensor"]) -> List:
        if hasattr(value, "tolist"):
            try:
                return list(value.tolist())  # type: ignore[call-arg]
            except TypeError:
                pass
        return list(value)

    gps_list = _to_list(gps)
    probs_list = _to_list(probs)
    return gps_list, probs_list


def _gpu_backend() -> Optional[str]:
    if not torch.cuda.is_available():
        return None
    if getattr(torch.version, "hip", None):
        return "rocm"
    return "cuda"


def select_device(requested: str) -> Tuple[str, str]:
    backend = _gpu_backend()
    if requested == "auto":
        if backend:
            return "cuda", backend
        return "cpu", "cpu"

    if requested == "cuda":
        if backend != "cuda":
            if backend is None:
                raise RuntimeError("CUDA requested but no compatible GPU is available.")
            raise RuntimeError("CUDA requested but current PyTorch build targets ROCm.")
        return "cuda", "cuda"

    if requested == "rocm":
        if backend != "rocm":
            if backend is None:
                raise RuntimeError("ROCm requested but no compatible GPU is available.")
            raise RuntimeError("ROCm requested but current PyTorch build targets CUDA.")
        return "cuda", "rocm"

    if requested == "cpu":
        return "cpu", "cpu"

    raise RuntimeError(f"Unknown device option '{requested}'.")


class GeoClipPredictor:
    """Thin wrapper around the GeoCLIP model providing batch prediction utilities."""

    def __init__(self, *, device: str = "auto") -> None:
        self._requested_device = device
        compute_device, display_device = select_device(device)
        self._device = compute_device
        self._device_label = display_device
        self._model = GeoCLIP()
        self._model.to(self._device)

    @property
    def device(self) -> str:
        return self._device

    @property
    def device_label(self) -> str:
        return self._device_label

    def predict_records(
        self,
        records: Iterable[InputRecord],
        *,
        top_k: int,
        skip_missing: bool,
    ) -> Iterator[PredictionOutcome]:
        for record in records:
            if not record.path.exists():
                message = f"File does not exist: {record.path}"
                if skip_missing:
                    yield PredictionOutcome(
                        record=record,
                        warnings=[message],
                        predictions=[],
                    )
                    continue
                yield PredictionOutcome(
                    record=record,
                    error=message,
                    predictions=[],
                )
                continue

            try:
                gps_predictions, probabilities = self._model.predict(
                    str(record.path), top_k=top_k
                )
                gps_predictions, probabilities = _materialize_predictions(
                    gps_predictions, probabilities
                )
            except Exception as exc:  # pragma: no cover - propagate details to caller
                yield PredictionOutcome(
                    record=record,
                    error=f"Prediction failed: {exc}",
                )
                continue

            if not gps_predictions:
                yield PredictionOutcome(
                    record=record,
                    error="Model returned no predictions.",
                )
                continue

            candidates: List[LocationPrediction] = []
            for rank, (coords, prob) in enumerate(
                zip(gps_predictions, probabilities), start=1
            ):
                lat, lon = map(float, coords)
                probability = float(prob)
                location = reverse_lookup(lat, lon)
                candidates.append(
                    LocationPrediction(
                        rank=rank,
                        latitude=lat,
                        longitude=lon,
                        probability=probability,
                        city=location.get("city", "") or "",
                        state=location.get("state", "") or "",
                        county=location.get("county", "") or "",
                        country=location.get("country", "") or "",
                    )
                )

            yield PredictionOutcome(record=record, predictions=candidates)


def ensure_output_directory(path: Path) -> None:
    """Guarantee that the parent directory for a file path exists."""
    if not path.parent.exists():
        path.parent.mkdir(parents=True, exist_ok=True)


def set_hf_cache_environment(cache_root: Optional[Path]) -> None:
    """Apply Hugging Face cache environment overrides."""
    if cache_root is None:
        return
    cache_root.mkdir(parents=True, exist_ok=True)
    for subdir in ("hub", "transformers"):
        (cache_root / subdir).mkdir(parents=True, exist_ok=True)
    os.environ["HF_HOME"] = str(cache_root)
    os.environ["HUGGINGFACE_HUB_CACHE"] = str(cache_root / "hub")
    os.environ["TRANSFORMERS_CACHE"] = str(cache_root / "transformers")
