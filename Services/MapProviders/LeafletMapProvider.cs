using GeoLens.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GeoLens.Services.MapProviders
{
    /// <summary>
    /// WebView2-based 2D map provider using Leaflet.js with dark mode styling
    /// Supports both online (CartoDB Dark, Stadia Dark) and offline (local tiles) modes
    /// </summary>
    public class LeafletMapProvider : IMapProvider
    {
        private readonly WebView2 _webView;
        private bool _isInitialized = false;
        private readonly string _htmlPath;
        private readonly bool _offlineMode;
        private readonly MapTileCacheService? _tileCacheService;

        public bool IsReady => _isInitialized;

        /// <summary>
        /// Create a new Leaflet map provider
        /// </summary>
        /// <param name="webView">The WebView2 control to use</param>
        /// <param name="offlineMode">Whether to use offline mode (local tiles only)</param>
        /// <param name="tileCacheService">Optional map tile cache service for offline viewing</param>
        public LeafletMapProvider(WebView2 webView, bool offlineMode = false, MapTileCacheService? tileCacheService = null)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _offlineMode = offlineMode;
            _tileCacheService = tileCacheService;

            // Determine HTML file path
            string appDir = AppContext.BaseDirectory;
            _htmlPath = Path.Combine(appDir, "Assets", "Maps", "leaflet_dark.html");

            Debug.WriteLine($"[LeafletMap] HTML path: {_htmlPath}");
            Debug.WriteLine($"[LeafletMap] Offline mode: {_offlineMode}");
            Debug.WriteLine($"[LeafletMap] Tile cache: {(_tileCacheService != null ? "Enabled" : "Disabled")}");
        }

        /// <summary>
        /// Initialize the WebView2 control and load the Leaflet map HTML
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[LeafletMap] Initializing...");

                // Ensure WebView2 is initialized
                await _webView.EnsureCoreWebView2Async();

                // Configure WebView2 settings
                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = Debugger.IsAttached;

                // Set background color
                _webView.DefaultBackgroundColor = Microsoft.UI.Colors.Black;

                // Check if HTML file exists
                if (!File.Exists(_htmlPath))
                {
                    Debug.WriteLine($"[LeafletMap] ERROR: HTML file not found: {_htmlPath}");
                    throw new FileNotFoundException($"Leaflet map HTML not found: {_htmlPath}");
                }

                // Load the HTML file
                string htmlUri = new Uri(_htmlPath).AbsoluteUri;
                Debug.WriteLine($"[LeafletMap] Loading: {htmlUri}");
                _webView.Source = new Uri(htmlUri);

                // Wait for navigation to complete
                var tcs = new TaskCompletionSource<bool>();
                void NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
                {
                    _webView.NavigationCompleted -= NavigationCompleted;
                    // Check if already completed to prevent race condition
                    if (!tcs.Task.IsCompleted)
                    {
                        if (args.IsSuccess)
                            tcs.SetResult(true);
                        else
                            tcs.SetException(new Exception("WebView2 navigation failed"));
                    }
                }
                _webView.NavigationCompleted += NavigationCompleted;

                bool success = await tcs.Task;

                if (!success)
                {
                    Debug.WriteLine("[LeafletMap] ERROR: Navigation failed");
                    throw new Exception("Failed to load Leaflet map HTML");
                }

                // Set map mode (online/offline)
                if (_offlineMode)
                {
                    await ExecuteScriptAsync("mapAPI.setMapMode('offline')");
                }

                // Register tile cache service if available
                if (_tileCacheService != null)
                {
                    await _tileCacheService.InitializeAsync();
                    _tileCacheService.RegisterWebViewInterception(_webView.CoreWebView2);
                    Debug.WriteLine("[LeafletMap] Tile cache service registered");
                }

                // Wait a bit for Leaflet to fully initialize
                await Task.Delay(500); // Leaflet is faster than Globe.GL

                // Check if map is ready
                string isReadyResult = await ExecuteScriptAsync("mapAPI.isReady()");
                _isInitialized = isReadyResult.Trim('"').ToLower() == "true";

                Debug.WriteLine($"[LeafletMap] Initialization complete. Ready: {_isInitialized}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR during initialization: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Add a pin/marker to the map
        /// </summary>
        public async Task AddPinAsync(double latitude, double longitude, string label, double confidence, int rank, bool isExif = false)
        {
            if (!_isInitialized)
            {
                Debug.WriteLine("[LeafletMap] WARNING: Not initialized, skipping AddPin");
                return;
            }

            try
            {
                // Escape label for JavaScript
                string escapedLabel = EscapeJavaScript(label);

                // Call JavaScript function
                string script = $"mapAPI.addPin({latitude}, {longitude}, '{escapedLabel}', {confidence}, {rank}, {isExif.ToString().ToLower()})";
                await ExecuteScriptAsync(script);

                Debug.WriteLine($"[LeafletMap] Added pin: {label} ({latitude}, {longitude})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR adding pin: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all markers from the map
        /// </summary>
        public async Task ClearPinsAsync()
        {
            if (!_isInitialized) return;

            try
            {
                await ExecuteScriptAsync("mapAPI.clearPins()");
                Debug.WriteLine("[LeafletMap] Cleared all pins");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR clearing pins: {ex.Message}");
            }
        }

        /// <summary>
        /// Fly/zoom the map to focus on a specific location
        /// </summary>
        public async Task RotateToLocationAsync(double latitude, double longitude, int durationMs = 1000)
        {
            if (!_isInitialized) return;

            try
            {
                string script = $"mapAPI.flyToLocation({latitude}, {longitude}, {durationMs})";
                await ExecuteScriptAsync(script);

                Debug.WriteLine($"[LeafletMap] Flying to: ({latitude}, {longitude})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR flying to location: {ex.Message}");
            }
        }

        /// <summary>
        /// Fit map bounds to show all markers
        /// </summary>
        public async Task FitToMarkersAsync()
        {
            if (!_isInitialized) return;

            try
            {
                await ExecuteScriptAsync("mapAPI.fitToMarkers()");
                Debug.WriteLine("[LeafletMap] Fitted bounds to markers");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR fitting to markers: {ex.Message}");
            }
        }

        /// <summary>
        /// Enable or disable heatmap visualization mode
        /// </summary>
        public async Task SetHeatmapModeAsync(bool enabled)
        {
            if (!_isInitialized) return;

            try
            {
                string script = $"mapAPI.setHeatmapMode({enabled.ToString().ToLower()})";
                await ExecuteScriptAsync(script);

                Debug.WriteLine($"[LeafletMap] Heatmap mode: {enabled}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR setting heatmap mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Show a heatmap visualization from multiple image predictions
        /// </summary>
        public async Task ShowHeatmapAsync(HeatmapData heatmap)
        {
            if (!_isInitialized) return;

            try
            {
                // Convert HeatmapData to JSON for JavaScript
                var heatmapJson = SerializeHeatmapData(heatmap);

                // Call JavaScript function with JSON data
                string script = $"mapAPI.showHeatmap({heatmapJson})";
                await ExecuteScriptAsync(script);

                Debug.WriteLine($"[LeafletMap] Showed heatmap: {heatmap.ImageCount} images, {heatmap.TotalPredictions} predictions, {heatmap.Hotspots.Count} hotspots");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR showing heatmap: {ex.Message}");
            }
        }

        /// <summary>
        /// Hide the current heatmap visualization
        /// </summary>
        public async Task HideHeatmapAsync()
        {
            if (!_isInitialized) return;

            try
            {
                await ExecuteScriptAsync("mapAPI.hideHeatmap()");
                Debug.WriteLine("[LeafletMap] Hid heatmap");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR hiding heatmap: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle between heatmap and individual pins mode
        /// </summary>
        public async Task ToggleHeatmapModeAsync(bool showHeatmap)
        {
            if (!_isInitialized) return;

            try
            {
                string script = $"mapAPI.toggleHeatmapMode({showHeatmap.ToString().ToLower()})";
                await ExecuteScriptAsync(script);

                Debug.WriteLine($"[LeafletMap] Toggled heatmap mode: {showHeatmap}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR toggling heatmap mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Serialize HeatmapData to JSON for JavaScript consumption
        /// </summary>
        private string SerializeHeatmapData(HeatmapData heatmap)
        {
            // Convert 2D grid to flat array format for JavaScript
            var gridPoints = new System.Collections.Generic.List<object>();

            for (int x = 0; x < heatmap.Width; x++)
            {
                for (int y = 0; y < heatmap.Height; y++)
                {
                    double intensity = heatmap.IntensityGrid[x, y];

                    // Only include cells with significant intensity (> 10%)
                    if (intensity > 0.1)
                    {
                        double lon = x - 180;
                        double lat = 90 - y;

                        gridPoints.Add(new
                        {
                            lat = lat,
                            lng = lon,
                            intensity = intensity
                        });
                    }
                }
            }

            // Build JSON object
            var jsonObject = new
            {
                width = heatmap.Width,
                height = heatmap.Height,
                resolution = heatmap.Resolution,
                gridPoints = gridPoints,
                hotspots = heatmap.Hotspots.Select(h => new
                {
                    lat = h.Latitude,
                    lng = h.Longitude,
                    intensity = h.Intensity,
                    radiusKm = h.RadiusKm,
                    predictionCount = h.PredictionCount,
                    locationName = h.LocationName ?? "Unknown"
                }).ToArray(),
                statistics = new
                {
                    totalPredictions = heatmap.TotalPredictions,
                    imageCount = heatmap.ImageCount,
                    exifCount = heatmap.Statistics.ExifCount,
                    aiCount = heatmap.Statistics.AiCount,
                    coverageAreaKm2 = heatmap.Statistics.CoverageAreaKm2
                }
            };

            return JsonSerializer.Serialize(jsonObject);
        }

        /// <summary>
        /// Execute JavaScript in the WebView2
        /// </summary>
        private async Task<string> ExecuteScriptAsync(string script)
        {
            try
            {
                string result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                return result ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] Script execution error: {ex.Message}");
                Debug.WriteLine($"[LeafletMap] Script: {script}");
                throw;
            }
        }

        /// <summary>
        /// Escape a string for use in JavaScript
        /// </summary>
        private string EscapeJavaScript(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            return input
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        /// <summary>
        /// Capture a screenshot of the current map view
        /// Returns the file path to the saved screenshot PNG
        /// </summary>
        public async Task<string?> CaptureScreenshotAsync()
        {
            if (!_isInitialized) return null;

            try
            {
                // Create temp directory if it doesn't exist
                var tempFolder = Path.GetTempPath();
                var screenshotPath = Path.Combine(tempFolder, $"geolens_map_{Guid.NewGuid()}.png");

                // Use WebView2's built-in screenshot capability
                using (var stream = new FileStream(screenshotPath, FileMode.Create, FileAccess.Write))
                {
                    await _webView.CoreWebView2.CapturePreviewAsync(
                        CoreWebView2CapturePreviewImageFormat.Png,
                        stream.AsRandomAccessStream()
                    );
                }

                Debug.WriteLine($"[LeafletMap] Screenshot captured: {screenshotPath}");
                return screenshotPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LeafletMap] ERROR capturing screenshot: {ex.Message}");
                return null;
            }
        }
    }

}
