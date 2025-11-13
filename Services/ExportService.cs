using CsvHelper;
using CsvHelper.Configuration;
using GeoLens.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace GeoLens.Services
{
    /// <summary>
    /// Service for exporting prediction results to multiple formats (CSV, JSON, PDF, KML)
    /// </summary>
    public class ExportService
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public ExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        #region CSV Export

        /// <summary>
        /// Export predictions to CSV format
        /// </summary>
        public async Task<string> ExportToCsvAsync(
            EnhancedPredictionResult result,
            string outputPath)
        {
            try
            {
                var records = BuildCsvRecords(result);

                using var writer = new StreamWriter(outputPath);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

                await csv.WriteRecordsAsync(records);
                await writer.FlushAsync();

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export CSV: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export multiple images to CSV format
        /// </summary>
        public async Task<string> ExportBatchToCsvAsync(
            IEnumerable<EnhancedPredictionResult> results,
            string outputPath)
        {
            try
            {
                var records = results.SelectMany(BuildCsvRecords).ToList();

                using var writer = new StreamWriter(outputPath);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

                await csv.WriteRecordsAsync(records);
                await writer.FlushAsync();

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export batch CSV: {ex.Message}", ex);
            }
        }

        private List<CsvExportRecord> BuildCsvRecords(EnhancedPredictionResult result)
        {
            var records = new List<CsvExportRecord>();

            // Add EXIF GPS data first if available
            if (result.HasExifGps && result.ExifGps != null)
            {
                records.Add(new CsvExportRecord
                {
                    ImagePath = result.ImagePath,
                    Source = "EXIF",
                    Rank = 0,
                    Latitude = result.ExifGps.Latitude,
                    Longitude = result.ExifGps.Longitude,
                    BaseProbability = 1.0,
                    ClusteringBoost = 0.0,
                    FinalProbability = 1.0,
                    Location = result.ExifGps.LocationName ?? "Unknown",
                    ConfidenceLevel = "VeryHigh",
                    Altitude = result.ExifGps.Altitude
                });
            }

            // Add AI predictions
            foreach (var pred in result.AiPredictions)
            {
                records.Add(new CsvExportRecord
                {
                    ImagePath = result.ImagePath,
                    Source = "AI",
                    Rank = pred.Rank,
                    Latitude = pred.Latitude,
                    Longitude = pred.Longitude,
                    BaseProbability = pred.Probability,
                    ClusteringBoost = pred.ConfidenceBoost,
                    FinalProbability = pred.AdjustedProbability,
                    Location = pred.LocationSummary,
                    ConfidenceLevel = pred.ConfidenceText,
                    IsPartOfCluster = pred.IsPartOfCluster
                });
            }

            return records;
        }

        private class CsvExportRecord
        {
            public string ImagePath { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public int Rank { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double BaseProbability { get; set; }
            public double ClusteringBoost { get; set; }
            public double FinalProbability { get; set; }
            public string Location { get; set; } = string.Empty;
            public string ConfidenceLevel { get; set; } = string.Empty;
            public bool IsPartOfCluster { get; set; }
            public double? Altitude { get; set; }
        }

        #endregion

        #region JSON Export

        /// <summary>
        /// Export predictions to JSON format
        /// </summary>
        public async Task<string> ExportToJsonAsync(
            EnhancedPredictionResult result,
            string outputPath)
        {
            try
            {
                var exportData = BuildJsonExport(result);
                var json = JsonSerializer.Serialize(exportData, _jsonOptions);

                await File.WriteAllTextAsync(outputPath, json);
                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export JSON: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export multiple images to JSON format
        /// </summary>
        public async Task<string> ExportBatchToJsonAsync(
            IEnumerable<EnhancedPredictionResult> results,
            string outputPath)
        {
            try
            {
                var exportData = new JsonBatchExport
                {
                    ExportDate = DateTime.UtcNow,
                    TotalImages = results.Count(),
                    Images = results.Select(BuildJsonExport).ToList()
                };

                var json = JsonSerializer.Serialize(exportData, _jsonOptions);
                await File.WriteAllTextAsync(outputPath, json);

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export batch JSON: {ex.Message}", ex);
            }
        }

        private JsonImageExport BuildJsonExport(EnhancedPredictionResult result)
        {
            return new JsonImageExport
            {
                ImagePath = result.ImagePath,
                ExifGps = result.HasExifGps && result.ExifGps != null
                    ? new JsonExifGps
                    {
                        Latitude = result.ExifGps.Latitude,
                        Longitude = result.ExifGps.Longitude,
                        LocationName = result.ExifGps.LocationName,
                        Altitude = result.ExifGps.Altitude
                    }
                    : null,
                AiPredictions = result.AiPredictions.Select(p => new JsonPrediction
                {
                    Rank = p.Rank,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    BaseProbability = p.Probability,
                    ClusteringBoost = p.ConfidenceBoost,
                    FinalProbability = p.AdjustedProbability,
                    City = p.City,
                    State = p.State,
                    County = p.County,
                    Country = p.Country,
                    LocationSummary = p.LocationSummary,
                    ConfidenceLevel = p.ConfidenceLevel,
                    IsPartOfCluster = p.IsPartOfCluster
                }).ToList(),
                ClusterInfo = result.ClusterInfo != null && result.ClusterInfo.IsClustered
                    ? new JsonClusterInfo
                    {
                        IsClustered = result.ClusterInfo.IsClustered,
                        ClusterRadius = result.ClusterInfo.ClusterRadius,
                        AverageDistance = result.ClusterInfo.AverageDistance,
                        ConfidenceBoost = result.ClusterInfo.ConfidenceBoost,
                        ClusterCenterLat = result.ClusterInfo.ClusterCenterLat,
                        ClusterCenterLon = result.ClusterInfo.ClusterCenterLon
                    }
                    : null,
                ReliabilityMessage = result.ReliabilityMessage
            };
        }

        private class JsonBatchExport
        {
            public DateTime ExportDate { get; set; }
            public int TotalImages { get; set; }
            public List<JsonImageExport> Images { get; set; } = new();
        }

        private class JsonImageExport
        {
            public string ImagePath { get; set; } = string.Empty;
            public JsonExifGps? ExifGps { get; set; }
            public List<JsonPrediction> AiPredictions { get; set; } = new();
            public JsonClusterInfo? ClusterInfo { get; set; }
            public string ReliabilityMessage { get; set; } = string.Empty;
        }

        private class JsonExifGps
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string? LocationName { get; set; }
            public double? Altitude { get; set; }
        }

        private class JsonPrediction
        {
            public int Rank { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public double BaseProbability { get; set; }
            public double ClusteringBoost { get; set; }
            public double FinalProbability { get; set; }
            public string City { get; set; } = string.Empty;
            public string State { get; set; } = string.Empty;
            public string County { get; set; } = string.Empty;
            public string Country { get; set; } = string.Empty;
            public string LocationSummary { get; set; } = string.Empty;
            public ConfidenceLevel ConfidenceLevel { get; set; }
            public bool IsPartOfCluster { get; set; }
        }

        private class JsonClusterInfo
        {
            public bool IsClustered { get; set; }
            public double ClusterRadius { get; set; }
            public double AverageDistance { get; set; }
            public double ConfidenceBoost { get; set; }
            public double ClusterCenterLat { get; set; }
            public double ClusterCenterLon { get; set; }
        }

        #endregion

        #region PDF Export

        /// <summary>
        /// Export predictions to PDF format with thumbnails and map
        /// </summary>
        public async Task<string> ExportToPdfAsync(
            EnhancedPredictionResult result,
            string outputPath,
            byte[]? thumbnailBytes = null)
        {
            try
            {
                await Task.Run(() =>
                {
                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.Letter);
                            page.Margin(40);
                            page.DefaultTextStyle(x => x.FontSize(10).FontColor("#FFFFFF"));
                            page.PageColor("#1E1E1E"); // Dark background

                            page.Header().Element(header =>
                            {
                                ComposeHeader(header, result);
                            });

                            page.Content().Element(content =>
                            {
                                ComposeContent(content, result, thumbnailBytes);
                            });

                            page.Footer().Element(footer =>
                            {
                                ComposeFooter(footer);
                            });
                        });
                    }).GeneratePdf(outputPath);
                });

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export PDF: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export multiple images to PDF format
        /// </summary>
        public async Task<string> ExportBatchToPdfAsync(
            IEnumerable<EnhancedPredictionResult> results,
            string outputPath,
            Dictionary<string, byte[]>? thumbnails = null)
        {
            try
            {
                await Task.Run(() =>
                {
                    Document.Create(container =>
                    {
                        // Title page
                        container.Page(page =>
                        {
                            page.Size(PageSizes.Letter);
                            page.Margin(40);
                            page.DefaultTextStyle(x => x.FontSize(10).FontColor("#FFFFFF"));
                            page.PageColor("#1E1E1E");

                            page.Content().Column(column =>
                            {
                                column.Spacing(20);
                                column.Item().AlignCenter().Text("GeoLens Batch Export Report")
                                    .FontSize(24).Bold().FontColor("#4FC3F7");
                                column.Item().AlignCenter().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                                    .FontSize(12).FontColor("#B0B0B0");
                                column.Item().AlignCenter().Text($"Total Images: {results.Count()}")
                                    .FontSize(14).Bold();
                            });
                        });

                        // Individual image pages
                        foreach (var result in results)
                        {
                            container.Page(page =>
                            {
                                page.Size(PageSizes.Letter);
                                page.Margin(40);
                                page.DefaultTextStyle(x => x.FontSize(10).FontColor("#FFFFFF"));
                                page.PageColor("#1E1E1E");

                                page.Header().Element(header =>
                                {
                                    ComposeHeader(header, result);
                                });

                                page.Content().Element(content =>
                                {
                                    var thumb = thumbnails?.GetValueOrDefault(result.ImagePath);
                                    ComposeContent(content, result, thumb);
                                });

                                page.Footer().Element(footer =>
                                {
                                    ComposeFooter(footer);
                                });
                            });
                        }
                    }).GeneratePdf(outputPath);
                });

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export batch PDF: {ex.Message}", ex);
            }
        }

        private void ComposeHeader(IContainer container, EnhancedPredictionResult result)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().Text("GeoLens Location Analysis Report")
                    .FontSize(20).Bold().FontColor("#4FC3F7");
                column.Item().Text($"Image: {Path.GetFileName(result.ImagePath)}")
                    .FontSize(12).FontColor("#B0B0B0");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor("#424242");
            });
        }

        private void ComposeContent(IContainer container, EnhancedPredictionResult result, byte[]? thumbnailBytes)
        {
            container.PaddingTop(10).Column(column =>
            {
                column.Spacing(15);

                // Thumbnail (if available)
                if (thumbnailBytes != null && thumbnailBytes.Length > 0)
                {
                    column.Item().AlignCenter().MaxWidth(300).Image(thumbnailBytes);
                }

                // Reliability message
                column.Item().Background("#2D2D2D").Padding(10).Text(result.ReliabilityMessage)
                    .FontSize(11).Bold().FontColor("#81C784");

                // EXIF GPS Data
                if (result.HasExifGps && result.ExifGps != null)
                {
                    column.Item().Element(c => ComposeExifSection(c, result.ExifGps));
                }

                // AI Predictions
                if (result.AiPredictions.Any())
                {
                    column.Item().Element(c => ComposePredictionsSection(c, result.AiPredictions));
                }

                // Cluster Info
                if (result.ClusterInfo != null && result.ClusterInfo.IsClustered)
                {
                    column.Item().Element(c => ComposeClusterSection(c, result.ClusterInfo));
                }
            });
        }

        private void ComposeExifSection(IContainer container, ExifGpsData exif)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().Text("EXIF GPS Data").FontSize(14).Bold().FontColor("#00E5FF");
                column.Item().Background("#2D2D2D").Padding(10).Column(inner =>
                {
                    inner.Spacing(3);
                    inner.Item().Text($"Coordinates: {exif.Coordinates}").FontColor("#E0E0E0");
                    if (!string.IsNullOrEmpty(exif.LocationName))
                        inner.Item().Text($"Location: {exif.LocationName}").FontColor("#E0E0E0");
                    if (exif.Altitude.HasValue)
                        inner.Item().Text($"Altitude: {exif.Altitude:F1} meters").FontColor("#E0E0E0");
                });
            });
        }

        private void ComposePredictionsSection(IContainer container, List<EnhancedLocationPrediction> predictions)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().Text("AI Predictions (Top 5)").FontSize(14).Bold().FontColor("#76FF03");

                // Take top 5 predictions
                foreach (var pred in predictions.Take(5))
                {
                    column.Item().Background("#2D2D2D").Padding(8).Column(inner =>
                    {
                        inner.Spacing(2);
                        inner.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"#{pred.Rank}: {pred.LocationSummary}")
                                .FontSize(11).Bold().FontColor(GetConfidenceColor(pred.ConfidenceLevel));
                            row.AutoItem().Text($"{pred.ProbabilityFormatted}")
                                .FontSize(10).FontWeight(QuestPDF.Infrastructure.FontWeight.Bold).FontColor("#FFFFFF");
                        });

                        // Show probability breakdown if boost was applied
                        if (pred.HasBoost)
                        {
                            inner.Item().Text($"Breakdown: {pred.ProbabilityBreakdown}")
                                .FontSize(8).FontColor("#76FF03");
                        }

                        inner.Item().Text($"Coordinates: {pred.Coordinates}")
                            .FontSize(9).FontColor("#E0E0E0");
                        inner.Item().Text($"Confidence: {pred.ConfidenceText}")
                            .FontSize(9).FontColor(GetConfidenceColor(pred.ConfidenceLevel));
                    });
                }
            });
        }

        private void ComposeClusterSection(IContainer container, ClusterAnalysisResult cluster)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().Text("Geographic Clustering").FontSize(14).Bold().FontColor("#FFD740");
                column.Item().Background("#2D2D2D").Padding(10).Column(inner =>
                {
                    inner.Spacing(3);
                    inner.Item().Text($"Cluster Radius: {cluster.ClusterRadius:F1} km").FontColor("#E0E0E0");
                    inner.Item().Text($"Average Distance: {cluster.AverageDistance:F1} km").FontColor("#E0E0E0");
                    inner.Item().Text($"Confidence Boost: +{cluster.ConfidenceBoost:P0}").FontColor("#E0E0E0");
                    inner.Item().Text($"Center: {FormatCoordinate(cluster.ClusterCenterLat, true)}, {FormatCoordinate(cluster.ClusterCenterLon, false)}")
                        .FontColor("#E0E0E0");
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("Generated by ").FontSize(8).FontColor("#757575");
                text.Span("GeoLens").FontSize(8).Bold().FontColor("#4FC3F7");
                text.Span($" on {DateTime.Now:yyyy-MM-dd HH:mm:ss}").FontSize(8).FontColor("#757575");
            });
        }

        private string GetConfidenceColor(ConfidenceLevel level)
        {
            return level switch
            {
                ConfidenceLevel.VeryHigh => "#00E5FF",
                ConfidenceLevel.High => "#76FF03",
                ConfidenceLevel.Medium => "#FFD740",
                ConfidenceLevel.Low => "#FF5252",
                _ => "#9E9E9E"
            };
        }

        #endregion

        #region KML Export

        /// <summary>
        /// Export predictions to KML format for Google Earth
        /// </summary>
        public async Task<string> ExportToKmlAsync(
            EnhancedPredictionResult result,
            string outputPath)
        {
            try
            {
                var kml = BuildKmlDocument(new[] { result });
                await File.WriteAllTextAsync(outputPath, kml.ToString());

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export KML: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Export multiple images to KML format
        /// </summary>
        public async Task<string> ExportBatchToKmlAsync(
            IEnumerable<EnhancedPredictionResult> results,
            string outputPath)
        {
            try
            {
                var kml = BuildKmlDocument(results);
                await File.WriteAllTextAsync(outputPath, kml.ToString());

                return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export batch KML: {ex.Message}", ex);
            }
        }

        private XDocument BuildKmlDocument(IEnumerable<EnhancedPredictionResult> results)
        {
            var kmlNs = XNamespace.Get("http://www.opengis.net/kml/2.2");

            var document = new XElement(kmlNs + "Document",
                new XElement(kmlNs + "name", "GeoLens Predictions"),
                new XElement(kmlNs + "description", $"Generated by GeoLens on {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
            );

            // Add style definitions for confidence levels
            document.Add(CreateKmlStyle(kmlNs, "veryHighStyle", "00ffff")); // Cyan
            document.Add(CreateKmlStyle(kmlNs, "highStyle", "00ff00"));     // Green
            document.Add(CreateKmlStyle(kmlNs, "mediumStyle", "00ffff"));   // Yellow
            document.Add(CreateKmlStyle(kmlNs, "lowStyle", "0000ff"));      // Red
            document.Add(CreateKmlStyle(kmlNs, "exifStyle", "ffff00"));     // Bright cyan

            // Add placemarks for each result
            foreach (var result in results)
            {
                var imageName = Path.GetFileName(result.ImagePath);

                // Create folder for this image
                var folder = new XElement(kmlNs + "Folder",
                    new XElement(kmlNs + "name", imageName)
                );

                // Add EXIF GPS placemark if available
                if (result.HasExifGps && result.ExifGps != null)
                {
                    var exifPlacemark = CreateKmlPlacemark(
                        kmlNs,
                        $"{imageName} - EXIF GPS",
                        result.ExifGps.LocationName ?? "EXIF Location",
                        result.ExifGps.Latitude,
                        result.ExifGps.Longitude,
                        result.ExifGps.Altitude,
                        "#exifStyle",
                        "EXIF GPS data from image metadata"
                    );
                    folder.Add(exifPlacemark);
                }

                // Add AI prediction placemarks (top 10)
                foreach (var pred in result.AiPredictions.Take(10))
                {
                    var styleUrl = pred.ConfidenceLevel switch
                    {
                        ConfidenceLevel.VeryHigh => "#veryHighStyle",
                        ConfidenceLevel.High => "#highStyle",
                        ConfidenceLevel.Medium => "#mediumStyle",
                        ConfidenceLevel.Low => "#lowStyle",
                        _ => "#lowStyle"
                    };

                    var description = $"Rank: {pred.Rank}\n" +
                                     $"Final Probability: {pred.ProbabilityFormatted}\n" +
                                     $"Confidence: {pred.ConfidenceText}\n" +
                                     $"Coordinates: {pred.Coordinates}\n" +
                                     $"Location: {pred.LocationSummary}";

                    if (pred.HasBoost)
                    {
                        description += $"\n\nProbability Breakdown:\n" +
                                      $"  Base: {pred.OriginalProbabilityFormatted}\n" +
                                      $"  Clustering Boost: {pred.BoostFormatted}\n" +
                                      $"  Final: {pred.AdjustedProbabilityFormatted}";
                    }

                    if (pred.IsPartOfCluster)
                    {
                        description += "\n\nPart of geographic cluster";
                    }

                    var placemark = CreateKmlPlacemark(
                        kmlNs,
                        $"{imageName} - Prediction #{pred.Rank}",
                        pred.LocationSummary,
                        pred.Latitude,
                        pred.Longitude,
                        null,
                        styleUrl,
                        description
                    );
                    folder.Add(placemark);
                }

                document.Add(folder);
            }

            return new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(kmlNs + "kml",
                    document
                )
            );
        }

        private XElement CreateKmlStyle(XNamespace ns, string id, string color)
        {
            return new XElement(ns + "Style",
                new XAttribute("id", id),
                new XElement(ns + "IconStyle",
                    new XElement(ns + "color", color),
                    new XElement(ns + "scale", "1.2"),
                    new XElement(ns + "Icon",
                        new XElement(ns + "href", "http://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png")
                    )
                ),
                new XElement(ns + "LabelStyle",
                    new XElement(ns + "color", color),
                    new XElement(ns + "scale", "0.8")
                )
            );
        }

        private XElement CreateKmlPlacemark(
            XNamespace ns,
            string name,
            string snippet,
            double latitude,
            double longitude,
            double? altitude,
            string styleUrl,
            string description)
        {
            var placemark = new XElement(ns + "Placemark",
                new XElement(ns + "name", name),
                new XElement(ns + "styleUrl", styleUrl),
                new XElement(ns + "description", description)
            );

            if (!string.IsNullOrEmpty(snippet))
            {
                placemark.Add(new XElement(ns + "snippet", snippet));
            }

            var coordinates = altitude.HasValue
                ? $"{longitude},{latitude},{altitude}"
                : $"{longitude},{latitude},0";

            placemark.Add(new XElement(ns + "Point",
                new XElement(ns + "coordinates", coordinates)
            ));

            return placemark;
        }

        #endregion

        #region Helper Methods

        private string FormatCoordinate(double value, bool isLatitude)
        {
            var direction = isLatitude
                ? (value >= 0 ? "N" : "S")
                : (value >= 0 ? "E" : "W");
            return $"{Math.Abs(value):F6}° {direction}";
        }

        /// <summary>
        /// Show file save picker for export
        /// </summary>
        public async Task<string?> ShowSaveFilePickerAsync(
            IntPtr windowHandle,
            string suggestedFileName,
            string fileTypeDescription,
            string fileExtension)
        {
            var savePicker = new FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, windowHandle);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.SuggestedFileName = suggestedFileName;
            savePicker.FileTypeChoices.Add(fileTypeDescription, new List<string> { fileExtension });

            var file = await savePicker.PickSaveFileAsync();
            return file?.Path;
        }

        #endregion
    }
}
