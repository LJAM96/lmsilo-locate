# Locate User Guide

## Overview
Locate is an AI-powered image geolocation tool that predicts where a photo was taken using the GeoCLIP model. It analyzes visual features to estimate GPS coordinates.

## Getting Started

### Access
Navigate to **http://localhost:8081** in your browser.

### Upload an Image
1. Click **Upload Image** or drag and drop
2. Supported formats: JPEG, PNG
3. Maximum file size: 50MB

### View Results
After processing, you'll see:
- **Top Predictions**: Ranked locations with confidence scores
- **Map View**: Interactive map with markers
- **Coordinates**: Latitude/longitude for each prediction

## Features

### Batch Processing
Upload multiple images to process them in sequence. Each image is queued and processed by the worker.

### Job History
View previous geolocation jobs in the sidebar. Results are stored in the database for later reference.

### Export Results
Download results as:
- JSON (full data)
- CSV (coordinates only)

## How It Works

1. **Image Upload**: Your image is uploaded to the server
2. **Queue**: Job is added to the Redis queue
3. **Processing**: Celery worker runs GeoCLIP model
4. **Results**: GPS predictions returned with confidence scores

## Model Details

**GeoCLIP** uses contrastive learning to match image features with geographic locations. It was trained on millions of geotagged images.

- Accuracy varies by scene type
- Works best with outdoor images
- Urban scenes often have higher accuracy

## Tips for Best Results

- Use high-resolution images
- Outdoor scenes work better than indoor
- Landmarks improve accuracy
- Avoid heavily cropped images

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Job stuck "pending" | Check worker is running |
| Low confidence | Try different image |
| No predictions | Ensure valid image format |
