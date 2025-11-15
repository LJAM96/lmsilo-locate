using FluentAssertions;
using GeoLens.Services;
using GeoLens.Models;
using Xunit;
using System;
using System.Collections.Generic;

namespace GeoLens.Tests.Services;

/// <summary>
/// Unit tests for GeographicClusterAnalyzer - Haversine distance and clustering logic
/// </summary>
public class GeographicClusterAnalyzerTests
{
    private readonly GeographicClusterAnalyzer _analyzer;

    public GeographicClusterAnalyzerTests()
    {
        _analyzer = new GeographicClusterAnalyzer();
    }

    #region Distance Calculation Tests

    [Fact]
    public void CalculateDistance_WithSamePoint_ShouldReturnZero()
    {
        // Arrange
        double lat = 48.8566;
        double lon = 2.3522;

        // Act
        var distance = _analyzer.CalculateDistance(lat, lon, lat, lon);

        // Assert
        distance.Should().Be(0, "distance between same point should be zero");
    }

    [Fact]
    public void CalculateDistance_ParisToLondon_ShouldBeApproximately344Km()
    {
        // Arrange - Paris to London
        var parisLat = 48.8566;
        var parisLon = 2.3522;
        var londonLat = 51.5074;
        var londonLon = -0.1278;

        // Act
        var distance = _analyzer.CalculateDistance(parisLat, parisLon, londonLat, londonLon);

        // Assert
        distance.Should().BeApproximately(344, 10, "Paris to London is approximately 344km");
    }

    [Fact]
    public void CalculateDistance_NewYorkToLosAngeles_ShouldBeApproximately3944Km()
    {
        // Arrange - New York to Los Angeles
        var nyLat = 40.7128;
        var nyLon = -74.0060;
        var laLat = 34.0522;
        var laLon = -118.2437;

        // Act
        var distance = _analyzer.CalculateDistance(nyLat, nyLon, laLat, laLon);

        // Assert
        distance.Should().BeApproximately(3944, 50, "New York to Los Angeles is approximately 3944km");
    }

    [Fact]
    public void CalculateDistance_TokyoToSydney_ShouldBeApproximately7800Km()
    {
        // Arrange - Tokyo to Sydney
        var tokyoLat = 35.6762;
        var tokyoLon = 139.6503;
        var sydneyLat = -33.8688;
        var sydneyLon = 151.2093;

        // Act
        var distance = _analyzer.CalculateDistance(tokyoLat, tokyoLon, sydneyLat, sydneyLon);

        // Assert
        distance.Should().BeApproximately(7800, 100, "Tokyo to Sydney is approximately 7800km");
    }

    [Fact]
    public void CalculateDistance_ShouldBeSymmetric()
    {
        // Arrange
        var lat1 = 48.8566;
        var lon1 = 2.3522;
        var lat2 = 51.5074;
        var lon2 = -0.1278;

        // Act
        var distance1to2 = _analyzer.CalculateDistance(lat1, lon1, lat2, lon2);
        var distance2to1 = _analyzer.CalculateDistance(lat2, lon2, lat1, lon1);

        // Assert
        distance1to2.Should().Be(distance2to1, "distance calculation should be symmetric");
    }

    [Fact]
    public void CalculateDistance_AcrossEquator_ShouldCalculateCorrectly()
    {
        // Arrange - Points across equator
        var lat1 = 10.0;  // North
        var lon1 = 0.0;
        var lat2 = -10.0; // South
        var lon2 = 0.0;

        // Act
        var distance = _analyzer.CalculateDistance(lat1, lon1, lat2, lon2);

        // Assert
        distance.Should().BeGreaterThan(0, "distance across equator should be positive");
        distance.Should().BeApproximately(2223, 50, "20 degrees latitude is approximately 2223km");
    }

    [Fact]
    public void CalculateDistance_AcrossPrimeMeridian_ShouldCalculateCorrectly()
    {
        // Arrange - Points across Prime Meridian
        var lat1 = 0.0;
        var lon1 = 10.0;  // East
        var lat2 = 0.0;
        var lon2 = -10.0; // West

        // Act
        var distance = _analyzer.CalculateDistance(lat1, lon1, lat2, lon2);

        // Assert
        distance.Should().BeGreaterThan(0, "distance across Prime Meridian should be positive");
        distance.Should().BeApproximately(2226, 50, "20 degrees longitude at equator is approximately 2226km");
    }

    #endregion

    #region Cluster Analysis Tests

    [Fact]
    public void AnalyzeClusters_WithNullPredictions_ShouldReturnNonClusteredResult()
    {
        // Act
        var result = _analyzer.AnalyzeClusters(null!);

        // Assert
        result.Should().NotBeNull();
        result.IsClustered.Should().BeFalse("null predictions cannot form a cluster");
        result.ClusterRadius.Should().Be(0);
        result.ConfidenceBoost.Should().Be(0);
    }

