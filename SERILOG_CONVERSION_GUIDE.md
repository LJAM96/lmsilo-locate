# Serilog Conversion Guide

This document provides guidance on converting the remaining `Debug.WriteLine` calls to structured logging with Serilog.

## Completed Work

### ✅ Infrastructure Setup
1. **GeoLens.csproj** - Added 4 Serilog package references:
   - `Serilog` (v3.1.1)
   - `Serilog.Sinks.Console` (v5.0.1)
   - `Serilog.Sinks.File` (v5.0.0)
   - `Serilog.Sinks.Debug` (v2.0.0)

2. **Services/LoggingService.cs** - Created new centralized logging service:
   - Initializes Serilog with console, debug, and file sinks
   - Logs to: `%LocalAppData%\GeoLens\Logs\geolens-{date}.log`
   - Retention: 7 days
   - Enriched with Application and Version properties

3. **App.xaml.cs** - Fully converted (17 structured log calls):
   - Added `using Serilog;`
   - Initialize logging in constructor: `LoggingService.Initialize()`
   - Shutdown logging in dispose: `LoggingService.Shutdown()`
   - Converted all Debug.WriteLine to Log.* methods

4. **Services/PythonRuntimeManager.cs** - Fully converted (16 structured log calls):
   - Added `using Serilog;`
   - All Debug.WriteLine converted to structured logging

## Remaining Priority Files

| File | Debug.WriteLine Count | Status |
|------|----------------------|--------|
| Services/GeoCLIPApiClient.cs | 12 | Pending |
| Services/PredictionCacheService.cs | 21 | Pending |
| Services/PredictionProcessor.cs | 27 | Pending |
| Views/MainPage.xaml.cs | 54 | Pending |
| **Total** | **114** | **Pending** |

## Conversion Patterns

### 1. Add Using Statement
At the top of each file, add:
```csharp
using Serilog;
```

### 2. Log Level Selection Guide

Use these log levels based on the nature of the message:

| Old Pattern | New Pattern | When to Use |
|-------------|-------------|-------------|
| `Debug.WriteLine($"Processing {fileName}")` | `Log.Debug("Processing {FileName}", fileName)` | Detailed trace information (development) |
| `Debug.WriteLine($"Starting service on {url}")` | `Log.Information("Starting service on {Url}", url)` | Normal operations, milestones |
| `Debug.WriteLine($"WARNING: {message}")` | `Log.Warning("Warning message: {Message}", message)` | Unexpected but recoverable situations |
| `Debug.WriteLine($"ERROR: {ex.Message}")` | `Log.Error(ex, "Error message")` | Error conditions that are handled |
| `Debug.WriteLine($"FATAL: {ex.Message}")` | `Log.Fatal(ex, "Fatal error")` | Critical failures |

### 3. Structured Logging Conversion Examples

#### Before:
```csharp
Debug.WriteLine($"[ServiceName] Processing {fileName}");
```

#### After:
```csharp
Log.Information("Processing {FileName} in {ServiceName}", fileName, "ServiceName");
```

#### Before:
```csharp
System.Diagnostics.Debug.WriteLine($"Cache HIT for: {Path.GetFileName(imagePath)}");
```

#### After:
```csharp
Log.Information("Cache HIT for: {FileName}", Path.GetFileName(imagePath));
```

#### Before:
```csharp
Debug.WriteLine($"[PredictionProcessor] ERROR: {ex.Message}");
Debug.WriteLine($"[PredictionProcessor] Stack trace: {ex.StackTrace}");
```

#### After:
```csharp
Log.Error(ex, "PredictionProcessor error");
// Note: Exception details (message, stack trace) are automatically logged
```

### 4. Exception Logging

#### Before:
```csharp
catch (Exception ex)
{
    Debug.WriteLine($"Error processing: {ex.Message}");
    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
}
```

#### After:
```csharp
catch (Exception ex)
{
    Log.Error(ex, "Error processing");
    // Serilog automatically captures exception message, stack trace, inner exceptions
}
```

### 5. Context-Rich Logging

#### Before:
```csharp
Debug.WriteLine($"[CacheService] Stored prediction for: {Path.GetFileName(imagePath)}");
```

#### After:
```csharp
Log.Information("Stored prediction for: {FileName} in {ServiceName}",
    Path.GetFileName(imagePath),
    "CacheService");
```

### 6. Conditional Logging

#### Before:
```csharp
if (!string.IsNullOrEmpty(e.Data))
{
    Debug.WriteLine($"[Python] {e.Data}");
}
```

#### After:
```csharp
if (!string.IsNullOrEmpty(e.Data))
{
    Log.Debug("[Python] {Output}", e.Data);
}
```

## File-Specific Conversion Guides

### Services/GeoCLIPApiClient.cs (12 occurrences)

**Key Patterns:**
- Health check failures → `Log.Debug()` or `Log.Warning()`
- Inference completion → `Log.Information()`
- Network errors → `Log.Error()`
- Parse failures → `Log.Error()`

