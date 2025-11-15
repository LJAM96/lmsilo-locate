using FluentAssertions;
using GeoLens.Services;
using GeoLens.Models;
using GeoLens.Services.DTOs;
using Xunit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GeoLens.Tests.Services;

/// <summary>
/// Unit tests for PredictionCacheService - two-tier caching with SQLite and XXHash64
/// </summary>
public class PredictionCacheServiceTests : IDisposable
{
    private readonly PredictionCacheService _cacheService;
    private readonly string _testDbPath;
    private readonly List<string> _testFiles;

    public PredictionCacheServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_cache_{Guid.NewGuid()}.db");
        _cacheService = new PredictionCacheService(_testDbPath);
        _cacheService.InitializeAsync().Wait();
        _testFiles = new List<string>();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDatabaseFile()
    {
        // Act
        await _cacheService.InitializeAsync();

        // Assert
        File.Exists(_testDbPath).Should().BeTrue("database file should be created");
    }

    [Fact]
    public async Task ComputeImageHashAsync_ShouldReturnConsistentHash()
    {
        // Arrange
        var testFile = CreateTestImageFile();

        // Act
        var hash1 = await _cacheService.ComputeImageHashAsync(testFile);
        var hash2 = await _cacheService.ComputeImageHashAsync(testFile);

        // Assert
        hash1.Should().Be(hash2, "hash should be deterministic for same file");
        hash1.Should().NotBeNullOrEmpty("hash should not be empty");
        hash1.Length.Should().BeGreaterThan(10, "hash should be a reasonable length");
    }

    [Fact]
    public async Task ComputeImageHashAsync_DifferentFiles_ShouldReturnDifferentHashes()
    {
        // Arrange
        var testFile1 = CreateTestImageFile(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });
        var testFile2 = CreateTestImageFile(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 });

        // Act
        var hash1 = await _cacheService.ComputeImageHashAsync(testFile1);
        var hash2 = await _cacheService.ComputeImageHashAsync(testFile2);

        // Assert
        hash1.Should().NotBe(hash2, "different files should produce different hashes");
    }

    [Fact]
    public async Task GetCachedPredictionAsync_WhenNoCacheExists_ShouldReturnNull()
    {
        // Arrange
        var testFile = CreateTestImageFile();

        // Act
        var result = await _cacheService.GetCachedPredictionAsync(testFile);

        // Assert
        result.Should().BeNull("no cache entry exists for new file");
    }

    [Fact]
    public async Task SaveAndRetrievePrediction_ShouldWorkCorrectly()
    {
        // Arrange
        var testFile = CreateTestImageFile();
        var predictions = new List<LocationPrediction>
        {
            new LocationPrediction
            {
                Latitude = 48.8566,
                Longitude = 2.3522,
                Probability = 0.85,
                City = "Paris",
                Country = "France"
            }
        };
        var exifGps = new ExifGpsData
        {
            Latitude = 48.8566,
            Longitude = 2.3522,
            HasGps = true
        };

        // Act
        await _cacheService.SavePredictionAsync(testFile, predictions, exifGps);
        var retrieved = await _cacheService.GetCachedPredictionAsync(testFile);

        // Assert
        retrieved.Should().NotBeNull("prediction should be cached");
        retrieved!.Predictions.Should().HaveCount(1);
        retrieved.Predictions[0].City.Should().Be("Paris");
        retrieved.Predictions[0].Probability.Should().Be(0.85);
        retrieved.ExifGps.Should().NotBeNull();
        retrieved.ExifGps!.HasGps.Should().BeTrue();
    }

    [Fact]
    public async Task ClearCache_ShouldRemoveAllEntries()
    {
        // Arrange
        var testFile = CreateTestImageFile();
        var predictions = new List<LocationPrediction>
        {
            new LocationPrediction
            {
                Latitude = 51.5074,
                Longitude = -0.1278,
                Probability = 0.75,
                City = "London",
                Country = "United Kingdom"
            }
        };

        await _cacheService.SavePredictionAsync(testFile, predictions, null);
        var beforeClear = await _cacheService.GetCachedPredictionAsync(testFile);

        // Act
        await _cacheService.ClearCacheAsync();
        var afterClear = await _cacheService.GetCachedPredictionAsync(testFile);

        // Assert
        beforeClear.Should().NotBeNull("entry should exist before clearing");
        afterClear.Should().BeNull("entry should be removed after clearing");
    }

    [Fact]
    public async Task GetCacheStats_ShouldReturnStatistics()
    {
        // Arrange
        var testFile = CreateTestImageFile();
        var predictions = new List<LocationPrediction>
        {
            new LocationPrediction { Latitude = 40.7128, Longitude = -74.0060, Probability = 0.9 }
        };

        // Act
        await _cacheService.SavePredictionAsync(testFile, predictions, null);
        var stats = await _cacheService.GetCacheStatsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.TotalEntries.Should().BeGreaterThan(0, "should have at least one entry");
    }

    [Fact]
    public async Task ComputeImageHashAsync_WithNonExistentFile_ShouldThrow()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.jpg");

        // Act
        Func<Task> act = async () => await _cacheService.ComputeImageHashAsync(nonExistentFile);

        // Assert
        await act.Should().ThrowAsync<Exception>("file does not exist");
    }

    [Fact]
    public async Task SavePredictionAsync_WithEmptyPredictions_ShouldStillCache()
    {
        // Arrange
        var testFile = CreateTestImageFile();
        var emptyPredictions = new List<LocationPrediction>();

        // Act
        await _cacheService.SavePredictionAsync(testFile, emptyPredictions, null);
        var retrieved = await _cacheService.GetCachedPredictionAsync(testFile);

        // Assert
        retrieved.Should().NotBeNull("empty predictions should still be cached");
        retrieved!.Predictions.Should().BeEmpty();
    }

    private string CreateTestImageFile(byte[]? content = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.jpg");
        var data = content ?? new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
        File.WriteAllBytes(path, data);
        _testFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        _cacheService?.Dispose();

        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { /* ignore */ }
        }

        // Clean up test image files
        foreach (var file in _testFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* ignore */ }
            }
        }
    }
}
