"""Job management API routes for Locate."""

from datetime import datetime
from typing import List, Optional
from uuid import UUID
import hashlib
import os

from fastapi import APIRouter, Depends, HTTPException, UploadFile, File, Form, Query
from sqlalchemy import select, desc
from sqlalchemy.ext.asyncio import AsyncSession

from services.database import get_session
from models.job import Job, JobStatus

router = APIRouter()


@router.post("", status_code=201)
async def create_job(
    file: UploadFile = File(...),
    top_k: int = Form(default=5),
    session: AsyncSession = Depends(get_session),
):
    """
    Create a new geolocation job.
    
    Uploads the image and queues for processing.
    """
    from config import settings
    
    # Validate file type
    if not file.content_type or not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="File must be an image")
    
    # Generate unique filename
    file_hash = hashlib.sha256(await file.read()).hexdigest()[:16]
    await file.seek(0)
    ext = os.path.splitext(file.filename or "image.jpg")[1]
    unique_filename = f"{file_hash}{ext}"
    
    # Save file
    upload_dir = settings.upload_dir
    upload_dir.mkdir(parents=True, exist_ok=True)
    file_path = upload_dir / unique_filename
    
    content = await file.read()
    with open(file_path, "wb") as f:
        f.write(content)
    
    # Create job record
    job = Job(
        filename=unique_filename,
        original_filename=file.filename or "image.jpg",
        file_path=str(file_path),
        top_k=top_k,
        status=JobStatus.PENDING,
    )
    
    session.add(job)
    await session.commit()
    await session.refresh(job)
    
    # Queue for processing
    from workers.tasks import process_geolocation
    process_geolocation.delay(str(job.id))
    
    return job.to_dict()


@router.get("", response_model=List[dict])
async def list_jobs(
    status: Optional[str] = Query(default=None),
    limit: int = Query(default=50, le=100),
    offset: int = Query(default=0),
    session: AsyncSession = Depends(get_session),
):
    """
    List all geolocation jobs.
    
    Supports filtering by status and pagination.
    All users see the same shared job list.
    """
    query = select(Job).order_by(desc(Job.created_at))
    
    if status:
        query = query.where(Job.status == status)
    
    query = query.offset(offset).limit(limit)
    
    result = await session.execute(query)
    jobs = result.scalars().all()
    
    return [job.to_dict() for job in jobs]


@router.get("/{job_id}")
async def get_job(
    job_id: UUID,
    session: AsyncSession = Depends(get_session),
):
    """Get a specific job by ID."""
    result = await session.execute(select(Job).where(Job.id == job_id))
    job = result.scalar_one_or_none()
    
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    
    return job.to_dict()


@router.delete("/{job_id}", status_code=204)
async def delete_job(
    job_id: UUID,
    session: AsyncSession = Depends(get_session),
):
    """Delete a job and its associated file."""
    result = await session.execute(select(Job).where(Job.id == job_id))
    job = result.scalar_one_or_none()
    
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    
    # Delete file if exists
    if job.file_path and os.path.exists(job.file_path):
        os.remove(job.file_path)
    
    await session.delete(job)
    await session.commit()
