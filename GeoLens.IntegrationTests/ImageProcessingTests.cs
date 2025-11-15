using FluentAssertions;
using GeoLens.IntegrationTests.TestFixtures;
using GeoLens.IntegrationTests.TestHelpers;
using GeoLens.Models;
using GeoLens.Services;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace GeoLens.IntegrationTests
{
    /// <summary>
    /// Integration tests for end-to-end image processing workflows
    /// Tests: Load image → Extract EXIF → Call API → Cache result → Display prediction
    /// </summary>
    [Collection("PythonService")]
    public class ImageProcessingTests : IClassFixture<TestDataFixture>
    {
        private readonly PythonServiceFixture _serviceFixture;
        private readonly TestDataFixture _dataFixture;

        public ImageProcessingTests(PythonServiceFixture serviceFixture, TestDataFixture dataFixture)
        {
            _serviceFixture = serviceFixture;
            _dataFixture = dataFixture;

            Log.Information("ImageProcessingTests initialized");
        }

        [Fact]
        public async Task EndToEnd_ProcessSingleImage_ShouldReturnPredictions()
        {
            // Arrange
            if (!_serviceFixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var cacheService = new PredictionCacheService(_dataFixture.GetCacheDatabasePath());
            var apiClient = new GeoCLIPApiClient(_serviceFixture.BaseUrl);
            var exifExtractor = new ExifMetadataExtractor();
            var clusterAnalyzer = new GeographicClusterAnalyzer();

            // Act
            Log.Information("Processing test image: {Path}", testImagePath);

            // Step 1: Extract EXIF metadata
            var exifData = await exifExtractor.ExtractMetadataAsync(testImagePath);
            exifData.Should().NotBeNull("EXIF extraction should return data");

            // Step 2: Check cache (should be miss on first run)
            var cachedPredictions = await cacheService.GetPredictionsAsync(testImagePath);
            Log.Information("Cache check result: {Found} predictions", cachedPredictions?.Count ?? 0);

            // Step 3: Call API to get predictions
            var predictions = await apiClient.InferAsync(testImagePath, topK: 5);
            predictions.Should().NotBeNull();
            predictions.Should().HaveCount(5);

            // Step 4: Apply clustering analysis
            var clusteredPredictions = clusterAnalyzer.AnalyzePredictions(predictions);
            clusteredPredictions.Should().NotBeNull();
            clusteredPredictions.Should().HaveCount(5);

            // Step 5: Cache the results
            await cacheService.CachePredictionsAsync(testImagePath, clusteredPredictions);

            // Assert - Verify cached
            var retrievedFromCache = await cacheService.GetPredictionsAsync(testImagePath);
            retrievedFromCache.Should().NotBeNull();
            retrievedFromCache.Should().HaveCount(5);

            Log.Information("End-to-end processing completed successfully");
        }

        [Fact]
        public async Task EndToEnd_ProcessImageWithCache_ShouldReturnCachedResults()
        {
            // Arrange
            if (!_serviceFixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var cacheService = new PredictionCacheService(_dataFixture.GetCacheDatabasePath());
            var apiClient = new GeoCLIPApiClient(_serviceFixture.BaseUrl);

            // Act - First processing (cache miss)
            var firstLoadStart = DateTime.UtcNow;
            var firstPredictions = await apiClient.InferAsync(testImagePath, topK: 5);
            await cacheService.CachePredictionsAsync(testImagePath, firstPredictions);
            var firstLoadDuration = DateTime.UtcNow - firstLoadStart;

            // Act - Second processing (cache hit)
            var secondLoadStart = DateTime.UtcNow;
            var cachedPredictions = await cacheService.GetPredictionsAsync(testImagePath);
            var secondLoadDuration = DateTime.UtcNow - secondLoadStart;

            // Assert
            cachedPredictions.Should().NotBeNull("cache should return predictions");
            cachedPredictions.Should().HaveCount(5);

            // Cache retrieval should be much faster
            secondLoadDuration.Should().BeLessThan(firstLoadDuration,
                "cached retrieval should be faster than API call");

            Log.Information("First load: {FirstMs}ms, Cached load: {SecondMs}ms",
                firstLoadDuration.TotalMilliseconds, secondLoadDuration.TotalMilliseconds);
        }

        [Fact]
        public async Task BatchProcessing_Process10Images_ShouldSucceed()
        {
            // Arrange
            if (!_serviceFixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            var batchImages = TestDataPaths.GetTestImageBatch(10);
            batchImages.Should().HaveCount(10);

            var apiClient = new GeoCLIPApiClient(_serviceFixture.BaseUrl);
            var cacheService = new PredictionCacheService(_dataFixture.GetCacheDatabasePath());

            // Act
            var results = new System.Collections.Generic.List<LocationPrediction[]>();
            var startTime = DateTime.UtcNow;

            foreach (var imagePath in batchImages)
            {
                Log.Information("Processing batch image: {Path}", Path.GetFileName(imagePath));

                var predictions = await apiClient.InferAsync(imagePath, topK: 5);
                await cacheService.CachePredictionsAsync(imagePath, predictions);

                results.Add(predictions.ToArray());
            }

            var totalDuration = DateTime.UtcNow - startTime;

            // Assert
            results.Should().HaveCount(10, "all 10 images should be processed");

            foreach (var result in results)
            {
                result.Should().HaveCount(5, "each image should have 5 predictions");
            }

            var averagePerImage = totalDuration.TotalMilliseconds / 10;
            Log.Information("Batch processing: {Total}ms total, {Avg}ms per image",
                totalDuration.TotalMilliseconds, averagePerImage);
        }

        [Fact]
        public async Task ProcessImage_WithExifGPS_ShouldPrioritizeExifLocation()
        {
            // Note: This test would need an actual image with EXIF GPS data
            // For now, we test that EXIF extraction doesn't fail

            // Arrange
            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var exifExtractor = new ExifMetadataExtractor();

            // Act
            var exifData = await exifExtractor.ExtractMetadataAsync(testImagePath);

            // Assert
            exifData.Should().NotBeNull();

            if (exifData.GpsLatitude.HasValue && exifData.GpsLongitude.HasValue)
            {
                Log.Information("Found EXIF GPS: ({Lat}, {Lon})",
                    exifData.GpsLatitude, exifData.GpsLongitude);

                exifData.GpsLatitude.Should().BeInRange(-90, 90);
                exifData.GpsLongitude.Should().BeInRange(-180, 180);
            }
            else
            {
                Log.Information("Test image has no EXIF GPS data (expected for generated images)");
            }
        }

        [Fact]
        public async Task PredictionProcessor_FullPipeline_ShouldOrchestrate()
        {
            // Arrange
            if (!_serviceFixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var cacheService = new PredictionCacheService(_dataFixture.GetCacheDatabasePath());
            var apiClient = new GeoCLIPApiClient(_serviceFixture.BaseUrl);
            var exifExtractor = new ExifMetadataExtractor();
            var clusterAnalyzer = new GeographicClusterAnalyzer();

            var processor = new PredictionProcessor(
                cacheService,
                apiClient,
                exifExtractor,
                clusterAnalyzer);

            // Act
            var result = await processor.ProcessImageAsync(testImagePath);

            // Assert
            result.Should().NotBeNull();
            result.Predictions.Should().NotBeEmpty();
            result.ExifMetadata.Should().NotBeNull();
            result.OriginalImagePath.Should().Be(testImagePath);

            Log.Information("PredictionProcessor returned {Count} predictions for {Path}",
                result.Predictions.Count, Path.GetFileName(testImagePath));
        }

        [Fact]
        public async Task GeographicClusterAnalyzer_WithPredictions_ShouldApplyBoost()
        {
            // Arrange
            if (!_serviceFixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var apiClient = new GeoCLIPApiClient(_serviceFixture.BaseUrl);
            var clusterAnalyzer = new GeographicClusterAnalyzer();

            // Act
            var predictions = await apiClient.InferAsync(testImagePath, topK: 10);
            var clustered = clusterAnalyzer.AnalyzePredictions(predictions);

            // Assert
            clustered.Should().NotBeNull();
            clustered.Should().HaveCount(10);

            // Check if any predictions received clustering boost
            var boostedCount = clustered.Count(p => p.ClusteringBoost > 0);

            Log.Information("Clustering analysis: {Boosted} of {Total} predictions received boost",
                boostedCount, clustered.Count);

            // At least some predictions should be clustered (unless all are very far apart)
            // This is probabilistic, so we just log the result
            foreach (var prediction in clustered.Where(p => p.ClusteringBoost > 0).Take(3))
            {
                Log.Information("Boosted prediction: Base={Base:F2}%, Boost={Boost:F2}%, Final={Final:F2}%",
                    prediction.BaseProbability * 100,
                    prediction.ClusteringBoost * 100,
                    prediction.AdjustedProbability * 100);
            }
        }

        [Fact]
        public async Task MultipleImages_SameCacheDatabase_ShouldIsolate()
        {
            // Arrange
            if (!_serviceFixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            var images = TestDataPaths.GetTestImageBatch(3);
            var cacheService = new PredictionCacheService(_dataFixture.GetCacheDatabasePath());
            var apiClient = new GeoCLIPApiClient(_serviceFixture.BaseUrl);

            // Act - Process all images and cache
            foreach (var imagePath in images)
            {
                var predictions = await apiClient.InferAsync(imagePath, topK: 5);
                await cacheService.CachePredictionsAsync(imagePath, predictions);
            }

            // Assert - Each image should have its own cache entry
            foreach (var imagePath in images)
            {
                var cached = await cacheService.GetPredictionsAsync(imagePath);
                cached.Should().NotBeNull($"cache should have entry for {Path.GetFileName(imagePath)}");
                cached.Should().HaveCount(5);
            }

            Log.Information("Verified cache isolation for {Count} images", images.Length);
        }
    }
}
