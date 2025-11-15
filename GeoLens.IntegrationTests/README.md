# GeoLens Integration Tests

Comprehensive integration tests for the GeoLens application, testing Python ↔ C# communication and end-to-end workflows.

## Overview

This test project contains integration tests that verify:

1. **Python Service Lifecycle** - Service startup, health checks, and graceful shutdown
2. **End-to-End Image Processing** - Complete workflows from image loading to prediction display
3. **Cache Integration** - SQLite database persistence and concurrent access
4. **Export Integration** - Multi-format export functionality (CSV, PDF, KML, JSON)

## Project Structure

```
GeoLens.IntegrationTests/
├── TestFixtures/
│   ├── PythonServiceFixture.cs      # Manages Python service lifecycle
│   └── TestDataFixture.cs           # Manages temporary test directories
├── TestHelpers/
│   ├── TestImageGenerator.cs        # Generates test images
│   └── TestDataPaths.cs             # Manages test data file paths
├── TestData/
│   └── README.md                    # Test data documentation
├── PythonServiceTests.cs            # Python service lifecycle tests
├── ImageProcessingTests.cs          # End-to-end processing tests
├── CacheIntegrationTests.cs         # Cache and database tests
├── ExportIntegrationTests.cs        # Export functionality tests
└── GeoLens.IntegrationTests.csproj  # Project file
```

## Running Tests

### Prerequisites

1. **Python 3.11+** with dependencies installed:
   ```bash
   pip install -r Core/requirements.txt
   pip install -r Core/requirements-cpu.txt
   ```

2. **.NET 9 SDK** installed

3. **Python service dependencies**:
   - FastAPI
   - uvicorn
   - GeoCLIP model (auto-downloaded on first run)

### Run All Integration Tests

```bash
# From repository root
dotnet test GeoLens.IntegrationTests/GeoLens.IntegrationTests.csproj --configuration Release
```

### Run Specific Test Class

```bash
# Run only Python service tests
dotnet test GeoLens.IntegrationTests/GeoLens.IntegrationTests.csproj --filter "FullyQualifiedName~PythonServiceTests"

# Run only cache tests
dotnet test GeoLens.IntegrationTests/GeoLens.IntegrationTests.csproj --filter "FullyQualifiedName~CacheIntegrationTests"
```

### Run with Verbose Logging

```bash
dotnet test GeoLens.IntegrationTests/GeoLens.IntegrationTests.csproj --verbosity detailed
```

## Test Fixtures

### PythonServiceFixture

Manages the Python service lifecycle for all tests in the `PythonService` collection. The fixture:

- Starts the Python FastAPI service once before all tests
- Provides a shared `PythonRuntimeManager` instance
- Cleans up and stops the service after all tests complete
- Supports service restart for recovery testing

**Usage:**
```csharp
[Collection("PythonService")]
public class MyIntegrationTests
{
    private readonly PythonServiceFixture _fixture;

    public MyIntegrationTests(PythonServiceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MyTest()
    {
        if (!_fixture.IsServiceAvailable)
            return; // Skip if service failed to start

        var apiClient = new GeoCLIPApiClient(_fixture.BaseUrl);
        // ... test code
    }
}
```

### TestDataFixture

Creates isolated temporary directories for each test run:

- `CacheDirectory` - SQLite cache databases
- `AuditLogDirectory` - Audit log databases
- `ExportDirectory` - Export file outputs
- `RecentFilesDirectory` - Recent files tracking

All directories are automatically cleaned up after tests complete.

## Test Helpers

### TestImageGenerator

Generates test images with known properties:

```csharp
// Generate a colored image
var path = TestImageGenerator.GenerateColoredImage(outputPath, color: "red");

// Generate multiple test images
var paths = TestImageGenerator.GenerateTestImageBatch(directory, count: 10);

// Calculate hash for cache verification
var md5Hash = TestImageGenerator.CalculateMD5Hash(imagePath);
var xxHash = TestImageGenerator.CalculateXXHash64(imagePath);
```

### TestDataPaths

Manages test data directory and file paths:

```csharp
// Get all test images
var images = TestDataPaths.GetAllTestImages();

// Get first available test image
var image = TestDataPaths.GetFirstTestImage();

// Get N test images (auto-generates if needed)
var batch = TestDataPaths.GetTestImageBatch(10);

// Ensure test data exists
TestDataPaths.EnsureTestDataExists();
```

## Test Categories

### Python Service Tests (10 tests)

Tests for Python service communication and lifecycle:

- `ServiceShouldBeRunning` - Verify service starts successfully
- `HealthEndpoint_ShouldRespond_WithSuccess` - Health check returns 200 OK
- `HealthEndpoint_ShouldReturn_ValidJson` - Health response is valid JSON
- `InferEndpoint_WithTestImage_ShouldReturnPredictions` - Inference endpoint works
- `InferEndpoint_WithTestImage_ShouldReturnValidPredictionStructure` - Predictions have correct structure
- `ServiceRestart_ShouldSucceed` - Service can be restarted
- `GeoCLIPApiClient_ShouldDetectHardware` - Hardware detection works
- `GeoCLIPApiClient_WithValidImage_ShouldReturnMultiplePredictions` - API client returns predictions
- `GeoCLIPApiClient_WithInvalidImage_ShouldHandleGracefully` - Invalid images are handled

