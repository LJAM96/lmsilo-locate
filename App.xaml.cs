using GeoLens.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace GeoLens
{
    public partial class App : Application
    {
        private Window? _mainWindow;
        private static Window? _settingsWindow;
        public static Window? MainWindow { get; private set; }
        public new static App Current => (App)Application.Current;

        // Services
        public static PythonRuntimeManager? PythonManager { get; private set; }
        public static GeoCLIPApiClient? ApiClient { get; private set; }
        public static HardwareInfo? DetectedHardware { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            _mainWindow ??= new Window
            {
                Title = "GeoLens - Initializing..."
            };

            // Show loading UI
            var loadingFrame = new Frame { RequestedTheme = ElementTheme.Dark };
            _mainWindow.Content = loadingFrame;
            _mainWindow.Activate();
            MainWindow = _mainWindow;

            // Initialize services in background
            bool servicesStarted = await InitializeServicesAsync();

            if (servicesStarted)
            {
                // Services ready - navigate to main page
                _mainWindow.Title = "GeoLens";
                var frame = new Frame { RequestedTheme = ElementTheme.Dark };
                frame.NavigationFailed += OnNavigationFailed;
                _mainWindow.Content = frame;
                frame.Navigate(typeof(Views.MainPage), args.Arguments);
            }
            else
            {
                // Failed to start - show error
                await ShowServiceErrorDialog();
                _mainWindow.Close();
            }
        }

        private async Task<bool> InitializeServicesAsync()
        {
            try
            {
                // 1. Detect hardware
                Debug.WriteLine("[App] Detecting hardware...");
                var hardwareService = new HardwareDetectionService();
                DetectedHardware = hardwareService.DetectHardware();
                Debug.WriteLine($"[App] Detected: {DetectedHardware.Description}");

                // 2. Determine runtime path
                string appDir = AppContext.BaseDirectory;
                string runtimePath;

                // For development: use local Python
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine("[App] Development mode - checking for conda environment");

                    // Check for CONDA_PREFIX environment variable (set when conda env is active)
                    var condaPrefix = Environment.GetEnvironmentVariable("CONDA_PREFIX");
                    if (!string.IsNullOrEmpty(condaPrefix))
                    {
                        runtimePath = Path.Combine(condaPrefix, "python.exe");
                        Debug.WriteLine($"[App] Using conda environment: {runtimePath}");
                    }
                    else
                    {
                        // Try to find geolens conda environment
                        var condaRoot = Environment.GetEnvironmentVariable("CONDA_ROOT")
                                     ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "miniconda3");
                        var geolensEnvPath = Path.Combine(condaRoot, "envs", "geolens", "python.exe");

                        if (File.Exists(geolensEnvPath))
                        {
                            runtimePath = geolensEnvPath;
                            Debug.WriteLine($"[App] Found geolens conda environment: {runtimePath}");
                        }
                        else
                        {
                            runtimePath = "python"; // Fallback to system Python
                            Debug.WriteLine("[App] Using system Python (conda env not found)");
                        }
                    }
                }
                else
                {
                    // For production: use embedded runtime
                    string runtimeFolder = DetectedHardware.Type switch
                    {
                        HardwareType.NvidiaGpu => "python_cuda",
                        HardwareType.AmdGpu => "python_rocm",
                        _ => "python_cpu"
                    };

                    runtimePath = Path.Combine(appDir, "Runtimes", runtimeFolder, "python.exe");

                    if (!File.Exists(runtimePath))
                    {
                        Debug.WriteLine($"[App] WARNING: Embedded runtime not found: {runtimePath}");
                        Debug.WriteLine("[App] Falling back to system Python");
                        runtimePath = "python";
                    }
                }

                // 3. Start Python service (includes health check)
                Debug.WriteLine($"[App] Starting Python service with runtime: {runtimePath}");
                PythonManager = new PythonRuntimeManager(runtimePath, port: 8899);
                bool serviceStarted = await PythonManager.StartAsync(DetectedHardware.DeviceChoice);

                if (!serviceStarted)
                {
                    Debug.WriteLine("[App] ERROR: Service failed to start or health check failed");
                    return false;
                }

                // 5. Initialize API client
                ApiClient = new GeoCLIPApiClient(PythonManager.BaseUrl);
                Debug.WriteLine("[App] Services initialized successfully");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] ERROR initializing services: {ex.Message}");
                Debug.WriteLine($"[App] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private async Task ShowServiceErrorDialog()
        {
            // Ensure window has a XamlRoot by waiting a moment for rendering
            await Task.Delay(100);

            // Check if XamlRoot is available
            if (_mainWindow?.Content?.XamlRoot == null)
            {
                Debug.WriteLine("[App] Cannot show error dialog - XamlRoot not available");
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Startup Error",
                Content = "Failed to start the AI service. Please ensure:\n\n" +
                          "• Python 3.11+ is installed (dev mode)\n" +
                          "• Required packages are installed\n" +
                          "• Port 8899 is not in use\n\n" +
                          "Check the debug output for details.",
                CloseButtonText = "Exit",
                XamlRoot = _mainWindow.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        public static void ShowSettingsWindow()
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            var frame = new Frame
            {
                RequestedTheme = ElementTheme.Dark
            };
            frame.NavigationFailed += (s, e) => Current.OnNavigationFailed(s, e);
            frame.Navigate(typeof(Views.SettingsPage));

            var settingsWindow = new Window
            {
                Title = "GeoLens Settings",
                Content = frame
            };

            settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow = settingsWindow;
            settingsWindow.Activate();
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"NavigationFailed: {e.SourcePageType.FullName} - {e.Exception}");
            e.Handled = true;
        }
    }
}
