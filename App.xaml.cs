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
        public static HardwareDetectionService.HardwareInfo? DetectedHardware { get; private set; }

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
                DetectedHardware = HardwareDetectionService.DetectHardware();
                Debug.WriteLine($"[App] Detected: {DetectedHardware.Description}");

                // 2. Determine runtime path
                string appDir = AppContext.BaseDirectory;
                string runtimePath;

                // For development: use local Python
                if (Debugger.IsAttached)
                {
                    Debug.WriteLine("[App] Development mode - using local Python");
                    runtimePath = "python"; // Use system Python
                }
                else
                {
                    // For production: use embedded runtime
                    string runtimeFolder = DetectedHardware.Type switch
                    {
                        HardwareDetectionService.HardwareType.NvidiaGpu => "python_cuda",
                        HardwareDetectionService.HardwareType.AmdGpu => "python_rocm",
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

                // 3. Start Python service
                Debug.WriteLine($"[App] Starting Python service with runtime: {runtimePath}");
                PythonManager = new PythonRuntimeManager(runtimePath, port: 8899);
                await PythonManager.StartAsync(DetectedHardware.DeviceChoice);

                // 4. Wait for health check
                Debug.WriteLine("[App] Waiting for service health check...");
                bool healthy = await PythonManager.WaitForHealthyAsync(timeoutSeconds: 30);

                if (!healthy)
                {
                    Debug.WriteLine("[App] ERROR: Service health check failed");
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
            var dialog = new ContentDialog
            {
                Title = "Startup Error",
                Content = "Failed to start the AI service. Please ensure:\n\n" +
                          "• Python 3.11+ is installed (dev mode)\n" +
                          "• Required packages are installed\n" +
                          "• Port 8899 is not in use\n\n" +
                          "Check the debug output for details.",
                CloseButtonText = "Exit",
                XamlRoot = _mainWindow?.Content?.XamlRoot
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
