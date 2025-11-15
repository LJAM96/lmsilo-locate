using FluentAssertions;
using GeoLens.Services;
using GeoLens.Models;
using Xunit;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace GeoLens.Tests.Services;

/// <summary>
/// Unit tests for ExifMetadataExtractor - GPS and metadata extraction from images
/// </summary>
public class ExifMetadataExtractorTests : IDisposable
{
    private readonly ExifMetadataExtractor _extractor;
    private readonly List<string> _testFiles;

    public ExifMetadataExtractorTests()
    {
        _extractor = new ExifMetadataExtractor();
        _testFiles = new List<string>();
    }

    [Fact]
    public async Task ExtractGpsDataAsync_WithNonExistentFile_ShouldReturnNull()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.jpg");

        // Act
        var result = await _extractor.ExtractGpsDataAsync(nonExistentFile);

        // Assert
        result.Should().BeNull("file does not exist");
    }

    [Fact]
    public async Task ExtractGpsDataAsync_WithEmptyPath_ShouldReturnNull()
    {
        // Act
        var result = await _extractor.ExtractGpsDataAsync(string.Empty);

        // Assert
        result.Should().BeNull("path is empty");
    }

    [Fact]
    public async Task ExtractGpsDataAsync_WithNullPath_ShouldReturnNull()
    {
        // Act
        var result = await _extractor.ExtractGpsDataAsync(null!);

        // Assert
        result.Should().BeNull("path is null");
    }

    [Fact]
    public async Task ExtractGpsDataAsync_WithImageWithoutGps_ShouldReturnNoGpsData()
    {
        // Arrange
        var testFile = CreateTestImageWithoutGps();

        // Act
        var result = await _extractor.ExtractGpsDataAsync(testFile);

        // Assert
        result.Should().NotBeNull("should return an ExifGpsData object");
        result!.HasGps.Should().BeFalse("test image has no GPS data");
        result.Latitude.Should().Be(0, "default latitude should be 0");
        result.Longitude.Should().Be(0, "default longitude should be 0");
    }

    [Theory]
    [InlineData("test.jpg")]
    [InlineData("test.jpeg")]
    [InlineData("test.JPG")]
    [InlineData("test.JPEG")]
    public async Task ExtractGpsDataAsync_WithValidJpegExtension_ShouldNotThrow(string filename)
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), filename);
        File.WriteAllBytes(testFile, CreateMinimalJpegBytes());
        _testFiles.Add(testFile);

        // Act
        Func<Task> act = async () => await _extractor.ExtractGpsDataAsync(testFile);

        // Assert
        await act.Should().NotThrowAsync("valid JPEG files should be processable");
    }

    [Theory]
    [InlineData("test.png")]
    [InlineData("test.bmp")]
    [InlineData("test.gif")]
    public async Task ExtractGpsDataAsync_WithOtherImageFormats_ShouldNotThrow(string filename)
    {
        // Arrange
        var testFile = Path.Combine(Path.GetTempPath(), filename);
        File.WriteAllBytes(testFile, CreateMinimalJpegBytes());
        _testFiles.Add(testFile);

        // Act
        Func<Task> act = async () => await _extractor.ExtractGpsDataAsync(testFile);

        // Assert
        await act.Should().NotThrowAsync("other image formats should be handled gracefully");
    }

    [Fact]
    public void ExifGpsData_LatitudeFormatted_ShouldFormatNorthCorrectly()
    {
        // Arrange
        var gpsData = new ExifGpsData
        {
            Latitude = 48.8566,
            Longitude = 2.3522,
            HasGps = true
        };

        // Act
        var formatted = gpsData.LatitudeFormatted;

        // Assert
        formatted.Should().Contain("N", "positive latitude is North");
        formatted.Should().Contain("48.8566", "should contain the latitude value");
    }

    [Fact]
    public void ExifGpsData_LatitudeFormatted_ShouldFormatSouthCorrectly()
    {
        // Arrange
        var gpsData = new ExifGpsData
        {
            Latitude = -33.8688,
            Longitude = 151.2093,
            HasGps = true
        };

        // Act
        var formatted = gpsData.LatitudeFormatted;

        // Assert
        formatted.Should().Contain("S", "negative latitude is South");
        formatted.Should().Contain("33.8688", "should contain the absolute latitude value");
    }

    [Fact]
    public void ExifGpsData_LongitudeFormatted_ShouldFormatEastCorrectly()
    {
        // Arrange
        var gpsData = new ExifGpsData
        {
            Latitude = 35.6762,
            Longitude = 139.6503,
            HasGps = true
        };

        // Act
        var formatted = gpsData.LongitudeFormatted;

        // Assert
        formatted.Should().Contain("E", "positive longitude is East");
        formatted.Should().Contain("139.6503", "should contain the longitude value");
    }

    [Fact]
    public void ExifGpsData_LongitudeFormatted_ShouldFormatWestCorrectly()
    {
        // Arrange
        var gpsData = new ExifGpsData
        {
            Latitude = 40.7128,
            Longitude = -74.0060,
            HasGps = true
        };

        // Act
        var formatted = gpsData.LongitudeFormatted;

        // Assert
        formatted.Should().Contain("W", "negative longitude is West");
        formatted.Should().Contain("74.0060", "should contain the absolute longitude value");
    }

    [Fact]
    public void ExifGpsData_Coordinates_ShouldCombineBothFormatted()
    {
        // Arrange
        var gpsData = new ExifGpsData
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            HasGps = true
        };

        // Act
        var coordinates = gpsData.Coordinates;

        // Assert
        coordinates.Should().Contain("51.5074° N", "should contain formatted latitude");
        coordinates.Should().Contain("0.1278° W", "should contain formatted longitude");
        coordinates.Should().Contain(",", "should separate with comma");
    }

    [Fact]
    public void ExifGpsData_WithAltitude_ShouldStoreCorrectly()
    {
        // Arrange & Act
        var gpsData = new ExifGpsData
        {
            Latitude = 48.8566,
            Longitude = 2.3522,
            HasGps = true,
            Altitude = 35.5
        };

        // Assert
        gpsData.Altitude.Should().Be(35.5, "altitude should be stored");
    }

    [Fact]
    public void ExifGpsData_WithLocationName_ShouldStoreCorrectly()
    {
        // Arrange & Act
        var gpsData = new ExifGpsData
        {
            Latitude = 48.8566,
            Longitude = 2.3522,
            HasGps = true,
            LocationName = "Paris, France"
        };

        // Assert
        gpsData.LocationName.Should().Be("Paris, France", "location name should be stored");
    }

    private string CreateTestImageWithoutGps()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_no_gps_{Guid.NewGuid()}.jpg");
        File.WriteAllBytes(path, CreateMinimalJpegBytes());
        _testFiles.Add(path);
        return path;
    }

    private byte[] CreateMinimalJpegBytes()
    {
        // Minimal JPEG file structure: SOI marker + APP0 marker + EOI marker
        return new byte[]
        {
            0xFF, 0xD8,       // SOI (Start of Image)
            0xFF, 0xE0,       // APP0 marker
            0x00, 0x10,       // APP0 length
            0x4A, 0x46, 0x49, 0x46, 0x00, // "JFIF\0"
            0x01, 0x01,       // Version 1.1
            0x00,             // Density units (none)
            0x00, 0x01,       // X density
            0x00, 0x01,       // Y density
            0x00, 0x00,       // Thumbnail dimensions
            0xFF, 0xD9        // EOI (End of Image)
        };
    }

    public void Dispose()
    {
        // Clean up test image files
        foreach (var file in _testFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { /* ignore cleanup errors */ }
            }
        }
    }
}
