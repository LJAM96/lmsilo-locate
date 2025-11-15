using GeoLens.Models;
using GeoLens.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoLens.Views
{
    /// <summary>
    /// Dialog for previewing export data before saving
    /// Shows first 10 rows/entries with format, size, and record count
    /// </summary>
    public sealed partial class ExportPreviewDialog : ContentDialog
    {
        private readonly EnhancedPredictionResult _result;
        private readonly string _format;
        private readonly ExportTemplate _template;
        private string _previewContent = string.Empty;

        public bool UserConfirmed { get; private set; } = false;

        public ExportPreviewDialog(
            EnhancedPredictionResult result,
            string format,
            ExportTemplate template)
        {
            this.InitializeComponent();

            _result = result ?? throw new ArgumentNullException(nameof(result));
            _format = format ?? throw new ArgumentNullException(nameof(format));
            _template = template ?? throw new ArgumentNullException(nameof(template));

            Loaded += ExportPreviewDialog_Loaded;
        }

        private async void ExportPreviewDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await GeneratePreviewAsync();
        }

        private async Task GeneratePreviewAsync()
        {
            try
            {
                // Update header information
                FormatText.Text = _format.ToUpper();
                TemplateText.Text = _template.Name;

                // Generate preview based on format
                switch (_format.ToLower())
                {
                    case "csv":
                        await GenerateCsvPreviewAsync();
                        break;

                    case "json":
                        await GenerateJsonPreviewAsync();
                        break;

                    case "pdf":
                        GeneratePdfPreview();
                        break;

                    case "kml":
                        await GenerateKmlPreviewAsync();
                        break;

                    default:
                        PreviewText.Text = "Preview not available for this format.";
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExportPreviewDialog] Error generating preview: {ex.Message}");
                PreviewText.Text = $"Error generating preview: {ex.Message}";
            }
        }

        private async Task GenerateCsvPreviewAsync()
        {
            await Task.Run(() =>
            {
                var sb = new StringBuilder();
                var records = BuildCsvRecords();

                // Add header if configured
                if (_template.CsvConfig.IncludeHeader)
                {
                    sb.AppendLine(string.Join(_template.CsvConfig.Delimiter, _template.CsvConfig.Columns));
                }

                // Add first 10 records
                var previewRecords = records.Take(10).ToList();
                foreach (var record in previewRecords)
                {
                    var values = _template.CsvConfig.Columns.Select(col => GetCsvFieldValue(record, col));
                    sb.AppendLine(string.Join(_template.CsvConfig.Delimiter, values));
                }

                // Update UI on dispatcher thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    _previewContent = sb.ToString();
                    PreviewText.Text = _previewContent;
                    RecordCountText.Text = records.Count.ToString();
                    EstimatedSizeText.Text = $"~{(_previewContent.Length * records.Count / 10) / 1024} KB";
                });
            });
        }

        private List<Dictionary<string, object>> BuildCsvRecords()
        {
            var records = new List<Dictionary<string, object>>();

            // Add EXIF GPS data if available and included in template
            if (_template.IncludeExifData && _result.HasExifGps && _result.ExifGps != null)
            {
                records.Add(new Dictionary<string, object>
                {
                    ["ImagePath"] = _result.ImagePath,
                    ["Source"] = "EXIF",
                    ["Rank"] = 0,
                    ["Latitude"] = _result.ExifGps.Latitude,
                    ["Longitude"] = _result.ExifGps.Longitude,
                    ["BaseProbability"] = 0.9,
                    ["ClusteringBoost"] = 0.0,
                    ["FinalProbability"] = 0.9,
                    ["Location"] = _result.ExifGps.LocationName ?? "Unknown",
                    ["ConfidenceLevel"] = "VeryHigh",
                    ["IsPartOfCluster"] = false,
                    ["Altitude"] = _result.ExifGps.Altitude ?? 0.0
                });
            }

            // Add AI predictions if included in template
            if (_template.IncludeAiPredictions)
            {
                foreach (var pred in _result.AiPredictions)
                {
                    var record = new Dictionary<string, object>
                    {
                        ["ImagePath"] = _result.ImagePath,
                        ["Source"] = "AI",
                        ["Rank"] = pred.Rank,
                        ["Latitude"] = pred.Latitude,
                        ["Longitude"] = pred.Longitude,
                        ["BaseProbability"] = pred.Probability,
                        ["ClusteringBoost"] = pred.ConfidenceBoost,
                        ["FinalProbability"] = pred.AdjustedProbability,
                        ["Location"] = pred.LocationSummary,
                        ["ConfidenceLevel"] = pred.ConfidenceLevel.ToString(),
                        ["IsPartOfCluster"] = pred.IsPartOfCluster,
                        ["Altitude"] = 0.0
                    };
                    records.Add(record);
                }
            }

            return records;
        }

        private string GetCsvFieldValue(Dictionary<string, object> record, string fieldName)
        {
            if (!record.ContainsKey(fieldName))
                return string.Empty;

            var value = record[fieldName];
            if (value == null)
                return string.Empty;

            // Format coordinates based on template setting
            if ((fieldName == "Latitude" || fieldName == "Longitude") && value is double coordValue)
            {
                return FormatCoordinate(coordValue, fieldName == "Latitude");
            }

            // Format probabilities as percentages
            if (fieldName.Contains("Probability") && value is double probValue)
            {
                return $"{probValue:P2}";
            }

            return value.ToString() ?? string.Empty;
        }

        private string FormatCoordinate(double value, bool isLatitude)
        {
            switch (_template.CoordinateFormat)
            {
                case CoordinateFormat.DecimalDegrees:
                    return $"{value:F6}";

                case CoordinateFormat.DegreesDecimalMinutes:
                    var degrees = (int)Math.Abs(value);
                    var minutes = (Math.Abs(value) - degrees) * 60;
                    var direction = isLatitude ? (value >= 0 ? "N" : "S") : (value >= 0 ? "E" : "W");
                    return $"{degrees}° {minutes:F3}'{direction}";

                case CoordinateFormat.DegreesMinutesSeconds:
                    var deg = (int)Math.Abs(value);
                    var min = (int)((Math.Abs(value) - deg) * 60);
                    var sec = ((Math.Abs(value) - deg) * 60 - min) * 60;
                    var dir = isLatitude ? (value >= 0 ? "N" : "S") : (value >= 0 ? "E" : "W");
                    return $"{deg}° {min}' {sec:F2}\"{dir}";

                default:
                    return $"{value:F6}";
            }
        }

        private async Task GenerateJsonPreviewAsync()
        {
            await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine($"  \"imagePath\": \"{_result.ImagePath}\",");

                // EXIF GPS
                if (_template.IncludeExifData && _result.HasExifGps && _result.ExifGps != null)
                {
                    sb.AppendLine("  \"exifGps\": {");
                    sb.AppendLine($"    \"latitude\": {_result.ExifGps.Latitude:F6},");
                    sb.AppendLine($"    \"longitude\": {_result.ExifGps.Longitude:F6},");
                    sb.AppendLine($"    \"locationName\": \"{_result.ExifGps.LocationName}\"");
                    sb.AppendLine("  },");
                }

                // AI Predictions (first 10)
                if (_template.IncludeAiPredictions)
                {
                    sb.AppendLine("  \"aiPredictions\": [");
                    var previewPredictions = _result.AiPredictions.Take(10).ToList();
                    for (int i = 0; i < previewPredictions.Count; i++)
                    {
                        var pred = previewPredictions[i];
                        sb.AppendLine("    {");
                        sb.AppendLine($"      \"rank\": {pred.Rank},");
                        sb.AppendLine($"      \"latitude\": {pred.Latitude:F6},");
                        sb.AppendLine($"      \"longitude\": {pred.Longitude:F6},");
                        if (_template.IncludeConfidenceScores)
                        {
                            sb.AppendLine($"      \"baseProbability\": {pred.Probability:F4},");
                            sb.AppendLine($"      \"finalProbability\": {pred.AdjustedProbability:F4},");
                        }
                        sb.AppendLine($"      \"location\": \"{pred.LocationSummary}\"");
                        sb.Append("    }");
                        if (i < previewPredictions.Count - 1)
                            sb.AppendLine(",");
                        else
                            sb.AppendLine();
                    }
                    sb.AppendLine("  ]");
                }

                sb.AppendLine("}");

                DispatcherQueue.TryEnqueue(() =>
                {
                    _previewContent = sb.ToString();
                    PreviewText.Text = _previewContent;
                    RecordCountText.Text = _result.AiPredictions.Count.ToString();
                    EstimatedSizeText.Text = $"~{_previewContent.Length / 1024} KB";
                });
            });
        }

        private void GeneratePdfPreview()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PDF Export Preview");
            sb.AppendLine("==================");
            sb.AppendLine();
            sb.AppendLine($"Layout Style: {_template.PdfConfig.LayoutStyle}");
            sb.AppendLine($"Include Thumbnail: {_template.PdfConfig.IncludeThumbnail}");
            sb.AppendLine($"Include Map: {_template.PdfConfig.IncludeMap}");
            sb.AppendLine($"Max Predictions: {_template.PdfConfig.MaxPredictionsToShow}");
            sb.AppendLine();
            sb.AppendLine("Content:");
            sb.AppendLine("--------");

            // EXIF GPS
            if (_template.IncludeExifData && _result.HasExifGps && _result.ExifGps != null)
            {
                sb.AppendLine();
                sb.AppendLine("EXIF GPS Data:");
                sb.AppendLine($"  Location: {_result.ExifGps.LocationName}");
                sb.AppendLine($"  Coordinates: {_result.ExifGps.Coordinates}");
            }

            // AI Predictions
            if (_template.IncludeAiPredictions)
            {
                sb.AppendLine();
                sb.AppendLine($"AI Predictions (Top {Math.Min(_template.PdfConfig.MaxPredictionsToShow, _result.AiPredictions.Count)}):");
                var previewPredictions = _result.AiPredictions.Take(_template.PdfConfig.MaxPredictionsToShow);
                foreach (var pred in previewPredictions)
                {
                    sb.AppendLine($"  #{pred.Rank}: {pred.LocationSummary}");
                    sb.AppendLine($"    Coordinates: {pred.Coordinates}");
                    if (_template.IncludeConfidenceScores)
                    {
                        sb.AppendLine($"    Probability: {pred.ProbabilityFormatted}");
                    }
                }
            }

            _previewContent = sb.ToString();
            PreviewText.Text = _previewContent;
            RecordCountText.Text = "1 PDF page";
            EstimatedSizeText.Text = "~50-200 KB";
        }

        private async Task GenerateKmlPreviewAsync()
        {
            await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
                sb.AppendLine("  <Document>");
                sb.AppendLine("    <name>GeoLens Predictions</name>");
                sb.AppendLine($"    <description>Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}</description>");
                sb.AppendLine();

                // Show first 10 placemarks as preview
                var allPredictions = new List<(string name, double lat, double lon, string desc)>();

                // EXIF GPS
                if (_template.IncludeExifData && _result.HasExifGps && _result.ExifGps != null)
                {
                    allPredictions.Add((
                        "EXIF GPS Location",
                        _result.ExifGps.Latitude,
                        _result.ExifGps.Longitude,
                        "GPS data from image metadata"
                    ));
                }

                // AI Predictions
                if (_template.IncludeAiPredictions)
                {
                    foreach (var pred in _result.AiPredictions)
                    {
                        allPredictions.Add((
                            $"Prediction #{pred.Rank}",
                            pred.Latitude,
                            pred.Longitude,
                            $"{pred.LocationSummary} - {pred.ProbabilityFormatted}"
                        ));
                    }
                }

                // Preview first 10
                foreach (var (name, lat, lon, desc) in allPredictions.Take(10))
                {
                    sb.AppendLine("    <Placemark>");
                    sb.AppendLine($"      <name>{name}</name>");
                    sb.AppendLine($"      <description>{desc}</description>");
                    sb.AppendLine("      <Point>");
                    sb.AppendLine($"        <coordinates>{lon:F6},{lat:F6},0</coordinates>");
                    sb.AppendLine("      </Point>");
                    sb.AppendLine("    </Placemark>");
                }

                sb.AppendLine("  </Document>");
                sb.AppendLine("</kml>");

                DispatcherQueue.TryEnqueue(() =>
                {
                    _previewContent = sb.ToString();
                    PreviewText.Text = _previewContent;
                    RecordCountText.Text = $"{allPredictions.Count} placemarks";
                    EstimatedSizeText.Text = $"~{(_previewContent.Length * allPredictions.Count / Math.Min(10, allPredictions.Count)) / 1024} KB";
                });
            });
        }

        private void PrimaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            UserConfirmed = true;
            Debug.WriteLine("[ExportPreviewDialog] User confirmed export");
        }

        private void SecondaryButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Copy preview to clipboard
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(_previewContent);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                Debug.WriteLine("[ExportPreviewDialog] Preview copied to clipboard");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExportPreviewDialog] Failed to copy to clipboard: {ex.Message}");
            }

            // Don't close the dialog
            args.Cancel = true;
        }

        private void CloseButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            UserConfirmed = false;
            Debug.WriteLine("[ExportPreviewDialog] User cancelled export");
        }
    }
}
