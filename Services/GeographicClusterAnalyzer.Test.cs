using System;
using System.Collections.Generic;
using GeoLens.Models;

namespace GeoLens.Services.Tests
{
    /// <summary>
    /// Test examples and validation for GeographicClusterAnalyzer
    /// This is not a unit test framework - just manual verification examples
    /// </summary>
    public class GeographicClusterAnalyzerTestExamples
    {
        /// <summary>
        /// Example 1: Test with clustered predictions (Paris, France area)
        /// All predictions within ~50km should form a cluster
        /// </summary>
        public static void TestClusteredPredictions()
        {
            var analyzer = new GeographicClusterAnalyzer();

            var predictions = new List<EnhancedLocationPrediction>
            {
                // Paris city center
                new EnhancedLocationPrediction
                {
                    Rank = 1,
                    Latitude = 48.8566,
                    Longitude = 2.3522,
                    Probability = 0.15,
                    AdjustedProbability = 0.15,
                    City = "Paris",
                    Country = "France"
                },
                // Versailles (nearby)
                new EnhancedLocationPrediction
                {
                    Rank = 2,
                    Latitude = 48.8049,
                    Longitude = 2.1204,
                    Probability = 0.12,
                    AdjustedProbability = 0.12,
                    City = "Versailles",
                    Country = "France"
                },
                // Charles de Gaulle Airport (nearby)
                new EnhancedLocationPrediction
                {
                    Rank = 3,
                    Latitude = 49.0097,
                    Longitude = 2.5479,
                    Probability = 0.08,
                    AdjustedProbability = 0.08,
                    City = "Roissy-en-France",
                    Country = "France"
                },
                // Berlin (far away - should not cluster)
                new EnhancedLocationPrediction
                {
                    Rank = 4,
                    Latitude = 52.5200,
                    Longitude = 13.4050,
                    Probability = 0.05,
                    AdjustedProbability = 0.05,
                    City = "Berlin",
                    Country = "Germany"
                }
            };

            var result = analyzer.AnalyzeClusters(predictions);

            Console.WriteLine("=== Test: Clustered Predictions (Paris Area) ===");
            Console.WriteLine($"Is Clustered: {result.IsClustered}");
            Console.WriteLine($"Cluster Center: {result.ClusterCenterLat:F4}°, {result.ClusterCenterLon:F4}°");
            Console.WriteLine($"Cluster Radius: {result.ClusterRadius:F2} km");
            Console.WriteLine($"Average Distance: {result.AverageDistance:F2} km");
            Console.WriteLine($"Confidence Boost: {result.ConfidenceBoost:F4} (+{result.ConfidenceBoost * 100:F1}%)");
            Console.WriteLine();

            Console.WriteLine("Predictions after clustering:");
            foreach (var pred in predictions)
            {
                Console.WriteLine($"  {pred.City}: {pred.AdjustedProbability:P1} " +
                                $"(Clustered: {pred.IsPartOfCluster}, Level: {pred.ConfidenceLevel})");
            }
            Console.WriteLine();

            // Expected: First 3 should cluster (within ~50km), Berlin should not
            // Confidence boost should be applied to clustered predictions
        }

        /// <summary>
        /// Example 2: Test with dispersed predictions (no clustering)
        /// Predictions far apart should not form a cluster
        /// </summary>
        public static void TestDispersedPredictions()
        {
            var analyzer = new GeographicClusterAnalyzer();

            var predictions = new List<EnhancedLocationPrediction>
            {
                // New York
                new EnhancedLocationPrediction
                {
                    Rank = 1,
                    Latitude = 40.7128,
                    Longitude = -74.0060,
                    Probability = 0.20,
                    AdjustedProbability = 0.20,
                    City = "New York",
                    Country = "USA"
                },
                // London
                new EnhancedLocationPrediction
                {
                    Rank = 2,
                    Latitude = 51.5074,
                    Longitude = -0.1278,
                    Probability = 0.15,
                    AdjustedProbability = 0.15,
                    City = "London",
                    Country = "UK"
                },
                // Tokyo
                new EnhancedLocationPrediction
                {
                    Rank = 3,
                    Latitude = 35.6762,
                    Longitude = 139.6503,
                    Probability = 0.10,
                    AdjustedProbability = 0.10,
                    City = "Tokyo",
                    Country = "Japan"
                }
            };

            var result = analyzer.AnalyzeClusters(predictions);

            Console.WriteLine("=== Test: Dispersed Predictions (No Clustering) ===");
            Console.WriteLine($"Is Clustered: {result.IsClustered}");
            Console.WriteLine($"Confidence Boost: {result.ConfidenceBoost}");
            Console.WriteLine();

            Console.WriteLine("Predictions (should remain unchanged):");
            foreach (var pred in predictions)
            {
                Console.WriteLine($"  {pred.City}: {pred.AdjustedProbability:P1} " +
                                $"(Clustered: {pred.IsPartOfCluster})");
            }
            Console.WriteLine();

            // Expected: No clustering, no confidence boost applied
        }

