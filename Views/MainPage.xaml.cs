using GeoLens.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GeoLens.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private bool _isAllSelected;
        private bool _hasExifGps;
        private string _reliabilityMessage = "No image selected";

        // Observable Collections
        public ObservableCollection<ImageQueueItem> ImageQueue { get; } = new();
        public ObservableCollection<EnhancedLocationPrediction> Predictions { get; } = new();

        // Properties for UI bindings
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (_isAllSelected != value)
                {
                    _isAllSelected = value;
                    OnPropertyChanged();
                    UpdateAllSelections(value);
                }
            }
        }

        public string SelectedCountText => $"{ImageQueue.Count(i => i.IsSelected)} selected";

        public string QueueStatusMessage => ImageQueue.Count == 0
            ? "No images in queue"
            : $"{ImageQueue.Count} image(s) in queue";

        public bool HasExifGps
        {
            get => _hasExifGps;
            set
            {
                if (_hasExifGps != value)
                {
                    _hasExifGps = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ExifLocationName { get; private set; } = "Paris, France";
        public string ExifLat { get; private set; } = "48.8566° N";
        public string ExifLon { get; private set; } = "2.3522° E";

        public string ReliabilityMessage
        {
            get => _reliabilityMessage;
            set
            {
                if (_reliabilityMessage != value)
                {
                    _reliabilityMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            LoadMockData();
        }

        private void LoadMockData()
        {
            // Create mock image queue items
            var mockImages = new[]
            {
                new ImageQueueItem
                {
                    FilePath = "C:\\Photos\\eiffel_tower.jpg",
                    FileSizeBytes = 2_450_000,
                    Status = QueueStatus.Done,
                    IsCached = false,
                    ThumbnailSource = CreateMockThumbnail("#8B4513")
                },
                new ImageQueueItem
                {
                    FilePath = "C:\\Photos\\temple_kyoto.jpg",
                    FileSizeBytes = 3_120_000,
                    Status = QueueStatus.Done,
                    IsCached = true,
                    ThumbnailSource = CreateMockThumbnail("#DC143C")
                },
                new ImageQueueItem
                {
                    FilePath = "C:\\Photos\\machu_picchu.jpg",
                    FileSizeBytes = 1_890_000,
                    Status = QueueStatus.Queued,
                    IsCached = false,
                    ThumbnailSource = CreateMockThumbnail("#228B22")
                },
                new ImageQueueItem
                {
                    FilePath = "C:\\Photos\\taj_mahal.jpg",
                    FileSizeBytes = 2_780_000,
                    Status = QueueStatus.Processing,
                    IsCached = false,
                    ThumbnailSource = CreateMockThumbnail("#FFD700")
                },
                new ImageQueueItem
                {
                    FilePath = "C:\\Photos\\grand_canyon.jpg",
                    FileSizeBytes = 4_200_000,
                    Status = QueueStatus.Queued,
                    IsCached = false,
                    ThumbnailSource = CreateMockThumbnail("#FF4500")
                }
            };

            foreach (var item in mockImages)
            {
                item.PropertyChanged += ImageQueueItem_PropertyChanged;
                ImageQueue.Add(item);
            }

            // Create mock predictions with various confidence levels
            var mockPredictions = new[]
            {
                new EnhancedLocationPrediction
                {
                    Rank = 1,
                    Latitude = 48.8584,
                    Longitude = 2.2945,
                    Probability = 0.342,
                    AdjustedProbability = 0.342,
                    City = "Paris",
                    State = "Île-de-France",
                    Country = "France",
                    LocationSummary = "Paris, Île-de-France, France",
                    IsPartOfCluster = false,
                    ConfidenceLevel = ConfidenceLevel.High
                },
                new EnhancedLocationPrediction
                {
                    Rank = 2,
                    Latitude = 48.8606,
                    Longitude = 2.3376,
                    Probability = 0.187,
                    AdjustedProbability = 0.337, // Boosted by clustering
                    City = "Paris",
                    State = "Île-de-France",
                    Country = "France",
                    LocationSummary = "Paris (Central), France",
                    IsPartOfCluster = true,
                    ConfidenceLevel = ConfidenceLevel.High
                },
                new EnhancedLocationPrediction
                {
                    Rank = 3,
                    Latitude = 48.8738,
                    Longitude = 2.2950,
                    Probability = 0.124,
                    AdjustedProbability = 0.274, // Boosted by clustering
                    City = "Neuilly-sur-Seine",
                    State = "Île-de-France",
                    Country = "France",
                    LocationSummary = "Neuilly-sur-Seine, France",
                    IsPartOfCluster = true,
                    ConfidenceLevel = ConfidenceLevel.High
                },
                new EnhancedLocationPrediction
                {
                    Rank = 4,
                    Latitude = 51.5074,
                    Longitude = -0.1278,
                    Probability = 0.082,
                    AdjustedProbability = 0.082,
                    City = "London",
                    State = "England",
                    Country = "United Kingdom",
                    LocationSummary = "London, United Kingdom",
                    IsPartOfCluster = false,
                    ConfidenceLevel = ConfidenceLevel.Medium
                },
                new EnhancedLocationPrediction
                {
                    Rank = 5,
                    Latitude = 52.5200,
                    Longitude = 13.4050,
                    Probability = 0.041,
                    AdjustedProbability = 0.041,
                    City = "Berlin",
                    State = "Berlin",
                    Country = "Germany",
                    LocationSummary = "Berlin, Germany",
                    IsPartOfCluster = false,
                    ConfidenceLevel = ConfidenceLevel.Low
                }
            };

            foreach (var prediction in mockPredictions)
            {
                Predictions.Add(prediction);
            }

            // Set EXIF GPS data (mock)
            HasExifGps = false; // Set to true to show EXIF panel
            ReliabilityMessage = "High reliability - predictions clustered within 12km";

            OnPropertyChanged(nameof(QueueStatusMessage));
        }

        private ImageSource CreateMockThumbnail(string colorHex)
        {
            // Create a simple colored bitmap as a mock thumbnail
            // In a real app, this would load the actual image thumbnail
            var writeableBitmap = new WriteableBitmap(200, 200);

            // For now, just return a placeholder
            // The actual thumbnail loading would happen asynchronously
            return writeableBitmap;
        }

        private void ImageQueueItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImageQueueItem.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCountText));
            }
        }

        private void UpdateAllSelections(bool selected)
        {
            foreach (var item in ImageQueue)
            {
                item.IsSelected = selected;
            }
        }

        // Event Handlers
        private void AddImages_Click(object sender, RoutedEventArgs e)
        {
            // Mock adding an image
            var newImage = new ImageQueueItem
            {
                FilePath = $"C:\\Photos\\image_{ImageQueue.Count + 1}.jpg",
                FileSizeBytes = 2_500_000,
                Status = QueueStatus.Queued,
                IsCached = false,
                ThumbnailSource = CreateMockThumbnail("#" + Random.Shared.Next(0x1000000).ToString("X6"))
            };

            newImage.PropertyChanged += ImageQueueItem_PropertyChanged;
            ImageQueue.Add(newImage);
            OnPropertyChanged(nameof(QueueStatusMessage));
        }

        private void ProcessImages_Click(object sender, RoutedEventArgs e)
        {
            // Mock processing
            foreach (var item in ImageQueue.Where(i => i.Status == QueueStatus.Queued))
            {
                item.Status = QueueStatus.Processing;
            }

            ReliabilityMessage = "Processing images...";
        }

        private void ExportSelection_Click(object sender, RoutedEventArgs e)
        {
            var selectedCount = ImageQueue.Count(i => i.IsSelected);
            // TODO: Implement export functionality
            System.Diagnostics.Debug.WriteLine($"Export {selectedCount} selected images");
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ImageQueue)
            {
                item.IsSelected = false;
            }
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = ImageQueue.Where(i => i.IsSelected).ToList();
            foreach (var item in toRemove)
            {
                item.PropertyChanged -= ImageQueueItem_PropertyChanged;
                ImageQueue.Remove(item);
            }
            OnPropertyChanged(nameof(QueueStatusMessage));
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            App.ShowSettingsWindow();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
