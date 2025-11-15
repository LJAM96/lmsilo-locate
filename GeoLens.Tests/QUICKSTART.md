# GeoLens Tests - Quick Start Guide

## Getting Started in 3 Steps

### 1. Add Test Project to Solution

```bash
cd /home/user/geolens
dotnet sln add GeoLens.Tests/GeoLens.Tests.csproj
```

### 2. Restore Dependencies

```bash
dotnet restore GeoLens.Tests/GeoLens.Tests.csproj
```

### 3. Run All Tests

```bash
dotnet test
```

## Expected Output

You should see output similar to:

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    45, Skipped:     0, Total:    45
```

## Verify Individual Test Classes

```bash
# Test caching service (9 tests)
dotnet test --filter "FullyQualifiedName~PredictionCacheServiceTests"

# Test EXIF extraction (13 tests)
dotnet test --filter "FullyQualifiedName~ExifMetadataExtractorTests"

# Test clustering logic (23 tests)
dotnet test --filter "FullyQualifiedName~GeographicClusterAnalyzerTests"
```

## Test Coverage Summary

- **PredictionCacheService**: 9 tests - Database, hashing, caching operations
- **ExifMetadataExtractor**: 13 tests - GPS extraction, coordinate formatting
- **GeographicClusterAnalyzer**: 23 tests - Distance calculations, clustering, confidence

**Total: 45 tests**

## Troubleshooting

### Issue: "dotnet: command not found"
**Solution**: Install .NET 9 SDK from https://dotnet.microsoft.com/download

### Issue: "Project reference could not be resolved"
**Solution**: Ensure you're running from `/home/user/geolens` directory

### Issue: Tests fail to find temporary directory
**Solution**: Ensure write permissions to system temp directory

## Next Steps

1. ✅ Run tests to verify all 45 tests pass
2. ✅ Review test code in `Services/` directory
3. ✅ Check code coverage: `dotnet test /p:CollectCoverage=true`
4. ✅ Read full documentation in `README.md`

## Quick Reference

| Command | Purpose |
|---------|---------|
| `dotnet test` | Run all tests |
| `dotnet test --logger "console;verbosity=detailed"` | Detailed output |
| `dotnet test --filter "FullyQualifiedName~TestName"` | Run specific test |
| `dotnet test /p:CollectCoverage=true` | Generate coverage |

## Files in This Project

```
GeoLens.Tests/
├── GeoLens.Tests.csproj                    # Project configuration
├── QUICKSTART.md                            # This file
├── README.md                                # Full documentation
└── Services/
    ├── PredictionCacheServiceTests.cs      # Cache tests (9)
    ├── ExifMetadataExtractorTests.cs       # EXIF tests (13)
    └── GeographicClusterAnalyzerTests.cs   # Clustering tests (23)
```

For comprehensive documentation, see `README.md`.
