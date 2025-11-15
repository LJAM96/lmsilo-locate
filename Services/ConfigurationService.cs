using GeoLens.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;

namespace GeoLens.Services;

/// <summary>
/// Singleton service for managing application configuration from appsettings.json
/// </summary>
public class ConfigurationService
{
    private static ConfigurationService? _instance;
    private static readonly object _lock = new();
    private readonly IConfiguration _configuration;
    private readonly AppConfiguration _appConfig;

    /// <summary>
    /// Get the singleton instance of ConfigurationService
    /// </summary>
    public static ConfigurationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ConfigurationService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Get the application configuration
    /// </summary>
    public AppConfiguration Config => _appConfig;

    /// <summary>
    /// Private constructor to enforce singleton pattern
    /// </summary>
    private ConfigurationService()
    {
        try
        {
            // Build configuration from appsettings.json
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
            _appConfig = new AppConfiguration();
            _configuration.Bind(_appConfig);

            Debug.WriteLine("[ConfigurationService] Configuration loaded successfully");
            LogConfigurationValues();
        }
        catch (FileNotFoundException ex)
        {
            Debug.WriteLine($"[ConfigurationService] ERROR: appsettings.json not found: {ex.Message}");
            Debug.WriteLine($"[ConfigurationService] Using default configuration values");

            // Use default values if configuration file is missing
            _configuration = new ConfigurationBuilder().Build();
            _appConfig = new AppConfiguration();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConfigurationService] ERROR: Failed to load configuration: {ex.Message}");
            Debug.WriteLine($"[ConfigurationService] Using default configuration values");

            // Use default values if configuration loading fails
            _configuration = new ConfigurationBuilder().Build();
            _appConfig = new AppConfiguration();
        }
    }

    /// <summary>
    /// Get a specific configuration section
    /// </summary>
    /// <typeparam name="T">Type of configuration section</typeparam>
    /// <param name="sectionName">Name of the configuration section</param>
    /// <returns>Configuration section object</returns>
    public T GetSection<T>(string sectionName) where T : new()
    {
        var section = new T();
        _configuration.GetSection(sectionName).Bind(section);
        return section;
    }

    /// <summary>
    /// Log current configuration values for debugging
    /// </summary>
    private void LogConfigurationValues()
    {
        Debug.WriteLine("[ConfigurationService] Current Configuration:");
        Debug.WriteLine($"  API Port: {_appConfig.GeoLens.Api.Port}");
        Debug.WriteLine($"  API Timeout: {_appConfig.GeoLens.Api.RequestTimeoutSeconds}s");
        Debug.WriteLine($"  Cache Expiration: {_appConfig.GeoLens.Cache.DefaultExpirationDays} days");
        Debug.WriteLine($"  Clustering Enabled: {_appConfig.GeoLens.Processing.EnableClustering}");
        Debug.WriteLine($"  Cluster Radius: {_appConfig.GeoLens.Processing.ClusterRadiusKm} km");
        Debug.WriteLine($"  Audit Logging: {_appConfig.GeoLens.Audit.EnableAuditLogging}");
    }

    /// <summary>
    /// Reload configuration from file (useful after manual edits)
    /// </summary>
    public void ReloadConfiguration()
    {
        try
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var newConfig = builder.Build();
            var newAppConfig = new AppConfiguration();
            newConfig.Bind(newAppConfig);

            // Update internal configuration
            typeof(ConfigurationService)
                .GetField("_configuration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .SetValue(this, newConfig);

            typeof(ConfigurationService)
                .GetField("_appConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .SetValue(this, newAppConfig);

            Debug.WriteLine("[ConfigurationService] Configuration reloaded successfully");
            LogConfigurationValues();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConfigurationService] ERROR: Failed to reload configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the path to the appsettings.json file
    /// </summary>
    public string GetConfigurationFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    /// <summary>
    /// Check if configuration file exists
    /// </summary>
    public bool ConfigurationFileExists()
    {
        return File.Exists(GetConfigurationFilePath());
    }
}
