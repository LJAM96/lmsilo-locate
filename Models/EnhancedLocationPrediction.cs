using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GeoLens.Models
{
    /// <summary>
    /// Enhanced location prediction with confidence level and clustering information
    /// </summary>
    public class EnhancedLocationPrediction : INotifyPropertyChanged
    {
        private double _adjustedProbability;
        private bool _isPartOfCluster;
        private ConfidenceLevel _confidenceLevel;

        public event PropertyChangedEventHandler? PropertyChanged;

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
        public double AdjustedProbability
        {
            get => _adjustedProbability;
            set
            {
                if (_adjustedProbability != value)
                {
                    _adjustedProbability = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProbabilityFormatted));
                }
            }
        }

        /// <summary>
        /// Whether this prediction is part of a geographic cluster
        /// </summary>
        public bool IsPartOfCluster
        {
            get => _isPartOfCluster;
            set
            {
                if (_isPartOfCluster != value)
                {
                    _isPartOfCluster = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Confidence level classification
        /// </summary>
        public ConfidenceLevel ConfidenceLevel
        {
            get => _confidenceLevel;
            set
            {
                if (_confidenceLevel != value)
                {
                    _confidenceLevel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ConfidenceText));
                    OnPropertyChanged(nameof(ConfidenceColor));
                    OnPropertyChanged(nameof(ConfidenceGlyph));
                }
            }
        }

        // Calculated Properties
        /// <summary>
        /// The confidence boost applied due to clustering (difference between adjusted and original)
        /// </summary>
        public double ConfidenceBoost => AdjustedProbability - Probability;

        /// <summary>
        /// Whether this prediction received a confidence boost
        /// </summary>
        public bool HasBoost => ConfidenceBoost > 0.001; // Account for floating point precision

        // UI Helper Properties
        public string LatitudeFormatted => $"{Math.Abs(Latitude):F6}° {(Latitude >= 0 ? "N" : "S")}";
        public string LongitudeFormatted => $"{Math.Abs(Longitude):F6}° {(Longitude >= 0 ? "E" : "W")}";
        public string Coordinates => $"{LatitudeFormatted}, {LongitudeFormatted}";

        /// <summary>
        /// Original probability from AI model
        /// </summary>
        public string OriginalProbabilityFormatted => $"{Probability:P1}";

        /// <summary>
        /// Adjusted probability (after clustering boost)
        /// </summary>
        public string AdjustedProbabilityFormatted => $"{AdjustedProbability:P1}";

        /// <summary>
        /// Primary display probability (uses adjusted value)
        /// </summary>
        public string ProbabilityFormatted => AdjustedProbabilityFormatted;

        /// <summary>
        /// Boost amount as formatted percentage
        /// </summary>
        public string BoostFormatted => $"+{ConfidenceBoost:P1}";

        /// <summary>
        /// Full probability breakdown for display
        /// Example: "3.2% base + 8.8% clustering boost = 12.0%"
        /// </summary>
        public string ProbabilityBreakdown
        {
            get
            {
                if (HasBoost)
                {
                    return $"{OriginalProbabilityFormatted} base + {BoostFormatted} clustering boost = {AdjustedProbabilityFormatted}";
                }
                return OriginalProbabilityFormatted;
            }
        }

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
        /// Very conservative thresholds for honest assessment:
        /// - High: >= 60% (very confident, more likely correct than wrong)
        /// - Medium: >= 30% (moderate confidence, reasonable possibility)
        /// - Low: < 30% (weak confidence, many possibilities)
        /// Note: EXIF GPS data is classified as VeryHigh (100% accuracy)
        /// </summary>
        public static ConfidenceLevel ClassifyConfidence(double probability, bool isClustered)
        {
            // High confidence: >= 60%
            if (probability >= 0.60)
                return ConfidenceLevel.High;

            // Medium confidence: >= 30%
            if (probability >= 0.30)
                return ConfidenceLevel.Medium;

            // Low confidence: < 30%
            return ConfidenceLevel.Low;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