**Example Conversions:**
```csharp
// Health checks
Debug.WriteLine($"[GeoCLIPApiClient] Health check failed - network error: {ex.Message}");
→ Log.Debug(ex, "Health check failed - network error");

// Successful inference
System.Diagnostics.Debug.WriteLine($"Inference completed on device: {inferenceResponse.Device}");
→ Log.Information("Inference completed on device: {Device}", inferenceResponse.Device);

// MD5 computation errors
System.Diagnostics.Debug.WriteLine($"[GeoCLIPApiClient] I/O error computing MD5 for {filePath}: {ex.Message}");
→ Log.Warning(ex, "I/O error computing MD5 for {FilePath}", filePath);
```

### Services/PredictionCacheService.cs (21 occurrences)

**Key Patterns:**
- Cache hits/misses → `Log.Information()`
- Database operations → `Log.Information()` or `Log.Debug()`
- Errors → `Log.Error()` or `Log.Warning()`
- Statistics → `Log.Information()`

**Example Conversions:**
```csharp
// Cache hits
Debug.WriteLine($"[PredictionCacheService] Memory cache hit for: {Path.GetFileName(imagePath)}");
→ Log.Information("Memory cache hit for: {FileName}", Path.GetFileName(imagePath));

// Database initialization
Debug.WriteLine($"[PredictionCacheService] Database initialized at: {_dbPath}");
→ Log.Information("Database initialized at: {DbPath}", _dbPath);

// Errors
Debug.WriteLine($"[PredictionCacheService] Failed to compute hash for {imagePath}: {ex.Message}");
→ Log.Error(ex, "Failed to compute hash for {ImagePath}", imagePath);
```

### Services/PredictionProcessor.cs (27 occurrences)

**Key Patterns:**
- Pipeline stages → `Log.Information()`
- Cache operations → `Log.Debug()` or `Log.Information()`
- API calls → `Log.Information()`
- Errors → `Log.Error()` or `Log.Warning()`

**Example Conversions:**
```csharp
// Pipeline start
Debug.WriteLine($"[PredictionProcessor] Starting pipeline for: {fileName}");
→ Log.Information("Starting pipeline for: {FileName}", fileName);

// Cache hit
Debug.WriteLine($"[PredictionProcessor] Cache HIT for: {fileName}");
→ Log.Information("Cache HIT for: {FileName}", fileName);

// API errors
Debug.WriteLine($"[PredictionProcessor] Network error in pipeline: {ex.Message}");
→ Log.Error(ex, "Network error in pipeline");
```

### Views/MainPage.xaml.cs (54 occurrences)

**Key Patterns:**
- UI operations → `Log.Information()` or `Log.Debug()`
- Processing → `Log.Information()`
- User actions → `Log.Information()`
- Errors → `Log.Error()` or `Log.Warning()`

**Example Conversions:**
```csharp
// UI initialization
Debug.WriteLine("[MainPage] Map provider disposed");
→ Log.Information("Map provider disposed");

// Image processing
System.Diagnostics.Debug.WriteLine($"[ProcessImages] Cache HIT for: {item.FileName}");
→ Log.Information("Cache HIT for: {FileName}", item.FileName);

// Errors
Debug.WriteLine($"[AddImageToQueue] Error adding {filePath}: {ex.Message}");
→ Log.Error(ex, "Error adding image to queue: {FilePath}", filePath);
```

## Automated Conversion Script

To help with bulk conversion, here are sed commands for common patterns:

```bash
# Add using Serilog at the top (after other usings)
sed -i '/^using System/a using Serilog;' Services/GeoCLIPApiClient.cs

# Convert simple Debug.WriteLine patterns
# Note: These are starting points - manual review is required for structured properties

# Pattern 1: Simple messages
sed -i 's/Debug\.WriteLine("\([^"]*\)")/Log.Information("\1")/g' file.cs

# Pattern 2: String interpolation (requires manual conversion to structured logging)
# This is a placeholder - manual conversion needed for proper structured logging
```

**Important:** Automated conversion should be followed by manual review to ensure:
1. Proper log level selection (Debug, Information, Warning, Error, Fatal)
2. Structured properties instead of string interpolation
3. Exception objects passed correctly to Log.Error()

## Testing

After conversion, verify:
1. Build succeeds: `dotnet build GeoLens.csproj`
2. Logs appear in: `%LocalAppData%\GeoLens\Logs\geolens-{date}.log`
3. Logs are structured (JSON-friendly format with properties)
4. No Debug.WriteLine calls remain: `grep -r "Debug.WriteLine" Services/ Views/`

## Benefits of Structured Logging

- **Searchable**: Query logs by properties (e.g., find all operations for a specific file)
- **Performance**: Structured logs can be efficiently indexed and searched
- **Production-Ready**: Logs can be sent to centralized logging systems (Seq, Elasticsearch, etc.)
- **Context-Rich**: Properties provide context without string concatenation
- **Type-Safe**: Properties maintain their types for better querying

## Next Steps

1. Convert remaining 114 Debug.WriteLine calls in priority files
2. Add Serilog to remaining service files (11 files identified)
3. Update CLAUDE.md to document logging approach
4. Consider adding Seq sink for development (optional)
5. Add log correlation IDs for request tracking (future enhancement)
