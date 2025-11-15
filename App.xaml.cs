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
        private Views.LoadingPage? _loadingPage;
        public static Window? MainWindow { get; private set; }
        public new static App Current => (App)Application.Current;

        // Services
        public static PythonRuntimeManager? PythonManager { get; private set; }
        public static GeoCLIPApiClient? ApiClient { get; private set; }
        public static HardwareInfo? DetectedHardware { get; private set; }
        public static UserSettingsService SettingsService { get; private set; } = null!;
        public static PredictionCacheService CacheService { get; private set; } = null!;
        public static AuditLogService AuditService { get; private set; } = null!;

        public App()
        {
            InitializeComponent();

            // Initialize services
            SettingsService = new UserSettingsService();
            CacheService = new PredictionCacheService();
            AuditService = new AuditLogService();

            // Register for application exit to dispose services
            this.UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            DisposeServices();
        }

        private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            DisposeServices();
        }

        private void DisposeServices()
        {
            try
            {
                ApiClient?.Dispose();
                PythonManager?.Dispose();
                CacheService?.Dispose();
                AuditService?.Dispose();
                Debug.WriteLine("[App] Services disposed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Error disposing services: {ex.Message}");
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            _mainWindow ??= new Window
            {
                Title = "GeoLens"
            };

            // Create and show loading page
            _loadingPage = new Views.LoadingPage();
            _loadingPage.RetryRequested += LoadingPage_RetryRequested;
            _loadingPage.ExitRequested += LoadingPage_ExitRequested;

            var loadingFrame = new Frame { RequestedTheme = ElementTheme.Dark };
            loadingFrame.Content = _loadingPage;
            _mainWindow.Content = loadingFrame;
            _mainWindow.Activate();
            MainWindow = _mainWindow;

            // Start rotating tips
            _ = _loadingPage.RotateTipsAsync();

            // Initialize services with progress reporting
            bool servicesStarted = await InitializeServicesWithProgressAsync();

            if (servicesStarted)
            {
                // Unsubscribe loading page events before navigation
                if (_loadingPage != null)
                {
                    _loadingPage.RetryRequested -= LoadingPage_RetryRequested;
                    _loadingPage.ExitRequested -= LoadingPage_ExitRequested;
                    _loadingPage = null;
                }

                // Services ready - navigate to main page
                var frame = new Frame { RequestedTheme = ElementTheme.Dark };
                frame.NavigationFailed += OnNavigationFailed;
                _mainWindow.Content = frame;
                frame.Navigate(typeof(Views.MainPage), args.Arguments);
            }
            // If failed, error is shown in loading page with retry option
        }

        private async void LoadingPage_RetryRequested(object? sender, EventArgs e)
        {
            bool servicesStarted = await InitializeServicesWithProgressAsync();
            if (servicesStarted)
            {
                // Unsubscribe loading page events before navigation
                if (_loadingPage != null)
                {
                    _loadingPage.RetryRequested -= LoadingPage_RetryRequested;
                    _loadingPage.ExitRequested -= LoadingPage_ExitRequested;
                    _loadingPage = null;
                }

                var frame = new Frame { RequestedTheme = ElementTheme.Dark };
                frame.NavigationFailed += OnNavigationFailed;
                _mainWindow!.Content = frame;
                frame.Navigate(typeof(Views.MainPage), null);
            }
        }

        private void LoadingPage_ExitRequested(object? sender, EventArgs e)
        {
            _mainWindow?.Close();
        }

        private async Task<bool> InitializeServicesWithProgressAsync()
        {
            try
            {
                // Stage 0: Load settings (2% progress)
                _loadingPage?.UpdateStatus("Loading settings...");
                _loadingPage?.UpdateProgress(2);
                await SettingsService.LoadSettingsAsync();
                await CacheService.InitializeAsync();
                Debug.WriteLine("[App] Settings and cache initialized");

                // Stage 1: Detect hardware (5% progress)
                _loadingPage?.UpdateStatus("Detecting hardware...");
                _loadingPage?.UpdateProgress(5);
                await Task.Delay(50); // Small delay to ensure UI updates

                Debug.WriteLine("[App] Detecting hardware...");
                var hardwareService = new HardwareDetectionService();
                DetectedHardware = hardwareService.DetectHardware();
                Debug.WriteLine($"[App] Detected: {DetectedHardware.Description}");

                // Update hardware info in settings
                SettingsService.UpdateHardwareInfo(
                    DetectedHardware.Description,
                    DetectedHardware.Type.ToString()
                );

                _loadingPage?.UpdateSubStatus(DetectedHardware.Description);
                await Task.Delay(100);

                // Stage 2: Determine runtime path (10% progress)
                _loadingPage?.UpdateStatus("Locating Python runtime...");
                _loadingPage?.UpdateProgress(10);

                string appDir = AppContext.BaseDirectory;
                string runtimePath;

                // For development: use local Python
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine("[App] Development mode - checking for conda environment");

                    var condaPrefix = Environment.GetEnvironmentVariable("CONDA_PREFIX");
                    if (!string.IsNullOrEmpty(condaPrefix))
                    {
                        runtimePath = Path.Combine(condaPrefix, "python.exe");
                        Debug.WriteLine($"[App] Using conda environment: {runtimePath}");
                        _loadingPage?.UpdateSubStatus("Using active conda environment");
                    }
                    else
                    {
                        var condaRoot = Environment.GetEnvironmentVariable("CONDA_ROOT")
                                     ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "miniconda3");
                        var geolensEnvPath = Path.Combine(condaRoot, "envs", "geolens", "python.exe");

                        if (File.Exists(geolensEnvPath))
                        {
                            runtimePath = geolensEnvPath;
                            Debug.WriteLine($"[App] Found geolens conda environment: {runtimePath}");
                            _loadingPage?.UpdateSubStatus("Using geolens conda environment");
                        }
                        else
                        {
                            runtimePath = "python";
                            Debug.WriteLine("[App] Using system Python (conda env not found)");
                            _loadingPage?.UpdateSubStatus("Using system Python");
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
                    _loadingPage?.UpdateSubStatus($"Using embedded runtime: {runtimeFolder}");

                    if (!File.Exists(runtimePath))
                    {
                        Debug.WriteLine($"[App] WARNING: Embedded runtime not found: {runtimePath}");
                        Debug.WriteLine("[App] Falling back to system Python");
                        runtimePath = "python";
                        _loadingPage?.UpdateSubStatus("Embedded runtime not found, using system Python");
                    }
                }

                await Task.Delay(100);

                // Stage 3: Start Python service (15-90% progress)
                _loadingPage?.UpdateStatus("Starting AI service...");
                _loadingPage?.UpdateProgress(15);
                _loadingPage?.UpdateSubStatus("This may take a few moments...");

                Debug.WriteLine($"[App] Starting Python service with runtime: {runtimePath}");
                PythonManager = new PythonRuntimeManager(runtimePath, port: 8899);

                // Start with progress updates
                var progressReporter = new Progress<int>(percentage =>
                {
                    _loadingPage?.UpdateProgress((int)(15 + (percentage * 0.75))); // Map 0-100 to 15-90
                    if (percentage < 30)
                        _loadingPage?.UpdateSubStatus("Launching Python process...");
                    else if (percentage < 70)
                        _loadingPage?.UpdateSubStatus("Waiting for service to respond...");
                    else
                        _loadingPage?.UpdateSubStatus("Verifying service health...");
                });

                bool serviceStarted = await PythonManager.StartAsync(
                    DetectedHardware.DeviceChoice,
                    progressReporter);

                if (!serviceStarted)
                {
                    Debug.WriteLine("[App] ERROR: Service failed to start or health check failed");

                    _loadingPage?.ShowError(
                        "Failed to start the AI service. Please check:\n\n" +
                        "• Python 3.11+ is installed (development mode)\n" +
                        "• Required packages are installed\n" +
                        "• Port 8899 is not already in use\n" +
                        "• No firewall blocking localhost:8899",
                        showRetry: true);

                    return false;
                }

                // Stage 4: Initialize API client (95% progress)
                _loadingPage?.UpdateStatus("Initializing API client...");
                _loadingPage?.UpdateProgress(95);
                _loadingPage?.UpdateSubStatus("");

                ApiClient = new GeoCLIPApiClient(PythonManager.BaseUrl);
                Debug.WriteLine("[App] Services initialized successfully");

                await Task.Delay(200);

                // Stage 5: Complete (100% progress)
                _loadingPage?.UpdateStatus("Ready!");
                _loadingPage?.UpdateProgress(100);
                await Task.Delay(300);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] ERROR initializing services: {ex.Message}");
                Debug.WriteLine($"[App] Stack trace: {ex.StackTrace}");

                _loadingPage?.ShowError(
                    $"Unexpected error during initialization:\n\n{ex.Message}\n\n" +
                    "Please check the debug output for details.",
                    showRetry: true);

                return false;
            }
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
