using System;
using System.Collections.Generic;
using System.Linq;
using GeoLens.Models;

namespace GeoLens.Services
{
    /// <summary>
    /// Service for analyzing geographic clustering of location predictions
    /// and boosting confidence for clustered results
    /// </summary>
    public class GeographicClusterAnalyzer
    {
        private const double ClusterRadiusKm = 100.0; // Maximum distance for clustering (100km threshold)
        private const double ConfidenceBoostFactor = 0.15; // Base boost factor for clustered predictions
        private const int MinimumClusterSize = 2; // Minimum predictions required to form a cluster

        /// <summary>
        /// Analyze predictions to detect geographic clustering and boost confidence
        /// </summary>
        /// <param name="predictions">List of location predictions to analyze</param>
        /// <returns>Cluster analysis result with clustering information</returns>
        public ClusterAnalysisResult AnalyzeClusters(List<EnhancedLocationPrediction> predictions)
        {
            if (predictions == null || predictions.Count < MinimumClusterSize)
            {
                return new ClusterAnalysisResult
                {
                    IsClustered = false,
                    ClusterRadius = 0,
                    AverageDistance = 0,
                    ConfidenceBoost = 0,
                    ClusterCenterLat = 0,
                    ClusterCenterLon = 0
                };
            }

            try
            {
                // Find the largest cluster within the radius threshold
                var clusterInfo = FindLargestCluster(predictions);

                if (clusterInfo.ClusteredPredictions.Count >= MinimumClusterSize)
                {
                    // Calculate cluster statistics
                    var (centerLat, centerLon) = FindClusterCenter(clusterInfo.ClusteredPredictions);
                    var avgDistance = CalculateAverageDistance(clusterInfo.ClusteredPredictions, centerLat, centerLon);
                    var maxDistance = CalculateMaxDistance(clusterInfo.ClusteredPredictions, centerLat, centerLon);
                    var confidenceBoost = CalculateConfidenceBoost(clusterInfo.ClusteredPredictions.Count, predictions.Count);

                    // Mark predictions as part of cluster
                    foreach (var prediction in clusterInfo.ClusteredPredictions)
                    {
                        prediction.IsPartOfCluster = true;
                    }

                    // Apply confidence boost to clustered predictions
                    BoostClusteredPredictions(clusterInfo.ClusteredPredictions, confidenceBoost);

                    return new ClusterAnalysisResult
                    {
                        IsClustered = true,
                        ClusterRadius = maxDistance,
                        AverageDistance = avgDistance,
                        ConfidenceBoost = confidenceBoost,
                        ClusterCenterLat = centerLat,
                        ClusterCenterLon = centerLon
                    };
                }

                // No significant cluster found
                return new ClusterAnalysisResult
                {
                    IsClustered = false,
                    ClusterRadius = 0,
                    AverageDistance = 0,
                    ConfidenceBoost = 0,
                    ClusterCenterLat = 0,
                    ClusterCenterLon = 0
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cluster analysis failed: {ex.Message}");
                return new ClusterAnalysisResult
                {
                    IsClustered = false,
                    ClusterRadius = 0,
                    AverageDistance = 0,
                    ConfidenceBoost = 0,
                    ClusterCenterLat = 0,
                    ClusterCenterLon = 0
                };
            }
        }

        /// <summary>
        /// Calculate distance between two geographic coordinates using Haversine formula
        /// </summary>
        /// <param name="lat1">Latitude of first point (degrees)</param>
        /// <param name="lon1">Longitude of first point (degrees)</param>
        /// <param name="lat2">Latitude of second point (degrees)</param>
        /// <param name="lon2">Longitude of second point (degrees)</param>
        /// <returns>Distance in kilometers</returns>
        public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double EarthRadiusKm = 6371.0;

            // Convert degrees to radians
            var lat1Rad = DegreesToRadians(lat1);
            var lon1Rad = DegreesToRadians(lon1);
            var lat2Rad = DegreesToRadians(lat2);
            var lon2Rad = DegreesToRadians(lon2);

            // Haversine formula
            var dLat = lat2Rad - lat1Rad;
            var dLon = lon2Rad - lon1Rad;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = EarthRadiusKm * c;

            return distance;
        }

