import { useState, useCallback, useEffect } from 'react';
import { Play, Download, Loader2, AlertCircle } from 'lucide-react';
import { ImageUploader } from '../components/ImageUploader';
import { ImageQueue } from '../components/ImageQueue';
import { MapView } from '../components/MapView';
import { PredictionResults } from '../components/PredictionResults';
import { ExifPanel } from '../components/ExifPanel';
import { useImageStore } from '../stores/imageStore';
import { usePredictionStore } from '../stores/predictionStore';
import { useSettingsStore } from '../stores/settingsStore';
import { apiClient } from '../api/client';
import type { Prediction } from '../types';

export function HomePage() {
    const [isProcessing, setIsProcessing] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const { images, selectedImageId, updateImageStatus, removeImage } = useImageStore();
    const { predictions, setPredictions } = usePredictionStore();
    const { topK, device } = useSettingsStore();

    const selectedImage = images.find((img) => img.id === selectedImageId);
    const currentPredictions = selectedImageId
        ? predictions.get(selectedImageId) ?? []
        : [];

    const queuedImages = images.filter((img) => img.status === 'queued');
    const canProcess = queuedImages.length > 0 && !isProcessing;

    const handleProcess = useCallback(async () => {
        if (!canProcess) return;

        setIsProcessing(true);
        setError(null);

        try {
            // Mark all queued images as processing
            queuedImages.forEach((img) => {
                updateImageStatus(img.id, 'processing');
            });

            // Upload and process files
            const files = queuedImages.map((img) => img.file);
            const response = await apiClient.inferWithUpload(files, { topK, device });

            // Map results back to images
            response.results.forEach((result, index) => {
                const image = queuedImages[index];
                if (!image) return;

                if (result.error) {
                    updateImageStatus(image.id, 'error', result.error);
                } else {
                    updateImageStatus(image.id, 'done');

                    // Convert response predictions to our format
                    const preds: Prediction[] = result.predictions.map((p) => ({
                        rank: p.rank,
                        latitude: p.latitude,
                        longitude: p.longitude,
                        probability: p.probability,
                        city: p.city,
                        state: p.state,
                        county: p.county,
                        country: p.country,
                        locationSummary: p.location_summary || `${p.city}, ${p.country}`.replace(/^, |, $/g, ''),
                    }));

                    setPredictions(image.id, preds);
                }
            });
        } catch (err: any) {
            console.error('Processing error:', err);
            let message = 'Processing failed';

            if (err.response?.data?.detail) {
                // Handle FastAPI specific error style
                message = typeof err.response.data.detail === 'string'
                    ? err.response.data.detail
                    : JSON.stringify(err.response.data.detail);
            } else if (err instanceof Error) {
                message = err.message;
            }

            setError(message);

            // Mark all processing images as error
            queuedImages.forEach((img) => {
                updateImageStatus(img.id, 'error', message);
            });
        } finally {
            setIsProcessing(false);
        }
    }, [canProcess, queuedImages, topK, device, updateImageStatus, setPredictions]);

    // Keyboard shortcuts
    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            // Delete selected image
            if ((e.key === 'Delete' || e.key === 'Backspace') && selectedImageId) {
                // Don't delete if user is typing in an input
                if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return;
                removeImage(selectedImageId);
            }

            // Ctrl+O to open upload
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'o') {
                e.preventDefault();
                document.getElementById('image-upload')?.click();
            }
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, [selectedImageId, removeImage]);

    const handleExportCSV = useCallback(() => {
        if (currentPredictions.length === 0) return;

        const headers = ['Rank', 'Latitude', 'Longitude', 'Probability', 'City', 'State', 'Country', 'Location'];
        const rows = currentPredictions.map((p) => [
            p.rank,
            p.latitude,
            p.longitude,
            p.probability,
            p.city,
            p.state,
            p.country,
            p.locationSummary,
        ]);

        const csv = [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = URL.createObjectURL(blob);

        const a = document.createElement('a');
        a.href = url;
        a.download = `geolens-predictions-${selectedImage?.filename ?? 'export'}.csv`;
        a.click();

        URL.revokeObjectURL(url);
    }, [currentPredictions, selectedImage]);

    return (
        <div className="p-6 lg:p-8 space-y-6">
            {/* Header */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="font-serif text-2xl lg:text-3xl text-surface-800 dark:text-cream-100">
                        Image Analysis
                    </h1>
                    <p className="text-surface-500 dark:text-surface-400 mt-1">
                        Upload images to predict their geographic location using AI
                    </p>
                </div>

                <div className="flex items-center gap-3">
                    {currentPredictions.length > 0 && (
                        <button
                            onClick={handleExportCSV}
                            className="btn-secondary flex items-center gap-2"
                        >
                            <Download className="w-4 h-4" />
                            Export CSV
                        </button>
                    )}

                    <button
                        onClick={handleProcess}
                        disabled={!canProcess}
                        className="btn-primary flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        {isProcessing ? (
                            <>
                                <Loader2 className="w-4 h-4 animate-spin" />
                                Processing...
                            </>
                        ) : (
                            <>
                                <Play className="w-4 h-4" />
                                Process ({queuedImages.length})
                            </>
                        )}
                    </button>
                </div>
            </div>

            {/* Error message */}
            {error && (
                <div className="flex items-center gap-3 p-4 rounded-xl bg-red-50 dark:bg-red-900/20 
                       border border-red-200 dark:border-red-800 text-red-700 dark:text-red-300">
                    <AlertCircle className="w-5 h-5 flex-shrink-0" />
                    <p>{error}</p>
                </div>
            )}

            {/* Main content grid */}
            <div className="grid lg:grid-cols-2 gap-6">
                {/* Left column: Upload & Queue */}
                <div className="space-y-6">
                    <ImageUploader />
                    <ImageQueue />
                </div>

                {/* Right column: Map & Predictions */}
                <div className="space-y-6">
                    {/* Map */}
                    <div className="card p-0 overflow-hidden h-[400px]">
                        <MapView
                            predictions={currentPredictions}
                            className="h-full"
                        />
                    </div>

                    {/* Predictions */}
                    <div>
                        <h3 className="font-medium text-surface-700 dark:text-cream-200 mb-3">
                            {selectedImage ? (
                                <>Predictions for <span className="font-mono text-sm">{selectedImage.filename}</span></>
                            ) : (
                                'Predictions'
                            )}
                        </h3>
                        <PredictionResults
                            predictions={currentPredictions}
                            isLoading={selectedImage?.status === 'processing'}
                        />

                        {/* EXIF Data */}
                        <div className="mt-6">
                            <ExifPanel exif={selectedImage?.exif} />
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
