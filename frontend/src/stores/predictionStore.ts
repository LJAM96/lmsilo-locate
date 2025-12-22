import { create } from 'zustand';
import type { Prediction, BatchProgress } from '../types';

interface PredictionStore {
    // Map of imageId -> predictions
    predictions: Map<string, Prediction[]>;

    // Processing state
    isProcessing: boolean;
    progress: BatchProgress;

    // Actions
    setPredictions: (imageId: string, predictions: Prediction[]) => void;
    clearPredictions: (imageId?: string) => void;
    setProcessing: (isProcessing: boolean) => void;
    updateProgress: (progress: Partial<BatchProgress>) => void;
    resetProgress: () => void;
}

const initialProgress: BatchProgress = {
    totalImages: 0,
    processedImages: 0,
    cachedImages: 0,
    currentImage: undefined,
};

export const usePredictionStore = create<PredictionStore>()((set) => ({
    predictions: new Map(),
    isProcessing: false,
    progress: initialProgress,

    setPredictions: (imageId: string, predictions: Prediction[]) => {
        set((state) => {
            const newPredictions = new Map(state.predictions);
            newPredictions.set(imageId, predictions);
            return { predictions: newPredictions };
        });
    },

    clearPredictions: (imageId?: string) => {
        if (imageId) {
            set((state) => {
                const newPredictions = new Map(state.predictions);
                newPredictions.delete(imageId);
                return { predictions: newPredictions };
            });
        } else {
            set({ predictions: new Map() });
        }
    },

    setProcessing: (isProcessing: boolean) => {
        set({ isProcessing });
    },

    updateProgress: (progress: Partial<BatchProgress>) => {
        set((state) => ({
            progress: { ...state.progress, ...progress },
        }));
    },

    resetProgress: () => {
        set({ progress: initialProgress });
    },
}));
