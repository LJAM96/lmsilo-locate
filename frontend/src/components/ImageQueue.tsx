import { useRef, useEffect } from 'react';
import { CheckCircle2, XCircle, Clock, Loader2, Trash2, Image as ImageIcon } from 'lucide-react';
import { useImageStore } from '../stores/imageStore';
import type { ImageState } from '../types';

export function ImageQueue() {
    const images = useImageStore((state) => state.images);
    const removeImage = useImageStore((state) => state.removeImage);
    const selectedImageId = useImageStore((state) => state.selectedImageId);
    const setSelectedImage = useImageStore((state) => state.setSelectedImage);

    const scrollRef = useRef<HTMLDivElement>(null);

    // Auto-scroll to bottom when new images are added
    useEffect(() => {
        if (scrollRef.current) {
            scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
        }
    }, [images.length]);

    if (images.length === 0) {
        return (
            <div className="text-center py-12 border-2 border-dashed border-cream-200 dark:border-dark-100 rounded-2xl bg-cream-50/50 dark:bg-dark-200/50">
                <div className="w-16 h-16 bg-cream-200 dark:bg-dark-100 rounded-2xl flex items-center justify-center mx-auto mb-4">
                    <ImageIcon className="w-8 h-8 text-surface-400" />
                </div>
                <p className="text-surface-500 font-medium">No images in queue</p>
                <p className="text-sm text-surface-400 mt-1">Upload files to get started</p>
            </div>
        );
    }

    return (
        <div className="space-y-3" ref={scrollRef}>
            {images.map((image) => (
                <QueueItem
                    key={image.id}
                    image={image}
                    isSelected={image.id === selectedImageId}
                    onSelect={() => setSelectedImage(image.id)}
                    onRemove={() => removeImage(image.id)}
                />
            ))}
        </div>
    );
}

interface QueueItemProps {
    image: ImageState;
    isSelected: boolean;
    onSelect: () => void;
    onRemove: () => void;
}

function QueueItem({ image, isSelected, onSelect, onRemove }: QueueItemProps) {
    const statusConfig = getStatusConfig(image.status);
    const isProcessing = image.status === 'processing';

    return (
        <div
            onClick={onSelect}
            className={`
                group relative overflow-hidden rounded-xl border p-4 transition-all cursor-pointer
                ${isSelected
                    ? 'bg-olive-50 dark:bg-olive-900/10 border-olive-500 dark:border-olive-400 shadow-md ring-1 ring-olive-500/20'
                    : 'bg-white dark:bg-dark-200 border-cream-200 dark:border-dark-100 hover:bg-cream-50 dark:hover:bg-dark-100 hover:shadow-sm'
                }
            `}
        >
            <div className="flex items-center gap-4">
                {/* Status Icon */}
                <div className={`w-12 h-12 rounded-xl flex items-center justify-center flex-shrink-0 ${statusConfig.bgColor} transition-colors`}>
                    {image.preview ? (
                        <img
                            src={image.preview}
                            alt="Thumbnail"
                            className="w-full h-full object-cover rounded-xl opacity-90"
                        />
                    ) : (
                        <statusConfig.icon className={`w-6 h-6 ${statusConfig.iconColor}`} />
                    )}
                </div>

                {/* Content */}
                <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                        <p className={`font-medium truncate ${isSelected ? 'text-olive-900 dark:text-olive-100' : 'text-surface-800 dark:text-cream-100'}`}>
                            {image.file.name}
                        </p>
                        <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium ${statusConfig.pillClass}`}>
                            <statusConfig.icon className="w-3 h-3" />
                            {formatStatus(image.status)}
                        </span>
                    </div>

                    <div className="flex items-center justify-between text-sm text-surface-500 dark:text-surface-400">
                        <span>{(image.file.size / 1024 / 1024).toFixed(2)} MB</span>
                        {isProcessing && <span className="text-olive-600 dark:text-olive-400 font-medium">Processing...</span>}
                    </div>

                    {/* Progress Bar for processing state */}
                    {isProcessing && (
                        <div className="mt-3 h-1.5 w-full bg-cream-200 dark:bg-dark-50 rounded-full overflow-hidden">
                            <div className="h-full bg-olive-500 rounded-full animate-pulse w-2/3"></div>
                        </div>
                    )}
                </div>

                {/* Actions */}
                <button
                    onClick={(e) => {
                        e.stopPropagation();
                        onRemove();
                    }}
                    className="p-2 text-surface-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 rounded-lg transition-colors opacity-0 group-hover:opacity-100 focus:opacity-100"
                    title="Remove image"
                >
                    <Trash2 className="w-5 h-5" />
                </button>
            </div>

            {/* Decorative status bar on left */}
            <div className={`absolute left-0 top-0 bottom-0 w-1 ${statusConfig.barColor}`} />
        </div>
    );
}

function getStatusConfig(status: ImageState['status']) {
    switch (status) {
        case 'done':
            return {
                icon: CheckCircle2,
                bgColor: 'bg-olive-100 dark:bg-olive-900/30',
                iconColor: 'text-olive-600 dark:text-olive-400',
                pillClass: 'bg-olive-100 dark:bg-olive-900/30 text-olive-700 dark:text-olive-300',
                barColor: 'bg-olive-500',
            };
        case 'error':
            return {
                icon: XCircle,
                bgColor: 'bg-red-100 dark:bg-red-900/30',
                iconColor: 'text-red-600 dark:text-red-400',
                pillClass: 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300',
                barColor: 'bg-red-500',
            };
        case 'processing':
            return {
                icon: Loader2,
                bgColor: 'bg-olive-100 dark:bg-olive-900/30',
                iconColor: 'text-olive-600 dark:text-olive-400 animate-spin',
                pillClass: 'bg-olive-100 dark:bg-olive-900/30 text-olive-700 dark:text-olive-300',
                barColor: 'bg-olive-500',
            };
        case 'queued':
        default:
            return {
                icon: Clock,
                bgColor: 'bg-cream-200 dark:bg-dark-50',
                iconColor: 'text-surface-500',
                pillClass: 'bg-cream-200 dark:bg-dark-50 text-surface-600 dark:text-surface-400',
                barColor: 'bg-surface-300 dark:bg-surface-700',
            };
    }
}

function formatStatus(status: string) {
    switch (status) {
        case 'done': return 'Completed';
        case 'error': return 'Failed';
        case 'processing': return 'Analysing';
        default: return 'Queued';
    }
}
