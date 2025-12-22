# Locate - Merged Docker Image
# Contains: Frontend (nginx) + Backend (uvicorn) + Worker (celery)
# Run mode determined by command override in docker-compose

# Stage 1: Build Frontend
FROM node:20-alpine AS frontend-builder

WORKDIR /app/frontend

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ .
RUN npm run build

# Stage 2: Python Backend
FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    nginx \
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

# Copy frontend build from builder stage
COPY --from=frontend-builder /app/frontend/dist /usr/share/nginx/html

# Copy nginx config
COPY docker/nginx.conf /etc/nginx/sites-available/default

# Copy supervisord config
COPY docker/supervisord.conf /etc/supervisor/conf.d/supervisord.conf

# Create directories
RUN mkdir -p /app/uploads /app/huggingface /var/log/supervisor

# Expose ports
EXPOSE 80 8000

# Default command runs supervisord (both nginx + uvicorn)
CMD ["supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
