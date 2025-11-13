using GeoLens.Models;
using GeoLens.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GeoLens.Views
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isLoading = true;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;
            await LoadSettingsAsync();
            await UpdateCacheStatisticsAsync();
            UpdateHardwareInfoAsync();
            _isLoading = false;
        }

        /// <summary>
        /// Load settings from service and populate UI controls
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                var settings = await App.SettingsService.LoadSettingsAsync();

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
                var stats = await App.CacheService.GetCacheStatisticsAsync();

                // Update main cache info
                CacheInfoText.Text = $"{stats.TotalEntries} entries, {stats.DatabaseSizeFormatted}";

                // Update statistics flyout
                StatTotalEntriesText.Text = $"Total Entries: {stats.TotalEntries}";
                StatCacheHitsText.Text = $"Cache Hits: {stats.CacheHits}";
                StatCacheMissesText.Text = $"Cache Misses: {stats.CacheMisses}";
                StatHitRateText.Text = $"Hit Rate: {stats.HitRate:P1}";
                StatTotalSizeText.Text = $"Total Size: {stats.DatabaseSizeFormatted}";

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
        private void UpdateHardwareInfoAsync()
        {
            try
            {
                var settings = App.SettingsService.Settings;

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
                var settings = App.SettingsService.Settings;

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

                // Save with debouncing (500ms delay)
                await App.SettingsService.SaveSettingsAsync();

                Debug.WriteLine("[SettingsPage] Settings changed and saved");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] Error saving settings: {ex.Message}");
            }
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
                var settings = App.SettingsService.Settings;

                if (ThumbnailSizeSmallRadio.IsChecked == true)
                    settings.ThumbnailSize = ThumbnailSize.Small;
                else if (ThumbnailSizeMediumRadio.IsChecked == true)
                    settings.ThumbnailSize = ThumbnailSize.Medium;
                else if (ThumbnailSizeLargeRadio.IsChecked == true)
                    settings.ThumbnailSize = ThumbnailSize.Large;

                await App.SettingsService.SaveSettingsAsync();
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
                var settings = App.SettingsService.Settings;

                if (ThemeDarkRadio.IsChecked == true)
                    settings.Theme = AppTheme.Dark;
                else if (ThemeLightRadio.IsChecked == true)
                    settings.Theme = AppTheme.Light;
                else if (ThemeSystemRadio.IsChecked == true)
                    settings.Theme = AppTheme.System;

                await App.SettingsService.SaveSettingsAsync();
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
                    await App.CacheService.ClearAllAsync();

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
    }
}
