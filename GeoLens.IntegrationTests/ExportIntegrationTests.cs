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
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace GeoLens.IntegrationTests
{
    /// <summary>
    /// Integration tests for export functionality
    /// Tests: CSV export, PDF export, KML export, JSON export
    /// </summary>
    [Collection("PythonService")]
    public class ExportIntegrationTests : IClassFixture<TestDataFixture>
    {
        private readonly PythonServiceFixture _serviceFixture;
        private readonly TestDataFixture _dataFixture;

        public ExportIntegrationTests(PythonServiceFixture serviceFixture, TestDataFixture dataFixture)
        {
            _serviceFixture = serviceFixture;
            _dataFixture = dataFixture;

            Log.Information("ExportIntegrationTests initialized");
        }

        [Fact]
        public async Task ExportCSV_WithPredictions_ShouldCreateValidFile()
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
            var predictions = await apiClient.InferAsync(testImagePath, topK: 5);

            var exportService = new ExportService();
            var outputPath = _dataFixture.GetExportFilePath("csv");

            var exportData = new PredictionResult
            {
                OriginalImagePath = testImagePath,
                Predictions = predictions.ToList(),
                ExifMetadata = new ExifMetadata()
            };

            // Act
            await exportService.ExportToCsvAsync(new[] { exportData }, outputPath);

            // Assert
            File.Exists(outputPath).Should().BeTrue("CSV file should be created");

            var csvContent = await File.ReadAllTextAsync(outputPath);
            csvContent.Should().NotBeNullOrEmpty();

            // Verify CSV structure
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.Should().HaveCountGreaterOrEqualTo(2, "should have header + at least one data row");

            var header = lines[0];
            header.Should().Contain("Latitude", "CSV should have Latitude column");
            header.Should().Contain("Longitude", "CSV should have Longitude column");
            header.Should().Contain("Probability", "CSV should have Probability column");

            Log.Information("CSV export verified: {Lines} lines, {Size} bytes",
                lines.Length, csvContent.Length);
        }

        [Fact]
        public async Task ExportCSV_MultipleImages_ShouldContainAllPredictions()
        {
            // Arrange
            if (!_serviceFixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            var images = TestDataPaths.GetTestImageBatch(3);
            var apiClient = new GeoCLIPApiClient(_serviceFixture.BaseUrl);
            var exportService = new ExportService();

            var exportDataList = new List<PredictionResult>();

            foreach (var imagePath in images)
            {
                var predictions = await apiClient.InferAsync(imagePath, topK: 3);
                exportDataList.Add(new PredictionResult
                {
                    OriginalImagePath = imagePath,
                    Predictions = predictions.ToList(),
                    ExifMetadata = new ExifMetadata()
                });
            }

            var outputPath = _dataFixture.GetExportFilePath("csv");

            // Act
            await exportService.ExportToCsvAsync(exportDataList, outputPath);

            // Assert
            var csvContent = await File.ReadAllTextAsync(outputPath);
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Header + (3 images * 3 predictions) = 10 lines minimum
            lines.Should().HaveCountGreaterOrEqualTo(10,
                "CSV should contain all predictions from all images");

            Log.Information("Multi-image CSV export: {Images} images, {Lines} total lines",
                images.Length, lines.Length);
        }

        [Fact]
        public async Task ExportPDF_WithPredictions_ShouldCreateValidFile()
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
            var predictions = await apiClient.InferAsync(testImagePath, topK: 5);

            var exportService = new ExportService();
            var outputPath = _dataFixture.GetExportFilePath("pdf");

            var exportData = new PredictionResult
            {
                OriginalImagePath = testImagePath,
                Predictions = predictions.ToList(),
                ExifMetadata = new ExifMetadata
                {
                    FileName = Path.GetFileName(testImagePath),
                    FileSize = new FileInfo(testImagePath).Length
                }
            };

            // Act
            await exportService.ExportToPdfAsync(new[] { exportData }, outputPath);

            // Assert
            File.Exists(outputPath).Should().BeTrue("PDF file should be created");

            var fileInfo = new FileInfo(outputPath);
            fileInfo.Length.Should().BeGreaterThan(0, "PDF should have content");

            // Verify PDF signature (magic bytes)
            var pdfHeader = new byte[4];
            using (var fs = File.OpenRead(outputPath))
            {
                await fs.ReadAsync(pdfHeader, 0, 4);
            }

            Encoding.ASCII.GetString(pdfHeader).Should().Be("%PDF",
                "file should have valid PDF header");

            Log.Information("PDF export verified: {Size} bytes", fileInfo.Length);
        }

        [Fact]
        public async Task ExportKML_WithPredictions_ShouldCreateValidFile()
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
            var predictions = await apiClient.InferAsync(testImagePath, topK: 5);

            var exportService = new ExportService();
            var outputPath = _dataFixture.GetExportFilePath("kml");

            var exportData = new PredictionResult
            {
                OriginalImagePath = testImagePath,
                Predictions = predictions.ToList(),
                ExifMetadata = new ExifMetadata()
            };

            // Act
            await exportService.ExportToKmlAsync(new[] { exportData }, outputPath);

            // Assert
            File.Exists(outputPath).Should().BeTrue("KML file should be created");

            var kmlContent = await File.ReadAllTextAsync(outputPath);
            kmlContent.Should().NotBeNullOrEmpty();

            // Verify KML is valid XML
            var kmlDoc = XDocument.Parse(kmlContent);
            kmlDoc.Should().NotBeNull("KML should be valid XML");

            // Verify KML structure
            var kmlNamespace = kmlDoc.Root?.Name.Namespace ?? XNamespace.None;
            kmlDoc.Root?.Name.LocalName.Should().Be("kml", "root element should be 'kml'");

            var placemarks = kmlDoc.Descendants(kmlNamespace + "Placemark");
            placemarks.Should().NotBeEmpty("KML should contain Placemark elements");

            var coordinates = kmlDoc.Descendants(kmlNamespace + "coordinates");
            coordinates.Should().NotBeEmpty("KML should contain coordinate elements");

            Log.Information("KML export verified: {Placemarks} placemarks, {Size} bytes",
                placemarks.Count(), kmlContent.Length);
        }

        [Fact]
        public async Task ExportJSON_WithPredictions_ShouldCreateValidFile()
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
            var predictions = await apiClient.InferAsync(testImagePath, topK: 5);

            var exportService = new ExportService();
            var outputPath = _dataFixture.GetExportFilePath("json");

            var exportData = new PredictionResult
            {
                OriginalImagePath = testImagePath,
                Predictions = predictions.ToList(),
                ExifMetadata = new ExifMetadata()
            };

            // Act
            await exportService.ExportToJsonAsync(new[] { exportData }, outputPath);

            // Assert
            File.Exists(outputPath).Should().BeTrue("JSON file should be created");

            var jsonContent = await File.ReadAllTextAsync(outputPath);
            jsonContent.Should().NotBeNullOrEmpty();

            // Verify JSON is parseable
            var jsonData = System.Text.Json.JsonDocument.Parse(jsonContent);
            jsonData.Should().NotBeNull("JSON should be valid");

            Log.Information("JSON export verified: {Size} bytes", jsonContent.Length);
        }

        [Fact]
        public async Task ExportKML_MultipleImages_ShouldGroupByImage()
        {
            // Arrange
            if (!_serviceFixture.IsServiceAvailable)
            {
                Log.Warning("Skipping test - Python service not available");
                return;
            }

            var images = TestDataPaths.GetTestImageBatch(2);
            var apiClient = new GeoCLIPApiClient(_serviceFixture.BaseUrl);
            var exportService = new ExportService();

            var exportDataList = new List<PredictionResult>();

            foreach (var imagePath in images)
            {
                var predictions = await apiClient.InferAsync(imagePath, topK: 3);
                exportDataList.Add(new PredictionResult
                {
                    OriginalImagePath = imagePath,
                    Predictions = predictions.ToList(),
                    ExifMetadata = new ExifMetadata()
                });
            }

            var outputPath = _dataFixture.GetExportFilePath("kml");

            // Act
            await exportService.ExportToKmlAsync(exportDataList, outputPath);

            // Assert
            var kmlContent = await File.ReadAllTextAsync(outputPath);
            var kmlDoc = XDocument.Parse(kmlContent);

            var kmlNamespace = kmlDoc.Root?.Name.Namespace ?? XNamespace.None;
            var folders = kmlDoc.Descendants(kmlNamespace + "Folder");

            // Should have folders for organizing by image
            folders.Should().NotBeEmpty("KML should organize predictions by image in folders");

            Log.Information("Multi-image KML export: {Folders} folders", folders.Count());
        }

        [Fact]
        public async Task ExportAllFormats_ShouldSucceedForSameData()
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
            var predictions = await apiClient.InferAsync(testImagePath, topK: 5);

            var exportService = new ExportService();

            var exportData = new PredictionResult
            {
                OriginalImagePath = testImagePath,
                Predictions = predictions.ToList(),
                ExifMetadata = new ExifMetadata()
            };

            var csvPath = _dataFixture.GetExportFilePath("csv");
            var jsonPath = _dataFixture.GetExportFilePath("json");
            var pdfPath = _dataFixture.GetExportFilePath("pdf");
            var kmlPath = _dataFixture.GetExportFilePath("kml");

            // Act - Export to all formats
            await exportService.ExportToCsvAsync(new[] { exportData }, csvPath);
            await exportService.ExportToJsonAsync(new[] { exportData }, jsonPath);
            await exportService.ExportToPdfAsync(new[] { exportData }, pdfPath);
            await exportService.ExportToKmlAsync(new[] { exportData }, kmlPath);

            // Assert
            File.Exists(csvPath).Should().BeTrue("CSV export should succeed");
            File.Exists(jsonPath).Should().BeTrue("JSON export should succeed");
            File.Exists(pdfPath).Should().BeTrue("PDF export should succeed");
            File.Exists(kmlPath).Should().BeTrue("KML export should succeed");

            Log.Information("All export formats verified: CSV, JSON, PDF, KML");
        }

        [Fact]
        public async Task ExportCSV_WithClusteringData_ShouldIncludeBoost()
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
            var predictions = await apiClient.InferAsync(testImagePath, topK: 10);

            var clusterAnalyzer = new GeographicClusterAnalyzer();
            var clusteredPredictions = clusterAnalyzer.AnalyzePredictions(predictions);

            var exportService = new ExportService();
            var outputPath = _dataFixture.GetExportFilePath("csv");

            var exportData = new PredictionResult
            {
                OriginalImagePath = testImagePath,
                Predictions = clusteredPredictions.ToList(),
                ExifMetadata = new ExifMetadata()
            };

            // Act
            await exportService.ExportToCsvAsync(new[] { exportData }, outputPath);

            // Assert
            var csvContent = await File.ReadAllTextAsync(outputPath);
            var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var header = lines[0];

            // Should include clustering-related columns
            header.Should().Contain("ClusteringBoost", "CSV should include clustering boost data");
            header.Should().Contain("AdjustedProbability", "CSV should include adjusted probability");

            Log.Information("CSV export with clustering data verified");
        }
    }
}
