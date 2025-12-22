"""
Celery tasks for Locate geolocation processing.
"""

from datetime import datetime
import logging

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession, create_async_engine, async_sessionmaker

from .celery_app import celery_app
from ..config import DATABASE_URL, DEVICE
from ..models.job import Job, JobStatus
from ..llocale import GeoClipPredictor, InputRecord

logger = logging.getLogger(__name__)

# Create async engine for tasks
engine = create_async_engine(DATABASE_URL, echo=False)
async_session_maker = async_sessionmaker(engine, class_=AsyncSession, expire_on_commit=False)

# Predictor cache (one per worker process)
_predictor = None


def get_predictor():
    """Get or create the GeoClip predictor."""
    global _predictor
    if _predictor is None:
        logger.info(f"Loading GeoClip predictor with device: {DEVICE}")
        _predictor = GeoClipPredictor(device=DEVICE)
    return _predictor


@celery_app.task(bind=True, queue="locate")
def process_geolocation(self, job_id: str):
    """
    Process a geolocation job.
    
    Args:
        job_id: UUID of the job to process
    """
    import asyncio
    asyncio.get_event_loop().run_until_complete(_process_geolocation_async(job_id))


async def _process_geolocation_async(job_id: str):
    """Async implementation of geolocation processing."""
    async with async_session_maker() as session:
        # Get the job
        result = await session.execute(
            select(Job).where(Job.id == job_id)
        )
        job = result.scalar_one_or_none()
        
        if not job:
            logger.error(f"Job {job_id} not found")
            return
        
        try:
            # Update status to processing
            job.status = JobStatus.PROCESSING
            job.started_at = datetime.utcnow()
            await session.commit()
            
            # Get predictor and run inference
            predictor = get_predictor()
            
            record = InputRecord(
                index=1,
                path=job.file_path,
                md5=None
            )
            
            outcomes = list(predictor.predict_records(
                [record],
                top_k=job.top_k,
                skip_missing=False,
            ))
            
            if outcomes and outcomes[0].predictions:
                # Convert predictions to serializable format
                predictions = []
                for pred in outcomes[0].predictions:
                    predictions.append({
                        "rank": pred.rank,
                        "latitude": pred.latitude,
                        "longitude": pred.longitude,
                        "probability": pred.probability,
                        "city": pred.city,
                        "state": pred.state,
                        "county": pred.county,
                        "country": pred.country,
                        "location_summary": pred.location_summary,
                    })
                
                job.results = {"predictions": predictions, "device": predictor.device_label}
                job.status = JobStatus.COMPLETED
            else:
                job.status = JobStatus.FAILED
                job.error = "No predictions generated"
            
        except Exception as e:
            logger.error(f"Job {job_id} failed: {e}")
            job.status = JobStatus.FAILED
            job.error = str(e)
        
        finally:
            job.completed_at = datetime.utcnow()
            await session.commit()
