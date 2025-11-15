using GeoLens.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GeoLens.Services
{
    /// <summary>
    /// Service for managing user settings persistence with JSON storage
    /// </summary>
    public class UserSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GeoLens",
            "settings.json");

        private UserSettings _settings;
        private readonly SemaphoreSlim _saveLock;
        private CancellationTokenSource? _debounceCts;
        private readonly int _debounceDelayMs = 500;

        public UserSettings Settings => _settings;

        public event EventHandler<UserSettings>? SettingsChanged;

        public UserSettingsService()
        {
            _settings = new UserSettings();
            _saveLock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Load settings from JSON file, create default if not exists
        /// </summary>
        public async Task<UserSettings> LoadSettingsAsync()
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Load from file if exists
                if (File.Exists(SettingsPath))
                {
                    var json = await File.ReadAllTextAsync(SettingsPath);
                    var loadedSettings = JsonSerializer.Deserialize<UserSettings>(json);

                    if (loadedSettings != null)
                    {
                        _settings = loadedSettings;
                        Debug.WriteLine($"[UserSettingsService] Settings loaded from: {SettingsPath}");
                    }
                    else
                    {
                        Debug.WriteLine("[UserSettingsService] Failed to deserialize settings, using defaults");
                        _settings = new UserSettings();
                    }
                }
                else
                {
                    Debug.WriteLine("[UserSettingsService] No settings file found, using defaults");
                    _settings = new UserSettings();
                    // Save defaults to create the file
                    await SaveSettingsImmediateAsync();
                }

                return _settings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserSettingsService] Error loading settings: {ex.Message}");
                _settings = new UserSettings();
                return _settings;
            }
        }

        /// <summary>
        /// Save settings to JSON with debouncing
        /// </summary>
        public async Task SaveSettingsAsync()
        {
            // Cancel any pending save
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();

            try
            {
                // Wait for debounce delay
                await Task.Delay(_debounceDelayMs, _debounceCts.Token);

                // Perform the actual save
                await SaveSettingsImmediateAsync();
            }
            catch (TaskCanceledException)
            {
                // Debounce was cancelled, another save is pending
                Debug.WriteLine("[UserSettingsService] Save debounced");
            }
        }

        /// <summary>
        /// Save settings immediately without debouncing
        /// </summary>
        public async Task SaveSettingsImmediateAsync()
        {
            await _saveLock.WaitAsync();
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Serialize with pretty formatting for readability
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_settings, options);
                await File.WriteAllTextAsync(SettingsPath, json);

                Debug.WriteLine($"[UserSettingsService] Settings saved to: {SettingsPath}");

                // Notify listeners
                SettingsChanged?.Invoke(this, _settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserSettingsService] Error saving settings: {ex.Message}");
                throw;
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// Update hardware information in settings
        /// </summary>
        public void UpdateHardwareInfo(string? detectedGpu, string? usingRuntime)
        {
            _settings.DetectedGpu = detectedGpu;
            _settings.UsingRuntime = usingRuntime;
        }

        /// <summary>
        /// Reset settings to default values
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            _settings = new UserSettings();
            await SaveSettingsImmediateAsync();
            Debug.WriteLine("[UserSettingsService] Settings reset to defaults");
        }
    }
}
