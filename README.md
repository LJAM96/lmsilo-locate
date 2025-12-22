# LMSilo Locate

AI-powered image geolocation service using GeoCLIP to predict where photos were taken.

## Features

- **GeoCLIP Model**: State-of-the-art image geolocation
- **Top-K Predictions**: Multiple location candidates with confidence
- **Batch Processing**: Process multiple images
- **Shared Workspace**: All users see job queue
- **Audit Logging**: Full usage tracking

## Architecture

```
locate/
├── backend/
│   ├── api_service.py    # FastAPI application
│   ├── api/
│   │   └── jobs.py       # Job management API
│   ├── models/
│   │   └── job.py        # Job SQLAlchemy model
│   ├── services/
│   │   └── database.py   # PostgreSQL connection
│   ├── workers/
│   │   ├── celery_app.py
│   │   └── tasks.py      # GeoCLIP processing
│   └── llocale/          # GeoCLIP predictor
├── frontend/
│   └── src/
│       ├── App.tsx
│       └── components/
│           ├── JobList.tsx
│           └── AuditLogViewer.tsx
└── Dockerfile
```

## API Endpoints

### Inference
- `POST /infer` - Batch image geolocation
- `GET /health` - Health check

### Jobs
- `POST /api/jobs` - Create geolocation job
- `GET /api/jobs` - List all jobs
- `GET /api/jobs/{id}` - Get job results
- `DELETE /api/jobs/{id}` - Delete job

### Audit
- `GET /api/audit` - List audit logs
- `GET /api/audit/export` - Export CSV/JSON

## Inference Request

```json
{
  "items": [
    {"path": "/path/to/image.jpg", "md5": "optional"}
  ],
  "top_k": 5,
  "device": "auto"
}
```

## Response

```json
{
  "device": "cuda",
  "results": [
    {
      "path": "/path/to/image.jpg",
      "predictions": [
        {
          "rank": 1,
          "latitude": 48.8584,
          "longitude": 2.2945,
          "probability": 0.85,
          "country": "France",
          "city": "Paris"
        }
      ]
    }
  ]
}
```

## Development

```bash
cd locate

# Backend
cd backend
pip install -r requirements.txt
uvicorn api_service:app --reload

# Worker
celery -A workers.celery_app worker -l info
```

## License

MIT
