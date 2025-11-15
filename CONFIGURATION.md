# GeoLens Configuration Guide

This document describes all configurable settings available in `appsettings.json`.

## Configuration File Location

The configuration file `appsettings.json` is located in the application root directory and is automatically copied to the output directory during build.

**File Path:** `appsettings.json`

## Configuration Structure

The configuration follows a hierarchical structure under the `GeoLens` root object:

```json
{
  "GeoLens": {
    "Api": { ... },
    "Cache": { ... },
    "Audit": { ... },
    "UI": { ... },
    "Processing": { ... }
  }
}
```

## Configuration Sections

### API Configuration (`GeoLens.Api`)

Controls the Python FastAPI service communication settings.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Port` | int | 8899 | Port number for the Python FastAPI service |
| `BaseUrl` | string | "http://localhost:8899" | Base URL for API requests |
| `HealthCheckEndpoint` | string | "/health" | Health check endpoint path |
| `InferEndpoint` | string | "/infer" | Inference endpoint path |
| `DefaultTopK` | int | 5 | Default number of top predictions to return |
| `RequestTimeoutSeconds` | int | 120 | Request timeout in seconds for API calls (2 minutes) |
| `HealthCheckTimeoutSeconds` | int | 2 | Health check timeout in seconds |
| `StartupTimeoutSeconds` | int | 15 | Service startup timeout in seconds |

**Example:**
```json
"Api": {
  "Port": 8899,
  "BaseUrl": "http://localhost:8899",
  "HealthCheckEndpoint": "/health",
  "InferEndpoint": "/infer",
  "DefaultTopK": 5,
  "RequestTimeoutSeconds": 120,
  "HealthCheckTimeoutSeconds": 2,
  "StartupTimeoutSeconds": 15
}
```

**Usage in Code:**
```csharp
var config = ConfigurationService.Instance.Config.GeoLens.Api;
var port = config.Port;
var timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);
```

### Cache Configuration (`GeoLens.Cache`)

Controls prediction caching behavior and SQLite database settings.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `DefaultExpirationDays` | int | 30 | Default cache expiration time in days |
| `MaxSizeMB` | int | 500 | Maximum cache size in megabytes (not enforced yet) |
| `EnableMemoryCache` | bool | true | Enable in-memory caching for hot data |
| `MemoryCacheMaxEntries` | int | 1000 | Maximum entries in memory cache (not enforced yet) |

**Example:**
```json
"Cache": {
  "DefaultExpirationDays": 30,
  "MaxSizeMB": 500,
  "EnableMemoryCache": true,
  "MemoryCacheMaxEntries": 1000
}
```

**Usage in Code:**
```csharp
var config = ConfigurationService.Instance.Config.GeoLens.Cache;
await cacheService.ClearExpiredAsync(config.DefaultExpirationDays);
```

**Notes:**
- `MaxSizeMB` and `MemoryCacheMaxEntries` are currently placeholders for future implementation
- Cache is stored in: `%LocalAppData%\GeoLens\cache.db`

### Audit Configuration (`GeoLens.Audit`)

Controls audit logging behavior for compliance tracking.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MaxEntriesBeforeWarning` | int | 10000 | Number of entries before showing a warning to the user |
| `RetentionDays` | int | 90 | Retention period for audit logs in days |
| `EnableAuditLogging` | bool | true | Enable/disable audit logging |

**Example:**
```json
"Audit": {
  "MaxEntriesBeforeWarning": 10000,
  "RetentionDays": 90,
  "EnableAuditLogging": true
}
```

**Usage in Code:**
```csharp
var config = ConfigurationService.Instance.Config.GeoLens.Audit;
if (config.EnableAuditLogging)
{
    await auditService.LogProcessingOperationAsync(entry);
}
```

**Notes:**
- Audit logs are stored in: `%LocalAppData%\GeoLens\audit.db`
- Set `EnableAuditLogging` to `false` to disable audit logging entirely

### UI Configuration (`GeoLens.UI`)

Controls user interface settings and preferences.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `DefaultThumbnailSize` | int | 72 | Default thumbnail size in pixels |
| `MaxRecentFiles` | int | 10 | Maximum number of recent files to track |
| `DefaultTheme` | string | "Dark" | Default application theme (Dark/Light) |

**Example:**
```json
"UI": {
  "DefaultThumbnailSize": 72,
  "MaxRecentFiles": 10,
  "DefaultTheme": "Dark"
}
```

**Usage in Code:**
```csharp
var config = ConfigurationService.Instance.Config.GeoLens.UI;
var thumbnailSize = config.DefaultThumbnailSize;
```

**Notes:**
- `DefaultTheme` currently supports "Dark" and "Light"
- UI settings may be overridden by user preferences stored in `UserSettings.json`

### Processing Configuration (`GeoLens.Processing`)

Controls image processing and clustering behavior.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MaxConcurrentImages` | int | 5 | Maximum number of images to process concurrently |
| `EnableClustering` | bool | true | Enable geographic clustering analysis |
| `ClusterRadiusKm` | int | 100 | Cluster radius in kilometers for geographic grouping |
| `ClusterBoostPercent` | int | 15 | Confidence boost percentage for clustered predictions (0-100) |
| `MinimumClusterSize` | int | 2 | Minimum number of predictions required to form a cluster |

**Example:**
```json
"Processing": {
  "MaxConcurrentImages": 5,
  "EnableClustering": true,
  "ClusterRadiusKm": 100,
  "ClusterBoostPercent": 15,
  "MinimumClusterSize": 2
}
```

**Usage in Code:**
```csharp
var config = ConfigurationService.Instance.Config.GeoLens.Processing;
var analyzer = new GeographicClusterAnalyzer(); // Uses config internally
```

**Notes:**
- `ClusterBoostPercent` is converted to a decimal (15% → 0.15) internally
- `ClusterRadiusKm` of 100km is recommended for most use cases
- `MinimumClusterSize` of 2 ensures at least two predictions must be near each other

## Configuration Service Usage

### Accessing Configuration

The `ConfigurationService` is a singleton that provides access to all configuration settings:

```csharp
using GeoLens.Services;

