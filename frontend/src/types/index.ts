// Image queue item
export interface ImageQueueItem {
    id: string;
    file: File;
    filename: string;
    fileSizeBytes: number;
    status: ProcessingStatus;
    thumbnailUrl?: string;
    exif?: ExifData;
    error?: string;
}

export type ProcessingStatus = 'queued' | 'processing' | 'done' | 'cached' | 'error';

// Predictions (app-internal camelCase)
export interface Prediction {
    rank: number;
    latitude: number;
    longitude: number;
    probability: number;
    city: string;
    state: string;
    county: string;
    country: string;
    locationSummary: string;
}

// API response types (snake_case from backend)
export interface ApiPrediction {
    rank: number;
    latitude: number;
    longitude: number;
    probability: number;
    city: string;
    state: string;
    county: string;
    country: string;
    location_summary: string;
}

export interface ApiPredictionResult {
    path: string;
    md5?: string;
    predictions: ApiPrediction[];
    warnings: string[];
    error?: string;
}

export interface InferenceResponse {
    device: string;
    results: ApiPredictionResult[];
}

// EXIF data
export interface ExifData {
    hasGps: boolean;
    latitude?: number;
    longitude?: number;
    altitude?: number;
    cameraMake?: string;
    cameraModel?: string;
    dateTaken?: string;
    focalLength?: number;
    fNumber?: number;
    iso?: number;
    exposureTime?: string;
    width?: number;
    height?: number;
}

// Batch processing progress
export interface BatchProgress {
    totalImages: number;
    processedImages: number;
    cachedImages: number;
    currentImage?: string;
}

// Settings
export interface AppSettings {
    apiUrl: string;
    wsUrl: string;
    topK: number;
    device: 'auto' | 'cpu' | 'cuda' | 'rocm';
}

// API health
export interface HealthStatus {
    status: 'ok' | 'error';
    message?: string;
}
