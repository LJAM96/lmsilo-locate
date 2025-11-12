using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace GeoLens.Services.MapProviders
{
    /// <summary>
    /// WebView2-based 3D globe provider using Three.js and Globe.GL
    /// Supports both online (CDN) and offline (bundled assets) modes
    /// </summary>
    public class WebView2GlobeProvider : IMapProvider
    {
        private readonly WebView2 _webView;
        private bool _isInitialized = false;
        private readonly string _htmlPath;
        private readonly bool _offlineMode;

        public bool IsReady => _isInitialized;

        /// <summary>
        /// Create a new WebView2 globe provider
        /// </summary>
        /// <param name="webView">The WebView2 control to use</param>
        /// <param name="offlineMode">Whether to use offline mode (bundled assets only)</param>
        public WebView2GlobeProvider(WebView2 webView, bool offlineMode = false)
        {
            _webView = webView ?? throw new ArgumentNullException(nameof(webView));
            _offlineMode = offlineMode;

            // Determine HTML file path
            string appDir = AppContext.BaseDirectory;
            _htmlPath = Path.Combine(appDir, "Assets", "Globe", "globe_dark.html");

            Debug.WriteLine($"[WebView2Globe] HTML path: {_htmlPath}");
            Debug.WriteLine($"[WebView2Globe] Offline mode: {_offlineMode}");
        }

        /// <summary>
        /// Initialize the WebView2 control and load the globe HTML
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[WebView2Globe] Initializing...");

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
                    Debug.WriteLine($"[WebView2Globe] ERROR: HTML file not found: {_htmlPath}");
                    throw new FileNotFoundException($"Globe HTML not found: {_htmlPath}");
                }

                // Load the HTML file
                string htmlUri = new Uri(_htmlPath).AbsoluteUri;
                Debug.WriteLine($"[WebView2Globe] Loading: {htmlUri}");
                _webView.Source = new Uri(htmlUri);

                // Wait for navigation to complete
                var tcs = new TaskCompletionSource<bool>();
                void NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
                {
                    _webView.NavigationCompleted -= NavigationCompleted;
                    tcs.SetResult(args.IsSuccess);
                }
                _webView.NavigationCompleted += NavigationCompleted;

                bool success = await tcs.Task;

                if (!success)
                {
                    Debug.WriteLine("[WebView2Globe] ERROR: Navigation failed");
                    throw new Exception("Failed to load globe HTML");
                }

                // Set globe mode (online/offline)
                if (_offlineMode)
                {
                    await ExecuteScriptAsync("globeAPI.setGlobeMode('offline')");
                }

                // Wait a bit for globe to fully initialize
                await Task.Delay(1000);

                // Check if globe is ready
                string isReadyResult = await ExecuteScriptAsync("globeAPI.isReady()");
                _isInitialized = isReadyResult == "true";

                Debug.WriteLine($"[WebView2Globe] Initialization complete. Ready: {_isInitialized}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView2Globe] ERROR during initialization: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Add a pin to the globe
        /// </summary>
        public async Task AddPinAsync(double latitude, double longitude, string label, double confidence, int rank, bool isExif = false)
        {
            if (!_isInitialized)
            {
                Debug.WriteLine("[WebView2Globe] WARNING: Not initialized, skipping AddPin");
                return;
            }

            try
            {
                // Escape label for JavaScript
                string escapedLabel = EscapeJavaScript(label);

                // Call JavaScript function
                string script = $"globeAPI.addPin({latitude}, {longitude}, '{escapedLabel}', {confidence}, {rank}, {isExif.ToString().ToLower()})";
                await ExecuteScriptAsync(script);

                Debug.WriteLine($"[WebView2Globe] Added pin: {label} ({latitude}, {longitude})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView2Globe] ERROR adding pin: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all pins from the globe
        /// </summary>
        public async Task ClearPinsAsync()
        {
            if (!_isInitialized) return;

            try
            {
                await ExecuteScriptAsync("globeAPI.clearPins()");
                Debug.WriteLine("[WebView2Globe] Cleared all pins");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView2Globe] ERROR clearing pins: {ex.Message}");
            }
        }

        /// <summary>
        /// Rotate the globe to focus on a specific location
        /// </summary>
        public async Task RotateToLocationAsync(double latitude, double longitude, int durationMs = 1000)
        {
            if (!_isInitialized) return;

            try
            {
                string script = $"globeAPI.rotateToLocation({latitude}, {longitude}, {durationMs})";
                await ExecuteScriptAsync(script);

                Debug.WriteLine($"[WebView2Globe] Rotated to: ({latitude}, {longitude})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView2Globe] ERROR rotating: {ex.Message}");
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
                string script = $"globeAPI.setHeatmapMode({enabled.ToString().ToLower()})";
                await ExecuteScriptAsync(script);

                Debug.WriteLine($"[WebView2Globe] Heatmap mode: {enabled}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView2Globe] ERROR setting heatmap mode: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute JavaScript in the WebView2
        /// </summary>
        private async Task<string> ExecuteScriptAsync(string script)
        {
            try
            {
                string result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                return result?.Trim('"') ?? "";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebView2Globe] Script execution error: {ex.Message}");
                Debug.WriteLine($"[WebView2Globe] Script: {script}");
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
    }
}
