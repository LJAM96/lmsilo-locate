using GeoLens.Models;
using GeoLens.Services;
using GeoLens.Services.MapProviders;
using GeoLens.Commands;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Windows.UI;
using SixLabors.ImageSharp.Processing;
using Windows.Graphics.Imaging;
using Microsoft.Extensions.DependencyInjection;
using MUXC = Microsoft.UI.Xaml.Controls;

namespace GeoLens.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        // Injected services
        private readonly PredictionCacheService _cacheService;
        private readonly RecentFilesService _recentFilesService;
        private readonly GeoCLIPApiClient _apiClient;
        private readonly ExportService _exportService;

        private bool _hasExifGps;
        private string _reliabilityMessage = "No image selected";
        private IMapProvider? _mapProvider;
        private bool _isLoadingPredictions = false;
        private CommandManager? _commandManager;

        // Current image data
        private string _currentImagePath = "";
        private ExifGpsData? _currentExifData;

        // Drag-drop state
        private int _dragStartIndex = -1;

        // Observable Collections
        public ObservableCollection<ImageQueueItem> ImageQueue { get; } = new();
        public ObservableCollection<EnhancedLocationPrediction> Predictions { get; } = new();

        // Properties for UI bindings
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

            // Get services from DI container
            _cacheService = App.Services.GetRequiredService<PredictionCacheService>();
            _recentFilesService = App.Services.GetRequiredService<RecentFilesService>();
            _apiClient = App.Services.GetRequiredService<GeoCLIPApiClient>();
            _exportService = App.Services.GetRequiredService<ExportService>();

            // Initialize map when page loads
            this.Loaded += MainPage_Loaded;
            this.Unloaded += MainPage_Unloaded;

            // Subscribe to flyout opening to update recent files
            AddImagesFlyout.Opening += AddImagesFlyout_Opening;

            // Get CommandManager from DI container
            try
            {
                _commandManager = App.Services.GetService<CommandManager>();
                if (_commandManager != null)
                {
                    _commandManager.StateChanged += CommandManager_StateChanged;
                    Log.Information("CommandManager initialized");
                }
                else
                {
                    Log.Warning("CommandManager not available in DI container");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting CommandManager");
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Cache service already initialized in App.xaml.cs
            await InitializeMapAsync();

            // Initialize network monitoring
            await CheckNetworkStatusAsync();

            // Subscribe to network status changes
            Windows.Networking.Connectivity.NetworkInformation.NetworkStatusChanged += async (sender) =>
            {
                await DispatcherQueue.TryEnqueueAsync(async () => await CheckNetworkStatusAsync());
            };

            Log.Information("Network monitoring initialized");
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // Clean up map provider
            if (_mapProvider != null)
            {
                try
                {
                    _mapProvider.Dispose();
                    Log.Information("Map provider disposed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disposing map provider");
                }
            }

            // Clear collections to allow GC
            ImageQueue.Clear();
            Predictions.Clear();

            // Unsubscribe events
            ImageListView.SelectionChanged -= ImageListView_SelectionChanged;
            this.Loaded -= MainPage_Loaded;
            this.Unloaded -= MainPage_Unloaded;

            Log.Information("Page cleanup complete");
        }

        private async Task InitializeMapAsync()
        {
            try
            {
                Log.Information("Initializing map...");

                // Create Leaflet map provider (hybrid mode: offline fallback with online tiles)
                _mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: false);

                // Initialize
                await _mapProvider.InitializeAsync();

                // Hide loading overlay
                GlobeLoadingOverlay.Visibility = Visibility.Collapsed;

                Log.Information("Map ready");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Map initialization failed");

                // Update loading overlay to show error
                GlobeLoadingOverlay.Visibility = Visibility.Visible;
                // TODO: Update overlay UI to show error message instead of loading
            }
        }

        /// <summary>
        /// Check network status and update the status bar indicator
        /// </summary>
        private async Task CheckNetworkStatusAsync()
        {
            try
            {
                var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
                bool isConnected = profile?.GetNetworkConnectivityLevel() ==
                    Windows.Networking.Connectivity.NetworkConnectivityLevel.InternetAccess;

                if (isConnected)
                {
                    NetworkStatusIcon.Glyph = "\uE774"; // Globe icon
                    NetworkStatusIcon.Foreground = new SolidColorBrush(Colors.LimeGreen);
                    NetworkStatusText.Text = "Online";
                    ProcessingStatusText.Text = "";
                    Log.Information("Network status: Online");
                }
                else
                {
                    NetworkStatusIcon.Glyph = "\uF384"; // Offline icon
                    NetworkStatusIcon.Foreground = new SolidColorBrush(Colors.Orange);
                    NetworkStatusText.Text = "Offline";
                    ProcessingStatusText.Text = "Reverse geocoding unavailable";
                    Log.Information("Network status: Offline");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking network status");
                NetworkStatusIcon.Glyph = "\uE783"; // Warning icon
                NetworkStatusIcon.Foreground = new SolidColorBrush(Colors.Red);
                NetworkStatusText.Text = "Unknown";
                ProcessingStatusText.Text = "Network status unknown";
            }
        }

        private async void ImageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Prevent concurrent execution to avoid duplicate predictions
            if (_isLoadingPredictions)
            {
                Log.Debug("Already loading predictions, skipping...");
                return;
            }

            // Single selection mode - load predictions for selected image
            var selectedItem = ImageListView.SelectedItem as ImageQueueItem;
            if (selectedItem != null &&
                (selectedItem.Status == QueueStatus.Done || selectedItem.Status == QueueStatus.Cached))
            {
                try
                {
                    _isLoadingPredictions = true;

                    // Load cached predictions for this image
                    var cached = await _cacheService.GetCachedPredictionAsync(selectedItem.FilePath);
                    if (cached != null)
                    {
                        await DisplayCachedPredictionsAsync(cached);
                        Log.Information("Loaded predictions for {FileName}", selectedItem.FileName);
                    }
                }
                finally
                {
                    _isLoadingPredictions = false;
                }
            }
        }

        // Event Handlers

        // Recent Files Menu
        private void AddImagesFlyout_Opening(object sender, object e)
        {
            UpdateRecentFilesMenu();
        }

        private void UpdateRecentFilesMenu()
        {
            RecentFilesMenu.Items.Clear();

            var recentFiles = _recentFilesService.GetRecentFiles();
            if (!recentFiles.Any())
            {
                var noItems = new MenuFlyoutItem { Text = "No recent files", IsEnabled = false };
                RecentFilesMenu.Items.Add(noItems);
                return;
            }

            foreach (var file in recentFiles)
            {
                var item = new MenuFlyoutItem
                {
                    Text = $"{file.FileName} ({file.LastAccessed:g})",
                    Tag = file.FilePath
                };
                item.Click += RecentFile_Click;
                RecentFilesMenu.Items.Add(item);
            }
        }

        private async void RecentFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string filePath)
            {
                if (File.Exists(filePath))
                {
                    await AddImageToQueueAsync(filePath);
                }
                else
                {
                    await ShowErrorDialog("File Not Found", $"The file no longer exists:\n{filePath}");
                }
            }
        }

        private async Task AddImageToQueueAsync(string filePath)
        {
            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
                var props = await file.GetBasicPropertiesAsync();

                Microsoft.UI.Xaml.Media.ImageSource? thumbnailImage = null;

                try
                {
                    var thumbnail = await file.GetThumbnailAsync(
                        Windows.Storage.FileProperties.ThumbnailMode.PicturesView,
                        140,
                        Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);

                    if (thumbnail != null && thumbnail.Size > 0)
                    {
                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(thumbnail);
                        thumbnailImage = bitmapImage;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Thumbnail failed for {FilePath}", filePath);
                }

                var newImage = new ImageQueueItem
                {
                    FilePath = filePath,
                    FileSizeBytes = (long)props.Size,
                    Status = QueueStatus.Queued,
                    IsCached = false,
                    ThumbnailSource = thumbnailImage
                };

                ImageQueue.Add(newImage);

                // Track in recent files
                _recentFilesService.AddRecentFile(filePath);

                OnPropertyChanged(nameof(QueueStatusMessage));
                Log.Information("Added {FileName} to queue", Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding image to queue: {FilePath}", filePath);
                await ShowErrorDialog("Error Adding Image", $"Failed to add image:\n{ex.Message}");
            }
        }

        private async Task ShowErrorDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

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
            picker.FileTypeFilter.Add(".heic");
            picker.FileTypeFilter.Add(".webp");

            // Let user select multiple files
            var files = await picker.PickMultipleFilesAsync();

            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    // Get file info
                    var props = await file.GetBasicPropertiesAsync();

                    Microsoft.UI.Xaml.Media.ImageSource? thumbnailImage = null;

                    try
                    {
                        // Try to load Windows thumbnail first (works for JPEG/PNG)
                        var thumbnail = await file.GetThumbnailAsync(
                            Windows.Storage.FileProperties.ThumbnailMode.PicturesView,
                            140,
                            Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);

                        if (thumbnail != null && thumbnail.Size > 0)
                        {
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(thumbnail);
                            thumbnailImage = bitmapImage;
                            Log.Debug("Loaded thumbnail for {FileName} via Windows thumbnail", file.Name);
                        }
                        else
                        {
                            throw new Exception("Thumbnail generation returned empty stream");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fallback: Use ImageSharp for WebP/HEIC support (no codec required)
                        Log.Debug(ex, "Windows thumbnail failed for {FileName}, trying ImageSharp", file.Name);

                        try
                        {
                            // Load image using ImageSharp with explicit pixel format
                            using var stream = await file.OpenReadAsync();
                            using var memStream = new MemoryStream();
                            await stream.AsStreamForRead().CopyToAsync(memStream);
                            memStream.Position = 0;

                            using var image = await SixLabors.ImageSharp.Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgba32>(memStream);

                            // Calculate thumbnail size maintaining aspect ratio
                            int targetSize = 140;
                            double scale = Math.Min(
                                (double)targetSize / image.Width,
                                (double)targetSize / image.Height
                            );
                            int scaledWidth = (int)(image.Width * scale);
                            int scaledHeight = (int)(image.Height * scale);

                            // Resize image
                            image.Mutate(x => x.Resize(scaledWidth, scaledHeight));

                            // Convert to BGRA8 byte array for WriteableBitmap
                            var pixelData = new byte[scaledWidth * scaledHeight * 4];
                            image.ProcessPixelRows(accessor =>
                            {
                                int offset = 0;
                                for (int y = 0; y < accessor.Height; y++)
                                {
                                    var row = accessor.GetRowSpan(y);
                                    for (int x = 0; x < row.Length; x++)
                                    {
                                        var pixel = row[x];
                                        // BGRA format
                                        pixelData[offset++] = pixel.B;
                                        pixelData[offset++] = pixel.G;
                                        pixelData[offset++] = pixel.R;
                                        pixelData[offset++] = pixel.A;
                                    }
                                }
                            });

                            // Create WriteableBitmap and copy pixel data
                            var writeableBitmap = new Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap(
                                scaledWidth,
                                scaledHeight
                            );

                            using var pixelStream = writeableBitmap.PixelBuffer.AsStream();
                            pixelStream.Write(pixelData, 0, pixelData.Length);

                            thumbnailImage = writeableBitmap;
                            Log.Debug("Successfully loaded thumbnail for {FileName} via ImageSharp ({Width}x{Height})", file.Name, scaledWidth, scaledHeight);
                        }
                        catch (Exception innerEx)
                        {
                            Log.Warning(innerEx, "Failed to load image {FileName} with ImageSharp", file.Name);
                            // Use null thumbnail - will show placeholder in UI
                            thumbnailImage = null;
                        }
                    }

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

                    // Track in recent files
                    App.RecentFilesService.AddRecentFile(file.Path);
                }

                OnPropertyChanged(nameof(QueueStatusMessage));

                Log.Information("Added {ImageCount} image(s) to queue", files.Count);
            }
        }

        private async void ProcessImages_Click(object sender, RoutedEventArgs e)
        {
            // Get queued images
            var queuedImages = ImageQueue.Where(i => i.Status == QueueStatus.Queued).ToList();

            if (queuedImages.Count == 0)
            {
                Log.Warning("No images in queue to process");
                return;
            }

            // Check if API client is available
            if (_apiClient == null)
            {
                ReliabilityMessage = "AI service not available";
                return;
            }

            try
            {
                // Show batch progress UI
                BatchProgressPanel.Visibility = Visibility.Visible;
                BatchProgressBar.Maximum = queuedImages.Count;
                BatchProgressBar.Value = 0;
                BatchProgressText.Text = $"Processing 0 of {queuedImages.Count} images...";

                ReliabilityMessage = "Checking cache and processing images...";

                var uncachedImages = new List<ImageQueueItem>();
                int cachedCount = 0;
                int processedCount = 0;
                bool displayedFirst = false;

                // Step 1: Check cache for all images in parallel
                var cacheCheckTasks = queuedImages.Select(async item =>
                {
                    item.Status = QueueStatus.Processing;
                    var cached = await App.CacheService.GetCachedPredictionAsync(item.FilePath);
                    return (item, cached);
                }).ToList();

                var cacheResults = await Task.WhenAll(cacheCheckTasks);

                foreach (var (item, cached) in cacheResults)
                {
                    if (cached != null)
                    {
                        // Cache hit - instant result!
                        Log.Information("Cache HIT for: {FileName}", item.FileName);

                        item.Status = QueueStatus.Cached;
                        item.IsCached = true;
                        cachedCount++;

                        // Display predictions for first cached image
                        if (!displayedFirst)
                        {
                            await DisplayCachedPredictionsAsync(cached);
                            displayedFirst = true;
                        }

                        processedCount++;

                        // Update progress
                        BatchProgressBar.Value = processedCount;
                        BatchProgressText.Text = $"Processing {processedCount} of {queuedImages.Count} images... ({item.FileName} - cached)";
                    }
                    else
                    {
                        // Cache miss - need to call API
                        Log.Information("Cache MISS for: {FileName}", item.FileName);
                        uncachedImages.Add(item);
                    }
                }

                // Step 2: Process uncached images via API
                if (uncachedImages.Count > 0)
                {
                    ReliabilityMessage = $"Processing {uncachedImages.Count} image(s) via AI...";

                    var imagePaths = uncachedImages.Select(i => i.FilePath).ToList();

                    // Process API and EXIF extraction in parallel
                    var apiTask = _apiClient.InferBatchAsync(imagePaths, topK: 5);
                    var exifExtractor = new Services.ExifMetadataExtractor();
                    var exifTasks = imagePaths.Select(path =>
                        Task.Run(async () => (path, await exifExtractor.ExtractGpsDataAsync(path)))
                    ).ToList();

                    // Wait for both API and EXIF extraction to complete
                    await Task.WhenAll(apiTask, Task.WhenAll(exifTasks));

                    var response = apiTask.Result; // Already awaited, safe to use .Result
                    var exifResults = Task.WhenAll(exifTasks).Result; // Already awaited, safe to use .Result
                    var exifData = exifResults.ToDictionary(r => r.path, r => r.Item2);

                    // Process API results
                    if (response != null)
                    {
                        foreach (var result in response)
                        {
                            var imageItem = ImageQueue.FirstOrDefault(i => i.FilePath == result.Path);
                            if (imageItem != null)
                            {
                                if (string.IsNullOrEmpty(result.Error))
                                {
                                    imageItem.Status = QueueStatus.Done;
                                    imageItem.IsCached = false;

                                    // Get EXIF data
                                    var gps = exifData.ContainsKey(result.Path) ? exifData[result.Path] : null;

                                    if (gps?.HasGps == true)
                                    {
                                        Log.Information("Found GPS in {FileName}: {Latitude:F6}, {Longitude:F6}", imageItem.FileName, gps.Latitude, gps.Longitude);
                                    }

                                    // Store in cache for future lookups
                                    await _cacheService.StorePredictionAsync(
                                        result.Path,
                                        result.Predictions ?? new List<Services.DTOs.PredictionCandidate>(),
                                        gps
                                    );

                                    Log.Debug("Stored in cache: {FileName}", System.IO.Path.GetFileName(result.Path));

                                    // Display predictions for first image if no cached images were shown
                                    if (!displayedFirst)
                                    {
                                        await DisplayPredictionsAsync(result, gps);
                                        displayedFirst = true;
                                    }
                                }
                                else
                                {
                                    imageItem.Status = QueueStatus.Error;
                                    imageItem.ErrorMessage = result.Error ?? "Unknown error occurred";
                                }

                                processedCount++;

                                // Update progress
                                BatchProgressBar.Value = processedCount;
                                BatchProgressText.Text = $"Processing {processedCount} of {queuedImages.Count} images... ({imageItem.FileName})";
                            }
                        }
                    }
                }

                // Update status message
                if (cachedCount > 0 && uncachedImages.Count > 0)
                {
                    ReliabilityMessage = $"Processed {processedCount} image(s) ({cachedCount} from cache, {uncachedImages.Count} via AI)";
                }
                else if (cachedCount > 0)
                {
                    ReliabilityMessage = $"Retrieved {cachedCount} image(s) from cache (instant results!)";
                }
                else
                {
                    ReliabilityMessage = $"Processed {processedCount} image(s) via AI";
                }

                Log.Information("Batch summary: {CachedCount} cached, {ProcessedViaApi} processed via API, {TotalCount} total", cachedCount, uncachedImages.Count, processedCount);

                // Hide batch progress UI
                BatchProgressPanel.Visibility = Visibility.Collapsed;
                BatchProgressText.Text = $"Completed: {processedCount} images processed";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing images");
                ReliabilityMessage = $"Error: {ex.Message}";

                // Mark as error
                foreach (var item in queuedImages)
                {
                    if (item.Status == QueueStatus.Processing)
                    {
                        item.Status = QueueStatus.Error;
                        item.ErrorMessage = ex.Message;
                    }
                }

                // Hide batch progress UI
                BatchProgressPanel.Visibility = Visibility.Collapsed;
                BatchProgressText.Text = "Error occurred during processing";
            }
        }

        private async Task DisplayPredictionsAsync(Services.DTOs.PredictionResult result, ExifGpsData? exifGps)
        {
            // Store current image data for export
            _currentImagePath = result.Path;
            _currentExifData = exifGps;

            // Load extended EXIF metadata for the panel
            var extractor = new ExifMetadataExtractor();
            var extendedMetadata = await extractor.ExtractExtendedMetadataAsync(result.Path);
            if (extendedMetadata != null)
            {
                ExifPanel.LoadMetadata(extendedMetadata);
                ExifPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ExifPanel.Visibility = Visibility.Collapsed;
            }

            // Clear existing predictions
            Predictions.Clear();

            // Clear map markers
            if (_mapProvider != null && _mapProvider.IsReady)
            {
                await _mapProvider.ClearPinsAsync();
            }

            // Show EXIF GPS if available
            if (exifGps?.HasGps == true)
            {
                HasExifGps = true;
                ExifLocationName = exifGps.LocationName ?? "GPS Location from Image";
                ExifLat = exifGps.LatitudeFormatted;
                ExifLon = exifGps.LongitudeFormatted;

                // Add EXIF GPS as a prediction (with VeryHigh confidence)
                // Note: 90% confidence because EXIF data can be manually edited
                var exifPrediction = new EnhancedLocationPrediction
                {
                    Rank = 0, // Special rank for EXIF
                    Latitude = exifGps.Latitude,
                    Longitude = exifGps.Longitude,
                    Probability = 0.9,
                    AdjustedProbability = 0.9,
                    City = "",
                    State = "",
                    Country = "",
                    LocationSummary = "EXIF GPS Data",
                    IsPartOfCluster = false,
                    ConfidenceLevel = ConfidenceLevel.VeryHigh
                };

                Predictions.Add(exifPrediction);

                // Add EXIF marker to map (cyan, special styling)
                if (_mapProvider != null && _mapProvider.IsReady)
                {
                    await _mapProvider.AddPinAsync(
                        exifGps.Latitude,
                        exifGps.Longitude,
                        "EXIF GPS Location",
                        0.9,
                        0,
                        isExif: true
                    );
                }

                ReliabilityMessage = "GPS coordinates found in image metadata - highest reliability";
            }
            else
            {
                HasExifGps = false;
            }

            // Add AI predictions
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
                        AdjustedProbability = pred.Probability,
                        City = pred.City ?? "",
                        State = pred.State ?? "",
                        County = pred.County ?? "",
                        Country = pred.Country ?? "",
                        LocationSummary = BuildLocationSummary(pred),
                        IsPartOfCluster = false,
                        ConfidenceLevel = ClassifyConfidence(pred.Probability)
                    };

                    Predictions.Add(prediction);

                    // Add marker to map
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

                // Fit map to show all predictions
                if (Predictions.Count > 0 && _mapProvider != null && _mapProvider.IsReady)
                {
                    await _mapProvider.FitToMarkersAsync();
                }

                if (!HasExifGps)
                {
                    ReliabilityMessage = $"Showing {result.Predictions.Count} AI predictions";
                }
            }
        }

        private async Task DisplayCachedPredictionsAsync(CachedPredictionEntry cached)
        {
            // Store current image data for export
            _currentImagePath = cached.FilePath;
            _currentExifData = cached.ExifGps;

            // Load extended EXIF metadata for the panel
            var extractor = new ExifMetadataExtractor();
            var extendedMetadata = await extractor.ExtractExtendedMetadataAsync(cached.FilePath);
            if (extendedMetadata != null)
            {
                ExifPanel.LoadMetadata(extendedMetadata);
                ExifPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ExifPanel.Visibility = Visibility.Collapsed;
            }

            // Clear existing predictions
            Predictions.Clear();

            // Clear map markers
            if (_mapProvider != null && _mapProvider.IsReady)
            {
                await _mapProvider.ClearPinsAsync();
            }

            // Show EXIF GPS if available in cached entry
            if (cached.ExifGps?.HasGps == true)
            {
                HasExifGps = true;
                ExifLocationName = cached.ExifGps.LocationName ?? "GPS Location from Image";
                ExifLat = cached.ExifGps.LatitudeFormatted;
                ExifLon = cached.ExifGps.LongitudeFormatted;

                // Add EXIF GPS as a prediction (with VeryHigh confidence)
                // Note: 90% confidence because EXIF data can be manually edited
                var exifPrediction = new EnhancedLocationPrediction
                {
                    Rank = 0, // Special rank for EXIF
                    Latitude = cached.ExifGps.Latitude,
                    Longitude = cached.ExifGps.Longitude,
                    Probability = 0.9,
                    AdjustedProbability = 0.9,
                    City = "",
                    State = "",
                    Country = "",
                    LocationSummary = "EXIF GPS Data",
                    IsPartOfCluster = false,
                    ConfidenceLevel = ConfidenceLevel.VeryHigh
                };

                Predictions.Add(exifPrediction);

                // Add EXIF marker to map
                if (_mapProvider != null && _mapProvider.IsReady)
                {
                    await _mapProvider.AddPinAsync(
                        cached.ExifGps.Latitude,
                        cached.ExifGps.Longitude,
                        "EXIF GPS Location",
                        0.9,
                        0,
                        isExif: true
                    );
                }

                ReliabilityMessage = "GPS coordinates found in image metadata - highest reliability (cached)";
            }
            else
            {
                HasExifGps = false;
            }

            // Add AI predictions from cache
            if (cached.Predictions != null && cached.Predictions.Count > 0)
            {
                for (int i = 0; i < cached.Predictions.Count; i++)
                {
                    var pred = cached.Predictions[i];

                    var prediction = new EnhancedLocationPrediction
                    {
                        Rank = i + 1,
                        Latitude = pred.Latitude,
                        Longitude = pred.Longitude,
                        Probability = pred.Probability,
                        AdjustedProbability = pred.Probability,
                        City = pred.City ?? "",
                        State = pred.State ?? "",
                        County = pred.County ?? "",
                        Country = pred.Country ?? "",
                        LocationSummary = BuildLocationSummary(pred),
                        IsPartOfCluster = false,
                        ConfidenceLevel = ClassifyConfidence(pred.Probability)
                    };

                    Predictions.Add(prediction);

                    // Add marker to map
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

                // Fit map to show all predictions
                if (Predictions.Count > 0 && _mapProvider != null && _mapProvider.IsReady)
                {
                    await _mapProvider.FitToMarkersAsync();
                }

                if (!HasExifGps)
                {
                    ReliabilityMessage = $"Showing {cached.Predictions.Count} AI predictions (cached)";
                }
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
                >= 0.60 => ConfidenceLevel.High,
                >= 0.30 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            };
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            // Remove currently selected image using command pattern
            var selectedItem = ImageListView.SelectedItem as ImageQueueItem;
            if (selectedItem != null && _commandManager != null)
            {
                var command = new RemoveImageCommand(ImageQueue, selectedItem);
                _commandManager.ExecuteCommand(command);
                ShowUndoToast($"Removed {selectedItem.FileName} (Ctrl+Z to undo)");
                OnPropertyChanged(nameof(QueueStatusMessage));
            }
            else if (selectedItem == null)
            {
                Log.Information("No item selected");
            }
        }

        private async void RetryImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageQueueItem item)
            {
                Log.Information("Retrying {FileName}", item.FileName);

                // Reset status and clear error
                item.Status = QueueStatus.Queued;
                item.ErrorMessage = null;

                // Process the image again (force refresh to bypass cache)
                try
                {
                    item.Status = QueueStatus.Processing;

                    if (_apiClient == null)
                    {
                        item.Status = QueueStatus.Error;
                        item.ErrorMessage = "AI service not available";
                        return;
                    }

                    // Process API and EXIF extraction
                    var apiTask = _apiClient.InferBatchAsync(new List<string> { item.FilePath }, topK: 5);
                    var exifExtractor = new Services.ExifMetadataExtractor();
                    var exifTask = exifExtractor.ExtractGpsDataAsync(item.FilePath);

                    await Task.WhenAll(apiTask, exifTask);

                    var response = apiTask.Result;
                    var exifData = exifTask.Result;

                    if (response != null && response.Count > 0)
                    {
                        var result = response[0];

                        if (string.IsNullOrEmpty(result.Error))
                        {
                            item.Status = QueueStatus.Done;
                            item.IsCached = false;

                            // Store in cache for future lookups
                            await App.CacheService.StorePredictionAsync(
                                result.Path,
                                result.Predictions ?? new List<Services.DTOs.PredictionCandidate>(),
                                exifData
                            );

                            Log.Information("Successfully processed {FileName}", item.FileName);

                            // Display predictions for this image
                            await DisplayPredictionsAsync(result, exifData);
                        }
                        else
                        {
                            item.Status = QueueStatus.Error;
                            item.ErrorMessage = result.Error ?? "Unknown error occurred";
                        }
                    }
                    else
                    {
                        item.Status = QueueStatus.Error;
                        item.ErrorMessage = "No response from API";
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error retrying {FileName}", item.FileName);
                    item.Status = QueueStatus.Error;
                    item.ErrorMessage = ex.Message;
                }
            }
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ImageQueueItem item)
            {
                Log.Information("Removing {FileName}", item.FileName);
                ImageQueue.Remove(item);
                OnPropertyChanged(nameof(QueueStatusMessage));
            }
        }

        private async void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            var dialog = new ContentDialog
            {
                Title = "Clear Queue",
                Content = $"Are you sure you want to remove all {ImageQueue.Count} images from the queue?",
                PrimaryButtonText = "Clear All",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && _commandManager != null)
            {
                // Use command pattern for undo support
                var command = new ClearAllCommand(ImageQueue, Predictions);
                _commandManager.ExecuteCommand(command);

                _currentImagePath = "";
                _currentExifData = null;

                // Clear map
                if (_mapProvider != null && _mapProvider.IsReady)
                {
                    await _mapProvider.ClearPinsAsync();
                }

                ShowUndoToast($"Cleared {command.Description} (Ctrl+Z to undo)");
                OnPropertyChanged(nameof(QueueStatusMessage));
                Log.Information("Queue cleared");
            }
        }

        private async void ExportResult_Click(object sender, RoutedEventArgs e)
        {
            // Check if there are predictions to export
            if (Predictions.Count == 0 || string.IsNullOrEmpty(_currentImagePath))
            {
                // Show info bar
                var dialog = new ContentDialog
                {
                    Title = "No Results to Export",
                    Content = "Please select and process an image first to see predictions that can be exported.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            // Show a professional menu flyout with export format options
            var flyout = new MenuFlyout();

            var pdfItem = new MenuFlyoutItem
            {
                Text = "Export as PDF (Professional Report)",
                Icon = new SymbolIcon(Symbol.Document)
            };
            pdfItem.Click += async (s, args) => await ExportWithMapAsync("pdf", "PDF Document", ".pdf");
            flyout.Items.Add(pdfItem);

            var jsonItem = new MenuFlyoutItem
            {
                Text = "Export as JSON (Data Export)",
                Icon = new SymbolIcon(Symbol.Document)
            };
            jsonItem.Click += async (s, args) => await ExportWithMapAsync("json", "JSON File", ".json");
            flyout.Items.Add(jsonItem);

            var csvItem = new MenuFlyoutItem
            {
                Text = "Export as CSV (Spreadsheet)",
                Icon = new SymbolIcon(Symbol.Document)
            };
            csvItem.Click += async (s, args) => await ExportWithMapAsync("csv", "CSV File", ".csv");
            flyout.Items.Add(csvItem);

            var kmlItem = new MenuFlyoutItem
            {
                Text = "Export as KML (Google Earth)",
                Icon = new SymbolIcon(Symbol.Map)
            };
            kmlItem.Click += async (s, args) => await ExportWithMapAsync("kml", "KML File", ".kml");
            flyout.Items.Add(kmlItem);

            // Show the flyout at the button location
            if (sender is FrameworkElement element)
            {
                flyout.ShowAt(element);
            }
        }

        private async Task ExportWithMapAsync(string format, string fileTypeDescription, string fileExtension)
        {
            // Capture map screenshot if available
            string? mapImagePath = null;
            if (_mapProvider != null && _mapProvider.IsReady)
            {
                try
                {
                    // Capture map screenshot
                    mapImagePath = await CaptureMapScreenshotAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to capture map screenshot");
                }
            }

            try
            {
                // Call the existing export method (which will now include the map if available)
                await ExportCurrentResultAsync(format, fileTypeDescription, fileExtension, mapImagePath);
            }
            finally
            {
                // Clean up temporary screenshot file with retry logic
                if (!string.IsNullOrEmpty(mapImagePath))
                {
                    await CleanupScreenshotAsync(mapImagePath);
                }
            }
        }

        private async Task<string?> CaptureMapScreenshotAsync()
        {
            try
            {
                // Use the map provider's screenshot capability
                if (_mapProvider is LeafletMapProvider leafletProvider)
                {
                    var screenshotPath = await leafletProvider.CaptureScreenshotAsync();

                    if (!string.IsNullOrEmpty(screenshotPath) && File.Exists(screenshotPath))
                    {
                        Log.Debug("Map screenshot captured: {ScreenshotPath}", screenshotPath);
                        return screenshotPath;
                    }
                    else
                    {
                        Log.Information("Map screenshot capture returned null or file not found");
                        return null;
                    }
                }

                Log.Information("Map provider is not LeafletMapProvider, cannot capture screenshot");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error capturing map screenshot");
                return null;
            }
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

        // Export Helper Methods
        private async Task ExportCurrentResultAsync(string format, string fileTypeDescription, string fileExtension, string? mapImagePath = null)
        {
            try
            {
                // Validate that we have predictions to export
                if (Predictions.Count == 0)
                {
                    ShowExportFeedback("No predictions available to export", InfoBarSeverity.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(_currentImagePath))
                {
                    ShowExportFeedback("No image selected for export", InfoBarSeverity.Warning);
                    return;
                }

                // Get window handle for file picker
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);

                // Show file save picker
                var suggestedFileName = $"{Path.GetFileNameWithoutExtension(_currentImagePath)}_report{fileExtension}";
                var outputPath = await _exportService.ShowSaveFilePickerAsync(
                    hwnd,
                    suggestedFileName,
                    fileTypeDescription,
                    fileExtension
                );

                if (string.IsNullOrEmpty(outputPath))
                {
                    // User cancelled
                    return;
                }

                // Build the enhanced prediction result
                var result = BuildEnhancedPredictionResult();

                // Export based on format
                string exportedPath;
                switch (format.ToLower())
                {
                    case "csv":
                        exportedPath = await _exportService.ExportToCsvAsync(result, outputPath);
                        break;

                    case "json":
                        exportedPath = await _exportService.ExportToJsonAsync(result, outputPath);
                        break;

                    case "pdf":
                        // Load thumbnail for PDF
                        byte[]? thumbnailBytes = null;
                        try
                        {
                            thumbnailBytes = await LoadThumbnailBytesAsync(_currentImagePath);
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, "Failed to load thumbnail for export");
                        }

                        // Load map image for PDF
                        byte[]? mapBytes = null;
                        if (!string.IsNullOrEmpty(mapImagePath) && File.Exists(mapImagePath))
                        {
                            try
                            {
                                mapBytes = await File.ReadAllBytesAsync(mapImagePath);
                                Log.Debug("Loaded map image: {MapImagePath} ({Bytes} bytes)", mapImagePath, mapBytes.Length);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Failed to load map image");
                            }
                        }

                        exportedPath = await _exportService.ExportToPdfAsync(result, outputPath, thumbnailBytes, mapBytes);
                        break;

                    case "kml":
                        exportedPath = await _exportService.ExportToKmlAsync(result, outputPath);
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown export format: {format}");
                }

                ShowExportFeedback($"Successfully exported to {Path.GetFileName(exportedPath)}", InfoBarSeverity.Success);
                Log.Information("Exported to: {ExportedPath}", exportedPath);
            }
            catch (Exception ex)
            {
                ShowExportFeedback($"Export failed: {ex.Message}", InfoBarSeverity.Error);
                Log.Error(ex, "Export error");
            }
        }

        private EnhancedPredictionResult BuildEnhancedPredictionResult()
        {
            // Get AI predictions (exclude EXIF which has Rank = 0)
            var aiPredictions = Predictions.Where(p => p.Rank > 0).ToList();

            // Remove any duplicates based on coordinates (defensive check)
            aiPredictions = aiPredictions
                .GroupBy(p => new { p.Latitude, p.Longitude, p.Rank })
                .Select(g => g.First())
                .OrderBy(p => p.Rank)
                .ToList();

            Log.Debug("Returning {PredictionCount} unique predictions", aiPredictions.Count);
            foreach (var pred in aiPredictions)
            {
                Log.Debug("  - Rank {Rank}: {LocationSummary} ({Probability})", pred.Rank, pred.LocationSummary, pred.ProbabilityFormatted);
            }

            return new EnhancedPredictionResult
            {
                ImagePath = _currentImagePath,
                AiPredictions = aiPredictions,
                ExifGps = _currentExifData,
                ClusterInfo = null
            };
        }

        private async Task<byte[]?> LoadThumbnailBytesAsync(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return null;

                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(imagePath);

                // Get thumbnail
                var thumbnail = await file.GetThumbnailAsync(
                    Windows.Storage.FileProperties.ThumbnailMode.PicturesView,
                    300,
                    Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale
                );

                if (thumbnail == null)
                    return null;

                // Convert to byte array
                using var stream = thumbnail.AsStream();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error loading thumbnail");
                return null;
            }
        }

        private void ShowExportFeedback(string message, InfoBarSeverity severity)
        {
            ExportFeedbackBar.Message = message;
            ExportFeedbackBar.Severity = severity;
            ExportFeedbackBar.IsOpen = true;
        }

        /// <summary>
        /// Efficiently load thumbnail for an image using WinRT BitmapDecoder
        /// This method scales the image during decoding to minimize memory usage
        /// </summary>
        private async Task<BitmapImage> LoadThumbnailAsync(string imagePath, int maxSize = 200)
        {
            await using var fileStream = File.OpenRead(imagePath);
            using var memStream = new MemoryStream();

            var decoder = await BitmapDecoder.CreateAsync(fileStream.AsRandomAccessStream());

            // Calculate thumbnail size
            double scale = Math.Min(maxSize / (double)decoder.PixelWidth, maxSize / (double)decoder.PixelHeight);
            uint thumbnailWidth = (uint)(decoder.PixelWidth * scale);
            uint thumbnailHeight = (uint)(decoder.PixelHeight * scale);

            // Create scaled version
            var transform = new BitmapTransform
            {
                ScaledWidth = thumbnailWidth,
                ScaledHeight = thumbnailHeight,
                InterpolationMode = BitmapInterpolationMode.Fant
            };

            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb
            );

            // Encode to stream
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memStream.AsRandomAccessStream());
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, thumbnailWidth, thumbnailHeight, 96, 96, pixelData.DetachPixelData());
            await encoder.FlushAsync();

            // Load into BitmapImage
            memStream.Position = 0;
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(memStream.AsRandomAccessStream());
            return bitmap;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handle prediction item tapped - focus map on that location
        /// </summary>
        private async void Prediction_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.Expander expander &&
                expander.DataContext is EnhancedLocationPrediction prediction)
            {
                // Focus map on this prediction
                if (_mapProvider != null && _mapProvider.IsReady)
                {
                    await _mapProvider.RotateToLocationAsync(
                        prediction.Latitude,
                        prediction.Longitude,
                        1000); // 1 second animation

                    Log.Debug("Focused map on {LocationSummary} ({Latitude:F6}, {Longitude:F6})", prediction.LocationSummary, prediction.Latitude, prediction.Longitude);
                }
            }
        }

        /// <summary>
        /// Clean up temporary screenshot file with retry logic for file locking issues
        /// </summary>
        private async Task CleanupScreenshotAsync(string screenshotPath)
        {
            const int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (File.Exists(screenshotPath))
                    {
                        File.Delete(screenshotPath);
                        Log.Debug("Deleted temporary screenshot: {ScreenshotPath}", screenshotPath);
                        return;
                    }
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    // File is locked, wait and retry
                    await Task.Delay(100 * (i + 1));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to delete screenshot {ScreenshotPath}", screenshotPath);
                    return;
                }
            }
        }

        // Keyboard Accelerator Event Handlers

        /// <summary>
        /// Handle Delete key press - remove selected image from queue
        /// </summary>
        private void DeleteSelected_Invoked(KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            if (ImageQueue.Count > 0 && ImageListView.SelectedItem != null)
            {
                RemoveSelected_Click(sender, args);
                args.Handled = true;
            }
        }

        /// <summary>
        /// Handle F5 key press - refresh/retry current image predictions
        /// </summary>
        private async void Refresh_Invoked(KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            // Refresh current image predictions
            if (ImageListView.SelectedItem is ImageQueueItem selectedItem)
            {
                // Clear cache for this image
                await _cacheService.ClearCacheAsync(selectedItem.FilePath);

                // Reset status to queued
                selectedItem.Status = QueueStatus.Queued;
                selectedItem.IsCached = false;

                // Re-process the image
                await ProcessSingleImageAsync(selectedItem);

                args.Handled = true;
                Log.Information("Refreshed predictions for {FileName}", selectedItem.FileName);
            }
        }

        /// <summary>
        /// Handle Ctrl+, key press - open settings
        /// </summary>
        private void OpenSettings_Invoked(KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            // Navigate to settings using the NavigationView
            var settingsItem = NavView.MenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == "settings");

            if (settingsItem != null)
            {
                NavView.SelectedItem = settingsItem;
            }

            args.Handled = true;
        }

        /// <summary>
        /// Process a single image (helper method for refresh functionality)
        /// </summary>
        private async Task ProcessSingleImageAsync(ImageQueueItem item)
        {
            if (_apiClient == null)
            {
                ReliabilityMessage = "AI service not available";
                return;
            }

            try
            {
                item.Status = QueueStatus.Processing;
                ReliabilityMessage = "Processing image...";

                // Process API and EXIF extraction in parallel
                var apiTask = App.ApiClient.InferBatchAsync(new List<string> { item.FilePath }, topK: 5);
                var exifExtractor = new ExifMetadataExtractor();
                var exifTask = exifExtractor.ExtractGpsDataAsync(item.FilePath);

                await Task.WhenAll(apiTask, exifTask);

                var response = apiTask.Result;
                var gps = exifTask.Result;

                if (response != null && response.Count > 0)
                {
                    var result = response[0];

                    if (string.IsNullOrEmpty(result.Error))
                    {
                        item.Status = QueueStatus.Done;
                        item.IsCached = false;

                        // Store in cache
                        await App.CacheService.StorePredictionAsync(
                            result.Path,
                            result.Predictions ?? new List<Services.DTOs.PredictionCandidate>(),
                            gps
                        );

                        // Display predictions
                        await DisplayPredictionsAsync(result, gps);

                        ReliabilityMessage = "Image processed successfully";
                    }
                    else
                    {
                        item.Status = QueueStatus.Error;
                        ReliabilityMessage = $"Error: {result.Error}";
                    }
                }
            }
            catch (Exception ex)
            {
                item.Status = QueueStatus.Error;
                ReliabilityMessage = $"Error: {ex.Message}";
                Log.Error(ex, "ProcessSingleImage error");
            }
        }

        // Copy to Clipboard Event Handlers

        /// <summary>
        /// Copy coordinates in decimal format (DD.DDDDDD)
        /// </summary>
        private void CopyDecimal_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is EnhancedLocationPrediction result)
            {
                var text = $"{result.Latitude:F6}, {result.Longitude:F6}";
                CopyToClipboard(text, "Decimal coordinates copied");
            }
        }

        /// <summary>
        /// Copy coordinates in DMS format (Degrees, Minutes, Seconds)
        /// </summary>
        private void CopyDMS_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is EnhancedLocationPrediction result)
            {
                var text = ConvertToDMS(result.Latitude, result.Longitude);
                CopyToClipboard(text, "DMS coordinates copied");
            }
        }

        /// <summary>
        /// Copy Google Maps link for this location
        /// </summary>
        private void CopyGoogleMaps_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is EnhancedLocationPrediction result)
            {
                var text = $"https://www.google.com/maps?q={result.Latitude},{result.Longitude}";
                CopyToClipboard(text, "Google Maps link copied");
            }
        }

        /// <summary>
        /// Copy geo URI format (geo:lat,lon)
        /// </summary>
        private void CopyGeoUri_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is EnhancedLocationPrediction result)
            {
                var text = $"geo:{result.Latitude},{result.Longitude}";
                CopyToClipboard(text, "Geo URI copied");
            }
        }

        /// <summary>
        /// Copy text to clipboard and show feedback
        /// </summary>
        private void CopyToClipboard(string text, string notification)
        {
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                // Show success feedback
                ShowExportFeedback(notification, InfoBarSeverity.Success);
                Log.Debug("Clipboard operation: {Notification} - {Text}", notification, text);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy to clipboard");
                ShowExportFeedback("Failed to copy to clipboard", InfoBarSeverity.Error);
            }
        }

        /// <summary>
        /// Convert decimal coordinates to DMS (Degrees, Minutes, Seconds) format
        /// </summary>
        private string ConvertToDMS(double latitude, double longitude)
        {
            string latDir = latitude >= 0 ? "N" : "S";
            string lonDir = longitude >= 0 ? "E" : "W";

            latitude = Math.Abs(latitude);
            longitude = Math.Abs(longitude);

            int latDeg = (int)latitude;
            int latMin = (int)((latitude - latDeg) * 60);
            double latSec = ((latitude - latDeg) * 60 - latMin) * 60;

            int lonDeg = (int)longitude;
            int lonMin = (int)((longitude - lonDeg) * 60);
            double lonSec = ((longitude - lonDeg) * 60 - lonMin) * 60;

            return $"{latDeg}°{latMin}'{latSec:F2}\"{latDir}, {lonDeg}°{lonMin}'{lonSec:F2}\"{lonDir}";
        }

        // ============================================================================
        // UNDO/REDO COMMAND SYSTEM
        // ============================================================================

        /// <summary>
        /// Handle CommandManager state changes to update UI button states
        /// </summary>
        private void CommandManager_StateChanged(object? sender, EventArgs e)
        {
            if (_commandManager == null) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                UndoButton.IsEnabled = _commandManager.CanUndo;
                RedoButton.IsEnabled = _commandManager.CanRedo;

                // Update tooltips with command descriptions
                UndoButton.Label = _commandManager.CanUndo
                    ? $"Undo: {_commandManager.GetUndoDescription()}"
                    : "Undo";

                RedoButton.Label = _commandManager.CanRedo
                    ? $"Redo: {_commandManager.GetRedoDescription()}"
                    : "Redo";
            });
        }

        /// <summary>
        /// Undo button click handler
        /// </summary>
        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_commandManager?.CanUndo == true)
            {
                var description = _commandManager.Undo();
                if (description != null)
                {
                    ShowUndoToast($"Undone: {description}");
                    Log.Information("Undo: {Description}", description);
                    OnPropertyChanged(nameof(QueueStatusMessage));
                }
            }
        }

        /// <summary>
        /// Redo button click handler
        /// </summary>
        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_commandManager?.CanRedo == true)
            {
                var description = _commandManager.Redo();
                if (description != null)
                {
                    ShowUndoToast($"Redone: {description}");
                    Log.Information("Redo: {Description}", description);
                    OnPropertyChanged(nameof(QueueStatusMessage));
                }
            }
        }

        /// <summary>
        /// Undo keyboard accelerator handler (Ctrl+Z)
        /// </summary>
        private void Undo_Invoked(KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_commandManager?.CanUndo == true)
            {
                Undo_Click(sender, args);
                args.Handled = true;
            }
        }

        /// <summary>
        /// Redo keyboard accelerator handler (Ctrl+Y)
        /// </summary>
        private void Redo_Invoked(KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            if (_commandManager?.CanRedo == true)
            {
                Redo_Click(sender, args);
                args.Handled = true;
            }
        }

        /// <summary>
        /// Show a toast notification for undo/redo actions
        /// </summary>
        private void ShowUndoToast(string message)
        {
            ExportFeedbackBar.Message = message;
            ExportFeedbackBar.Severity = InfoBarSeverity.Informational;
            ExportFeedbackBar.IsOpen = true;

            // Auto-close after 3 seconds
            var timer = new System.Timers.Timer(3000) { AutoReset = false };
            timer.Elapsed += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    ExportFeedbackBar.IsOpen = false;
                });
                timer.Dispose();
            };
            timer.Start();
        }

        // ============================================================================
        // DRAG-AND-DROP REORDERING
        // ============================================================================

        /// <summary>
        /// Handle drag operation start
        /// </summary>
        private void ImageListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 0 && e.Items[0] is ImageQueueItem item)
            {
                _dragStartIndex = ImageQueue.IndexOf(item);
                e.Data.Properties.Add("DraggedItem", item);
                Log.Debug("Starting drag for item at index {DragStartIndex}: {FileName}", _dragStartIndex, item.FileName);
            }
        }

        /// <summary>
        /// Handle drag over event (show visual feedback)
        /// </summary>
        private void ImageListView_DragOver(object sender, DragEventArgs e)
        {
            // Allow reordering
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        }

        /// <summary>
        /// Handle drop event (execute reorder command)
        /// </summary>
        private void ImageListView_Drop(object sender, DragEventArgs e)
        {
            // Get the drop target position
            if (sender is ListView listView && e.Data.Properties.ContainsKey("DraggedItem"))
            {
                var draggedItem = e.Data.Properties["DraggedItem"] as ImageQueueItem;
                if (draggedItem == null || _dragStartIndex < 0)
                {
                    Log.Information("Invalid drag state");
                    return;
                }

                // Get drop position
                var position = e.GetPosition(listView);
                var dropTargetIndex = GetDropTargetIndex(listView, position);

                if (dropTargetIndex >= 0 && dropTargetIndex < ImageQueue.Count && dropTargetIndex != _dragStartIndex)
                {
                    // Create and execute reorder command
                    var command = new ReorderImagesCommand(
                        ImageQueue,
                        draggedItem,
                        _dragStartIndex,
                        dropTargetIndex
                    );

                    _commandManager?.ExecuteCommand(command);

                    Log.Information("Reordered item from {FromIndex} to {ToIndex}", _dragStartIndex, dropTargetIndex);
                    OnPropertyChanged(nameof(QueueStatusMessage));
                }

                _dragStartIndex = -1;
            }
        }

        /// <summary>
        /// Calculate the drop target index based on mouse position
        /// </summary>
        private int GetDropTargetIndex(ListView listView, Windows.Foundation.Point position)
        {
            // Simple approach: find the nearest item
            for (int i = 0; i < ImageQueue.Count; i++)
            {
                var container = listView.ContainerFromIndex(i) as ListViewItem;
                if (container != null)
                {
                    var transform = container.TransformToVisual(listView);
                    var itemPosition = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                    var itemHeight = container.ActualHeight;

                    if (position.Y >= itemPosition.Y && position.Y < itemPosition.Y + itemHeight)
                    {
                        // Determine if we should insert before or after this item
                        var midPoint = itemPosition.Y + (itemHeight / 2);
                        return position.Y < midPoint ? i : i + 1;
                    }
                }
            }

            // Default to end of list
            return ImageQueue.Count - 1;
        }
    }
}
