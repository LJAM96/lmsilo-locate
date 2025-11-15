using GeoLens.Models;
using GeoLens.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GeoLens.Views
{
    public sealed partial class SettingsPage : Page
    {
        // Injected services
        private readonly UserSettingsService _settingsService;
        private readonly PredictionCacheService _cacheService;
        private readonly AuditLogService _auditService;

        private bool _isLoading = true;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;

            // Get services from DI container
            _settingsService = App.Services.GetRequiredService<UserSettingsService>();
            _cacheService = App.Services.GetRequiredService<PredictionCacheService>();
            _auditService = App.Services.GetRequiredService<AuditLogService>();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            await LoadSettingsAsync();
            await UpdateCacheStatisticsAsync();
            await UpdateAuditStatisticsAsync();
            UpdateHardwareInfo();
            _isLoading = false;
        }

        /// <summary>
        /// Load settings from service and populate UI controls
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();

                // Cache Settings
                EnableCacheToggle.IsOn = settings.EnableCache;
                CacheExpirationCombo.SelectedIndex = settings.CacheExpirationDays switch
                {
                    7 => 0,
                    30 => 1,
                    90 => 2,
                    _ => 3 // 0 (never)
                };

                // Prediction Settings
                ExifGpsFirstToggle.IsOn = settings.ShowExifGpsFirst;
                ClusteringToggle.IsOn = settings.EnableClustering;

                // Map Settings
                OfflineModeToggle.IsOn = settings.OfflineMode;

                // Interface Settings
                ShowThumbnailsToggle.IsOn = settings.ShowThumbnails;
                ShowSkeletonLoadersToggle.IsOn = settings.ShowSkeletonLoaders;

                // Thumbnail Size
                ThumbnailSizeSmallRadio.IsChecked = settings.ThumbnailSize == ThumbnailSize.Small;
                ThumbnailSizeMediumRadio.IsChecked = settings.ThumbnailSize == ThumbnailSize.Medium;
                ThumbnailSizeLargeRadio.IsChecked = settings.ThumbnailSize == ThumbnailSize.Large;

                // Theme
                ThemeDarkRadio.IsChecked = settings.Theme == AppTheme.Dark;
                ThemeLightRadio.IsChecked = settings.Theme == AppTheme.Light;
                ThemeSystemRadio.IsChecked = settings.Theme == AppTheme.System;

                Debug.WriteLine("[SettingsPage] Settings loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error loading settings: {ex.Message}");
                await ShowErrorDialog("Failed to load settings", ex.Message);
            }
        }

        /// <summary>
        /// Update cache statistics display
        /// </summary>
        private async Task UpdateCacheStatisticsAsync()
        {
            try
            {
                var stats = await _cacheService.GetCacheStatisticsAsync();

                // Update main cache info
                CacheInfoText.Text = $"{stats.TotalEntries} entries, {stats.DatabaseSizeFormatted}";

                // Update statistics flyout
                StatTotalEntriesText.Text = $"Total Entries: {stats.TotalEntries}";
                StatCacheHitsText.Text = $"Cache Hits: {stats.CacheHits}";
                StatCacheMissesText.Text = $"Cache Misses: {stats.CacheMisses}";
                StatHitRateText.Text = $"Hit Rate: {stats.HitRate:P1}";
                StatTotalSizeText.Text = $"Total Size: {stats.DatabaseSizeFormatted}";

                // New enhanced statistics
                StatAverageSizeText.Text = $"Average Entry Size: {stats.AverageEntrySizeFormatted}";
                StatOldestEntryText.Text = $"Oldest Entry: {(stats.OldestEntryDate?.ToLocalTime().ToString("g") ?? "N/A")}";
                StatNewestEntryText.Text = $"Newest Entry: {(stats.NewestAccessDate?.ToLocalTime().ToString("g") ?? "N/A")}";

                Debug.WriteLine($"[SettingsPage] Cache statistics updated: {stats.TotalEntries} entries");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error updating cache statistics: {ex.Message}");
                CacheInfoText.Text = "Error loading cache stats";
            }
        }

        /// <summary>
        /// Update hardware information display
        /// </summary>
        private void UpdateHardwareInfo()
        {
            try
            {
                var settings = _settingsService.Settings;

                if (!string.IsNullOrEmpty(settings.DetectedGpu))
                {
                    DetectedGpuText.Text = settings.DetectedGpu;
                }
                else if (App.DetectedHardware != null)
                {
                    DetectedGpuText.Text = App.DetectedHardware.Description;
                }

                if (!string.IsNullOrEmpty(settings.UsingRuntime))
                {
                    UsingRuntimeText.Text = settings.UsingRuntime;
                }
                else if (App.DetectedHardware != null)
                {
                    var runtimeName = App.DetectedHardware.Type switch
                    {
                        HardwareType.NvidiaGpu => "CUDA (python_cuda)",
                        HardwareType.AmdGpu => "ROCm (python_rocm)",
                        _ => "CPU (python_cpu)"
                    };
                    UsingRuntimeText.Text = runtimeName;
                }

                // Check if Python service is running
                if (App.PythonManager != null && App.PythonManager.IsRunning)
                {
                    ModelStatusText.Text = "Running";
                }
                else
                {
                    ModelStatusText.Text = "Not loaded";
                }

                Debug.WriteLine("[SettingsPage] Hardware information updated");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error updating hardware info: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle setting changes with debouncing
        /// </summary>
        private async void OnSettingChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
                return;

            try
            {
                var settings = _settingsService.Settings;

                // Cache Settings
                settings.EnableCache = EnableCacheToggle.IsOn;
                settings.CacheExpirationDays = CacheExpirationCombo.SelectedIndex switch
                {
                    0 => 7,
                    1 => 30,
                    2 => 90,
                    _ => 0 // Never
                };

                // Prediction Settings
                settings.ShowExifGpsFirst = ExifGpsFirstToggle.IsOn;
                settings.EnableClustering = ClusteringToggle.IsOn;

                // Map Settings
                settings.OfflineMode = OfflineModeToggle.IsOn;

                // Interface Settings
                settings.ShowThumbnails = ShowThumbnailsToggle.IsOn;
                settings.ShowSkeletonLoaders = ShowSkeletonLoadersToggle.IsOn;

                // Validate settings before saving
                if (!ValidateCacheSettings(settings))
                {
                    CacheValidationMessage.Visibility = Visibility.Visible;
                    await ShowErrorDialog("Invalid Settings",
                        "Cache expiration must be between 1 and 365 days, or set to Never.");
                    await LoadSettingsAsync(); // Revert to previous values
                    return;
                }
                else
                {
                    CacheValidationMessage.Visibility = Visibility.Collapsed;
                }

                // Save with debouncing (500ms delay)
                await _settingsService.SaveSettingsAsync();

                Debug.WriteLine("[SettingsPage] Settings changed and saved");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error saving settings: {ex.Message}");
                await ShowErrorDialog("Save Failed", $"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate cache settings
        /// </summary>
        private bool ValidateCacheSettings(UserSettings settings)
        {
            // Validate cache expiration days
            // Valid values: 0 (Never), or 1-365 days
            if (settings.CacheExpirationDays < 0 || settings.CacheExpirationDays > 365)
            {
                if (settings.CacheExpirationDays != 0) // 0 = Never
                    return false;
            }

            // Add more validation as needed
            return true;
        }

        /// <summary>
        /// Handle thumbnail size radio button changes
        /// </summary>
        private async void ThumbnailSize_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
                return;

            try
            {
                var settings = _settingsService.Settings;

                if (ThumbnailSizeSmallRadio.IsChecked == true)
                    settings.ThumbnailSize = ThumbnailSize.Small;
                else if (ThumbnailSizeMediumRadio.IsChecked == true)
                    settings.ThumbnailSize = ThumbnailSize.Medium;
                else if (ThumbnailSizeLargeRadio.IsChecked == true)
                    settings.ThumbnailSize = ThumbnailSize.Large;

                await _settingsService.SaveSettingsAsync();
                Debug.WriteLine($"[SettingsPage] Thumbnail size changed to: {settings.ThumbnailSize}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error saving thumbnail size: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle app theme radio button changes
        /// </summary>
        private async void AppTheme_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
                return;

            try
            {
                var settings = _settingsService.Settings;

                if (ThemeDarkRadio.IsChecked == true)
                    settings.Theme = AppTheme.Dark;
                else if (ThemeLightRadio.IsChecked == true)
                    settings.Theme = AppTheme.Light;
                else if (ThemeSystemRadio.IsChecked == true)
                    settings.Theme = AppTheme.System;

                await _settingsService.SaveSettingsAsync();
                Debug.WriteLine($"[SettingsPage] Theme changed to: {settings.Theme}");

                // Note: Actual theme change would require app restart or manual theme switching
                await ShowInfoDialog(
                    "Theme Change",
                    "Theme preferences saved. Restart the application to apply the new theme."
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error saving theme: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle clear cache button click
        /// </summary>
        private async void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation dialog
                var dialog = new ContentDialog
                {
                    Title = "Clear Cache",
                    Content = "Are you sure you want to clear all cached predictions? This action cannot be undone.",
                    PrimaryButtonText = "Clear Cache",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // Clear the cache
                    await _cacheService.ClearAllAsync();

                    // Update statistics display
                    await UpdateCacheStatisticsAsync();

                    Debug.WriteLine("[SettingsPage] Cache cleared successfully");

                    await ShowInfoDialog("Success", "Cache cleared successfully.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error clearing cache: {ex.Message}");
                await ShowErrorDialog("Error", $"Failed to clear cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Show an error dialog
        /// </summary>
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

        /// <summary>
        /// Show an info dialog
        /// </summary>
        private async Task ShowInfoDialog(string title, string message)
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

        /// <summary>
        /// Update audit log statistics display
        /// </summary>
        private async Task UpdateAuditStatisticsAsync()
        {
            try
            {
                var totalCount = await _auditService.GetTotalCountAsync();
                var oldestEntry = await _auditService.GetOldestEntryDateAsync();

                AuditCountText.Text = totalCount.ToString();
                AuditOldestEntryText.Text = oldestEntry?.ToLocalTime().ToString("g") ?? "N/A";
                AuditCurrentUserText.Text = Environment.UserName;

                Debug.WriteLine($"[SettingsPage] Audit statistics updated: {totalCount} entries");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error updating audit statistics: {ex.Message}");
                AuditCountText.Text = "Error";
                AuditOldestEntryText.Text = "Error";
            }
        }

        /// <summary>
        /// Export audit log as CSV
        /// </summary>
        private async void ExportAuditCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("CSV File", new[] { ".csv" });
                savePicker.SuggestedFileName = $"GeoLens_Audit_{DateTime.Now:yyyyMMdd_HHmmss}";

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    await _auditService.ExportToCsvAsync(file.Path);
                    await ShowInfoDialog("Export Successful", $"Audit log exported to:\n{file.Path}");
                    Debug.WriteLine($"[SettingsPage] Audit log exported to CSV: {file.Path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error exporting audit log to CSV: {ex.Message}");
                await ShowErrorDialog("Export Failed", $"Failed to export audit log:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Export audit log as JSON
        /// </summary>
        private async void ExportAuditJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("JSON File", new[] { ".json" });
                savePicker.SuggestedFileName = $"GeoLens_Audit_{DateTime.Now:yyyyMMdd_HHmmss}";

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    await _auditService.ExportToJsonAsync(file.Path);
                    await ShowInfoDialog("Export Successful", $"Audit log exported to:\n{file.Path}");
                    Debug.WriteLine($"[SettingsPage] Audit log exported to JSON: {file.Path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error exporting audit log to JSON: {ex.Message}");
                await ShowErrorDialog("Export Failed", $"Failed to export audit log:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Export audit log as PDF
        /// </summary>
        private async void ExportAuditPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("PDF File", new[] { ".pdf" });
                savePicker.SuggestedFileName = $"GeoLens_Audit_{DateTime.Now:yyyyMMdd_HHmmss}";

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    await _auditService.ExportToPdfAsync(file.Path);
                    await ShowInfoDialog("Export Successful", $"Audit log exported to:\n{file.Path}");
                    Debug.WriteLine($"[SettingsPage] Audit log exported to PDF: {file.Path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error exporting audit log to PDF: {ex.Message}");
                await ShowErrorDialog("Export Failed", $"Failed to export audit log:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Clear all audit log entries
        /// </summary>
        private async void ClearAuditLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation dialog with warning
                var dialog = new ContentDialog
                {
                    Title = "Clear Audit Log",
                    Content = "⚠️ WARNING ⚠️\n\n" +
                             "This will permanently delete ALL audit log entries.\n\n" +
                             "This action cannot be undone and may violate compliance requirements.\n\n" +
                             "Are you sure you want to proceed?",
                    PrimaryButtonText = "Delete All Entries",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    // Clear the audit log
                    await _auditService.ClearAllEntriesAsync();

                    // Update statistics display
                    await UpdateAuditStatisticsAsync();

                    Debug.WriteLine("[SettingsPage] Audit log cleared successfully");

                    await ShowInfoDialog("Success", "Audit log cleared successfully.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error clearing audit log: {ex.Message}");
                await ShowErrorDialog("Error", $"Failed to clear audit log:\n{ex.Message}");
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe events
            Loaded -= Page_Loaded;
            Unloaded -= Page_Unloaded;

            Debug.WriteLine("[SettingsPage] Page cleanup complete");
        }
    }
}