### Image Processing Tests (8 tests)

End-to-end workflows:

- `EndToEnd_ProcessSingleImage_ShouldReturnPredictions` - Complete processing pipeline
- `EndToEnd_ProcessImageWithCache_ShouldReturnCachedResults` - Cache hit scenario
- `BatchProcessing_Process10Images_ShouldSucceed` - Batch processing
- `ProcessImage_WithExifGPS_ShouldPrioritizeExifLocation` - EXIF GPS handling
- `PredictionProcessor_FullPipeline_ShouldOrchestrate` - PredictionProcessor orchestration
- `GeographicClusterAnalyzer_WithPredictions_ShouldApplyBoost` - Clustering boost
- `MultipleImages_SameCacheDatabase_ShouldIsolate` - Cache isolation

### Cache Integration Tests (11 tests)

Database and persistence tests:

- `PredictionCache_CreateDatabase_ShouldPersist` - SQLite database creation
- `PredictionCache_MultipleImages_ShouldStoreIndependently` - Independent storage
- `PredictionCache_UpdateExisting_ShouldOverwrite` - Cache updates
- `PredictionCache_ClearAll_ShouldRemoveAllEntries` - Cache clearing
- `AuditLogService_LogEvent_ShouldPersist` - Audit log persistence
- `AuditLogService_MultipleEvents_ShouldMaintainOrder` - Event ordering
- `RecentFilesService_TrackFiles_ShouldPersist` - Recent files tracking
- `RecentFilesService_LimitResults_ShouldRespectLimit` - Result limiting
- `ConcurrentAccess_MultipleCacheWrites_ShouldSucceed` - Concurrent writes
- `ConcurrentAccess_ReadWhileWriting_ShouldNotCorrupt` - Concurrent read/write

### Export Integration Tests (9 tests)

Export functionality tests:

- `ExportCSV_WithPredictions_ShouldCreateValidFile` - CSV export
- `ExportCSV_MultipleImages_ShouldContainAllPredictions` - Multi-image CSV
- `ExportPDF_WithPredictions_ShouldCreateValidFile` - PDF export
- `ExportKML_WithPredictions_ShouldCreateValidFile` - KML export
- `ExportJSON_WithPredictions_ShouldCreateValidFile` - JSON export
- `ExportKML_MultipleImages_ShouldGroupByImage` - KML grouping
- `ExportAllFormats_ShouldSucceedForSameData` - All format export
- `ExportCSV_WithClusteringData_ShouldIncludeBoost` - Clustering data export

**Total: 38 integration tests**

## CI/CD Integration

Integration tests run automatically in GitHub Actions on:

- Push to `main` or `develop` branches
- Pull requests to `main`

See `.github/workflows/build.yml` for CI configuration.

### CI Test Flow

1. Build solution
2. Run unit tests (GeoLens.Tests)
3. Start Python service in background
4. Run integration tests (GeoLens.IntegrationTests)
5. Stop Python service
6. Upload test coverage

## Test Data Management

Test images are **auto-generated** during test execution. The `TestImageGenerator` creates:

- 5 default colored images (red, green, blue, landscape, portrait)
- Additional images as needed for batch tests
- Images with known hashes for cache verification

Test images are **not committed** to the repository to keep it lightweight.

## Troubleshooting

### Python Service Not Starting

If tests skip with "Python service not available":

1. Ensure Python dependencies are installed
2. Check Python is in PATH
3. Verify port 8899 is not in use
4. Check GeoCLIP model downloaded successfully

### Test Failures in CI

Common CI issues:

- **Timeout**: GeoCLIP model download in CI can be slow (first run only)
- **Port conflicts**: Ensure no other services use port 8899
- **Python version**: Requires Python 3.11+ for compatibility

### Database Locked Errors

If you see SQLite database locked errors:

- Tests use isolated temp directories (should not conflict)
- Check no external processes are accessing test databases
- Verify proper fixture cleanup

## Code Coverage

Integration tests contribute to overall code coverage. Target: **70%+ coverage** for service layer.

To generate coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Contributing

When adding new integration tests:

1. Use appropriate test fixture (`PythonServiceFixture`, `TestDataFixture`)
2. Add `[Collection("PythonService")]` for tests needing Python service
3. Check `_fixture.IsServiceAvailable` before running tests
4. Use FluentAssertions for readable assertions
5. Clean up resources in `Dispose()` method
6. Add descriptive test method names

## See Also

- `GeoLens.Tests/` - Unit tests for individual services
- `Docs/09_Testing_Strategy.md` - Overall testing strategy
- `INTEGRATION_TESTS_IMPLEMENTATION_SUMMARY.md` - Implementation details
