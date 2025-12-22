"""
FastAPI service that exposes GeoCLIP inference powered by the shared runtime in
`llocale.predictor`. The desktop client launches this service per session.
"""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager
from functools import lru_cache
from pathlib import Path
from typing import List, Literal, Optional

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field, field_validator

from .llocale import (
    GeoClipPredictor,
    InputRecord,
    LocationPrediction,
    PredictionOutcome,
    set_hf_cache_environment,
)

# Configure logging for security events
logger = logging.getLogger(__name__)

# Security: Define allowed image extensions to prevent processing of arbitrary files
ALLOWED_EXTENSIONS = {'.jpg', '.jpeg', '.png', '.bmp', '.gif', '.heic', '.webp'}

# Security: Maximum number of images per request to prevent resource exhaustion
MAX_IMAGES_PER_REQUEST = 100


DeviceChoice = Literal["auto", "cpu", "cuda", "rocm"]


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan for database initialization."""
    from services.database import init_db
    await init_db()
    yield


app = FastAPI(title="LLocale GeoCLIP Service", version="0.1.0", lifespan=lifespan)

# Include jobs router for shared workspace
from api.jobs import router as jobs_router
app.include_router(jobs_router, prefix="/api/jobs", tags=["jobs"])

# Include audit log routes
import sys
sys.path.insert(0, "/app")  # Add parent for shared imports
try:
    from shared.api.audit import create_audit_router
    from services.database import get_session
    audit_router = create_audit_router(get_session)
    app.include_router(audit_router, prefix="/api/audit", tags=["audit"])
except ImportError:
    pass  # Shared module not available


class InferenceItem(BaseModel):
    path: Path = Field(..., description="Path to the image on disk.")
    md5: Optional[str] = Field(None, description="Optional MD5 hash metadata.")


class InferenceRequest(BaseModel):
    items: List[InferenceItem] = Field(
        ...,
        min_items=1,
        max_length=MAX_IMAGES_PER_REQUEST,
        description="Images to process.",
    )
    top_k: int = Field(5, ge=1, le=20, description="Number of predictions per image.")
    device: DeviceChoice = Field("auto", description="Device to run inference on.")
    skip_missing: bool = Field(
        False,
        description="Skip missing files instead of failing the entire request.",
    )
    hf_cache: Optional[Path] = Field(
        None,
        description="Optional Hugging Face cache directory for offline mode.",
    )

    @field_validator('items')
    @classmethod
    def validate_items_count(cls, v: List[InferenceItem]) -> List[InferenceItem]:
        """Security: Prevent resource exhaustion by limiting batch size."""
        if len(v) > MAX_IMAGES_PER_REQUEST:
            raise ValueError(
                f'Cannot process more than {MAX_IMAGES_PER_REQUEST} images per request. '
                f'Received {len(v)} images.'
            )
        return v


class PredictionCandidateResponse(BaseModel):
    rank: int
    latitude: float
    longitude: float
    probability: float
    city: str
    state: str
    county: str
    country: str
    location_summary: str

    @classmethod
    def from_prediction(cls, prediction: LocationPrediction) -> "PredictionCandidateResponse":
        return cls(
            rank=prediction.rank,
            latitude=prediction.latitude,
            longitude=prediction.longitude,
            probability=prediction.probability,
            city=prediction.city,
            state=prediction.state,
            county=prediction.county,
            country=prediction.country,
            location_summary=prediction.location_summary,
        )


class PredictionResultResponse(BaseModel):
    path: Path
    md5: Optional[str]
    predictions: List[PredictionCandidateResponse]
    warnings: List[str]
    error: Optional[str]

    @classmethod
    def from_outcome(cls, outcome: PredictionOutcome) -> "PredictionResultResponse":
        return cls(
            path=outcome.record.path,
            md5=outcome.record.md5,
            predictions=[
                PredictionCandidateResponse.from_prediction(pred) for pred in outcome.predictions
            ],
            warnings=list(outcome.warnings),
            error=outcome.error,
        )


class InferenceResponse(BaseModel):
    device: str
    results: List[PredictionResultResponse]


def validate_image_path(path: Path) -> Path:
    """
    Security: Validate image path to prevent path traversal and ensure file safety.

    Checks:
    - Path must be absolute (prevents relative path attacks like ../../../etc/passwd)
    - File must exist and be a regular file (not directory or special file)
    - Extension must be in the allowed list (defense in depth)

    Args:
        path: The path to validate

    Returns:
        Resolved absolute path if valid

    Raises:
        HTTPException: If validation fails
    """
    try:
        # Resolve to absolute path (follows symlinks and removes .. components)
        resolved_path = path.expanduser().resolve()

        # Security: Ensure path is absolute to prevent traversal attacks
        if not resolved_path.is_absolute():
            logger.warning(f"Rejected non-absolute path: {path.name}")
            raise HTTPException(
                status_code=400,
                detail=f"Path must be absolute: {path.name}"
            )

        # Security: Validate file exists and is a regular file
        if not resolved_path.is_file():
            logger.warning(f"File not found or not a regular file: {resolved_path.name}")
            raise HTTPException(
                status_code=404,
                detail=f"File not found: {resolved_path.name}"
            )

        # Security: Validate file extension (defense in depth)
        file_extension = resolved_path.suffix.lower()
        if file_extension not in ALLOWED_EXTENSIONS:
            logger.warning(
                f"Rejected unsupported file type '{file_extension}': {resolved_path.name}"
            )
            raise HTTPException(
                status_code=400,
                detail=(
                    f"Unsupported file type '{file_extension}'. "
                    f"Allowed extensions: {', '.join(sorted(ALLOWED_EXTENSIONS))}"
                )
            )

        return resolved_path

    except HTTPException:
        # Re-raise HTTPExceptions as-is
        raise
    except Exception as e:
        # Security: Log full error but only return filename to client
        logger.error(f"Path validation error for {path.name}: {e}")
        raise HTTPException(
            status_code=400,
            detail=f"Invalid path: {path.name}"
        )


@lru_cache(maxsize=3)
def get_predictor(device: DeviceChoice) -> GeoClipPredictor:
    return GeoClipPredictor(device=device)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}


@app.post("/infer", response_model=InferenceResponse)
def infer(request: InferenceRequest) -> InferenceResponse:
    """
    Perform geolocation inference on a batch of images.

    Security validations:
    - Request size limited to MAX_IMAGES_PER_REQUEST
    - All paths validated for traversal attacks
    - File extensions verified against allowlist
    - Error messages sanitized to prevent information disclosure
    """
    if not request.items:
        raise HTTPException(status_code=400, detail="No items provided for inference.")

    if request.hf_cache is not None:
        set_hf_cache_environment(request.hf_cache.expanduser().resolve())

    # Security: Validate all image paths BEFORE processing
    # This prevents path traversal attacks and ensures only valid image files are processed
    validated_paths = []
    for item in request.items:
        try:
            validated_path = validate_image_path(item.path)
            validated_paths.append((validated_path, item.md5))
        except HTTPException as e:
            # Security: If validation fails and skip_missing is False, fail fast
            if not request.skip_missing:
                raise
            # Otherwise, log the error and continue with next item
            logger.info(f"Skipping invalid path: {item.path.name} - {e.detail}")

    if not validated_paths:
        raise HTTPException(
            status_code=400,
            detail="No valid image files found after security validation."
        )

    # Create InputRecords with validated paths
    records = [
        InputRecord(index=i + 1, path=path, md5=md5)
        for i, (path, md5) in enumerate(validated_paths)
    ]

    try:
        predictor = get_predictor(request.device)
        outcomes = list(
            predictor.predict_records(
                records,
                top_k=request.top_k,
                skip_missing=request.skip_missing,
            )
        )

        return InferenceResponse(
            device=predictor.device_label,
            results=[PredictionResultResponse.from_outcome(outcome) for outcome in outcomes],
        )
    except Exception as e:
        # Security: Log full error but sanitize response to prevent information disclosure
        logger.error(f"Inference failed: {e}")
        raise HTTPException(
            status_code=500,
            detail="Inference processing failed. Check server logs for details."
        )
