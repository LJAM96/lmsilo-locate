"""
FastAPI service that exposes GeoCLIP inference powered by the shared runtime in
`llocale.predictor`. The desktop client launches this service per session.
"""

from __future__ import annotations

from functools import lru_cache
from pathlib import Path
from typing import List, Literal, Optional

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from llocale import (
    GeoClipPredictor,
    InputRecord,
    LocationPrediction,
    PredictionOutcome,
    set_hf_cache_environment,
)


DeviceChoice = Literal["auto", "cpu", "cuda", "rocm"]

app = FastAPI(title="LLocale GeoCLIP Service", version="0.1.0")


class InferenceItem(BaseModel):
    path: Path = Field(..., description="Path to the image on disk.")
    md5: Optional[str] = Field(None, description="Optional MD5 hash metadata.")


class InferenceRequest(BaseModel):
    items: List[InferenceItem] = Field(..., min_items=1, description="Images to process.")
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


@lru_cache(maxsize=3)
def get_predictor(device: DeviceChoice) -> GeoClipPredictor:
    return GeoClipPredictor(device=device)


@app.get("/health")
def health() -> dict:
    return {"status": "ok"}


@app.post("/infer", response_model=InferenceResponse)
def infer(request: InferenceRequest) -> InferenceResponse:
    if not request.items:
        raise HTTPException(status_code=400, detail="No items provided for inference.")

    if request.hf_cache is not None:
        set_hf_cache_environment(request.hf_cache.expanduser().resolve())

    records = [
        InputRecord(index=i + 1, path=item.path.expanduser(), md5=item.md5)
        for i, item in enumerate(request.items)
    ]

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
