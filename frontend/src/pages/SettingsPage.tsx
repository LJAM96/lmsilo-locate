import { useState, useEffect } from 'react';
import { Save, RotateCcw, CheckCircle, AlertCircle, Loader2 } from 'lucide-react';
import { useSettingsStore } from '../stores/settingsStore';
import { apiClient } from '../api/client';

export function SettingsPage() {
    const settings = useSettingsStore();
    const [localApiUrl, setLocalApiUrl] = useState(settings.apiUrl);
    const [localTopK, setLocalTopK] = useState(settings.topK);
    const [healthStatus, setHealthStatus] = useState<'checking' | 'ok' | 'error'>('checking');
    const [saved, setSaved] = useState(false);

    useEffect(() => {
        checkHealth();
    }, []);

    const checkHealth = async () => {
        setHealthStatus('checking');
        const health = await apiClient.health();
        setHealthStatus(health.status);
    };

    const handleSave = () => {
        settings.setApiUrl(localApiUrl);
        settings.setTopK(localTopK);
        apiClient.setBaseUrl(localApiUrl);

        setSaved(true);
        setTimeout(() => setSaved(false), 2000);

        checkHealth();
    };

    const handleReset = () => {
        settings.reset();
        setLocalApiUrl(settings.apiUrl);
        setLocalTopK(settings.topK);
        checkHealth();
    };

    return (
        <div className="p-6 lg:p-8 max-w-2xl">
            <div className="mb-8">
                <h1 className="font-serif text-2xl lg:text-3xl text-surface-800 dark:text-cream-100">
                    Settings
                </h1>
                <p className="text-surface-500 dark:text-surface-400 mt-1">
                    Configure API connection and processing options
                </p>
            </div>

            <div className="space-y-8">
                {/* API Configuration */}
                <section className="card space-y-4">
                    <h2 className="font-semibold text-surface-800 dark:text-cream-100">
                        API Configuration
                    </h2>

                    <div>
                        <label className="block text-sm font-medium text-surface-700 dark:text-cream-200 mb-2">
                            API URL
                        </label>
                        <div className="flex gap-3">
                            <input
                                type="url"
                                value={localApiUrl}
                                onChange={(e) => setLocalApiUrl(e.target.value)}
                                className="input flex-1"
                                placeholder="http://localhost:8000"
                            />
                            <button
                                onClick={checkHealth}
                                className="btn-secondary px-4"
                                title="Test connection"
                            >
                                {healthStatus === 'checking' ? (
                                    <Loader2 className="w-4 h-4 animate-spin" />
                                ) : healthStatus === 'ok' ? (
                                    <CheckCircle className="w-4 h-4 text-olive-600" />
                                ) : (
                                    <AlertCircle className="w-4 h-4 text-red-500" />
                                )}
                            </button>
                        </div>
                        <p className="text-xs text-surface-500 dark:text-surface-400 mt-1">
                            {healthStatus === 'ok' && 'Connected to API'}
                            {healthStatus === 'error' && 'Cannot connect to API'}
                            {healthStatus === 'checking' && 'Checking connection...'}
                        </p>
                    </div>
                </section>

                {/* Processing Options */}
                <section className="card space-y-4">
                    <h2 className="font-semibold text-surface-800 dark:text-cream-100">
                        Processing Options
                    </h2>

                    <div>
                        <label className="block text-sm font-medium text-surface-700 dark:text-cream-200 mb-2">
                            Predictions per image (Top-K)
                        </label>
                        <input
                            type="number"
                            min={1}
                            max={20}
                            value={localTopK}
                            onChange={(e) => setLocalTopK(parseInt(e.target.value) || 5)}
                            className="input w-24"
                        />
                        <p className="text-xs text-surface-500 dark:text-surface-400 mt-1">
                            Number of location predictions to return (1-20)
                        </p>
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-surface-700 dark:text-cream-200 mb-2">
                            Processing device
                        </label>
                        <select
                            value={settings.device}
                            onChange={(e) => settings.setDevice(e.target.value as any)}
                            className="input"
                        >
                            <option value="auto">Auto-detect</option>
                            <option value="cpu">CPU only</option>
                            <option value="cuda">NVIDIA CUDA</option>
                            <option value="rocm">AMD ROCm</option>
                        </select>
                        <p className="text-xs text-surface-500 dark:text-surface-400 mt-1">
                            GPU acceleration significantly speeds up processing
                        </p>
                    </div>
                </section>

                {/* Appearance */}
                <section className="card space-y-4">
                    <h2 className="font-semibold text-surface-800 dark:text-cream-100">
                        Appearance
                    </h2>

                    <div className="flex items-center justify-between">
                        <div>
                            <p className="font-medium text-surface-700 dark:text-cream-200">Dark mode</p>
                            <p className="text-sm text-surface-500 dark:text-surface-400">
                                Use dark theme for the interface
                            </p>
                        </div>
                        <button
                            onClick={settings.toggleDarkMode}
                            className={`
                relative w-12 h-6 rounded-full transition-colors
                ${settings.darkMode ? 'bg-olive-600' : 'bg-surface-300'}
              `}
                        >
                            <span
                                className={`
                  absolute top-1 left-1 w-4 h-4 rounded-full bg-white
                  transition-transform
                  ${settings.darkMode ? 'translate-x-6' : 'translate-x-0'}
                `}
                            />
                        </button>
                    </div>
                </section>

                {/* Actions */}
                <div className="flex items-center gap-3">
                    <button onClick={handleSave} className="btn-primary flex items-center gap-2">
                        {saved ? (
                            <>
                                <CheckCircle className="w-4 h-4" />
                                Saved!
                            </>
                        ) : (
                            <>
                                <Save className="w-4 h-4" />
                                Save Settings
                            </>
                        )}
                    </button>
                    <button onClick={handleReset} className="btn-secondary flex items-center gap-2">
                        <RotateCcw className="w-4 h-4" />
                        Reset to defaults
                    </button>
                </div>
            </div>
        </div>
    );
}
