using GeoLens.Services;
using GeoLens.Services.MapProviders;
using GeoLens.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
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

        // Dependency Injection Container
        public static IServiceProvider Services { get; private set; } = null!;

        // Legacy static properties (deprecated - use Services.GetRequiredService<T>() instead)
        [Obsolete("Use Services.GetRequiredService<PythonRuntimeManager>() instead")]
        public static PythonRuntimeManager? PythonManager { get; private set; }

        [Obsolete("Use Services.GetRequiredService<GeoCLIPApiClient>() instead")]
        public static GeoCLIPApiClient? ApiClient { get; private set; }

        public static HardwareInfo? DetectedHardware { get; private set; }

        [Obsolete("Use Services.GetRequiredService<UserSettingsService>() instead")]
        public static UserSettingsService SettingsService { get; private set; } = null!;

        [Obsolete("Use Services.GetRequiredService<PredictionCacheService>() instead")]
        public static PredictionCacheService CacheService { get; private set; } = null!;

        [Obsolete("Use Services.GetRequiredService<AuditLogService>() instead")]
        public static AuditLogService AuditService { get; private set; } = null!;

        [Obsolete("Use Services.GetRequiredService<RecentFilesService>() instead")]
        public static RecentFilesService RecentFilesService { get; private set; } = null!;

        public App()
        {
            InitializeComponent();

            // Initialize logging FIRST
            LoggingService.Initialize();
            Log.Information("GeoLens application starting");

            // Configure dependency injection
            ConfigureServices();

            // Initialize services from DI container
            SettingsService = Services.GetRequiredService<UserSettingsService>();
            CacheService = Services.GetRequiredService<PredictionCacheService>();
            AuditService = Services.GetRequiredService<AuditLogService>();
            RecentFilesService = Services.GetRequiredService<RecentFilesService>();

            // Register for application exit to dispose services
            this.UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        /// <summary>
        /// Configure dependency injection services
        /// </summary>
        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core singleton services (application lifetime)
            services.AddSingleton<UserSettingsService>();
            services.AddSingleton<PredictionCacheService>();
            services.AddSingleton<AuditLogService>();
            services.AddSingleton<RecentFilesService>();
            services.AddSingleton<ThumbnailCacheService>();
            services.AddSingleton<MapTileCacheService>();
            services.AddSingleton<ConfigurationService>(sp => ConfigurationService.Instance);
            services.AddSingleton<CommandManager>();

            // Processing services (transient - new instance per request)
            services.AddTransient<ExifMetadataExtractor>();
            services.AddTransient<GeographicClusterAnalyzer>();
            services.AddTransient<ExportService>();
            services.AddTransient<PredictionHeatmapGenerator>();

            // Map providers
            services.AddTransient<IMapProvider, LeafletMapProvider>();

            // PredictionProcessor requires dependencies (transient)
            services.AddTransient<PredictionProcessor>(sp =>
                new PredictionProcessor(
                    sp.GetRequiredService<PredictionCacheService>(),
                    sp.GetRequiredService<ExifMetadataExtractor>(),
                    sp.GetRequiredService<GeoCLIPApiClient>()
                )
            );

            // Runtime services will be registered after initialization
            // These are set to null initially and populated during startup
            services.AddSingleton<PythonRuntimeManager>(sp => PythonManager!);
            services.AddSingleton<GeoCLIPApiClient>(sp => ApiClient!);

            // Build the service provider
            Services = services.BuildServiceProvider();

            Log.Information("Dependency injection container configured");
        }

        /// <summary>
        /// Rebuild the DI container after runtime services (PythonManager, ApiClient) are initialized
        /// </summary>
        private void RebuildServicesWithRuntimeDependencies()
        {
            // Dispose old service provider
            if (Services is IDisposable oldProvider)
            {
                oldProvider.Dispose();
            }

            var services = new ServiceCollection();

            // Core singleton services (application lifetime)
            services.AddSingleton(SettingsService);
            services.AddSingleton(CacheService);
            services.AddSingleton(AuditService);
            services.AddSingleton(RecentFilesService);
            services.AddSingleton(Services.GetRequiredService<ThumbnailCacheService>());
            services.AddSingleton(Services.GetRequiredService<MapTileCacheService>());
            services.AddSingleton<ConfigurationService>(sp => ConfigurationService.Instance);

            // Processing services (transient - new instance per request)
            services.AddTransient<ExifMetadataExtractor>();
            services.AddTransient<GeographicClusterAnalyzer>();
            services.AddTransient<ExportService>();
            services.AddTransient<PredictionHeatmapGenerator>();

            // Map providers
            services.AddTransient<IMapProvider, LeafletMapProvider>();

            // Runtime services (now initialized)
            services.AddSingleton(PythonManager!);
            services.AddSingleton(ApiClient!);

            // PredictionProcessor requires dependencies (transient)
            services.AddTransient<PredictionProcessor>();

            // Build the new service provider
            Services = services.BuildServiceProvider();

            Log.Information("Dependency injection container rebuilt with runtime dependencies");
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

                // Dispose DI container
                if (Services is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                Log.Information("Services disposed successfully");
                LoggingService.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error disposing services");
                LoggingService.Shutdown();
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
                Log.Information("Settings and cache initialized");

                // Stage 1: Detect hardware (5% progress)
                _loadingPage?.UpdateStatus("Detecting hardware...");
                _loadingPage?.UpdateProgress(5);
                await Task.Delay(50); // Small delay to ensure UI updates

                Log.Information("Detecting hardware...");
                var hardwareService = new HardwareDetectionService();
                DetectedHardware = hardwareService.DetectHardware();
                Log.Information("Hardware detected: {HardwareDescription}", DetectedHardware.Description);

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
                    Log.Information("Development mode - checking for conda environment");

                    var condaPrefix = Environment.GetEnvironmentVariable("CONDA_PREFIX");
                    if (!string.IsNullOrEmpty(condaPrefix))
                    {
                        runtimePath = Path.Combine(condaPrefix, "python.exe");
                        Log.Information("Using conda environment: {RuntimePath}", runtimePath);
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
                            Log.Information("Found geolens conda environment: {RuntimePath}", runtimePath);
                            _loadingPage?.UpdateSubStatus("Using geolens conda environment");
                        }
                        else
                        {
                            runtimePath = "python";
                            Log.Information("Using system Python (conda env not found)");
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
                        Log.Warning("Embedded runtime not found: {RuntimePath}", runtimePath);
                        Log.Information("Falling back to system Python");
                        runtimePath = "python";
                        _loadingPage?.UpdateSubStatus("Embedded runtime not found, using system Python");
                    }
                }

                await Task.Delay(100);

                // Stage 3: Start Python service (15-90% progress)
                _loadingPage?.UpdateStatus("Starting AI service...");
                _loadingPage?.UpdateProgress(15);
                _loadingPage?.UpdateSubStatus("This may take a few moments...");

                Log.Information("Starting Python service with runtime: {RuntimePath}", runtimePath);
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
                    Log.Error("Service failed to start or health check failed");

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

                // Rebuild DI container now that runtime services are available
                RebuildServicesWithRuntimeDependencies();

                Log.Information("Services initialized successfully");

                await Task.Delay(200);

                // Stage 5: Complete (100% progress)
                _loadingPage?.UpdateStatus("Ready!");
                _loadingPage?.UpdateProgress(100);
                await Task.Delay(300);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing services");

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
            Log.Error(e.Exception, "Navigation failed to {PageType}", e.SourcePageType.FullName);
            e.Handled = true;
        }
    }
}
