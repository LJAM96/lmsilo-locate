# GeoLens

AI-powered image geolocation using GeoCLIP.

## Architecture

```
geolens/
├── backend/          # Python FastAPI + GeoCLIP
│   ├── api_web.py    # Web API with file upload
│   ├── api_service.py # Original path-based API
│   ├── ai_street.py  # CLI tool
│   └── llocale/      # GeoCLIP predictor
├── frontend/         # React + TypeScript + Tailwind
│   ├── src/
│   │   ├── components/
│   │   ├── pages/
│   │   ├── stores/   # Zustand state management
│   │   └── api/      # API client
│   └── electron/     # Electron main process
├── docs/             # Documentation
└── docker/           # Docker configuration
```

## Quick Start

### Prerequisites
- Python 3.10+
- Node.js 18+
- (Optional) NVIDIA GPU with CUDA for faster inference

### Development

**Backend:**
```bash
cd backend
pip install -r requirements.txt
pip install uvicorn python-multipart

# Run API server
uvicorn backend.api_web:app --reload --host 0.0.0.0 --port 8000
```

**Frontend:**
```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173

### Docker Deployment

```bash
# Build and run both services
docker-compose up --build

# Or build individually
docker build -t geolens-frontend -f Dockerfile.frontend .
docker build -t geolens-backend -f Dockerfile.backend .
```

Frontend: http://localhost:8080  
Backend API: http://localhost:8000

### Electron Desktop App

```bash
cd frontend

# Development mode
npm run electron:dev

# Build for Windows
npm run electron:build:win

# Build for macOS
npm run electron:build:mac

# Build for Linux
npm run electron:build:linux
```

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| POST | `/upload` | Upload images (returns paths) |
| POST | `/infer` | Run geolocation inference |

### Example: Upload and Infer

```bash
# Upload images
curl -X POST http://localhost:8000/upload \
  -F "files=@photo1.jpg" \
  -F "files=@photo2.jpg"

# Run inference
curl -X POST http://localhost:8000/infer \
  -H "Content-Type: application/json" \
  -d '{
    "items": [{"path": "/tmp/geolens_uploads/abc123.jpg"}],
    "top_k": 5,
    "device": "auto"
  }'
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `VITE_API_URL` | Backend API URL | `http://localhost:8000` |
| `VITE_WS_URL` | WebSocket URL | `ws://localhost:8000/ws` |
| `HF_HOME` | HuggingFace cache directory | `~/.cache/huggingface` |

## Design System

The frontend uses a "Cream & Olive" aesthetic:

- **Cream** - Warm backgrounds (`#fefdfb`, `#fdf9f3`)
- **Olive** - Brand color, buttons (`#6d7a4e`, `#8a9766`)
- **Surface** - Warm gray text (`#514d49`)
- **Dark Mode** - Rich warm blacks (`#151413`)

See [docs/frontend.md](docs/frontend.md) for full design system.

## License

MIT
