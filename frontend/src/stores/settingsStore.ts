import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AppSettings } from '../types';

interface SettingsStore extends AppSettings {
    // Actions
    setApiUrl: (url: string) => void;
    setWsUrl: (url: string) => void;
    setTopK: (topK: number) => void;
    setDevice: (device: AppSettings['device']) => void;
    reset: () => void;
}

const defaultSettings: AppSettings = {
    apiUrl: import.meta.env.VITE_API_URL || 'http://localhost:8000',
    wsUrl: import.meta.env.VITE_WS_URL || 'ws://localhost:8000/ws',
    topK: 5,
    device: 'auto',
};

export const useSettingsStore = create<SettingsStore>()(
    persist(
        (set) => ({
            ...defaultSettings,

            setApiUrl: (apiUrl: string) => set({ apiUrl }),

            setWsUrl: (wsUrl: string) => set({ wsUrl }),

            setTopK: (topK: number) => set({ topK: Math.min(20, Math.max(1, topK)) }),

            setDevice: (device: AppSettings['device']) => set({ device }),

            reset: () => set(defaultSettings),
        }),
        {
            name: 'locate-settings',
        }
    )
);