        /// <summary>
        /// Calculate the geographic center (centroid) of a cluster of predictions
        /// </summary>
        /// <param name="predictions">List of predictions in the cluster</param>
        /// <returns>Tuple of (latitude, longitude) for cluster center</returns>
        public (double latitude, double longitude) FindClusterCenter(List<EnhancedLocationPrediction> predictions)
        {
            if (predictions == null || predictions.Count == 0)
            {
                return (0, 0);
            }

            // For geographic coordinates, we need to use spherical averaging
            // Convert to Cartesian coordinates, average, then convert back
            double x = 0, y = 0, z = 0;

            foreach (var prediction in predictions)
            {
                var latRad = DegreesToRadians(prediction.Latitude);
                var lonRad = DegreesToRadians(prediction.Longitude);

                x += Math.Cos(latRad) * Math.Cos(lonRad);
                y += Math.Cos(latRad) * Math.Sin(lonRad);
                z += Math.Sin(latRad);
            }

            var total = predictions.Count;
            x /= total;
            y /= total;
            z /= total;

            // Convert average Cartesian coordinates back to latitude/longitude
            var centralLongitude = Math.Atan2(y, x);
            var centralSquareRoot = Math.Sqrt(x * x + y * y);
            var centralLatitude = Math.Atan2(z, centralSquareRoot);

            return (RadiansToDegrees(centralLatitude), RadiansToDegrees(centralLongitude));
        }

        /// <summary>
        /// Boost confidence (adjusted probability) for clustered predictions
        /// </summary>
        /// <param name="predictions">Clustered predictions to boost</param>
        /// <param name="boostAmount">Amount to boost (added to adjusted probability)</param>
        public void BoostClusteredPredictions(List<EnhancedLocationPrediction> predictions, double boostAmount)
        {
            if (predictions == null || predictions.Count == 0 || boostAmount <= 0)
            {
                return;
            }

            foreach (var prediction in predictions)
            {
                // Apply boost to adjusted probability
                prediction.AdjustedProbability = Math.Min(1.0, prediction.AdjustedProbability + boostAmount);

                // Reclassify confidence level based on new adjusted probability and clustering status
                prediction.ConfidenceLevel = EnhancedLocationPrediction.ClassifyConfidence(
                    prediction.AdjustedProbability,
                    prediction.IsPartOfCluster
                );
            }
        }

        /// <summary>
        /// Find the largest cluster of predictions within the radius threshold
        /// </summary>
        private ClusterInfo FindLargestCluster(List<EnhancedLocationPrediction> predictions)
        {
            var bestCluster = new ClusterInfo { ClusteredPredictions = new List<EnhancedLocationPrediction>() };

            // Try each prediction as a potential cluster center
            foreach (var centerPrediction in predictions)
            {
                var cluster = new List<EnhancedLocationPrediction>();

                // Find all predictions within the cluster radius
                foreach (var prediction in predictions)
                {
                    var distance = CalculateDistance(
                        centerPrediction.Latitude, centerPrediction.Longitude,
                        prediction.Latitude, prediction.Longitude
                    );

                    if (distance <= ClusterRadiusKm)
                    {
                        cluster.Add(prediction);
                    }
                }

                // Keep track of the largest cluster found
                if (cluster.Count > bestCluster.ClusteredPredictions.Count)
                {
                    bestCluster.ClusteredPredictions = cluster;
                }
            }

            return bestCluster;
        }

        /// <summary>
        /// Calculate average distance of predictions from cluster center
        /// </summary>
        private double CalculateAverageDistance(List<EnhancedLocationPrediction> predictions, double centerLat, double centerLon)
        {
            if (predictions.Count == 0)
            {
                return 0;
            }

            var totalDistance = predictions.Sum(p => CalculateDistance(p.Latitude, p.Longitude, centerLat, centerLon));
            return totalDistance / predictions.Count;
        }

        /// <summary>
        /// Calculate maximum distance of predictions from cluster center
        /// </summary>
        private double CalculateMaxDistance(List<EnhancedLocationPrediction> predictions, double centerLat, double centerLon)
        {
            if (predictions.Count == 0)
            {
                return 0;
            }

            return predictions.Max(p => CalculateDistance(p.Latitude, p.Longitude, centerLat, centerLon));
        }

        /// <summary>
        /// Calculate confidence boost based on cluster size
        /// Formula: boost = 0.15 * (clusterSize / totalPredictions)
        /// </summary>
        private double CalculateConfidenceBoost(int clusterSize, int totalPredictions)
        {
            if (totalPredictions == 0)
            {
                return 0;
            }

            return ConfidenceBoostFactor * ((double)clusterSize / totalPredictions);
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Convert radians to degrees
        /// </summary>
        private double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        /// <summary>
        /// Internal class to hold cluster information during analysis
        /// </summary>
        private class ClusterInfo
        {
            public List<EnhancedLocationPrediction> ClusteredPredictions { get; set; } = new();
        }
    }
}
