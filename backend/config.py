"""
Locate Backend Configuration

Settings for database, Redis, and other service configuration.
"""

import os
from pathlib import Path
from typing import Literal

# Database
DATABASE_URL = os.getenv(
    "DATABASE_URL",
    "postgresql+asyncpg://lmsilo:lmsilo_password@localhost:5432/lmsilo"
)

# Redis
REDIS_URL = os.getenv("REDIS_URL", "redis://localhost:6379/0")

# File storage
UPLOAD_DIR = Path(os.getenv("UPLOAD_DIR", "/app/uploads"))
UPLOAD_DIR.mkdir(parents=True, exist_ok=True)

# HuggingFace
HF_HOME = os.getenv("HF_HOME", "/app/huggingface")
HF_TOKEN = os.getenv("HF_TOKEN", "")

# Device settings
DeviceChoice = Literal["auto", "cpu", "cuda", "rocm"]
DEVICE: DeviceChoice = os.getenv("DEVICE", "auto")  # type: ignore

# CORS origins
CORS_ORIGINS = [
    "http://localhost",
    "http://localhost:80",
    "http://localhost:8081",
    "http://localhost:5173",
]

# Security settings
ALLOWED_EXTENSIONS = {'.jpg', '.jpeg', '.png', '.bmp', '.gif', '.heic', '.webp'}
MAX_IMAGES_PER_REQUEST = 100
MAX_FILE_SIZE = 50 * 1024 * 1024  # 50MB per file
