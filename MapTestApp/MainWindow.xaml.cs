using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MapTestApp
{
    public sealed partial class MainWindow : Window
    {
        private readonly List<TestLocation> _testLocations = new()
        {
            new TestLocation { Name = "Paris, France", Lat = 48.8566, Lon = 2.3522, Confidence = 0.92 },
            new TestLocation { Name = "London, UK", Lat = 51.5074, Lon = -0.1278, Confidence = 0.15 },
            new TestLocation { Name = "New York, USA", Lat = 40.7128, Lon = -74.0060, Confidence = 0.08 },
            new TestLocation { Name = "Tokyo, Japan", Lat = 35.6762, Lon = 139.6503, Confidence = 0.05 },
            new TestLocation { Name = "Sydney, Australia", Lat = -33.8688, Lon = 151.2093, Confidence = 0.03 }
        };

        public MainWindow()
        {
            InitializeComponent();
            Title = "GeoLens Map Comparison Tool";

            // Set window size
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1400, Height = 900 });
        }

        private async void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MapTabView.SelectedItem is not TabViewItem selectedTab)
                return;

            var tag = selectedTab.Tag?.ToString();
            if (tag == null)
                return;

            Debug.WriteLine($"[MainWindow] Switched to tab: {tag}");

            // Initialize the selected map
            switch (tag)
            {
                case "globe_nasa":
                    await InitializeMapAsync(GlobeNasaWebView, "globe_nasa.html");
                    break;
                case "leaflet":
                    await InitializeMapAsync(LeafletWebView, "leaflet_dark.html");
                    break;
                case "maplibre_2d":
                    await InitializeMapAsync(MapLibre2DWebView, "maplibre_2d.html");
                    break;
                case "maplibre_3d":
                    await InitializeMapAsync(MapLibre3DWebView, "maplibre_3d.html");
                    break;
                case "cesium":
                    await InitializeMapAsync(CesiumWebView, "cesium_dark.html");
                    break;
            }
        }

        private async System.Threading.Tasks.Task InitializeMapAsync(WebView2 webView, string htmlFile)
        {
            if (webView.CoreWebView2 != null)
                return; // Already initialized

            try
            {
                Debug.WriteLine($"[MainWindow] Initializing {htmlFile}...");
                await webView.EnsureCoreWebView2Async();

                // Configure WebView2
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                // Load HTML file
                string appDir = AppContext.BaseDirectory;
                string htmlPath = Path.Combine(appDir, "Assets", htmlFile);

                if (!File.Exists(htmlPath))
                {
                    Debug.WriteLine($"[MainWindow] ERROR: HTML file not found: {htmlPath}");
                    return;
                }

                string htmlUri = new Uri(htmlPath).AbsoluteUri;
                webView.Source = new Uri(htmlUri);

                // Wait for load and add test pins
                webView.NavigationCompleted += async (s, args) =>
                {
                    if (!args.IsSuccess)
                        return;

                    await System.Threading.Tasks.Task.Delay(2000); // Wait for map initialization

                    // Add test pins
                    foreach (var loc in _testLocations)
                    {
                        var script = $"if(window.mapAPI) {{ mapAPI.addPin({loc.Lat}, {loc.Lon}, '{loc.Name}', {loc.Confidence}, {_testLocations.IndexOf(loc) + 1}, false); }}";
                        await webView.CoreWebView2.ExecuteScriptAsync(script);
                    }

                    // Fly to first location
                    var firstLoc = _testLocations[0];
                    var flyScript = $"if(window.mapAPI) {{ mapAPI.flyTo({firstLoc.Lat}, {firstLoc.Lon}, 4, 2000); }}";
                    await webView.CoreWebView2.ExecuteScriptAsync(flyScript);

                    Debug.WriteLine($"[MainWindow] {htmlFile} initialized with test pins");
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error initializing {htmlFile}: {ex.Message}");
            }
        }

        private class TestLocation
        {
            public string Name { get; set; } = "";
            public double Lat { get; set; }
            public double Lon { get; set; }
            public double Confidence { get; set; }
        }
    }
}
