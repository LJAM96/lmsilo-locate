# Locate - Backend Only
# API server + Celery worker for image geolocation
# Frontend now served by Portal

FROM python:3.11-slim

WORKDIR /app

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    supervisor \
    && rm -rf /var/lib/apt/lists/*

# Copy backend requirements and install
COPY backend/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Install additional dependencies for queue system
RUN pip install --no-cache-dir \
    uvicorn[standard] \
    python-multipart \
    celery[redis] \
    sqlalchemy[asyncio] \
    asyncpg \
    psycopg2-binary

# Copy backend code
COPY backend/ ./backend/

# Create supervisord config
RUN mkdir -p /etc/supervisor/conf.d /var/log/supervisor
COPY <<EOF /etc/supervisor/conf.d/supervisord.conf
[supervisord]
nodaemon=true
user=root
logfile=/var/log/supervisor/supervisord.log
pidfile=/var/run/supervisord.pid

[program:uvicorn]
command=python -m uvicorn backend.main:app --host 0.0.0.0 --port 8000
directory=/app
autostart=true
autorestart=true
stdout_logfile=/var/log/supervisor/uvicorn.log
stderr_logfile=/var/log/supervisor/uvicorn_error.log

[program:celery]
command=python -m celery -A backend.workers.celery_app worker -Q locate -c 1 --loglevel=info
directory=/app
autostart=true
autorestart=true
stdout_logfile=/var/log/supervisor/celery.log
stderr_logfile=/var/log/supervisor/celery_error.log
EOF

# Create directories
RUN mkdir -p /app/uploads /app/huggingface

# Expose API port only
EXPOSE 8000

# Run supervisord
CMD ["supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
