"""
Batch import implementation for Locate service.

Provides CSV and folder-based import for geolocation jobs.
"""

from pathlib import Path
from typing import Dict, Any
from uuid import UUID, uuid4

from shared.batch import BatchImporter


class LocateBatchImporter(BatchImporter):
    """
    Batch importer for Locate (geolocation) service.
    
    Supports image files: .jpg, .jpeg, .png, .bmp, .gif, .heic, .webp
    """
    
    def __init__(self):
        super().__init__(
            service_name="locate",
            allowed_extensions={'.jpg', '.jpeg', '.png', '.bmp', '.gif', '.heic', '.webp'},
            max_items=100,
            default_options={"top_k": 5},
        )
    
    async def create_job(
        self,
        file_path: Path,
        options: Dict[str, Any],
        session: Any,
    ) -> UUID:
        """
        Create a geolocation job for an image file.
        
        Args:
            file_path: Path to image file
            options: Job options (top_k, etc.)
            session: Database session
        
        Returns:
            Created job UUID
        """
        from models.database import Job
        from workers.tasks import process_geolocation
        
        # Create job record
        job = Job(
            id=uuid4(),
            filename=file_path.name,
            file_path=str(file_path),
            status="pending",
            top_k=options.get("top_k", 5),
        )
        
        session.add(job)
        await session.commit()
        await session.refresh(job)
        
        # Queue for processing
        process_geolocation.delay(str(job.id))
        
        return job.id


# Create singleton instance
locate_batch_importer = LocateBatchImporter()