// Access configuration through singleton instance
var config = ConfigurationService.Instance.Config;

// Access specific sections
var apiConfig = config.GeoLens.Api;
var cacheConfig = config.GeoLens.Cache;
var auditConfig = config.GeoLens.Audit;
var uiConfig = config.GeoLens.UI;
var processingConfig = config.GeoLens.Processing;
```

### Reloading Configuration

To reload configuration after manual edits:

```csharp
ConfigurationService.Instance.ReloadConfiguration();
```

### Checking Configuration File

To verify the configuration file exists:

```csharp
if (ConfigurationService.Instance.ConfigurationFileExists())
{
    var path = ConfigurationService.Instance.GetConfigurationFilePath();
    Console.WriteLine($"Configuration loaded from: {path}");
}
```

## Configuration Priority

1. **appsettings.json** - Primary configuration source
2. **Default values** - Used if configuration file is missing or invalid
3. **Code defaults** - Fallback values defined in model classes

## Error Handling

If `appsettings.json` is missing or invalid:
- The application will log an error to Debug output
- Default values will be used automatically
- The application will continue to run normally

## Best Practices

### Development

- Keep `appsettings.json` in source control with sensible defaults
- Document any changes to configuration settings
- Test configuration changes in a development environment first

### Production

- Review all settings before deployment
- Adjust timeouts based on hardware capabilities:
  - Increase `RequestTimeoutSeconds` for slower machines
  - Decrease `StartupTimeoutSeconds` for faster machines with SSDs
- Consider increasing `CacheExpirationDays` for better performance
- Set `EnableAuditLogging` based on compliance requirements

### Performance Tuning

**For faster processing:**
```json
{
  "Processing": {
    "MaxConcurrentImages": 10,
    "EnableClustering": false
  },
  "Cache": {
    "DefaultExpirationDays": 90
  }
}
```

**For better accuracy:**
```json
{
  "Processing": {
    "EnableClustering": true,
    "ClusterRadiusKm": 50,
    "ClusterBoostPercent": 20
  },
  "Api": {
    "DefaultTopK": 10
  }
}
```

**For compliance/audit:**
```json
{
  "Audit": {
    "EnableAuditLogging": true,
    "RetentionDays": 365,
    "MaxEntriesBeforeWarning": 50000
  }
}
```

## Troubleshooting

### Configuration Not Loading

**Symptom:** Application uses default values instead of appsettings.json

**Solution:**
1. Verify `appsettings.json` exists in the application directory
2. Check that `CopyToOutputDirectory` is set to `PreserveNewest` in `.csproj`
3. Rebuild the application

### Invalid JSON

**Symptom:** Application fails to start or uses default values

**Solution:**
1. Validate JSON syntax at https://jsonlint.com
2. Check for missing commas, brackets, or quotes
3. Review Debug output for error messages

### Services Not Using Configuration

**Symptom:** Hardcoded values are still being used

**Solution:**
1. Ensure service constructors accept nullable parameters
2. Use `ConfigurationService.Instance.Config` to access settings
3. Rebuild and restart the application

## Migration from Hardcoded Values

The following services have been updated to use configuration:

| Service | Hardcoded Values Replaced | Configuration Section |
|---------|---------------------------|----------------------|
| `PythonRuntimeManager` | Port (8899), timeouts (2s, 15s) | `GeoLens.Api` |
| `GeoCLIPApiClient` | Base URL, timeout (5 min), topK (5) | `GeoLens.Api` |
| `GeographicClusterAnalyzer` | Radius (100km), boost (0.15), min size (2) | `GeoLens.Processing` |
| `PredictionCacheService` | Expiration (90 days) | `GeoLens.Cache` |

**Breaking Changes:**
- `PythonRuntimeManager` constructor: `port` parameter is now nullable
- `GeoCLIPApiClient` constructor: `baseUrl` parameter is now nullable
- `GeoCLIPApiClient.InferSingleAsync`: `topK` parameter is now nullable
- `GeoCLIPApiClient.InferBatchAsync`: `topK` parameter is now nullable
- `PredictionCacheService.ClearExpiredAsync`: `expirationDays` parameter is now nullable

## Future Enhancements

The following settings are planned for future implementation:

- `Cache.MaxSizeMB` - Automatic cache size management
- `Cache.MemoryCacheMaxEntries` - LRU eviction for memory cache
- `Processing.MaxConcurrentImages` - Concurrent processing limits
- `UI.DefaultTheme` - Theme switching support

## Example Configurations

### Minimal Configuration

```json
{
  "GeoLens": {
    "Api": {
      "Port": 8899
    }
  }
}
```

### Full Configuration (with comments removed)

See `appsettings.json` in the application root directory.

## Support

For configuration-related issues:
1. Review this documentation
2. Check Debug output for error messages
3. Verify `appsettings.json` syntax
4. Report issues on GitHub with configuration file attached
