"""
Celery application for Locate queue system.

Handles async geolocation job processing.
"""

from celery import Celery

from ..config import REDIS_URL


# Create Celery app
celery_app = Celery(
    "locate",
    broker=REDIS_URL,
    backend=REDIS_URL,
    include=["backend.workers.tasks"],
)

# Celery configuration
celery_app.conf.update(
    task_serializer="json",
    accept_content=["json"],
    result_serializer="json",
    timezone="UTC",
    enable_utc=True,
    task_track_started=True,
    task_time_limit=600,  # 10 minute timeout
    worker_prefetch_multiplier=1,  # One task at a time for GPU work
)
