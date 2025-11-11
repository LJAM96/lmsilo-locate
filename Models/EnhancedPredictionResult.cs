using System.Collections.Generic;

namespace GeoLens.Models
{
    /// <summary>
    /// Complete prediction result for an image including AI predictions and EXIF data
    /// </summary>
    public class EnhancedPredictionResult
    {
        public string ImagePath { get; set; } = string.Empty;
        public List<EnhancedLocationPrediction> AiPredictions { get; set; } = new();
        public ExifGpsData? ExifGps { get; set; }
        public bool HasExifGps => ExifGps?.HasGps == true;
        public ClusterAnalysisResult? ClusterInfo { get; set; }

        /// <summary>
        /// Reliability message for UI display
        /// </summary>
        public string ReliabilityMessage
        {
            get
            {
                if (HasExifGps)
                    return "GPS coordinates found in image metadata - highest reliability";

                if (ClusterInfo?.IsClustered == true)
                {
                    var radius = ClusterInfo.ClusterRadius;
                    return $"High reliability - predictions clustered within {radius:F0}km";
                }

                if (AiPredictions.Count > 0 && AiPredictions[0].Probability > 0.1)
                    return "Moderate reliability - strong AI prediction confidence";

                return "Lower reliability - AI predictions have low confidence";
            }
        }
    }

    /// <summary>
    /// Result of geographic cluster analysis
    /// </summary>
    public class ClusterAnalysisResult
    {
        public bool IsClustered { get; set; }
        public double ClusterRadius { get; set; }
        public double AverageDistance { get; set; }
        public double ConfidenceBoost { get; set; }
        public double ClusterCenterLat { get; set; }
        public double ClusterCenterLon { get; set; }
    }
}
