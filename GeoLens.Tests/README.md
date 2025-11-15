# GeoLens Unit Tests

This project contains comprehensive unit tests for the GeoLens application, focusing on core services and business logic.

## Test Framework

- **xUnit** - Modern, extensible testing framework for .NET
- **FluentAssertions** - Expressive assertion library for readable test code
- **Moq** - Mocking framework for isolating dependencies (available for future use)

## Running Tests

### Run All Tests

```bash
dotnet test
```

### Run Tests with Detailed Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Tests in Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~PredictionCacheServiceTests"
dotnet test --filter "FullyQualifiedName~ExifMetadataExtractorTests"
dotnet test --filter "FullyQualifiedName~GeographicClusterAnalyzerTests"
```

### Run Single Test

```bash
dotnet test --filter "FullyQualifiedName~CalculateDistance_ParisToLondon_ShouldBeApproximately344Km"
```

## Test Coverage

### Generate Coverage Report

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Generate HTML Coverage Report (requires reportgenerator)

```bash
# Install report generator globally (one time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./TestResults/

# Generate HTML report
reportgenerator -reports:./TestResults/coverage.cobertura.xml -targetdir:./TestResults/CoverageReport -reporttypes:Html
```

## Test Organization

### Services/

Contains tests for all service layer components:

- **PredictionCacheServiceTests.cs** - Two-tier caching system (SQLite + memory)
  - Hash computation and consistency
  - Cache storage and retrieval
  - Cache statistics
  - Cache clearing
  - Error handling

- **ExifMetadataExtractorTests.cs** - EXIF metadata and GPS extraction
  - GPS data extraction
  - File format handling
  - Coordinate formatting
  - Error handling for missing/invalid files
  - Model property validation

- **GeographicClusterAnalyzerTests.cs** - Geographic clustering and distance calculations
  - Haversine distance formula accuracy
  - Cluster detection logic
  - Confidence boosting for clusters
  - Confidence level classification
  - Prediction property calculations

## Test Patterns

### Arrange-Act-Assert (AAA)

All tests follow the AAA pattern for clarity:

```csharp
[Fact]
public void TestName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and dependencies
    var testData = CreateTestData();

    // Act - Execute the code under test
    var result = _service.MethodUnderTest(testData);

    // Assert - Verify the expected outcome
    result.Should().Be(expectedValue, "reason for assertion");
}
```

### Theory-Based Tests

Data-driven tests using `[Theory]` and `[InlineData]`:

```csharp
[Theory]
[InlineData(0.65, true, ConfidenceLevel.High)]
[InlineData(0.45, true, ConfidenceLevel.Medium)]
[InlineData(0.25, true, ConfidenceLevel.Low)]
public void ClassifyConfidence_ShouldReturnCorrectLevel(
    double probability,
    bool isClustered,
    ConfidenceLevel expected)
{
    var result = EnhancedLocationPrediction.ClassifyConfidence(probability, isClustered);
    result.Should().Be(expected);
}
```

### Test Cleanup

Tests that create temporary resources implement `IDisposable`:

```csharp
public class ServiceTests : IDisposable
{
    public void Dispose()
    {
        // Clean up temporary files, database connections, etc.
    }
}
```

## Current Test Coverage

### PredictionCacheService (12 tests)
- ✅ Database initialization
- ✅ Hash computation consistency
- ✅ Hash uniqueness for different files
- ✅ Cache miss for new files
- ✅ Save and retrieve predictions
- ✅ Cache clearing
- ✅ Cache statistics
- ✅ Error handling for non-existent files
- ✅ Empty prediction caching

### ExifMetadataExtractor (14 tests)
- ✅ Non-existent file handling
- ✅ Null/empty path handling
- ✅ Images without GPS data
- ✅ JPEG file format support
- ✅ Other image format support
- ✅ Coordinate formatting (N/S/E/W)
- ✅ Latitude/longitude formatting
- ✅ Altitude storage
- ✅ Location name storage

### GeographicClusterAnalyzer (21 tests)
- ✅ Distance calculations (same point, real-world distances)
- ✅ Haversine formula accuracy (Paris-London, NY-LA, Tokyo-Sydney)
- ✅ Distance symmetry
- ✅ Cross-equator and cross-meridian calculations
- ✅ Null/empty prediction handling
- ✅ Cluster detection for close predictions
- ✅ Non-clustering for distant predictions
- ✅ Confidence boosting logic
- ✅ Confidence level classification
- ✅ Prediction property calculations

**Total: 47 tests covering 3 core services**

## Best Practices

1. **Descriptive Test Names**: Use format `MethodName_Scenario_ExpectedBehavior`
2. **Clear Assertions**: Always include a reason message in `.Should()` assertions
3. **Test Isolation**: Each test should be independent and not rely on others
4. **Resource Cleanup**: Always dispose temporary resources (files, connections)
5. **Readable Test Code**: Use FluentAssertions for expressive, human-readable assertions
6. **Edge Cases**: Test boundary conditions, null inputs, and error scenarios
7. **Real-World Data**: Use actual geographic coordinates for distance calculation tests

## Adding New Tests

When adding new tests:

1. Create test class in appropriate directory (e.g., `Services/NewServiceTests.cs`)
2. Implement `IDisposable` if test creates temporary resources
3. Follow AAA pattern and naming conventions
4. Use `[Fact]` for single test cases, `[Theory]` for data-driven tests
5. Add descriptive assertion messages
6. Update this README with new test coverage information

## Continuous Integration

These tests are designed to run in CI/CD pipelines:

```yaml
# Example GitHub Actions workflow
- name: Run Tests
  run: dotnet test --logger "trx" --results-directory TestResults

- name: Upload Test Results
  uses: actions/upload-artifact@v3
  with:
    name: test-results
    path: TestResults
```

## Future Test Additions

Planned test coverage expansion:

- [ ] PythonRuntimeManager (process lifecycle, health checks)
- [ ] GeoCLIPApiClient (HTTP communication, batch processing)
- [ ] ExportService (CSV, JSON, PDF, KML generation)
- [ ] PredictionProcessor (full pipeline integration)
- [ ] LeafletMapProvider (WebView2 integration)
- [ ] UserSettingsService (JSON persistence, debouncing)
- [ ] Integration tests (end-to-end workflows)
- [ ] Performance benchmarks (caching, clustering algorithms)

## License

Part of the GeoLens project. See main repository for license information.