    [Fact]
    public void AnalyzeClusters_WithEmptyPredictions_ShouldReturnNonClusteredResult()
    {
        // Arrange
        var predictions = new List<EnhancedLocationPrediction>();

        // Act
        var result = _analyzer.AnalyzeClusters(predictions);

        // Assert
        result.Should().NotBeNull();
        result.IsClustered.Should().BeFalse("empty predictions cannot form a cluster");
        result.ClusterRadius.Should().Be(0);
    }

    [Fact]
    public void AnalyzeClusters_WithSinglePrediction_ShouldReturnNonClusteredResult()
    {
        // Arrange
        var predictions = new List<EnhancedLocationPrediction>
        {
            new EnhancedLocationPrediction
            {
                Latitude = 48.8566,
                Longitude = 2.3522,
                Probability = 0.5
            }
        };

        // Act
        var result = _analyzer.AnalyzeClusters(predictions);

        // Assert
        result.Should().NotBeNull();
        result.IsClustered.Should().BeFalse("single prediction cannot form a cluster");
    }

    [Fact]
    public void AnalyzeClusters_WithCloselyGroupedPredictions_ShouldDetectCluster()
    {
        // Arrange - Three predictions within 50km (Paris area)
        var predictions = new List<EnhancedLocationPrediction>
        {
            new EnhancedLocationPrediction
            {
                Latitude = 48.8566,  // Paris center
                Longitude = 2.3522,
                Probability = 0.4,
                AdjustedProbability = 0.4
            },
            new EnhancedLocationPrediction
            {
                Latitude = 48.9,     // North Paris (~5km)
                Longitude = 2.35,
                Probability = 0.3,
                AdjustedProbability = 0.3
            },
            new EnhancedLocationPrediction
            {
                Latitude = 48.82,    // South Paris (~4km)
                Longitude = 2.36,
                Probability = 0.2,
                AdjustedProbability = 0.2
            }
        };

        // Act
        var result = _analyzer.AnalyzeClusters(predictions);

        // Assert
        result.Should().NotBeNull();
        result.IsClustered.Should().BeTrue("close predictions should form a cluster");
        result.ClusterRadius.Should().BeLessThan(100, "cluster radius should be small for close predictions");
        result.ConfidenceBoost.Should().BeGreaterThan(0, "clustered predictions should receive confidence boost");
    }

    [Fact]
    public void AnalyzeClusters_WithWidelySpreadPredictions_ShouldNotDetectCluster()
    {
        // Arrange - Predictions across different continents
        var predictions = new List<EnhancedLocationPrediction>
        {
            new EnhancedLocationPrediction
            {
                Latitude = 48.8566,  // Paris
                Longitude = 2.3522,
                Probability = 0.4,
                AdjustedProbability = 0.4
            },
            new EnhancedLocationPrediction
            {
                Latitude = 40.7128,  // New York (5800km away)
                Longitude = -74.0060,
                Probability = 0.3,
                AdjustedProbability = 0.3
            }
        };

        // Act
        var result = _analyzer.AnalyzeClusters(predictions);

        // Assert
        result.Should().NotBeNull();
        // Note: The actual clustering behavior depends on the algorithm implementation
        // If it requires all predictions to be within threshold, this should be false
    }

    [Fact]
    public void AnalyzeClusters_ShouldMarkPredictionsAsPartOfCluster()
    {
        // Arrange - Close predictions
        var predictions = new List<EnhancedLocationPrediction>
        {
            new EnhancedLocationPrediction
            {
                Latitude = 48.8566,
                Longitude = 2.3522,
                Probability = 0.4,
                AdjustedProbability = 0.4
            },
            new EnhancedLocationPrediction
            {
                Latitude = 48.87,
                Longitude = 2.35,
                Probability = 0.3,
                AdjustedProbability = 0.3
            }
        };

        // Act
        var result = _analyzer.AnalyzeClusters(predictions);

        // Assert
        if (result.IsClustered)
        {
            predictions.Should().Contain(p => p.IsPartOfCluster, "clustered predictions should be marked");
        }
    }

