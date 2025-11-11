using Microsoft.UI.Xaml.Media;
using System;

namespace GeoLens.Models
{
    /// <summary>
    /// Enhanced location prediction with confidence level and clustering information
    /// </summary>
    public class EnhancedLocationPrediction
    {
        public int Rank { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Probability { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string LocationSummary { get; set; } = string.Empty;

        /// <summary>
        /// Probability adjusted for clustering bonus
        /// </summary>
        public double AdjustedProbability { get; set; }

        /// <summary>
        /// Whether this prediction is part of a geographic cluster
        /// </summary>
        public bool IsPartOfCluster { get; set; }

        /// <summary>
        /// Confidence level classification
        /// </summary>
        public ConfidenceLevel ConfidenceLevel { get; set; }

        // UI Helper Properties
        public string LatitudeFormatted => $"{Math.Abs(Latitude):F6}° {(Latitude >= 0 ? "N" : "S")}";
        public string LongitudeFormatted => $"{Math.Abs(Longitude):F6}° {(Longitude >= 0 ? "E" : "W")}";
        public string Coordinates => $"{LatitudeFormatted}, {LongitudeFormatted}";
        public string ProbabilityFormatted => $"{AdjustedProbability:P1}";

        public string ConfidenceText => ConfidenceLevel switch
        {
            ConfidenceLevel.VeryHigh => "VERY HIGH",
            ConfidenceLevel.High => "HIGH",
            ConfidenceLevel.Medium => "MEDIUM",
            ConfidenceLevel.Low => "LOW",
            _ => "UNKNOWN"
        };

        public Brush ConfidenceColor => ConfidenceLevel switch
        {
            ConfidenceLevel.VeryHigh => new SolidColorBrush(Microsoft.UI.Colors.Cyan),
            ConfidenceLevel.High => new SolidColorBrush(Microsoft.UI.Colors.LimeGreen),
            ConfidenceLevel.Medium => new SolidColorBrush(Microsoft.UI.Colors.Gold),
            ConfidenceLevel.Low => new SolidColorBrush(Microsoft.UI.Colors.IndianRed),
            _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
        };

        public string ConfidenceGlyph => ConfidenceLevel switch
        {
            ConfidenceLevel.VeryHigh => "\uE81D", // Location
            ConfidenceLevel.High => "\uE73E", // Checkmark
            ConfidenceLevel.Medium => "\uE946", // Info
            ConfidenceLevel.Low => "\uE7BA", // Warning
            _ => "\uE946" // Info
        };

        /// <summary>
        /// Calculate confidence level based on probability and clustering
        /// </summary>
        public static ConfidenceLevel ClassifyConfidence(double probability, bool isClustered)
        {
            if (probability > 0.1 || isClustered)
                return ConfidenceLevel.High;
            if (probability >= 0.05)
                return ConfidenceLevel.Medium;
            return ConfidenceLevel.Low;
        }
    }
}
