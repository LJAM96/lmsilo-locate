import { Camera, Calendar, MapPin, Aperture, Clock } from 'lucide-react';
import type { ExifData } from '../types';

interface ExifPanelProps {
    exif?: ExifData;
    isLoading?: boolean;
}

export function ExifPanel({ exif, isLoading }: ExifPanelProps) {
    console.log('ExifPanel render props:', { exif, isLoading, keys: exif ? Object.keys(exif) : 'none' });

    if (isLoading) {
        return (
            <div className="card animate-pulse space-y-4">
                <div className="h-6 w-1/3 bg-cream-200 dark:bg-dark-50 rounded"></div>
                <div className="grid grid-cols-2 gap-4">
                    <div className="h-10 bg-cream-100 dark:bg-dark-100 rounded"></div>
                    <div className="h-10 bg-cream-100 dark:bg-dark-100 rounded"></div>
                    <div className="h-10 bg-cream-100 dark:bg-dark-100 rounded"></div>
                    <div className="h-10 bg-cream-100 dark:bg-dark-100 rounded"></div>
                </div>
            </div>
        );
    }

    if (!exif) {
        return null;
    }

    // Check if we have any meaningful data to show
    const hasData = Object.values(exif).some(v => v !== undefined && v !== false);
    if (!hasData) return null;

    return (
        <div className="card space-y-4">
            <div className="flex items-center gap-2 text-surface-800 dark:text-cream-100 font-medium">
                <Camera className="w-5 h-5 text-olive-600 dark:text-olive-400" />
                <h3>Camera Data</h3>
            </div>

            <div className="grid grid-cols-2 gap-4 text-sm">
                {/* Camera Info */}
                {(exif.cameraMake || exif.cameraModel) && (
                    <div className="col-span-2 p-3 bg-cream-50 dark:bg-dark-100 rounded-xl border border-cream-200 dark:border-dark-50">
                        <span className="text-surface-500 dark:text-surface-400 block text-xs uppercase tracking-wider mb-1">Device</span>
                        <div className="font-medium text-surface-800 dark:text-cream-200">
                            {exif.cameraMake} {exif.cameraModel}
                        </div>
                    </div>
                )}

                {/* Date Taken */}
                {exif.dateTaken && (
                    <div className="p-3 bg-cream-50 dark:bg-dark-100 rounded-xl border border-cream-200 dark:border-dark-50">
                        <div className="flex items-center gap-2 mb-1">
                            <Calendar className="w-3 h-3 text-surface-400" />
                            <span className="text-surface-500 dark:text-surface-400 text-xs uppercase tracking-wider">Date</span>
                        </div>
                        <div className="font-medium text-surface-800 dark:text-cream-200 truncate" title={exif.dateTaken}>
                            {new Date(exif.dateTaken.replace(/:/g, '/').replace('/', ':').replace('/', ':')).toLocaleDateString()}
                        </div>
                    </div>
                )}

                {/* Time Taken */}
                {exif.dateTaken && (
                    <div className="p-3 bg-cream-50 dark:bg-dark-100 rounded-xl border border-cream-200 dark:border-dark-50">
                        <div className="flex items-center gap-2 mb-1">
                            <Clock className="w-3 h-3 text-surface-400" />
                            <span className="text-surface-500 dark:text-surface-400 text-xs uppercase tracking-wider">Time</span>
                        </div>
                        <div className="font-medium text-surface-800 dark:text-cream-200">
                            {new Date(exif.dateTaken.replace(/:/g, '/').replace('/', ':').replace('/', ':')).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                        </div>
                    </div>
                )}

                {/* Settings: ISO, Aperture, Shutter */}
                {(exif.iso || exif.fNumber || exif.exposureTime) && (
                    <div className="col-span-2 p-3 bg-cream-50 dark:bg-dark-100 rounded-xl border border-cream-200 dark:border-dark-50">
                        <div className="flex items-center gap-2 mb-2">
                            <Aperture className="w-3 h-3 text-surface-400" />
                            <span className="text-surface-500 dark:text-surface-400 text-xs uppercase tracking-wider">Settings</span>
                        </div>
                        <div className="grid grid-cols-3 gap-2">
                            {exif.iso && (
                                <div>
                                    <span className="text-xs text-surface-500 block">ISO</span>
                                    <span className="font-medium text-surface-800 dark:text-cream-200">{exif.iso}</span>
                                </div>
                            )}
                            {exif.fNumber && (
                                <div>
                                    <span className="text-xs text-surface-500 block">Aperture</span>
                                    <span className="font-medium text-surface-800 dark:text-cream-200">f/{exif.fNumber}</span>
                                </div>
                            )}
                            {exif.exposureTime && (
                                <div>
                                    <span className="text-xs text-surface-500 block">Shutter</span>
                                    <span className="font-medium text-surface-800 dark:text-cream-200">{exif.exposureTime}s</span>
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {/* GPS */}
                {exif.hasGps && (exif.latitude || exif.longitude) && (
                    <div className="col-span-2 p-3 bg-olive-50 dark:bg-olive-900/20 rounded-xl border border-olive-200 dark:border-olive-800/30">
                        <div className="flex items-center gap-2 mb-1">
                            <MapPin className="w-3 h-3 text-olive-600 dark:text-olive-400" />
                            <span className="text-olive-700 dark:text-olive-300 text-xs uppercase tracking-wider">GPS Coordinates</span>
                        </div>
                        <div className="font-mono text-xs text-olive-800 dark:text-olive-200 truncate">
                            {exif.latitude?.toFixed(6)}, {exif.longitude?.toFixed(6)}
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
