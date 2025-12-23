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
    import time
    start_time = time.time()
    
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
            
            processing_time_ms = int((time.time() - start_time) * 1000)
            
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
                
                # Log audit event for completion
                await _log_audit_event(
                    session, job_id, "job_completed", processing_time_ms,
                    "success", None, {"num_predictions": len(predictions)}
                )
            else:
                job.status = JobStatus.FAILED
                job.error = "No predictions generated"
                await _log_audit_event(
                    session, job_id, "job_failed", processing_time_ms,
                    "failed", "No predictions generated", None
                )
            
        except Exception as e:
            logger.error(f"Job {job_id} failed: {e}")
            job.status = JobStatus.FAILED
            job.error = str(e)
            processing_time_ms = int((time.time() - start_time) * 1000)
            await _log_audit_event(
                session, job_id, "job_failed", processing_time_ms,
                "failed", str(e), None
            )
        
        finally:
            job.completed_at = datetime.utcnow()
            await session.commit()


async def _log_audit_event(
    session,
    job_id: str,
    action: str,
    processing_time_ms: int,
    status: str,
    error_message: str | None,
    metadata: dict | None,
):
    """Log an audit event from the Celery worker."""
    try:
        import sys
        sys.path.insert(0, "/app")
        from shared.models.audit import AuditLog
        from uuid import UUID
        
        audit = AuditLog(
            service="locate",
            action=action,
            job_id=UUID(job_id) if job_id else None,
            processing_time_ms=processing_time_ms,
            status=status,
            error_message=error_message,
            metadata=metadata,
        )
        session.add(audit)
        await session.commit()
    except ImportError:
        pass  # Shared module not available
    except Exception:
        pass  # Don't fail job on audit logging error