        /// <summary>
        /// Example 3: Test Haversine distance calculation
        /// Verify accurate distance calculations between known locations
        /// </summary>
        public static void TestDistanceCalculations()
        {
            var analyzer = new GeographicClusterAnalyzer();

            Console.WriteLine("=== Test: Distance Calculations (Haversine) ===");

            // Paris to London (known distance: ~344 km)
            var parisLondon = analyzer.CalculateDistance(48.8566, 2.3522, 51.5074, -0.1278);
            Console.WriteLine($"Paris to London: {parisLondon:F2} km (expected ~344 km)");

            // New York to Los Angeles (known distance: ~3,944 km)
            var nyLa = analyzer.CalculateDistance(40.7128, -74.0060, 34.0522, -118.2437);
            Console.WriteLine($"New York to Los Angeles: {nyLa:F2} km (expected ~3,944 km)");

            // Sydney to Melbourne (known distance: ~714 km)
            var sydMel = analyzer.CalculateDistance(-33.8688, 151.2093, -37.8136, 144.9631);
            Console.WriteLine($"Sydney to Melbourne: {sydMel:F2} km (expected ~714 km)");

            // Short distance: Paris to Versailles (known distance: ~17 km)
            var parisVersailles = analyzer.CalculateDistance(48.8566, 2.3522, 48.8049, 2.1204);
            Console.WriteLine($"Paris to Versailles: {parisVersailles:F2} km (expected ~17 km)");

            Console.WriteLine();
        }

        /// <summary>
        /// Example 4: Test cluster center calculation
        /// Verify centroid calculation for multiple points
        /// </summary>
        public static void TestClusterCenter()
        {
            var analyzer = new GeographicClusterAnalyzer();

            var predictions = new List<EnhancedLocationPrediction>
            {
                new EnhancedLocationPrediction { Latitude = 48.8566, Longitude = 2.3522 },  // Paris
                new EnhancedLocationPrediction { Latitude = 48.8049, Longitude = 2.1204 },  // Versailles
                new EnhancedLocationPrediction { Latitude = 49.0097, Longitude = 2.5479 },  // CDG Airport
            };

            var (centerLat, centerLon) = analyzer.FindClusterCenter(predictions);

            Console.WriteLine("=== Test: Cluster Center Calculation ===");
            Console.WriteLine($"Cluster center: {centerLat:F4}°, {centerLon:F4}°");
            Console.WriteLine($"(Should be near Paris area: ~48.89°N, 2.34°E)");
            Console.WriteLine();
        }

        /// <summary>
        /// Example 5: Test confidence boost calculation
        /// Verify boost formula: 0.15 * (clusterSize / totalPredictions)
        /// </summary>
        public static void TestConfidenceBoost()
        {
            Console.WriteLine("=== Test: Confidence Boost Formula ===");

            // Test case 1: 3 out of 5 predictions cluster
            var boost1 = 0.15 * (3.0 / 5.0);
            Console.WriteLine($"3/5 predictions clustered: boost = {boost1:F4} ({boost1 * 100:F1}%)");

            // Test case 2: 4 out of 4 predictions cluster (all)
            var boost2 = 0.15 * (4.0 / 4.0);
            Console.WriteLine($"4/4 predictions clustered: boost = {boost2:F4} ({boost2 * 100:F1}%)");

            // Test case 3: 2 out of 10 predictions cluster
            var boost3 = 0.15 * (2.0 / 10.0);
            Console.WriteLine($"2/10 predictions clustered: boost = {boost3:F4} ({boost3 * 100:F1}%)");

            Console.WriteLine();
        }

        /// <summary>
        /// Run all test examples
        /// </summary>
        public static void RunAllTests()
        {
            Console.WriteLine("===================================================");
            Console.WriteLine("   GeographicClusterAnalyzer Test Examples");
            Console.WriteLine("===================================================");
            Console.WriteLine();

            TestClusteredPredictions();
            TestDispersedPredictions();
            TestDistanceCalculations();
            TestClusterCenter();
            TestConfidenceBoost();

            Console.WriteLine("===================================================");
            Console.WriteLine("   All tests completed");
            Console.WriteLine("===================================================");
        }
    }
}
