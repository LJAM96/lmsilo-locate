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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using MUXC = Microsoft.UI.Xaml.Controls;

namespace GeoLens.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private bool _hasExifGps;
        private string _reliabilityMessage = "No image selected";
        private IMapProvider? _mapProvider;
        private readonly PredictionCacheService _cacheService = new();
        private readonly GeographicClusterAnalyzer _clusterAnalyzer = new();
        private readonly ExportService _exportService = new();
        private ClusterAnalysisResult? _currentClusterInfo;

        // Current image data
        private string _currentImagePath = "";
        private ExifGpsData? _currentExifData;

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

            // Wire up selection changed event
            ImageListView.SelectionChanged += ImageListView_SelectionChanged;

            // Initialize map when page loads
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize cache service
            try
            {
                await _cacheService.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("[MainPage] Cache service initialized");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Cache initialization failed (non-fatal): {ex.Message}");
                // Cache failure should not block app startup
            }

            await InitializeMapAsync();
        }

        private async Task InitializeMapAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] Initializing map...");

                // Create Leaflet map provider (hybrid mode: offline fallback with online tiles)
                _mapProvider = new LeafletMapProvider(GlobeWebView, offlineMode: false);

                // Initialize
                await _mapProvider.InitializeAsync();

                // Hide loading overlay
                GlobeLoadingOverlay.Visibility = Visibility.Collapsed;

                System.Diagnostics.Debug.WriteLine("[MainPage] Map ready");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Map initialization failed: {ex.Message}");

                // Update loading overlay to show error
                GlobeLoadingOverlay.Visibility = Visibility.Visible;
                // TODO: Update overlay UI to show error message instead of loading
            }
        }

        private async void ImageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Single selection mode - load predictions for selected image
            var selectedItem = ImageListView.SelectedItem as ImageQueueItem;
            if (selectedItem != null &&
                (selectedItem.Status == QueueStatus.Done || selectedItem.Status == QueueStatus.Cached))
            {
                // Load cached predictions for this image
                var cached = await _cacheService.GetCachedPredictionAsync(selectedItem.FilePath);
                if (cached != null)
                {
                    await DisplayCachedPredictionsAsync(cached);
                    Debug.WriteLine($"[Selection] Loaded predictions for {selectedItem.FileName}");
                }
            }
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
                            Debug.WriteLine($"[AddImages] Loaded thumbnail for {file.Name} via Windows thumbnail");
                        }
                        else
                        {
                            throw new Exception("Thumbnail generation returned empty stream");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fallback: Use ImageSharp for WebP/HEIC support (no codec required)
                        Debug.WriteLine($"[AddImages] Windows thumbnail failed for {file.Name}, trying ImageSharp: {ex.Message}");

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
                            Debug.WriteLine($"[AddImages] Successfully loaded thumbnail for {file.Name} via ImageSharp ({scaledWidth}x{scaledHeight})");
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"[AddImages] Failed to load image {file.Name} with ImageSharp: {innerEx.Message}");
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
                ReliabilityMessage = "Checking cache and processing images...";

                var exifExtractor = new Services.ExifMetadataExtractor();
                var uncachedImages = new List<ImageQueueItem>();
                var exifData = new Dictionary<string, ExifGpsData?>();
                int cachedCount = 0;
                int processedCount = 0;

                // Process each image: check cache first, then API if needed
                foreach (var item in queuedImages)
                {
                    item.Status = QueueStatus.Processing;

                    // Step 1: Check cache
                    var cached = await _cacheService.GetCachedPredictionAsync(item.FilePath);

                    if (cached != null)
                    {
                        // Cache hit - instant result!
                        System.Diagnostics.Debug.WriteLine($"[ProcessImages] Cache HIT for: {item.FileName}");

                        item.Status = QueueStatus.Cached;
                        item.IsCached = true;
                        cachedCount++;

                        // Display predictions for first image
                        if (processedCount == 0)
                        {
                            await DisplayCachedPredictionsAsync(cached);
                        }

                        processedCount++;
                    }
                    else
                    {
                        // Cache miss - need to call API
                        System.Diagnostics.Debug.WriteLine($"[ProcessImages] Cache MISS for: {item.FileName}");
                        uncachedImages.Add(item);

                        // Extract EXIF data for uncached images
                        var gpsData = await exifExtractor.ExtractGpsDataAsync(item.FilePath);
                        exifData[item.FilePath] = gpsData;

                        if (gpsData?.HasGps == true)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ProcessImages] Found GPS in {item.FileName}: {gpsData.Latitude:F6}, {gpsData.Longitude:F6}");
                        }
                    }
                }

                // Step 2: Process uncached images via API
                if (uncachedImages.Count > 0)
                {
                    ReliabilityMessage = $"Processing {uncachedImages.Count} image(s) via AI...";

                    var imagePaths = uncachedImages.Select(i => i.FilePath).ToList();
                    var response = await App.ApiClient.InferBatchAsync(imagePaths, topK: 5);

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

                                    // Store in cache for future lookups
                                    var gps = exifData.ContainsKey(result.Path) ? exifData[result.Path] : null;
                                    await _cacheService.StorePredictionAsync(
                                        result.Path,
                                        result.Predictions ?? new List<Services.DTOs.PredictionCandidate>(),
                                        gps
                                    );

                                    System.Diagnostics.Debug.WriteLine($"[ProcessImages] Stored in cache: {System.IO.Path.GetFileName(result.Path)}");

                                    // Display predictions for first image if no cached images were shown
                                    if (processedCount == cachedCount && imageItem == uncachedImages.First())
                                    {
                                        await DisplayPredictionsAsync(result, gps);
                                    }
                                }
                                else
                                {
                                    imageItem.Status = QueueStatus.Error;
                                }

                                processedCount++;
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

                System.Diagnostics.Debug.WriteLine($"[ProcessImages] Summary: {cachedCount} cached, {uncachedImages.Count} processed, {processedCount} total");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing images: {ex.Message}");
                ReliabilityMessage = $"Error: {ex.Message}";

                // Mark as error
                foreach (var item in queuedImages)
                {
                    if (item.Status == QueueStatus.Processing)
                    {
                        item.Status = QueueStatus.Error;
                    }
                }
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
                        AdjustedProbability = pred.Probability, // Will be updated by cluster analysis
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

                // Run cluster analysis on AI predictions (exclude EXIF which has Rank = 0)
                var aiPredictions = Predictions.Where(p => p.Rank > 0).ToList();
                if (aiPredictions.Count >= 2)
                {
                    _currentClusterInfo = _clusterAnalyzer.AnalyzeClusters(aiPredictions);

                    System.Diagnostics.Debug.WriteLine($"[DisplayPredictions] Cluster analysis: IsClustered={_currentClusterInfo.IsClustered}, Radius={_currentClusterInfo.ClusterRadius:F1}km");

                    // Update ReliabilityMessage based on clustering (only if no EXIF GPS)
                    if (!HasExifGps && _currentClusterInfo.IsClustered)
                    {
                        ReliabilityMessage = $"High reliability - predictions clustered within {_currentClusterInfo.ClusterRadius:F0}km";
                    }
                }
                else
                {
                    _currentClusterInfo = null;
                }

                // Rotate to first location (EXIF if available, otherwise first AI prediction)
                if (Predictions.Count > 0 && _mapProvider != null && _mapProvider.IsReady)
                {
                    var first = Predictions[0];
                    await _mapProvider.RotateToLocationAsync(first.Latitude, first.Longitude, 1500);
                }

                if (!HasExifGps)
                {
                    // Set default message if clustering didn't already set it
                    if (_currentClusterInfo == null || !_currentClusterInfo.IsClustered)
                    {
                        ReliabilityMessage = $"Showing {result.Predictions.Count} AI predictions";
                    }
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
                        AdjustedProbability = pred.Probability, // Will be updated by cluster analysis
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

                // Run cluster analysis on AI predictions (exclude EXIF which has Rank = 0)
                var aiPredictions = Predictions.Where(p => p.Rank > 0).ToList();
                if (aiPredictions.Count >= 2)
                {
                    _currentClusterInfo = _clusterAnalyzer.AnalyzeClusters(aiPredictions);

                    System.Diagnostics.Debug.WriteLine($"[DisplayCachedPredictions] Cluster analysis: IsClustered={_currentClusterInfo.IsClustered}, Radius={_currentClusterInfo.ClusterRadius:F1}km");

                    // Update ReliabilityMessage based on clustering (only if no EXIF GPS)
                    if (!HasExifGps && _currentClusterInfo.IsClustered)
                    {
                        ReliabilityMessage = $"High reliability - predictions clustered within {_currentClusterInfo.ClusterRadius:F0}km (cached)";
                    }
                }
                else
                {
                    _currentClusterInfo = null;
                }

                // Rotate to first location (EXIF if available, otherwise first AI prediction)
                if (Predictions.Count > 0 && _mapProvider != null && _mapProvider.IsReady)
                {
                    var first = Predictions[0];
                    await _mapProvider.RotateToLocationAsync(first.Latitude, first.Longitude, 1500);
                }

                if (!HasExifGps)
                {
                    // Set default message if clustering didn't already set it
                    if (_currentClusterInfo == null || !_currentClusterInfo.IsClustered)
                    {
                        ReliabilityMessage = $"Showing {cached.Predictions.Count} AI predictions (cached)";
                    }
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
                >= 0.1 => ConfidenceLevel.High,
                >= 0.05 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            };
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            // Remove currently selected image
            var selectedItem = ImageListView.SelectedItem as ImageQueueItem;
            if (selectedItem != null)
            {
                ImageQueue.Remove(selectedItem);
                OnPropertyChanged(nameof(QueueStatusMessage));
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
                    Debug.WriteLine($"[Export] Failed to capture map screenshot: {ex.Message}");
                }
            }

            // Call the existing export method (which will now include the map if available)
            await ExportCurrentResultAsync(format, fileTypeDescription, fileExtension, mapImagePath);
        }

        private async Task<string?> CaptureMapScreenshotAsync()
        {
            try
            {
                // Create a temporary file path for the screenshot
                var tempFolder = Path.GetTempPath();
                var screenshotPath = Path.Combine(tempFolder, $"geolens_map_{Guid.NewGuid()}.png");

                // Use WebView2 screenshot capability if available
                if (_mapProvider is LeafletMapProvider leafletProvider)
                {
                    // Get the WebView2 control from the map provider
                    // For now, we'll use JavaScript to capture the map canvas
                    var jsScript = @"
                        (async function() {
                            const map = window.leafletMap;
                            if (!map) return null;

                            // Get the map container
                            const container = map.getContainer();
                            const canvas = await html2canvas(container, {
                                useCORS: true,
                                allowTaint: false,
                                backgroundColor: '#1a1a1a'
                            });

                            return canvas.toDataURL('image/png');
                        })();
                    ";

                    // Note: This is a simplified version. Full implementation would need html2canvas library
                    // or use a different screenshot method
                    Debug.WriteLine("[Export] Map screenshot capture not fully implemented yet");
                    return null;
                }

                return screenshotPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Export] Error capturing map screenshot: {ex.Message}");
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
                            System.Diagnostics.Debug.WriteLine($"[Export] Failed to load thumbnail: {ex.Message}");
                        }

                        // Load map image for PDF
                        byte[]? mapBytes = null;
                        if (!string.IsNullOrEmpty(mapImagePath) && File.Exists(mapImagePath))
                        {
                            try
                            {
                                mapBytes = await File.ReadAllBytesAsync(mapImagePath);
                                Debug.WriteLine($"[Export] Loaded map image: {mapImagePath} ({mapBytes.Length} bytes)");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Export] Failed to load map image: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[Export] Exported to: {exportedPath}");
            }
            catch (Exception ex)
            {
                ShowExportFeedback($"Export failed: {ex.Message}", InfoBarSeverity.Error);
                System.Diagnostics.Debug.WriteLine($"[Export] Error: {ex}");
            }
        }

        private EnhancedPredictionResult BuildEnhancedPredictionResult()
        {
            // Get AI predictions (exclude EXIF which has Rank = 0)
            var aiPredictions = Predictions.Where(p => p.Rank > 0).ToList();

            return new EnhancedPredictionResult
            {
                ImagePath = _currentImagePath,
                AiPredictions = aiPredictions,
                ExifGps = _currentExifData,
                ClusterInfo = _currentClusterInfo
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
                System.Diagnostics.Debug.WriteLine($"[LoadThumbnail] Error: {ex.Message}");
                return null;
            }
        }

        private void ShowExportFeedback(string message, InfoBarSeverity severity)
        {
            ExportFeedbackBar.Message = message;
            ExportFeedbackBar.Severity = severity;
            ExportFeedbackBar.IsOpen = true;
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

                    Debug.WriteLine($"[Prediction] Focused map on {prediction.LocationSummary} ({prediction.Latitude:F6}, {prediction.Longitude:F6})");
                }
            }
        }
    }
}
