import { useRef, useEffect } from 'react';
import { Image as ImageIcon } from 'lucide-react';
import { useImageStore } from '../stores/imageStore';
import { JobQueue, QueueItemData } from '@shared/components/JobQueue';

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

    const queueItems: QueueItemData[] = images.map(img => ({
        id: img.id,
        title: img.file.name,
        subtitle: `${(img.file.size / 1024 / 1024).toFixed(2)} MB`,
        status: img.status,
        thumbnailUrl: img.preview,
        // If processing and no specific progress, JobQueue defaults to indeterminate pulse
        progress: undefined, // Locate currently doesn't track granular progress
        icon: ImageIcon
    }));

    return (
        <div ref={scrollRef}>
            <JobQueue 
                items={queueItems}
                selectedId={selectedImageId || undefined}
                onSelect={(item) => setSelectedImage(item.id)}
                onDelete={(id) => removeImage(id)}
                emptyMessage="No images in queue"
            />
        </div>
    );
}
