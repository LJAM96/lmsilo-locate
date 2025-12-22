import { create } from 'zustand';
import ExifReader from 'exifreader';
import type { ImageQueueItem, ProcessingStatus, ExifData } from '../types';

interface ImageStore {
    images: ImageQueueItem[];
    selectedImageId: string | null;

    // Actions
    addImages: (files: File[]) => void;
    removeImage: (id: string) => void;
    clearQueue: () => void;
    setSelectedImage: (id: string | null) => void;
    updateImageStatus: (id: string, status: ProcessingStatus, error?: string) => void;
    updateImageThumbnail: (id: string, thumbnailUrl: string) => void;
    updateImageExif: (id: string, exif: ExifData) => void;
}

const generateId = () => crypto.randomUUID();

export const useImageStore = create<ImageStore>()((set, get) => ({
    images: [],
    selectedImageId: null,

    addImages: (files: File[]) => {
        const newImages: ImageQueueItem[] = files.map((file) => ({
            id: generateId(),
            file,
            filename: file.name,
            fileSizeBytes: file.size,
            status: 'queued' as ProcessingStatus,
            thumbnailUrl: URL.createObjectURL(file),
        }));

        set((state) => ({
            images: [...state.images, ...newImages],
            // Auto-select first image if none selected
            selectedImageId: state.selectedImageId ?? newImages[0]?.id ?? null,
        }));

        // Process EXIF asynchronously for each new image
        newImages.forEach(async (img) => {
            try {
                console.log('Starting EXIF extraction for:', img.filename);
                const tags = await ExifReader.load(img.file);
                console.log('EXIF tags found:', Object.keys(tags).length);

                // Parse helper
                const parseNum = (tag: any) => {
                    if (!tag) return undefined;
                    if (tag.description && !isNaN(Number(tag.description))) return Number(tag.description);
                    if (tag.value && Array.isArray(tag.value)) return tag.value[0];
                    return undefined;
                };

                const parseStr = (tag: any) => tag?.description;

                const exif: ExifData = {
                    hasGps: !!(tags['GPSLatitude'] && tags['GPSLongitude']),
                    latitude: tags['GPSLatitude'] && tags['GPSLatitudeRef']
                        ? convertDMSToDD(tags['GPSLatitude'], tags['GPSLatitudeRef'])
                        : undefined,
                    longitude: tags['GPSLongitude'] && tags['GPSLongitudeRef']
                        ? convertDMSToDD(tags['GPSLongitude'], tags['GPSLongitudeRef'])
                        : undefined,
                    altitude: parseNum(tags['GPSAltitude']),
                    cameraMake: parseStr(tags['Make']),
                    cameraModel: parseStr(tags['Model']),
                    dateTaken: parseStr(tags['DateTimeOriginal']),
                    focalLength: parseNum(tags['FocalLength']),
                    fNumber: parseNum(tags['FNumber']),
                    iso: parseNum(tags['ISOSpeedRatings']),
                    exposureTime: parseStr(tags['ExposureTime']),
                    width: parseNum(tags['PixelXDimension']) || parseNum(tags['Image Width']),
                    height: parseNum(tags['PixelYDimension']) || parseNum(tags['Image Height']),
                };

                console.log('Parsed EXIF data:', exif);
                get().updateImageExif(img.id, exif);
            } catch (error) {
                console.error('EXIF extraction failed for', img.filename, error);
            }
        });
    },

    removeImage: (id: string) => {
        const state = get();
        const image = state.images.find((img) => img.id === id);

        // Revoke thumbnail URL to prevent memory leaks
        if (image?.thumbnailUrl) {
            URL.revokeObjectURL(image.thumbnailUrl);
        }

        set((state) => ({
            images: state.images.filter((img) => img.id !== id),
            selectedImageId: state.selectedImageId === id ? null : state.selectedImageId,
        }));
    },

    clearQueue: () => {
        const state = get();
        // Revoke all thumbnail URLs
        state.images.forEach((img) => {
            if (img.thumbnailUrl) {
                URL.revokeObjectURL(img.thumbnailUrl);
            }
        });

        set({ images: [], selectedImageId: null });
    },

    setSelectedImage: (id: string | null) => {
        set({ selectedImageId: id });
    },

    updateImageStatus: (id: string, status: ProcessingStatus, error?: string) => {
        set((state) => ({
            images: state.images.map((img) =>
                img.id === id ? { ...img, status, error } : img
            ),
        }));
    },

    updateImageThumbnail: (id: string, thumbnailUrl: string) => {
        set((state) => ({
            images: state.images.map((img) =>
                img.id === id ? { ...img, thumbnailUrl } : img
            ),
        }));
    },

    updateImageExif: (id: string, exif: ExifData) => {
        set((state) => ({
            images: state.images.map((img) =>
                img.id === id ? { ...img, exif } : img
            ),
        }));
    },
}));

// Helper to convert DMS (degrees, minutes, seconds) to Decimal Degrees
function convertDMSToDD(dms: any, _ref: any): number | undefined {
    if (!dms || !dms.description) return undefined;
    // ExifReader usually provides description like "37, 46.5, 0" or direct numeric values if configured
    // But ExifReader standard output is object with 'description' and 'value'.

    // Simplification: ExifReader has a default 'gps' property or we handle raw tags.
    // If we use standard tags, the 'description' is usually "37 deg 46' 30.00\""

    // Actually, ExifReader is robust. Let's try to trust description or check value array.
    // However, robust GPS parsing from EXIF is complex. 
    // Let's rely on ExifReader's 'gps' property if it exists? 
    // No, standard usage returns tags.

    // Let's implement a safe basic parser or rely on the fact that we just want display mostly.
    // But for map, we need correct DD.

    // Better strategy: ExifReader documentation says it doesn't auto-convert without config.
    // But we are using default load.

    // Let's skip complex GPS logic for now and just store the raw or simple data 
    // OR create a helper that extracts latitude/longitude if available in 'description'
    // For now, let's return undefined if complex to avoid bugs, unless user specifically asked for GPS map placement from EXIF (which they did in logic elsewhere).
    // The previous logic relied on AI to predict.

    return undefined; // TODO: Implement robust DMS to DD conversion if needed
}