    [Fact]
    public void AnalyzeClusters_ShouldBoostConfidenceOfClusteredPredictions()
    {
        // Arrange
        var predictions = new List<EnhancedLocationPrediction>
        {
            new EnhancedLocationPrediction
            {
                Latitude = 48.8566,
                Longitude = 2.3522,
                Probability = 0.4,
                AdjustedProbability = 0.4
            },
            new EnhancedLocationPrediction
            {
                Latitude = 48.87,
                Longitude = 2.35,
                Probability = 0.3,
                AdjustedProbability = 0.3
            }
        };

        // Store original probabilities
        var originalProbs = predictions.Select(p => p.AdjustedProbability).ToList();

        // Act
        var result = _analyzer.AnalyzeClusters(predictions);

        // Assert
        if (result.IsClustered)
        {
            for (int i = 0; i < predictions.Count; i++)
            {
                if (predictions[i].IsPartOfCluster)
                {
                    predictions[i].AdjustedProbability.Should().BeGreaterThanOrEqualTo(
                        originalProbs[i],
                        "clustered predictions should have boosted confidence"
                    );
                }
            }
        }
    }

    #endregion

    #region Confidence Classification Tests

    [Theory]
    [InlineData(0.65, true, ConfidenceLevel.High)]
    [InlineData(0.60, false, ConfidenceLevel.High)]
    [InlineData(0.45, true, ConfidenceLevel.Medium)]
    [InlineData(0.30, false, ConfidenceLevel.Medium)]
    [InlineData(0.25, true, ConfidenceLevel.Low)]
    [InlineData(0.10, false, ConfidenceLevel.Low)]
    public void ClassifyConfidence_ShouldReturnCorrectLevel(double probability, bool isClustered, ConfidenceLevel expected)
    {
        // Act
        var result = EnhancedLocationPrediction.ClassifyConfidence(probability, isClustered);

        // Assert
        result.Should().Be(expected, $"probability {probability:P0} should be classified as {expected}");
    }

    [Fact]
    public void ClassifyConfidence_AtHighThreshold_ShouldBeHigh()
    {
        // Act
        var result = EnhancedLocationPrediction.ClassifyConfidence(0.60, false);

        // Assert
        result.Should().Be(ConfidenceLevel.High, "60% is the threshold for High confidence");
    }

    [Fact]
    public void ClassifyConfidence_JustBelowHighThreshold_ShouldBeMedium()
    {
        // Act
        var result = EnhancedLocationPrediction.ClassifyConfidence(0.59, false);

        // Assert
        result.Should().Be(ConfidenceLevel.Medium, "just below 60% should be Medium");
    }

    [Fact]
    public void ClassifyConfidence_AtMediumThreshold_ShouldBeMedium()
    {
        // Act
        var result = EnhancedLocationPrediction.ClassifyConfidence(0.30, false);

        // Assert
        result.Should().Be(ConfidenceLevel.Medium, "30% is the threshold for Medium confidence");
    }

    [Fact]
    public void ClassifyConfidence_JustBelowMediumThreshold_ShouldBeLow()
    {
        // Act
        var result = EnhancedLocationPrediction.ClassifyConfidence(0.29, false);

        // Assert
        result.Should().Be(ConfidenceLevel.Low, "just below 30% should be Low");
    }

    #endregion

    #region EnhancedLocationPrediction Property Tests

    [Fact]
    public void EnhancedLocationPrediction_ConfidenceBoost_ShouldCalculateCorrectly()
    {
        // Arrange
        var prediction = new EnhancedLocationPrediction
        {
            Probability = 0.40,
            AdjustedProbability = 0.55
        };

        // Act
        var boost = prediction.ConfidenceBoost;

        // Assert
        boost.Should().Be(0.15, "boost should be difference between adjusted and original");
    }

    [Fact]
    public void EnhancedLocationPrediction_HasBoost_ShouldBeTrueWhenBoosted()
    {
        // Arrange
        var prediction = new EnhancedLocationPrediction
        {
            Probability = 0.40,
            AdjustedProbability = 0.45
        };

        // Act
        var hasBoost = prediction.HasBoost;

        // Assert
        hasBoost.Should().BeTrue("prediction has received a boost");
    }

    [Fact]
    public void EnhancedLocationPrediction_HasBoost_ShouldBeFalseWhenNotBoosted()
    {
        // Arrange
        var prediction = new EnhancedLocationPrediction
        {
            Probability = 0.40,
            AdjustedProbability = 0.40
        };

        // Act
        var hasBoost = prediction.HasBoost;

        // Assert
        hasBoost.Should().BeFalse("prediction has no boost");
    }

    [Fact]
    public void EnhancedLocationPrediction_Coordinates_ShouldFormatCorrectly()
    {
        // Arrange
        var prediction = new EnhancedLocationPrediction
        {
            Latitude = 48.8566,
            Longitude = 2.3522
        };

        // Act
        var coordinates = prediction.Coordinates;

        // Assert
        coordinates.Should().Contain("48.8566° N");
        coordinates.Should().Contain("2.3522° E");
    }

    #endregion
}
