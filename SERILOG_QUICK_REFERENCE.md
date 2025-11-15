# Serilog Quick Reference Card

## Common Conversion Patterns

### ✅ Simple Information
```csharp
Debug.WriteLine("Service started");
→ Log.Information("Service started");
```

### ✅ Information with Variable
```csharp
Debug.WriteLine($"Processing file: {fileName}");
→ Log.Information("Processing file: {FileName}", fileName);
```

### ✅ Information with Multiple Variables
```csharp
Debug.WriteLine($"[{serviceName}] Processing {fileName} ({size} bytes)");
→ Log.Information("Processing {FileName} ({Size} bytes) in {ServiceName}", fileName, size, serviceName);
```

### ✅ Error with Exception
```csharp
Debug.WriteLine($"Error: {ex.Message}");
Debug.WriteLine($"Stack: {ex.StackTrace}");
→ Log.Error(ex, "Error occurred");
// Exception message & stack trace automatically included
```

### ✅ Warning
```csharp
Debug.WriteLine($"WARNING: {message}");
→ Log.Warning("Warning message: {Message}", message);
```

### ✅ Debug Trace
```csharp
Debug.WriteLine($"[Debug] Entering method with param={value}");
→ Log.Debug("Entering method with param={Value}", value);
```

### ✅ Conditional Logging
```csharp
if (result != null)
    Debug.WriteLine($"Result: {result}");
→
if (result != null)
    Log.Information("Result: {Result}", result);
```

## Log Level Decision Tree

```
Is this an exception?
├─ YES → Log.Error(ex, "message") or Log.Fatal(ex, "message")
└─ NO → Is this unexpected but recoverable?
    ├─ YES → Log.Warning("message")
    └─ NO → Is this operational information?
        ├─ YES → Log.Information("message")
        └─ NO → Log.Debug("message")
```

## Property Naming

Use PascalCase for property names:
- `{FileName}` not `{fileName}` or `{file_name}`
- `{ImagePath}` not `{imagePath}` or `{image_path}`
- `{ProcessedCount}` not `{processedCount}`

## Add to Each File

```csharp
using Serilog;
```

That's it! Now use `Log.Information()`, `Log.Error()`, etc. instead of `Debug.WriteLine()`.

## Testing

After conversion:
1. Build: Check for compilation errors
2. Run: Logs appear in `%LocalAppData%\GeoLens\Logs\geolens-{date}.log`
3. Verify: Properties are structured (not concatenated strings)

