using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GeoLens.Models
{
    /// <summary>
    /// Represents an image in the processing queue
    /// </summary>
    public class ImageQueueItem : INotifyPropertyChanged
    {
        // Cached brushes to avoid allocating new brushes on every property access
        private static readonly SolidColorBrush QueuedBrush = new(Microsoft.UI.Colors.Gray);
        private static readonly SolidColorBrush ProcessingBrush = new(Microsoft.UI.Colors.DodgerBlue);
        private static readonly SolidColorBrush DoneBrush = new(Microsoft.UI.Colors.LimeGreen);
        private static readonly SolidColorBrush ErrorBrush = new(Microsoft.UI.Colors.IndianRed);
        private static readonly SolidColorBrush CachedBrush = new(Microsoft.UI.Colors.Cyan);

        private QueueStatus _status;
        private bool _isSelected;
        private ImageSource? _thumbnailSource;

        public string FilePath { get; set; } = string.Empty;
        public string FileName => System.IO.Path.GetFileName(FilePath);
        public long FileSizeBytes { get; set; }
        public string FileSizeFormatted => FormatFileSize(FileSizeBytes);
        public bool IsCached { get; set; }

        public QueueStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusGlyph));
                    OnPropertyChanged(nameof(IsProcessing));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public ImageSource? ThumbnailSource
        {
            get => _thumbnailSource;
            set
            {
                if (_thumbnailSource != value)
                {
                    _thumbnailSource = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusText => Status switch
        {
            QueueStatus.Queued => "Queued",
            QueueStatus.Processing => "Processing",
            QueueStatus.Done => "Done",
            QueueStatus.Error => "Error",
            QueueStatus.Cached => "Cached",
            _ => "Unknown"
        };

        public Brush StatusColor => Status switch
        {
            QueueStatus.Queued => QueuedBrush,
            QueueStatus.Processing => ProcessingBrush,
            QueueStatus.Done => DoneBrush,
            QueueStatus.Error => ErrorBrush,
            QueueStatus.Cached => CachedBrush,
            _ => QueuedBrush
        };

        public string StatusGlyph => Status switch
        {
            QueueStatus.Queued => "\uE917", // Clock
            QueueStatus.Processing => "\uE895", // Sync
            QueueStatus.Done => "\uE73E", // Checkmark
            QueueStatus.Error => "\uE711", // Cancel
            QueueStatus.Cached => "\uE8FB", // Accept
            _ => "\uE946" // Info
        };

        public bool IsProcessing => Status == QueueStatus.Processing;

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
