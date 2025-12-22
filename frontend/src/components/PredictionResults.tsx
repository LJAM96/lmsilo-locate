import { MapPin, ChevronDown, ChevronUp } from 'lucide-react';
import { useState } from 'react';
import type { Prediction } from '../types';

interface PredictionCardProps {
    prediction: Prediction;
    isExpanded?: boolean;
}

function PredictionCard({ prediction, isExpanded: initialExpanded = false }: PredictionCardProps) {
    const [isExpanded, setIsExpanded] = useState(initialExpanded);

    const confidencePercent = (prediction.probability * 100).toFixed(1);
    const confidenceLevel =
        prediction.probability >= 0.1 ? 'High' :
            prediction.probability >= 0.05 ? 'Medium' : 'Low';

    const confidenceColor =
        prediction.probability >= 0.1 ? 'text-olive-600 dark:text-olive-400' :
            prediction.probability >= 0.05 ? 'text-amber-600 dark:text-amber-400' :
                'text-surface-500 dark:text-surface-400';

    return (
        <div className="card p-4 space-y-3">
            <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                    {/* Rank badge */}
                    <div className="w-8 h-8 rounded-lg bg-olive-100 dark:bg-olive-900/40 
                         flex items-center justify-center text-olive-700 dark:text-olive-300 
                         font-semibold text-sm">
                        {prediction.rank}
                    </div>

                    <div>
                        <p className="font-medium text-surface-800 dark:text-cream-100">
                            {prediction.locationSummary || 'Unknown Location'}
                        </p>
                        <p className={`text-sm ${confidenceColor}`}>
                            {confidencePercent}% • {confidenceLevel} confidence
                        </p>
                    </div>
                </div>

                <button
                    onClick={() => setIsExpanded(!isExpanded)}
                    className="p-1.5 rounded-lg hover:bg-cream-100 dark:hover:bg-dark-400 transition-colors"
                >
                    {isExpanded ? (
                        <ChevronUp className="w-4 h-4 text-surface-500" />
                    ) : (
                        <ChevronDown className="w-4 h-4 text-surface-500" />
                    )}
                </button>
            </div>

            {isExpanded && (
                <div className="pt-3 border-t border-cream-200 dark:border-dark-200 space-y-2">
                    <div className="grid grid-cols-2 gap-4 text-sm">
                        <div>
                            <p className="text-surface-500 dark:text-surface-400">Latitude</p>
                            <p className="font-mono text-surface-700 dark:text-cream-200">
                                {prediction.latitude.toFixed(6)}°
                            </p>
                        </div>
                        <div>
                            <p className="text-surface-500 dark:text-surface-400">Longitude</p>
                            <p className="font-mono text-surface-700 dark:text-cream-200">
                                {prediction.longitude.toFixed(6)}°
                            </p>
                        </div>
                    </div>

                    {(prediction.city || prediction.state || prediction.country) && (
                        <div className="grid grid-cols-3 gap-2 text-sm">
                            {prediction.city && (
                                <div>
                                    <p className="text-surface-500 dark:text-surface-400">City</p>
                                    <p className="text-surface-700 dark:text-cream-200">{prediction.city}</p>
                                </div>
                            )}
                            {prediction.state && (
                                <div>
                                    <p className="text-surface-500 dark:text-surface-400">State</p>
                                    <p className="text-surface-700 dark:text-cream-200">{prediction.state}</p>
                                </div>
                            )}
                            {prediction.country && (
                                <div>
                                    <p className="text-surface-500 dark:text-surface-400">Country</p>
                                    <p className="text-surface-700 dark:text-cream-200">{prediction.country}</p>
                                </div>
                            )}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}

interface PredictionResultsProps {
    predictions: Prediction[];
    isLoading?: boolean;
}

export function PredictionResults({ predictions, isLoading = false }: PredictionResultsProps) {
    if (isLoading) {
        return (
            <div className="space-y-3">
                {[1, 2, 3].map((i) => (
                    <div key={i} className="card p-4 animate-pulse">
                        <div className="flex items-center gap-3">
                            <div className="w-8 h-8 rounded-lg bg-cream-200 dark:bg-dark-300" />
                            <div className="flex-1 space-y-2">
                                <div className="h-4 bg-cream-200 dark:bg-dark-300 rounded w-3/4" />
                                <div className="h-3 bg-cream-200 dark:bg-dark-300 rounded w-1/2" />
                            </div>
                        </div>
                    </div>
                ))}
            </div>
        );
    }

    if (predictions.length === 0) {
        return (
            <div className="card p-8 text-center">
                <MapPin className="w-12 h-12 text-surface-300 dark:text-dark-100 mx-auto mb-3" />
                <p className="text-surface-500 dark:text-surface-400">
                    No predictions yet
                </p>
                <p className="text-sm text-surface-400 dark:text-surface-500 mt-1">
                    Select an image and click "Process" to get location predictions
                </p>
            </div>
        );
    }

    return (
        <div className="space-y-3">
            {predictions.map((prediction) => (
                <PredictionCard
                    key={prediction.rank}
                    prediction={prediction}
                    isExpanded={prediction.rank === 1}
                />
            ))}
        </div>
    );
}
