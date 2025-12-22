# Locate API Reference

## Base URL
```
http://localhost:8081/api
```

## Endpoints

### Health Check
```
GET /health
```
Returns service health status.

**Response:**
```json
{
  "status": "healthy",
  "version": "1.0.0"
}
```

---

### Submit Geolocation Job
```
POST /jobs
```
Submit an image for geolocation analysis.

**Request:** `multipart/form-data`
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| file | File | Yes | Image file (JPEG, PNG) |
| top_k | int | No | Number of location predictions (default: 5) |

**Response:**
```json
{
  "job_id": "uuid",
  "status": "pending",
  "created_at": "2024-01-01T00:00:00Z"
}
```

---

### Get Job Status
```
GET /jobs/{job_id}
```
Get the status and results of a geolocation job.

**Response:**
```json
{
  "job_id": "uuid",
  "status": "completed",
  "results": [
    {
      "latitude": 48.8584,
      "longitude": 2.2945,
      "confidence": 0.85,
      "location_name": "Paris, France"
    }
  ],
  "created_at": "2024-01-01T00:00:00Z",
  "completed_at": "2024-01-01T00:00:05Z"
}
```

**Status Values:**
- `pending` - Job queued
- `processing` - Being processed
- `completed` - Results ready
- `failed` - Processing error

---

### List Jobs
```
GET /jobs
```
List all geolocation jobs.

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| limit | int | Max results (default: 50) |
| offset | int | Pagination offset |
| status | string | Filter by status |

---

### Delete Job
```
DELETE /jobs/{job_id}
```
Delete a job and its results.

## Error Responses
```json
{
  "detail": "Error message"
}
```

| Code | Description |
|------|-------------|
| 400 | Invalid request |
| 404 | Job not found |
| 500 | Server error |
