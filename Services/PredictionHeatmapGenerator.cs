using GeoLens.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeoLens.Services
{
    /// <summary>
    /// Generates heatmaps from multiple image predictions
    /// Uses Gaussian smoothing for visualization
    /// </summary>
    public class PredictionHeatmapGenerator
    {
        private const int GridWidth = 360;   // Longitude: -180 to +180
        private const int GridHeight = 180;  // Latitude: -90 to +90
        private const double GaussianSigma = 3.0; // Smoothing radius in degrees

        /// <summary>
        /// Generate a heatmap from multiple prediction results
        /// </summary>
        /// <param name="results">List of enhanced prediction results from multiple images</param>
        /// <returns>Heatmap data with intensity grid and hotspots</returns>
        public HeatmapData GenerateHeatmap(List<EnhancedPredictionResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return new HeatmapData();
            }

            // Step 1: Aggregate predictions from all images
            var predictions = AggregatePredictions(results);

            if (predictions.Count == 0)
            {
                return new HeatmapData { ImageCount = results.Count };
            }

            // Step 2: Initialize grid
            var grid = new double[GridWidth, GridHeight];

            // Step 3: Apply Gaussian kernel for each prediction
            foreach (var pred in predictions)
            {
                ApplyGaussianKernel(
                    grid,
                    pred.Latitude,
                    pred.Longitude,
                    pred.Weight,
                    GaussianSigma
                );
            }

            // Step 4: Normalize to 0-1 range
            NormalizeGrid(grid);

            // Step 5: Detect hotspots (threshold at 70% intensity)
            var hotspots = DetectHotspots(grid, threshold: 0.7);

            // Step 6: Calculate statistics
            var stats = CalculateStatistics(predictions);

            // Step 7: Create result
            return new HeatmapData
            {
                Width = GridWidth,
                Height = GridHeight,
                IntensityGrid = grid,
                Resolution = 1.0,
                TotalPredictions = predictions.Count,
                ImageCount = results.Count,
                Hotspots = hotspots,
                Statistics = stats
            };
        }

        /// <summary>
        /// Aggregate predictions from multiple results with weighting
        /// </summary>
        private List<WeightedPrediction> AggregatePredictions(List<EnhancedPredictionResult> results)
        {
            var aggregated = new List<WeightedPrediction>();

            foreach (var result in results)
            {
                // Add EXIF GPS with maximum weight (2x)
                if (result.ExifGps?.HasGps == true)
                {
                    aggregated.Add(new WeightedPrediction
                    {
                        Latitude = result.ExifGps.Latitude,
                        Longitude = result.ExifGps.Longitude,
                        Weight = 2.0, // Double weight for EXIF GPS
                        Source = "EXIF GPS",
                        ImagePath = result.ImagePath
                    });
                }

                // Add AI predictions with rank-based weighting
                foreach (var pred in result.AiPredictions)
                {
                    // Weight formula: (probability) * (1 / rank)
                    // Rank 1 with 0.85 confidence = 0.85 * 1.0 = 0.85
                    // Rank 5 with 0.50 confidence = 0.50 * 0.2 = 0.10
                    double weight = pred.AdjustedProbability * (1.0 / pred.Rank);

                    aggregated.Add(new WeightedPrediction
                    {
                        Latitude = pred.Latitude,
                        Longitude = pred.Longitude,
                        Weight = weight,
                        Source = pred.LocationSummary,
                        ImagePath = result.ImagePath,
                        Rank = pred.Rank
                    });
                }
            }

            return aggregated;
        }

        /// <summary>
        /// Apply Gaussian kernel to grid for a single prediction point
        /// </summary>
        private void ApplyGaussianKernel(
            double[,] grid,
            double lat,
            double lon,
            double weight,
            double sigma)
        {
            // Convert lat/lon to grid coordinates
            // Longitude: -180 to +180 maps to 0 to 359
            // Latitude: +90 to -90 maps to 0 to 179
            int centerX = (int)Math.Round((lon + 180) % 360);
            int centerY = (int)Math.Round(90 - lat);

            // Clamp to valid range
            centerX = Math.Clamp(centerX, 0, GridWidth - 1);
            centerY = Math.Clamp(centerY, 0, GridHeight - 1);

            // Apply kernel in 3-sigma radius (covers 99.7% of distribution)
            int radius = (int)Math.Ceiling(sigma * 3);

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    // Wrap longitude (periodic boundary)
                    int x = (centerX + dx + GridWidth) % GridWidth;

                    // Clamp latitude (no wrapping at poles)
                    int y = centerY + dy;
                    if (y < 0 || y >= GridHeight) continue;

                    // Calculate Euclidean distance
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    // Gaussian function: exp(-(distance²) / (2 * sigma²))
                    double gaussianValue = Math.Exp(
                        -(distance * distance) / (2 * sigma * sigma)
                    );

                    // Add weighted contribution
                    grid[x, y] += weight * gaussianValue;
                }
            }
        }

        /// <summary>
        /// Normalize grid values to 0-1 range
        /// </summary>
        private void NormalizeGrid(double[,] grid)
        {
            // Find max value
            double maxValue = 0;
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    maxValue = Math.Max(maxValue, grid[x, y]);
                }
            }

            // Normalize to 0-1
            if (maxValue > 0)
            {
                for (int x = 0; x < GridWidth; x++)
                {
                    for (int y = 0; y < GridHeight; y++)
                    {
                        grid[x, y] /= maxValue;
                    }
                }
            }
        }

        /// <summary>
        /// Detect hotspot regions above threshold
        /// </summary>
        private List<HeatmapHotspot> DetectHotspots(double[,] grid, double threshold)
        {
            var hotspots = new List<HeatmapHotspot>();

            // Find all cells above threshold
            var candidates = new List<(int x, int y, double intensity)>();

            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    if (grid[x, y] >= threshold)
                    {
                        candidates.Add((x, y, grid[x, y]));
                    }
                }
            }

            if (candidates.Count == 0)
                return hotspots;

            // Cluster nearby hotspots using flood fill
            var visited = new HashSet<(int, int)>();

            foreach (var (x, y, intensity) in candidates.OrderByDescending(c => c.intensity))
            {
                if (visited.Contains((x, y))) continue;

                // Find cluster using flood fill
                var cluster = FindCluster(grid, x, y, threshold, visited);

                if (cluster.Count > 0)
                {
                    // Calculate centroid (weighted average)
                    double totalWeight = cluster.Sum(c => c.intensity);
                    double avgX = cluster.Sum(c => c.x * c.intensity) / totalWeight;
                    double avgY = cluster.Sum(c => c.y * c.intensity) / totalWeight;
                    double avgIntensity = cluster.Average(c => c.intensity);

                    // Convert back to lat/lon
                    double lon = avgX - 180;
                    double lat = 90 - avgY;

                    hotspots.Add(new HeatmapHotspot
                    {
                        Latitude = lat,
                        Longitude = lon,
                        Intensity = avgIntensity,
                        CellCount = cluster.Count,
                        PredictionCount = cluster.Count, // Approximate
                        RadiusKm = EstimateRadiusKm(cluster, lat)
                    });
                }
            }

            return hotspots;
        }

        /// <summary>
        /// Find contiguous cluster using flood fill algorithm
        /// </summary>
        private List<(int x, int y, double intensity)> FindCluster(
            double[,] grid,
            int startX,
            int startY,
            double threshold,
            HashSet<(int, int)> visited)
        {
            var cluster = new List<(int x, int y, double intensity)>();
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue((startX, startY));

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();

                if (visited.Contains((x, y))) continue;
                if (grid[x, y] < threshold) continue;

                visited.Add((x, y));
                cluster.Add((x, y, grid[x, y]));

                // Check 8-connected neighbors
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        // Wrap longitude
                        int nx = (x + dx + GridWidth) % GridWidth;

                        // Clamp latitude
                        int ny = y + dy;
                        if (ny < 0 || ny >= GridHeight) continue;

                        if (!visited.Contains((nx, ny)))
                        {
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }

            return cluster;
        }

        /// <summary>
        /// Estimate radius in kilometers for a cluster
        /// </summary>
        private double EstimateRadiusKm(List<(int x, int y, double intensity)> cluster, double avgLat)
        {
            if (cluster.Count < 2) return 0;

            // Calculate bounding box
            int minX = cluster.Min(c => c.x);
            int maxX = cluster.Max(c => c.x);
            int minY = cluster.Min(c => c.y);
            int maxY = cluster.Max(c => c.y);

            // Handle longitude wrapping (if cluster spans 0°/360° boundary)
            int lonSpan = maxX - minX;
            if (lonSpan > 180) // Cluster wraps around
            {
                lonSpan = 360 - lonSpan;
            }

            // Convert to approximate km (1° ≈ 111.32 km at equator)
            double latKm = (maxY - minY) * 111.32;

            // Longitude distance varies by latitude: cos(lat) * 111.32 km/degree
            double lonKm = lonSpan * 111.32 * Math.Cos(avgLat * Math.PI / 180.0);

            // Return radius of equivalent circle
            return Math.Sqrt(latKm * latKm + lonKm * lonKm) / 2;
        }

        /// <summary>
        /// Calculate statistics about the prediction distribution
        /// </summary>
        private HeatmapStatistics CalculateStatistics(List<WeightedPrediction> predictions)
        {
            if (predictions.Count == 0)
                return new HeatmapStatistics();

            int exifCount = predictions.Count(p => p.Source == "EXIF GPS");
            int aiCount = predictions.Count - exifCount;

            // Calculate coverage area
            double minLat = predictions.Min(p => p.Latitude);
            double maxLat = predictions.Max(p => p.Latitude);
            double minLon = predictions.Min(p => p.Longitude);
            double maxLon = predictions.Max(p => p.Longitude);

            double latDiff = maxLat - minLat;
            double lonDiff = maxLon - minLon;
            double avgLat = (minLat + maxLat) / 2;

            // Convert degrees to km
            double latKm = latDiff * 111.32;
            double lonKm = lonDiff * 111.32 * Math.Cos(avgLat * Math.PI / 180.0);
            double coverageArea = latKm * lonKm;

            return new HeatmapStatistics
            {
                ExifCount = exifCount,
                AiCount = aiCount,
                AverageWeight = predictions.Average(p => p.Weight),
                MaxWeight = predictions.Max(p => p.Weight),
                CoverageAreaKm2 = coverageArea
            };
        }
    }
}
