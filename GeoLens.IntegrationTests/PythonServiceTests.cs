using FluentAssertions;
using GeoLens.IntegrationTests.TestFixtures;
using GeoLens.IntegrationTests.TestHelpers;
using GeoLens.Services;
using Serilog;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GeoLens.IntegrationTests
{
    /// <summary>
    /// Integration tests for Python service lifecycle and communication
    /// </summary>
    [Collection("PythonService")]
    public class PythonServiceTests : IDisposable
    {
        private readonly PythonServiceFixture _fixture;
        private readonly HttpClient _httpClient;

        public PythonServiceTests(PythonServiceFixture fixture)
        {
            _fixture = fixture;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_fixture.BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Log.Information("PythonServiceTests initialized");
        }

        [Fact]
        public void ServiceShouldBeRunning()
        {
            // Arrange & Act & Assert
            _fixture.IsServiceAvailable.Should().BeTrue(
                "Python service should be running after fixture initialization");

            _fixture.RuntimeManager.IsRunning.Should().BeTrue(
                "RuntimeManager should report service as running");
        }

        [Fact]
        public async Task HealthEndpoint_ShouldRespond_WithSuccess()
        {
            // Arrange
            if (!_fixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            // Act
            var response = await _httpClient.GetAsync("/health");

            // Assert
            response.Should().NotBeNull();
            response.IsSuccessStatusCode.Should().BeTrue(
                "health endpoint should return success status code");

            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();

            Log.Information("Health check response: {Content}", content);
        }

        [Fact]
        public async Task HealthEndpoint_ShouldReturn_ValidJson()
        {
            // Arrange
            if (!_fixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            // Act
            var response = await _httpClient.GetAsync("/health");
            var healthData = await response.Content.ReadFromJsonAsync<HealthResponse>();

            // Assert
            healthData.Should().NotBeNull();
            healthData!.Status.Should().NotBeNullOrEmpty();

            Log.Information("Health status: {Status}", healthData.Status);
        }

        [Fact]
        public async Task InferEndpoint_WithTestImage_ShouldReturnPredictions()
        {
            // Arrange
            if (!_fixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            // Act
            using var formData = new MultipartFormDataContent();
            using var imageStream = File.OpenRead(testImagePath);
            using var streamContent = new StreamContent(imageStream);
            formData.Add(streamContent, "file", Path.GetFileName(testImagePath));

            var response = await _httpClient.PostAsync("/infer", formData);

            // Assert
            response.Should().NotBeNull();
            response.IsSuccessStatusCode.Should().BeTrue(
                "infer endpoint should return success for valid image");

            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();

            Log.Information("Infer response (first 200 chars): {Content}",
                content.Substring(0, Math.Min(200, content.Length)));
        }

        [Fact]
        public async Task InferEndpoint_WithTestImage_ShouldReturnValidPredictionStructure()
        {
            // Arrange
            if (!_fixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            // Act
            using var formData = new MultipartFormDataContent();
            using var imageStream = File.OpenRead(testImagePath);
            using var streamContent = new StreamContent(imageStream);
            formData.Add(streamContent, "file", Path.GetFileName(testImagePath));

            var response = await _httpClient.PostAsync("/infer", formData);
            var predictionData = await response.Content.ReadFromJsonAsync<PredictionResponse>();

            // Assert
            predictionData.Should().NotBeNull();
            predictionData!.Predictions.Should().NotBeNull();
            predictionData.Predictions.Should().NotBeEmpty(
                "predictions array should contain at least one result");

            var firstPrediction = predictionData.Predictions[0];
            firstPrediction.Latitude.Should().BeInRange(-90, 90,
                "latitude should be valid geographic coordinate");
            firstPrediction.Longitude.Should().BeInRange(-180, 180,
                "longitude should be valid geographic coordinate");
            firstPrediction.Probability.Should().BeInRange(0, 1,
                "probability should be between 0 and 1");

            Log.Information("First prediction: ({Lat}, {Lon}) with probability {Prob}",
                firstPrediction.Latitude, firstPrediction.Longitude, firstPrediction.Probability);
        }

        [Fact]
        public async Task ServiceRestart_ShouldSucceed()
        {
            // Arrange
            if (!_fixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            // Act
            Log.Information("Testing service restart...");
            var restartSuccessful = await _fixture.RestartServiceAsync();

            // Assert
            restartSuccessful.Should().BeTrue("service restart should succeed");
            _fixture.IsServiceAvailable.Should().BeTrue("service should be available after restart");

            // Verify service responds after restart
            var response = await _httpClient.GetAsync("/health");
            response.IsSuccessStatusCode.Should().BeTrue(
                "health check should succeed after restart");

            Log.Information("Service restart test completed successfully");
        }

        [Fact]
        public async Task GeoCLIPApiClient_ShouldDetectHardware()
        {
            // Arrange
            if (!_fixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            var hardwareService = new HardwareDetectionService();
            var detectedDevice = hardwareService.DetectBestDevice();

            // Act & Assert
            detectedDevice.Should().NotBeNullOrEmpty("hardware detection should return a device type");
            detectedDevice.Should().BeOneOf("cuda", "rocm", "cpu",
                "detected device should be one of the supported types");

            Log.Information("Detected hardware device: {Device}", detectedDevice);
        }

        [Fact]
        public async Task GeoCLIPApiClient_WithValidImage_ShouldReturnMultiplePredictions()
        {
            // Arrange
            if (!_fixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            TestDataPaths.EnsureTestDataExists();
            var testImagePath = TestDataPaths.GetFirstTestImage();

            var apiClient = new GeoCLIPApiClient(_fixture.BaseUrl);

            // Act
            var predictions = await apiClient.InferAsync(testImagePath, topK: 5);

            // Assert
            predictions.Should().NotBeNull();
            predictions.Should().HaveCount(5, "requested top 5 predictions");

            foreach (var prediction in predictions)
            {
                prediction.Latitude.Should().BeInRange(-90, 90);
                prediction.Longitude.Should().BeInRange(-180, 180);
                prediction.Probability.Should().BeGreaterThan(0);
            }

            Log.Information("Received {Count} predictions from GeoCLIPApiClient", predictions.Count);
        }

        [Fact]
        public async Task GeoCLIPApiClient_WithInvalidImage_ShouldHandleGracefully()
        {
            // Arrange
            if (!_fixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            var invalidImagePath = Path.Combine(Path.GetTempPath(), "invalid-image.txt");
            File.WriteAllText(invalidImagePath, "This is not an image");

            var apiClient = new GeoCLIPApiClient(_fixture.BaseUrl);

            // Act
            Func<Task> act = async () => await apiClient.InferAsync(invalidImagePath);

            // Assert
            await act.Should().ThrowAsync<Exception>(
                "API client should throw exception for invalid image");

            // Cleanup
            File.Delete(invalidImagePath);

            Log.Information("Invalid image handling test completed");
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        // Helper DTOs for deserialization
        private class HealthResponse
        {
            public string Status { get; set; } = string.Empty;
        }

        private class PredictionResponse
        {
            public PredictionItem[] Predictions { get; set; } = Array.Empty<PredictionItem>();
        }

        private class PredictionItem
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double Probability { get; set; }
        }
    }
}
