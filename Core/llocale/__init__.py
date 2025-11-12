"""
Shared GeoCLIP runtime helpers for both the CLI (`ai_street.py`) and the
desktop FastAPI service. Exposes reusable dataclasses and prediction logic
so both surfaces stay in sync.
"""

from .predictor import (  # noqa: F401
    GeoClipPredictor,
    InputRecord,
    LocationPrediction,
    PredictionOutcome,
    ensure_output_directory,
    load_records_from_path,
    normalise_extensions,
    set_hf_cache_environment,
)
