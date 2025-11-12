using System.Collections.Generic;

namespace GeoLens.Models
{
    /// <summary>
    /// Heatmap data structure for multi-image visualization
    /// Contains intensity grid and detected hotspots
    /// </summary>
    public class HeatmapData
    {
        /// <summary>
        /// Grid width (longitude) - default 360 for 1° resolution
        /// </summary>
        public int Width { get; set; } = 360;

        /// <summary>
        /// Grid height (latitude) - default 180 for 1° resolution
        /// </summary>
        public int Height { get; set; } = 180;

        /// <summary>
        /// Intensity grid normalized to 0-1 range
        /// [longitude (0-359), latitude (0-179)]
        /// </summary>
        public double[,] IntensityGrid { get; set; } = new double[360, 180];

        /// <summary>
        /// Detected hotspot regions (areas with high intensity)
        /// </summary>
        public List<HeatmapHotspot> Hotspots { get; set; } = new();

        /// <summary>
        /// Total number of predictions used to generate this heatmap
        /// </summary>
        public int TotalPredictions { get; set; }

        /// <summary>
        /// Number of images that contributed to this heatmap
        /// </summary>
        public int ImageCount { get; set; }

        /// <summary>
        /// Grid resolution in degrees (default 1.0 for 1° per cell)
        /// </summary>
        public double Resolution { get; set; } = 1.0;

        /// <summary>
        /// Statistics about the prediction distribution
        /// </summary>
        public HeatmapStatistics Statistics { get; set; } = new();
    }

    /// <summary>
    /// Represents a hotspot region in the heatmap
    /// </summary>
    public class HeatmapHotspot
    {
        /// <summary>
        /// Center latitude of the hotspot
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Center longitude of the hotspot
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Normalized intensity (0-1)
        /// </summary>
        public double Intensity { get; set; }

        /// <summary>
        /// Estimated radius in kilometers
        /// </summary>
        public double RadiusKm { get; set; }

        /// <summary>
        /// Number of predictions contributing to this hotspot
        /// </summary>
        public int PredictionCount { get; set; }

        /// <summary>
        /// Number of grid cells in this hotspot region
        /// </summary>
        public int CellCount { get; set; }

        /// <summary>
        /// Reverse geocoded location name (optional)
        /// </summary>
        public string? LocationName { get; set; }
    }

    /// <summary>
    /// Statistics about the heatmap prediction distribution
    /// </summary>
    public class HeatmapStatistics
    {
        /// <summary>
        /// Number of EXIF GPS predictions
        /// </summary>
        public int ExifCount { get; set; }

        /// <summary>
        /// Number of AI-generated predictions
        /// </summary>
        public int AiCount { get; set; }

        /// <summary>
        /// Average prediction weight
        /// </summary>
        public double AverageWeight { get; set; }

        /// <summary>
        /// Maximum prediction weight
        /// </summary>
        public double MaxWeight { get; set; }

        /// <summary>
        /// Approximate coverage area in km²
        /// </summary>
        public double CoverageAreaKm2 { get; set; }
    }

    /// <summary>
    /// Weighted prediction for heatmap generation
    /// </summary>
    public class WeightedPrediction
    {
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double Weight { get; init; }
        public string Source { get; init; } = "";
        public string ImagePath { get; init; } = "";
        public int? Rank { get; init; }
    }
}
