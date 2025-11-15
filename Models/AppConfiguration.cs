namespace GeoLens.Models;

/// <summary>
/// Root configuration object for GeoLens application settings
/// </summary>
public class AppConfiguration
{
    public GeoLensConfig GeoLens { get; set; } = new();
}

/// <summary>
/// Main GeoLens configuration container
/// </summary>
public class GeoLensConfig
{
    public ApiConfig Api { get; set; } = new();
    public CacheConfig Cache { get; set; } = new();
    public AuditConfig Audit { get; set; } = new();
    public UIConfig UI { get; set; } = new();
    public ProcessingConfig Processing { get; set; } = new();
}

/// <summary>
/// API service configuration (Python FastAPI communication)
/// </summary>
public class ApiConfig
{
    /// <summary>
    /// Port number for the Python FastAPI service
    /// </summary>
    public int Port { get; set; } = 8899;

    /// <summary>
    /// Base URL for API requests (http://localhost:{Port})
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8899";

    /// <summary>
    /// Health check endpoint path
    /// </summary>
    public string HealthCheckEndpoint { get; set; } = "/health";

    /// <summary>
    /// Inference endpoint path
    /// </summary>
    public string InferEndpoint { get; set; } = "/infer";

    /// <summary>
    /// Default number of top predictions to return
    /// </summary>
    public int DefaultTopK { get; set; } = 5;

    /// <summary>
    /// Request timeout in seconds for API calls
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Health check timeout in seconds
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 2;

    /// <summary>
    /// Service startup timeout in seconds
    /// </summary>
    public int StartupTimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// Prediction cache configuration
/// </summary>
public class CacheConfig
{
    /// <summary>
    /// Default cache expiration time in days
    /// </summary>
    public int DefaultExpirationDays { get; set; } = 30;

    /// <summary>
    /// Maximum cache size in megabytes
    /// </summary>
    public int MaxSizeMB { get; set; } = 500;

    /// <summary>
    /// Enable in-memory caching for hot data
    /// </summary>
    public bool EnableMemoryCache { get; set; } = true;

    /// <summary>
    /// Maximum entries in memory cache
    /// </summary>
    public int MemoryCacheMaxEntries { get; set; } = 1000;
}

/// <summary>
/// Audit logging configuration
/// </summary>
public class AuditConfig
{
    /// <summary>
    /// Number of entries before showing a warning to the user
    /// </summary>
    public int MaxEntriesBeforeWarning { get; set; } = 10000;

    /// <summary>
    /// Retention period for audit logs in days
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Enable/disable audit logging
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;
}

/// <summary>
/// UI configuration
/// </summary>
public class UIConfig
{
    /// <summary>
    /// Default thumbnail size in pixels
    /// </summary>
    public int DefaultThumbnailSize { get; set; } = 72;

    /// <summary>
    /// Maximum number of recent files to track
    /// </summary>
    public int MaxRecentFiles { get; set; } = 10;

    /// <summary>
    /// Default application theme (Dark/Light)
    /// </summary>
    public string DefaultTheme { get; set; } = "Dark";
}

/// <summary>
/// Image processing configuration
/// </summary>
public class ProcessingConfig
{
    /// <summary>
    /// Maximum number of images to process concurrently
    /// </summary>
    public int MaxConcurrentImages { get; set; } = 5;

    /// <summary>
    /// Enable geographic clustering analysis
    /// </summary>
    public bool EnableClustering { get; set; } = true;

    /// <summary>
    /// Cluster radius in kilometers for geographic grouping
    /// </summary>
    public int ClusterRadiusKm { get; set; } = 100;

    /// <summary>
    /// Confidence boost percentage for clustered predictions (0-100)
    /// </summary>
    public int ClusterBoostPercent { get; set; } = 15;

    /// <summary>
    /// Minimum number of predictions required to form a cluster
    /// </summary>
    public int MinimumClusterSize { get; set; } = 2;
}
