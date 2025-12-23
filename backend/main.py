"""
GeoLens FastAPI service with file upload support and CORS for web frontend.
Main entry point for the Locate backend API.
"""

from __future__ import annotations

import logging
import os
import shutil
import tempfile
import uuid
from functools import lru_cache
from pathlib import Path
from typing import List, Literal, Optional

from fastapi import FastAPI, HTTPException, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, Field, field_validator

from .llocale import (
    GeoClipPredictor,
    InputRecord,
    LocationPrediction,
    PredictionOutcome,
    set_hf_cache_environment,
)

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Security settings
ALLOWED_EXTENSIONS = {'.jpg', '.jpeg', '.png', '.bmp', '.gif', '.heic', '.webp'}
MAX_IMAGES_PER_REQUEST = 100
MAX_FILE_SIZE = 50 * 1024 * 1024  # 50MB per file

# Upload directory
UPLOAD_DIR = Path(tempfile.gettempdir()) / "geolens_uploads"
UPLOAD_DIR.mkdir(exist_ok=True)

DeviceChoice = Literal["auto", "cpu", "cuda", "rocm"]

# Create FastAPI app
app = FastAPI(
    title="GeoLens API",
    version="5.0.0",
    description="AI-powered image geolocation using GeoCLIP"
)

# Configure CORS for React frontend
app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "http://localhost:5173",  # Vite dev server
        "http://localhost:3000",  # Alternative dev port
        "http://localhost:8080",  # Production
    ],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Serve uploaded files
app.mount("/uploads", StaticFiles(directory=str(UPLOAD_DIR)), name="uploads")


# Request/Response models
class InferenceItem(BaseModel):
    path: Path = Field(..., description="Path to the image on disk.")
    md5: Optional[str] = Field(None, description="Optional MD5 hash metadata.")


class InferenceRequest(BaseModel):
    items: List[InferenceItem] = Field(
        ...,
        min_length=1,
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
    path: str  # Use string instead of Path for JSON serialization
    md5: Optional[str]
    predictions: List[PredictionCandidateResponse]
    warnings: List[str]
    error: Optional[str]

    @classmethod
    def from_outcome(cls, outcome: PredictionOutcome) -> "PredictionResultResponse":
        return cls(
            path=str(outcome.record.path),
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


class UploadResponse(BaseModel):
    paths: List[str]
    errors: List[str]


# Predictor cache
@lru_cache(maxsize=3)
def get_predictor(device: DeviceChoice) -> GeoClipPredictor:
    logger.info(f"Loading predictor with device: {device}")
    return GeoClipPredictor(device=device)


def validate_image_path(path: Path) -> Path:
    """Validate image path for security."""
    try:
        resolved_path = path.expanduser().resolve()
        
        if not resolved_path.is_absolute():
            raise HTTPException(status_code=400, detail=f"Path must be absolute: {path.name}")
        
        if not resolved_path.is_file():
            raise HTTPException(status_code=404, detail=f"File not found: {path.name}")
        
        file_extension = resolved_path.suffix.lower()
        if file_extension not in ALLOWED_EXTENSIONS:
            raise HTTPException(
                status_code=400,
                detail=f"Unsupported file type '{file_extension}'. Allowed: {', '.join(sorted(ALLOWED_EXTENSIONS))}"
            )
        
        return resolved_path
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Path validation error for {path.name}: {e}")
        raise HTTPException(status_code=400, detail=f"Invalid path: {path.name}")


# Endpoints
@app.get("/health")
def health() -> dict:
    """Health check endpoint."""
    return {"status": "ok", "version": "5.0.0"}


@app.post("/upload", response_model=UploadResponse)
async def upload_images(files: List[UploadFile] = File(...)) -> UploadResponse:
    """
    Upload images for processing.
    Returns the server paths of uploaded files.
    """
    if len(files) > MAX_IMAGES_PER_REQUEST:
        raise HTTPException(
            status_code=400,
            detail=f"Cannot upload more than {MAX_IMAGES_PER_REQUEST} files at once."
        )
    
    paths = []
    errors = []
    
    for file in files:
        try:
            # Validate file extension
            ext = Path(file.filename or "").suffix.lower()
            if ext not in ALLOWED_EXTENSIONS:
                errors.append(f"{file.filename}: unsupported file type '{ext}'")
                continue
            
            # Generate unique filename
            unique_name = f"{uuid.uuid4()}{ext}"
            file_path = UPLOAD_DIR / unique_name
            
            # Save file
            with open(file_path, "wb") as buffer:
                content = await file.read()
                if len(content) > MAX_FILE_SIZE:
                    errors.append(f"{file.filename}: file too large (max {MAX_FILE_SIZE // (1024*1024)}MB)")
                    continue
                buffer.write(content)
            
            paths.append(str(file_path))
            logger.info(f"Uploaded: {file.filename} -> {file_path}")
            
        except Exception as e:
            logger.error(f"Upload error for {file.filename}: {e}")
            errors.append(f"{file.filename}: upload failed")
    
    return UploadResponse(paths=paths, errors=errors)


@app.post("/infer", response_model=InferenceResponse)
def infer(request: InferenceRequest) -> InferenceResponse:
    """
    Perform geolocation inference on a batch of images.
    """
    if not request.items:
        raise HTTPException(status_code=400, detail="No items provided for inference.")

    if request.hf_cache is not None:
        set_hf_cache_environment(request.hf_cache.expanduser().resolve())

    # Validate all image paths
    validated_paths = []
    for item in request.items:
        try:
            validated_path = validate_image_path(item.path)
            validated_paths.append((validated_path, item.md5))
        except HTTPException as e:
            if not request.skip_missing:
                raise
            logger.info(f"Skipping invalid path: {item.path.name} - {e.detail}")

    if not validated_paths:
        raise HTTPException(
            status_code=400,
            detail="No valid image files found after validation."
        )

    # Create InputRecords
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
        logger.error(f"Inference failed: {e}")
        raise HTTPException(
            status_code=500,
            detail="Inference processing failed. Check server logs for details."
        )


@app.get("/")
def root():
    return {"message": "GeoLens API is running. Documentation at /docs"}


@app.on_event("shutdown")
def cleanup():
    """Clean up uploaded files on shutdown."""
    try:
        shutil.rmtree(UPLOAD_DIR, ignore_errors=True)
        logger.info("Cleaned up upload directory")
    except Exception as e:
        logger.error(f"Cleanup failed: {e}")


# Run with: uvicorn backend.main:app --reload --host 0.0.0.0 --port 8000
