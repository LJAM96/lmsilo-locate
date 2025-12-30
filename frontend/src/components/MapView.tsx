import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet';
import { useEffect, useMemo } from 'react';
import L from 'leaflet';
import type { Prediction } from '../types';

// Custom marker icons
const createMarkerIcon = (rank: number, isExif: boolean = false) => {
    const color = isExif ? '#22d3d1' : rank === 1 ? '#6d7a4e' : '#8a9766';
    const size = isExif ? 32 : rank === 1 ? 28 : 24;

    return L.divIcon({
        className: 'custom-marker',
        html: `
      <div style="
        width: ${size}px;
        height: ${size}px;
        background: ${color};
        border: 3px solid white;
        border-radius: 50%;
        box-shadow: 0 2px 8px rgba(0,0,0,0.3);
        display: flex;
        align-items: center;
        justify-content: center;
        color: white;
        font-size: 12px;
        font-weight: 600;
      ">
        ${isExif ? '📍' : rank}
      </div>
    `,
        iconSize: [size, size],
        iconAnchor: [size / 2, size / 2],
    });
};

// Component to fit bounds when predictions change
function FitBounds({ predictions }: { predictions: Prediction[] }) {
    const map = useMap();

    useEffect(() => {
        if (predictions.length === 0) return;

        const bounds = L.latLngBounds(
            predictions.map((p) => [p.latitude, p.longitude] as [number, number])
        );

        map.fitBounds(bounds, { padding: [50, 50], maxZoom: 10 });
    }, [map, predictions]);

    return null;
}

interface MapViewProps {
    predictions: Prediction[];
    exifLocation?: { latitude: number; longitude: number };
    className?: string;
}

import { useTheme } from '../lib/theme';

export function MapView({ predictions, exifLocation, className = '' }: MapViewProps) {
    // Dark mode tile layer
    const tileUrl = 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png';
    const darkTileUrl = 'https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png';

    // Get reactive dark mode state
    const { isDark } = useTheme();

    // Default center (world view)
    const defaultCenter: [number, number] = [20, 0];
    const defaultZoom = 2;

    const allMarkers = useMemo(() => {
        const markers: Array<{
            position: [number, number];
            prediction?: Prediction;
            isExif: boolean;
        }> = [];

        // Add EXIF marker first (if available)
        if (exifLocation) {
            markers.push({
                position: [exifLocation.latitude, exifLocation.longitude],
                isExif: true,
            });
        }

        // Add prediction markers
        predictions.forEach((pred) => {
            markers.push({
                position: [pred.latitude, pred.longitude],
                prediction: pred,
                isExif: false,
            });
        });

        return markers;
    }, [predictions, exifLocation]);

    return (
        <div className={`rounded-2xl overflow-hidden ${className}`}>
            <MapContainer
                center={defaultCenter}
                zoom={defaultZoom}
                className="w-full h-full min-h-[300px]"
                scrollWheelZoom={true}
                attributionControl={false}
            >
                <TileLayer
                    key={isDark ? 'dark' : 'light'}
                    attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>'
                    url={isDark ? darkTileUrl : tileUrl}
                />

                {predictions.length > 0 && <FitBounds predictions={predictions} />}

                {allMarkers.map((marker, index) => (
                    <Marker
                        key={index}
                        position={marker.position}
                        icon={createMarkerIcon(
                            marker.prediction?.rank ?? 0,
                            marker.isExif
                        )}
                    >
                        <Popup>
                            <div className="text-sm">
                                {marker.isExif ? (
                                    <>
                                        <p className="font-semibold text-cyan-600">EXIF GPS Location</p>
                                        <p className="text-gray-600">
                                            {marker.position[0].toFixed(6)}, {marker.position[1].toFixed(6)}
                                        </p>
                                    </>
                                ) : marker.prediction ? (
                                    <>
                                        <p className="font-semibold">
                                            #{marker.prediction.rank}: {marker.prediction.locationSummary || 'Unknown'}
                                        </p>
                                        <p className="text-gray-600">
                                            {(marker.prediction.probability * 100).toFixed(1)}% confidence
                                        </p>
                                        <p className="text-gray-500 text-xs mt-1">
                                            {marker.position[0].toFixed(6)}, {marker.position[1].toFixed(6)}
                                        </p>
                                    </>
                                ) : null}
                            </div>
                        </Popup>
                    </Marker>
                ))}
            </MapContainer>
        </div>
    );
}
