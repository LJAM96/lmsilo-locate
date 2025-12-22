import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AppSettings } from '../types';

interface SettingsStore extends AppSettings {
    // Actions
    setApiUrl: (url: string) => void;
    setWsUrl: (url: string) => void;
    toggleDarkMode: () => void;
    setDarkMode: (enabled: boolean) => void;
    setTopK: (topK: number) => void;
    setDevice: (device: AppSettings['device']) => void;
    reset: () => void;
}

const defaultSettings: AppSettings = {
    apiUrl: import.meta.env.VITE_API_URL || 'http://localhost:8000',
    wsUrl: import.meta.env.VITE_WS_URL || 'ws://localhost:8000/ws',
    darkMode: false,
    topK: 5,
    device: 'auto',
};

export const useSettingsStore = create<SettingsStore>()(
    persist(
        (set, get) => ({
            ...defaultSettings,

            setApiUrl: (apiUrl: string) => set({ apiUrl }),

            setWsUrl: (wsUrl: string) => set({ wsUrl }),

            toggleDarkMode: () => {
                const newDarkMode = !get().darkMode;
                set({ darkMode: newDarkMode });

                // Update document class for Tailwind dark mode
                if (newDarkMode) {
                    document.documentElement.classList.add('dark');
                } else {
                    document.documentElement.classList.remove('dark');
                }
            },

            setDarkMode: (darkMode: boolean) => {
                set({ darkMode });
                if (darkMode) {
                    document.documentElement.classList.add('dark');
                } else {
                    document.documentElement.classList.remove('dark');
                }
            },

            setTopK: (topK: number) => set({ topK: Math.min(20, Math.max(1, topK)) }),

            setDevice: (device: AppSettings['device']) => set({ device }),

            reset: () => {
                set(defaultSettings);
                document.documentElement.classList.remove('dark');
            },
        }),
        {
            name: 'geolens-settings',
            onRehydrateStorage: () => (state) => {
                // Apply dark mode on rehydration
                if (state?.darkMode) {
                    document.documentElement.classList.add('dark');
                }
            },
        }
    )
);
