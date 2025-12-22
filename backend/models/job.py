"""
Job model for Locate queue system.

Stores geolocation job state and results for shared workspace.
"""

from datetime import datetime
from typing import Optional
from enum import Enum
import uuid

from sqlalchemy import String, Text, DateTime, JSON
from sqlalchemy.orm import Mapped, mapped_column
from sqlalchemy.dialects.postgresql import UUID

from ..services.database import Base


class JobStatus(str, Enum):
    """Job processing status."""
    PENDING = "pending"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"


class Job(Base):
    """Geolocation job model."""
    
    __tablename__ = "locate_jobs"
    
    id: Mapped[uuid.UUID] = mapped_column(
        UUID(as_uuid=True), primary_key=True, default=uuid.uuid4
    )
    
    # File info
    filename: Mapped[str] = mapped_column(String(255))
    original_filename: Mapped[str] = mapped_column(String(255))
    file_path: Mapped[str] = mapped_column(String(512))
    
    # Status
    status: Mapped[str] = mapped_column(String(50), default=JobStatus.PENDING)
    error: Mapped[Optional[str]] = mapped_column(Text, nullable=True)
    
    # Results (JSON with predictions)
    results: Mapped[Optional[dict]] = mapped_column(JSON, nullable=True)
    
    # Settings
    top_k: Mapped[int] = mapped_column(default=5)
    
    # Timestamps
    created_at: Mapped[datetime] = mapped_column(
        DateTime, default=datetime.utcnow
    )
    started_at: Mapped[Optional[datetime]] = mapped_column(
        DateTime, nullable=True
    )
    completed_at: Mapped[Optional[datetime]] = mapped_column(
        DateTime, nullable=True
    )
    
    def to_dict(self) -> dict:
        """Convert to dictionary for API responses."""
        return {
            "id": str(self.id),
            "filename": self.original_filename,
            "status": self.status,
            "error": self.error,
            "results": self.results,
            "top_k": self.top_k,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "started_at": self.started_at.isoformat() if self.started_at else None,
            "completed_at": self.completed_at.isoformat() if self.completed_at else None,
        }
