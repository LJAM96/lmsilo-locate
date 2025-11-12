using GeoLens.Models;
using GeoLens.Services;
using GeoLens.Services.MapProviders;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MUXC = Microsoft.UI.Xaml.Controls;

namespace GeoLens.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private bool _hasExifGps;
        private string _reliabilityMessage = "No image selected";
        private string _selectedCountText = "";
        private IMapProvider? _mapProvider;

        // Observable Collections
        public ObservableCollection<ImageQueueItem> ImageQueue { get; } = new();
        public ObservableCollection<EnhancedLocationPrediction> Predictions { get; } = new();

        // Properties for UI bindings
        public string SelectedCountText
        {
            get => _selectedCountText;
            set
            {
                if (_selectedCountText != value)
                {
                    _selectedCountText = value;
                    OnPropertyChanged();
                }
            }
        }

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

            // Wire up selection changed event
            ImageListView.SelectionChanged += ImageListView_SelectionChanged;

            // Initialize globe when page loads
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeGlobeAsync();
        }

        private async Task InitializeGlobeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] Initializing globe...");

                // Create provider (auto-detect online/offline)
                _mapProvider = new WebView2GlobeProvider(GlobeWebView, offlineMode: false);

                // Initialize
                await _mapProvider.InitializeAsync();

                // Hide loading overlay
                GlobeLoadingOverlay.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine("[MainPage] Globe ready");

                // If we already have mock predictions, add them to the globe
                if (Predictions.Count > 0)
                {
                    foreach (var pred in Predictions)
                    {
                        await _mapProvider.AddPinAsync(
                            pred.Latitude,
                            pred.Longitude,
                            pred.LocationSummary,
                            pred.Probability,
                            pred.Rank,
                            isExif: false
                        );
                    }

                    // Rotate to first prediction
                    var first = Predictions[0];
                    await _mapProvider.RotateToLocationAsync(first.Latitude, first.Longitude, 1500);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Globe initialization failed: {ex.Message}");

                // Update loading overlay to show error
                GlobeLoadingOverlay.Visibility = Visibility.Visible;
                // TODO: Update overlay UI to show error message instead of loading
            }
        }

        private void ImageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedCount = ImageListView.SelectedItems.Count;
            SelectedCountText = selectedCount > 0 ? $"{selectedCount} selected" : "";
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

        // Event Handlers
        private async void AddImages_Click(object sender, RoutedEventArgs e)
        {
            // Use file picker to select images
            var picker = new Windows.Storage.Pickers.FileOpenPicker();

            // Initialize with window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            // Configure picker
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".gif");

            // Let user select multiple files
            var files = await picker.PickMultipleFilesAsync();

            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    // Get file info
                    var props = await file.GetBasicPropertiesAsync();

                    // Load thumbnail
                    var thumbnail = await file.GetThumbnailAsync(
                        Windows.Storage.FileProperties.ThumbnailMode.PicturesView,
                        140,
                        Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);

                    BitmapImage thumbnailImage = new BitmapImage();
                    await thumbnailImage.SetSourceAsync(thumbnail);

                    // Add to queue
                    var newImage = new ImageQueueItem
                    {
                        FilePath = file.Path,
                        FileSizeBytes = (long)props.Size,
                        Status = QueueStatus.Queued,
                        IsCached = false,
                        ThumbnailSource = thumbnailImage
                    };

                    ImageQueue.Add(newImage);
                }

                OnPropertyChanged(nameof(QueueStatusMessage));

                System.Diagnostics.Debug.WriteLine($"Added {files.Count} image(s) to queue");
            }
        }

        private async void ProcessImages_Click(object sender, RoutedEventArgs e)
        {
            // Get queued images
            var queuedImages = ImageQueue.Where(i => i.Status == QueueStatus.Queued).ToList();

            if (queuedImages.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("No images in queue to process");
                return;
            }

            // Check if API client is available
            if (App.ApiClient == null)
            {
                ReliabilityMessage = "AI service not available";
                return;
            }

            try
            {
                // Update status to processing
                foreach (var item in queuedImages)
                {
                    item.Status = QueueStatus.Processing;
                }

                ReliabilityMessage = "Processing images...";

                // Call API for predictions
                var imagePaths = queuedImages.Select(i => i.FilePath).ToList();
                var response = await App.ApiClient.InferBatchAsync(imagePaths, topK: 5);

                // Process results
                if (response != null)
                {
                    foreach (var result in response)
                {
                    var imageItem = ImageQueue.FirstOrDefault(i => i.FilePath == result.Path);
                    if (imageItem != null)
                    {
                        imageItem.Status = string.IsNullOrEmpty(result.Error) ? QueueStatus.Done : QueueStatus.Error;

                        // Display predictions for first image (or selected image)
                        if (imageItem == queuedImages.First())
                        {
                            DisplayPredictions(result);
                        }
                    }
                }
                }

                ReliabilityMessage = $"Processed {queuedImages.Count} image(s)";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing images: {ex.Message}");
                ReliabilityMessage = $"Error: {ex.Message}";

                // Mark as error
                foreach (var item in queuedImages)
                {
                    item.Status = QueueStatus.Error;
                }
            }
        }

        private async void DisplayPredictions(Services.DTOs.PredictionResult result)
        {
            // Clear existing predictions
            Predictions.Clear();

            // Clear globe pins
            if (_mapProvider != null && _mapProvider.IsReady)
            {
                await _mapProvider.ClearPinsAsync();
            }

            if (result.Predictions != null)
            {
                for (int i = 0; i < result.Predictions.Count; i++)
                {
                    var pred = result.Predictions[i];

                    var prediction = new EnhancedLocationPrediction
                    {
                        Rank = i + 1,
                        Latitude = pred.Latitude,
                        Longitude = pred.Longitude,
                        Probability = pred.Probability,
                        City = pred.City ?? "",
                        State = pred.State ?? "",
                        County = pred.County ?? "",
                        Country = pred.Country ?? "",
                        LocationSummary = BuildLocationSummary(pred),
                        IsPartOfCluster = false,
                        ConfidenceLevel = ClassifyConfidence(pred.Probability)
                    };

                    Predictions.Add(prediction);

                    // Add pin to globe
                    if (_mapProvider != null && _mapProvider.IsReady)
                    {
                        await _mapProvider.AddPinAsync(
                            prediction.Latitude,
                            prediction.Longitude,
                            prediction.LocationSummary,
                            prediction.Probability,
                            prediction.Rank,
                            isExif: false
                        );
                    }
                }

                // Rotate to first prediction
                if (Predictions.Count > 0 && _mapProvider != null && _mapProvider.IsReady)
                {
                    var first = Predictions[0];
                    await _mapProvider.RotateToLocationAsync(first.Latitude, first.Longitude, 1500);
                }

                ReliabilityMessage = $"Showing {Predictions.Count} predictions";
            }
        }

        private string BuildLocationSummary(Services.DTOs.PredictionCandidate pred)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(pred.City)) parts.Add(pred.City);
            if (!string.IsNullOrEmpty(pred.State)) parts.Add(pred.State);
            if (!string.IsNullOrEmpty(pred.Country)) parts.Add(pred.Country);
            return string.Join(", ", parts);
        }

        private ConfidenceLevel ClassifyConfidence(double probability)
        {
            return probability switch
            {
                >= 0.1 => ConfidenceLevel.High,
                >= 0.05 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            };
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            ImageListView.SelectAll();
        }

        private void ExportSelection_Click(object sender, RoutedEventArgs e)
        {
            var selectedCount = ImageListView.SelectedItems.Count;
            // TODO: Implement export functionality
            System.Diagnostics.Debug.WriteLine($"Export {selectedCount} selected images");
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            ImageListView.SelectedItems.Clear();
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var toRemove = ImageListView.SelectedItems.Cast<ImageQueueItem>().ToList();
            foreach (var item in toRemove)
            {
                ImageQueue.Remove(item);
            }
            OnPropertyChanged(nameof(QueueStatusMessage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();
                switch (tag)
                {
                    case "main":
                        // Show main geolocation content
                        MainContentGrid.Visibility = Visibility.Visible;
                        SettingsFrame.Visibility = Visibility.Collapsed;
                        break;

                    case "settings":
                        // Navigate to settings within the same window
                        MainContentGrid.Visibility = Visibility.Collapsed;
                        SettingsFrame.Visibility = Visibility.Visible;
                        SettingsFrame.Navigate(typeof(SettingsPage));
                        break;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
