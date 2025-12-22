import { useCallback, useState } from 'react';
import { Upload, ImagePlus } from 'lucide-react';
import { useImageStore } from '../stores/imageStore';

const ACCEPTED_TYPES = [
    'image/jpeg',
    'image/png',
    'image/gif',
    'image/bmp',
    'image/webp',
    'image/heic',
];

export function ImageUploader() {
    const [isDragOver, setIsDragOver] = useState(false);
    const addImages = useImageStore((s) => s.addImages);

    const handleFiles = useCallback(
        (files: FileList | null) => {
            if (!files) return;

            const validFiles = Array.from(files).filter((file) =>
                ACCEPTED_TYPES.includes(file.type) ||
                file.name.toLowerCase().endsWith('.heic')
            );

            if (validFiles.length > 0) {
                addImages(validFiles);
            }
        },
        [addImages]
    );

    const handleDragOver = useCallback((e: React.DragEvent) => {
        e.preventDefault();
        e.stopPropagation();
        setIsDragOver(true);
    }, []);

    const handleDragLeave = useCallback((e: React.DragEvent) => {
        e.preventDefault();
        e.stopPropagation();
        setIsDragOver(false);
    }, []);

    const handleDrop = useCallback(
        (e: React.DragEvent) => {
            e.preventDefault();
            e.stopPropagation();
            setIsDragOver(false);
            handleFiles(e.dataTransfer.files);
        },
        [handleFiles]
    );

    const handleInputChange = useCallback(
        (e: React.ChangeEvent<HTMLInputElement>) => {
            handleFiles(e.target.files);
            // Reset input so same file can be selected again
            e.target.value = '';
        },
        [handleFiles]
    );

    return (
        <div
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            className={`upload-zone ${isDragOver ? 'dragover' : ''}`}
        >
            <input
                type="file"
                id="image-upload"
                multiple
                accept={ACCEPTED_TYPES.join(',')}
                onChange={handleInputChange}
                className="hidden"
            />

            <label
                htmlFor="image-upload"
                className="flex flex-col items-center gap-4 cursor-pointer"
            >
                <div className={`
          w-16 h-16 rounded-2xl flex items-center justify-center
          ${isDragOver
                        ? 'bg-olive-100 dark:bg-olive-900/40'
                        : 'bg-cream-100 dark:bg-dark-400'
                    }
          transition-colors
        `}>
                    {isDragOver ? (
                        <ImagePlus className="w-8 h-8 text-olive-600 dark:text-olive-400" />
                    ) : (
                        <Upload className="w-8 h-8 text-surface-500 dark:text-surface-400" />
                    )}
                </div>

                <div className="text-center">
                    <p className="text-surface-700 dark:text-cream-200 font-medium">
                        {isDragOver ? 'Drop images here' : 'Drag & drop images'}
                    </p>
                    <p className="text-sm text-surface-500 dark:text-surface-400 mt-1">
                        or <span className="text-olive-600 dark:text-olive-400 font-medium">browse files</span>
                    </p>
                    <p className="text-xs text-surface-400 dark:text-surface-500 mt-2">
                        JPG, PNG, GIF, BMP, WebP, HEIC
                    </p>
                </div>
            </label>
        </div>
    );
}
