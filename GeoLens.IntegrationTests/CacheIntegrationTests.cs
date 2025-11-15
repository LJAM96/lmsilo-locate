using FluentAssertions;
using GeoLens.IntegrationTests.TestFixtures;
using GeoLens.IntegrationTests.TestHelpers;
using GeoLens.Models;
using GeoLens.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace GeoLens.IntegrationTests
{
    /// <summary>
    /// Integration tests for cache persistence and database operations
    /// Tests: SQLite database, audit logging, recent files, concurrent access
    /// </summary>
    public class CacheIntegrationTests : IClassFixture<TestDataFixture>
    {
        private readonly TestDataFixture _dataFixture;

        public CacheIntegrationTests(TestDataFixture dataFixture)
        {
            _dataFixture = dataFixture;
            Log.Information("CacheIntegrationTests initialized");
        }

        [Fact]
        public async Task PredictionCache_CreateDatabase_ShouldPersist()
        {
            // Arrange
            var dbPath = _dataFixture.GetCacheDatabasePath();
            var cacheService = new PredictionCacheService(dbPath);

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var predictions = new List<LocationPrediction>
            {
                new() { Latitude = 51.5074, Longitude = -0.1278, Probability = 0.85 }, // London
                new() { Latitude = 48.8566, Longitude = 2.3522, Probability = 0.12 },  // Paris
                new() { Latitude = 40.7128, Longitude = -74.0060, Probability = 0.03 }  // NYC
            };

            // Act
            await cacheService.CachePredictionsAsync(testImagePath, predictions);

            // Assert
            File.Exists(dbPath).Should().BeTrue("SQLite database file should be created");

            var retrieved = await cacheService.GetPredictionsAsync(testImagePath);
            retrieved.Should().NotBeNull();
            retrieved.Should().HaveCount(3);

            Log.Information("Cache database created and verified at {Path}", dbPath);
        }

        [Fact]
        public async Task PredictionCache_MultipleImages_ShouldStoreIndependently()
        {
            // Arrange
            var dbPath = _dataFixture.GetCacheDatabasePath();
            var cacheService = new PredictionCacheService(dbPath);

            var images = TestDataPaths.GetTestImageBatch(5);

            // Act - Cache different predictions for each image
            for (int i = 0; i < images.Length; i++)
            {
                var predictions = new List<LocationPrediction>
                {
                    new() { Latitude = i * 10.0, Longitude = i * 10.0, Probability = 0.9 }
                };

                await cacheService.CachePredictionsAsync(images[i], predictions);
            }

            // Assert - Verify each has unique predictions
            for (int i = 0; i < images.Length; i++)
            {
                var retrieved = await cacheService.GetPredictionsAsync(images[i]);
                retrieved.Should().NotBeNull();
                retrieved.Should().HaveCount(1);
                retrieved[0].Latitude.Should().Be(i * 10.0);
            }

            Log.Information("Verified independent storage for {Count} images", images.Length);
        }

        [Fact]
        public async Task PredictionCache_UpdateExisting_ShouldOverwrite()
        {
            // Arrange
            var dbPath = _dataFixture.GetCacheDatabasePath();
            var cacheService = new PredictionCacheService(dbPath);

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var initialPredictions = new List<LocationPrediction>
            {
                new() { Latitude = 10.0, Longitude = 10.0, Probability = 0.5 }
            };

            var updatedPredictions = new List<LocationPrediction>
            {
                new() { Latitude = 20.0, Longitude = 20.0, Probability = 0.8 }
            };

            // Act
            await cacheService.CachePredictionsAsync(testImagePath, initialPredictions);
            var firstRetrieval = await cacheService.GetPredictionsAsync(testImagePath);

            await cacheService.CachePredictionsAsync(testImagePath, updatedPredictions);
            var secondRetrieval = await cacheService.GetPredictionsAsync(testImagePath);

            // Assert
            firstRetrieval[0].Latitude.Should().Be(10.0);
            secondRetrieval[0].Latitude.Should().Be(20.0, "cache should be updated with new predictions");

            Log.Information("Cache update verified: {Old} -> {New}",
                firstRetrieval[0].Latitude, secondRetrieval[0].Latitude);
        }

        [Fact]
        public async Task PredictionCache_ClearAll_ShouldRemoveAllEntries()
        {
            // Arrange
            var dbPath = _dataFixture.GetCacheDatabasePath();
            var cacheService = new PredictionCacheService(dbPath);

            var images = TestDataPaths.GetTestImageBatch(3);

            foreach (var imagePath in images)
            {
                var predictions = new List<LocationPrediction>
                {
                    new() { Latitude = 0, Longitude = 0, Probability = 0.5 }
                };
                await cacheService.CachePredictionsAsync(imagePath, predictions);
            }

            // Act
            await cacheService.ClearAllAsync();

            // Assert
            foreach (var imagePath in images)
            {
                var retrieved = await cacheService.GetPredictionsAsync(imagePath);
                retrieved.Should().BeNull("all cache entries should be cleared");
            }

            Log.Information("Cache cleared successfully");
        }

        [Fact]
        public async Task AuditLogService_LogEvent_ShouldPersist()
        {
            // Arrange
            var dbPath = _dataFixture.GetAuditLogDatabasePath();
            var auditService = new AuditLogService(dbPath);

            // Act
            await auditService.LogEventAsync(
                eventType: "TEST_EVENT",
                message: "This is a test audit log entry",
                details: "Additional details here");

            // Assert
            File.Exists(dbPath).Should().BeTrue("audit log database should be created");

            // Retrieve recent logs
            var logs = await auditService.GetRecentLogsAsync(limit: 10);
            logs.Should().NotBeEmpty("should retrieve logged events");

            var testLog = logs.FirstOrDefault(l => l.EventType == "TEST_EVENT");
            testLog.Should().NotBeNull("should find the test event");
            testLog!.Message.Should().Be("This is a test audit log entry");

            Log.Information("Audit log verified: {EventType}", testLog.EventType);
        }

        [Fact]
        public async Task AuditLogService_MultipleEvents_ShouldMaintainOrder()
        {
            // Arrange
            var dbPath = _dataFixture.GetAuditLogDatabasePath();
            var auditService = new AuditLogService(dbPath);

            // Act - Log events in sequence
            await auditService.LogEventAsync("EVENT_1", "First event");
            await Task.Delay(10); // Ensure different timestamps
            await auditService.LogEventAsync("EVENT_2", "Second event");
            await Task.Delay(10);
            await auditService.LogEventAsync("EVENT_3", "Third event");

            // Assert
            var logs = await auditService.GetRecentLogsAsync(limit: 10);
            logs.Should().HaveCountGreaterOrEqualTo(3);

            // Recent logs should be in reverse chronological order
            var ourLogs = logs.Where(l => l.EventType.StartsWith("EVENT_")).Take(3).ToList();
            ourLogs[0].EventType.Should().Be("EVENT_3", "most recent event should be first");
            ourLogs[1].EventType.Should().Be("EVENT_2");
            ourLogs[2].EventType.Should().Be("EVENT_1");

            Log.Information("Verified audit log ordering");
        }

        [Fact]
        public async Task RecentFilesService_TrackFiles_ShouldPersist()
        {
            // Arrange
            var dbPath = Path.Combine(_dataFixture.RecentFilesDirectory, "recent_files.db");
            var recentFilesService = new RecentFilesService(dbPath);

            var images = TestDataPaths.GetTestImageBatch(5);

            // Act
            foreach (var imagePath in images)
            {
                await recentFilesService.AddFileAsync(imagePath);
            }

            // Assert
            var recentFiles = await recentFilesService.GetRecentFilesAsync(limit: 10);
            recentFiles.Should().HaveCount(5);

            // Most recent should be last added
            recentFiles.First().Should().Be(images[images.Length - 1],
                "most recent file should be first in list");

            Log.Information("Recent files tracking verified: {Count} files", recentFiles.Count);
        }

        [Fact]
        public async Task RecentFilesService_LimitResults_ShouldRespectLimit()
        {
            // Arrange
            var dbPath = Path.Combine(_dataFixture.RecentFilesDirectory, "recent_files.db");
            var recentFilesService = new RecentFilesService(dbPath);

            var images = TestDataPaths.GetTestImageBatch(10);

            // Act
            foreach (var imagePath in images)
            {
                await recentFilesService.AddFileAsync(imagePath);
            }

            var limited = await recentFilesService.GetRecentFilesAsync(limit: 5);

            // Assert
            limited.Should().HaveCount(5, "should respect the limit parameter");

            Log.Information("Recent files limit verified");
        }

        [Fact]
        public async Task ConcurrentAccess_MultipleCacheWrites_ShouldSucceed()
        {
            // Arrange
            var dbPath = _dataFixture.GetCacheDatabasePath();
            var cacheService = new PredictionCacheService(dbPath);

            var images = TestDataPaths.GetTestImageBatch(10);

            // Act - Concurrent writes
            var tasks = images.Select(async (imagePath, index) =>
            {
                var predictions = new List<LocationPrediction>
                {
                    new() { Latitude = index, Longitude = index, Probability = 0.5 }
                };

                await cacheService.CachePredictionsAsync(imagePath, predictions);
                return imagePath;
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert - Verify all writes succeeded
            foreach (var imagePath in images)
            {
                var retrieved = await cacheService.GetPredictionsAsync(imagePath);
                retrieved.Should().NotBeNull($"concurrent write should succeed for {Path.GetFileName(imagePath)}");
            }

            Log.Information("Concurrent cache access test passed: {Count} writes", images.Length);
        }

        [Fact]
        public async Task ConcurrentAccess_ReadWhileWriting_ShouldNotCorrupt()
        {
            // Arrange
            var dbPath = _dataFixture.GetCacheDatabasePath();
            var cacheService = new PredictionCacheService(dbPath);

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var predictions = new List<LocationPrediction>
            {
                new() { Latitude = 42.0, Longitude = 42.0, Probability = 0.9 }
            };

            // Initial write
            await cacheService.CachePredictionsAsync(testImagePath, predictions);

            // Act - Concurrent reads and writes
            var tasks = new List<Task>();

            // Start multiple read tasks
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await cacheService.GetPredictionsAsync(testImagePath);
                    result.Should().NotBeNull();
                }));
            }

            // Start some write tasks
            for (int i = 0; i < 5; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var newPredictions = new List<LocationPrediction>
                    {
                        new() { Latitude = index, Longitude = index, Probability = 0.5 }
                    };
                    await cacheService.CachePredictionsAsync(testImagePath, newPredictions);
                }));
            }

            // Assert - All operations should complete without exception
            await Task.WhenAll(tasks);

            var finalResult = await cacheService.GetPredictionsAsync(testImagePath);
            finalResult.Should().NotBeNull("database should not be corrupted by concurrent access");

            Log.Information("Concurrent read/write test passed");
        }
    }
}
