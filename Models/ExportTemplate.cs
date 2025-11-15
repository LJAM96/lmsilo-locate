using System;
using System.Collections.Generic;

namespace GeoLens.Models
{
    /// <summary>
    /// Defines a customizable export template for different output formats
    /// </summary>
    public class ExportTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsBuiltIn { get; set; } = false;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;

        // CSV Configuration
        public CsvTemplateConfig CsvConfig { get; set; } = new();

        // PDF Configuration
        public PdfTemplateConfig PdfConfig { get; set; } = new();

        // Coordinate Format
        public CoordinateFormat CoordinateFormat { get; set; } = CoordinateFormat.DecimalDegrees;

        // Include Options (applies to all formats)
        public bool IncludeExifData { get; set; } = true;
        public bool IncludeAiPredictions { get; set; } = true;
        public bool IncludeClusteringInfo { get; set; } = true;
        public bool IncludeConfidenceScores { get; set; } = true;
    }

    /// <summary>
    /// CSV-specific template configuration
    /// </summary>
    public class CsvTemplateConfig
    {
        public List<string> Columns { get; set; } = new()
        {
            "ImagePath",
            "Source",
            "Rank",
            "Latitude",
            "Longitude",
            "BaseProbability",
            "ClusteringBoost",
            "FinalProbability",
            "Location",
            "ConfidenceLevel"
        };

        public bool IncludeHeader { get; set; } = true;
        public string Delimiter { get; set; } = ",";
    }

    /// <summary>
    /// PDF-specific template configuration
    /// </summary>
    public class PdfTemplateConfig
    {
        public PdfLayoutStyle LayoutStyle { get; set; } = PdfLayoutStyle.Detailed;
        public bool IncludeThumbnail { get; set; } = true;
        public bool IncludeMap { get; set; } = true;
        public bool IncludeIntelligenceWarning { get; set; } = true;
        public int MaxPredictionsToShow { get; set; } = 5;
        public bool ShowProbabilityBreakdown { get; set; } = true;
    }

    /// <summary>
    /// Coordinate format options
    /// </summary>
    public enum CoordinateFormat
    {
        DecimalDegrees,         // 48.856614, 2.352222
        DegreesDecimalMinutes,  // 48° 51.397'N, 2° 21.133'E
        DegreesMinutesSeconds   // 48° 51' 23.8"N, 2° 21' 8.0"E
    }

    /// <summary>
    /// PDF layout style options
    /// </summary>
    public enum PdfLayoutStyle
    {
        Detailed,      // Full details with images and maps
        Compact,       // Condensed layout without images
        SimpleList     // Just coordinates and location names
    }

    /// <summary>
    /// Built-in template presets
    /// </summary>
    public static class ExportTemplatePresets
    {
        /// <summary>
        /// Detailed template - includes all available information
        /// </summary>
        public static ExportTemplate Detailed => new()
        {
            Id = "builtin-detailed",
            Name = "Detailed",
            Description = "Complete export with all predictions, EXIF data, clustering information, and maps",
            IsBuiltIn = true,
            CoordinateFormat = CoordinateFormat.DecimalDegrees,
            IncludeExifData = true,
            IncludeAiPredictions = true,
            IncludeClusteringInfo = true,
            IncludeConfidenceScores = true,
            CsvConfig = new CsvTemplateConfig
            {
                Columns = new List<string>
                {
                    "ImagePath",
                    "Source",
                    "Rank",
                    "Latitude",
                    "Longitude",
                    "BaseProbability",
                    "ClusteringBoost",
                    "FinalProbability",
                    "Location",
                    "ConfidenceLevel",
                    "IsPartOfCluster",
                    "Altitude"
                },
                IncludeHeader = true,
                Delimiter = ","
            },
            PdfConfig = new PdfTemplateConfig
            {
                LayoutStyle = PdfLayoutStyle.Detailed,
                IncludeThumbnail = true,
                IncludeMap = true,
                IncludeIntelligenceWarning = true,
                MaxPredictionsToShow = 10,
                ShowProbabilityBreakdown = true
            }
        };

        /// <summary>
        /// Simple template - essential information only
        /// </summary>
        public static ExportTemplate Simple => new()
        {
            Id = "builtin-simple",
            Name = "Simple",
            Description = "Basic export with top predictions and locations (no clustering details)",
            IsBuiltIn = true,
            CoordinateFormat = CoordinateFormat.DecimalDegrees,
            IncludeExifData = true,
            IncludeAiPredictions = true,
            IncludeClusteringInfo = false,
            IncludeConfidenceScores = true,
            CsvConfig = new CsvTemplateConfig
            {
                Columns = new List<string>
                {
                    "ImagePath",
                    "Rank",
                    "Latitude",
                    "Longitude",
                    "FinalProbability",
                    "Location",
                    "ConfidenceLevel"
                },
                IncludeHeader = true,
                Delimiter = ","
            },
            PdfConfig = new PdfTemplateConfig
            {
                LayoutStyle = PdfLayoutStyle.Compact,
                IncludeThumbnail = false,
                IncludeMap = true,
                IncludeIntelligenceWarning = true,
                MaxPredictionsToShow = 5,
                ShowProbabilityBreakdown = false
            }
        };

        /// <summary>
        /// Coordinates Only template - minimal data for mapping
        /// </summary>
        public static ExportTemplate CoordinatesOnly => new()
        {
            Id = "builtin-coordinates",
            Name = "Coordinates Only",
            Description = "Minimal export with just coordinates and rank (for quick mapping)",
            IsBuiltIn = true,
            CoordinateFormat = CoordinateFormat.DecimalDegrees,
            IncludeExifData = true,
            IncludeAiPredictions = true,
            IncludeClusteringInfo = false,
            IncludeConfidenceScores = false,
            CsvConfig = new CsvTemplateConfig
            {
                Columns = new List<string>
                {
                    "ImagePath",
                    "Rank",
                    "Latitude",
                    "Longitude",
                    "Location"
                },
                IncludeHeader = true,
                Delimiter = ","
            },
            PdfConfig = new PdfTemplateConfig
            {
                LayoutStyle = PdfLayoutStyle.SimpleList,
                IncludeThumbnail = false,
                IncludeMap = false,
                IncludeIntelligenceWarning = true,
                MaxPredictionsToShow = 3,
                ShowProbabilityBreakdown = false
            }
        };

        /// <summary>
        /// Get all built-in templates
        /// </summary>
        public static List<ExportTemplate> GetAllBuiltInTemplates()
        {
            return new List<ExportTemplate>
            {
                Detailed,
                Simple,
                CoordinatesOnly
            };
        }
    }
}
