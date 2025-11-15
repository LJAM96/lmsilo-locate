using GeoLens.Models;
using GeoLens.Services.DTOs;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GeoLens.Services
{
    /// <summary>
    /// Orchestrates the complete prediction pipeline: cache → EXIF → API → clustering → cache storage
    /// </summary>
    public class PredictionProcessor
    {
        private readonly PredictionCacheService _cacheService;
        private readonly ExifMetadataExtractor _exifExtractor;
        private readonly GeoCLIPApiClient _apiClient;

        public PredictionProcessor(
            PredictionCacheService cacheService,
            ExifMetadataExtractor exifExtractor,
            GeoCLIPApiClient apiClient)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _exifExtractor = exifExtractor ?? throw new ArgumentNullException(nameof(exifExtractor));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        /// <summary>
        /// Process a single image through the complete prediction pipeline
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="topK">Number of predictions to return (default: 5)</param>
        /// <param name="device">Device to use for inference: auto, cpu, cuda, rocm (default: auto)</param>
        /// <param name="forceApiCall">Force API call even if cached result exists (default: false)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Enhanced prediction result with all processing complete</returns>
        public async Task<EnhancedPredictionResult> ProcessImageAsync(
            string imagePath,
            int topK = 5,
            string device = "auto",
            bool forceApiCall = false,
            CancellationToken cancellationToken = default)
        {
            // Start timing for audit log
            var stopwatch = Stopwatch.StartNew();
            string? imageHash = null;
            var fileName = System.IO.Path.GetFileName(imagePath);

            try
            {
                Log.Information("Starting pipeline for: {FileName}", fileName);

                // Compute image hash early for audit logging
                imageHash = await _cacheService.ComputeImageHashAsync(imagePath);

                // Step 1: Check cache first (unless force refresh)
                if (!forceApiCall)
                {
                    var cached = await _cacheService.GetCachedPredictionAsync(imagePath);
                    if (cached != null)
                    {
                        Log.Information("Cache HIT for: {FileName}", fileName);

                        // Log audit for cached result
                        stopwatch.Stop();
                        await LogAuditAsync(imagePath, fileName, imageHash, cached.Predictions, cached.ExifGps?.HasGps ?? false, stopwatch.ElapsedMilliseconds, true, null);

                        return await BuildResultFromCacheAsync(cached);
                    }
                }

                Log.Information("Cache MISS - proceeding with full pipeline");

                // Step 2: Extract EXIF GPS data
                var exifGps = await _exifExtractor.ExtractGpsDataAsync(imagePath);
                if (exifGps?.HasGps == true)
                {
                    Log.Information("Found GPS in EXIF: {Latitude:F6}, {Longitude:F6}", exifGps.Latitude, exifGps.Longitude);
                }
                else
                {
                    Log.Debug("No GPS data in EXIF");
                }

                // Step 3: Call API for AI predictions
                Log.Information("Calling GeoCLIP API (device={Device}, topK={TopK})", device, topK);
                var apiResult = await _apiClient.InferSingleAsync(imagePath, topK, device, cancellationToken);

                if (apiResult == null)
                {
                    Log.Warning("API call failed - no result returned");
                    return CreateEmptyResult(imagePath, exifGps);
                }

                if (!string.IsNullOrEmpty(apiResult.Error))
                {
                    Log.Warning("API returned error: {Error}", apiResult.Error);
                    return CreateEmptyResult(imagePath, exifGps, apiResult.Error);
                }

                // Step 4: Convert API predictions to enhanced predictions
                var enhancedPredictions = ConvertToEnhancedPredictions(apiResult.Predictions);
                Log.Debug("Converted {PredictionCount} predictions", enhancedPredictions.Count);

                // Step 5: Store in cache for future lookups
                try
                {
                    await _cacheService.StorePredictionAsync(imagePath, apiResult.Predictions, exifGps);
                    Log.Debug("Stored result in cache");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to store in cache (non-fatal)");
                    // Cache storage failure should not prevent returning the result
                }

                // Step 6: Build and return final result
                var result = new EnhancedPredictionResult
                {
                    ImagePath = imagePath,
                    AiPredictions = enhancedPredictions,
                    ExifGps = exifGps,
                    ClusterInfo = null,
                    FromCache = false,
                    ProcessedAt = DateTime.UtcNow
                };

                // Log successful processing to audit
                stopwatch.Stop();
                await LogAuditAsync(imagePath, fileName, imageHash ?? "unknown", apiResult.Predictions, exifGps?.HasGps ?? false, stopwatch.ElapsedMilliseconds, true, null);

                Log.Information("Pipeline complete for: {FileName}", fileName);
                return result;
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "Network error in pipeline");

                // Log failed processing to audit
                stopwatch.Stop();
                await LogAuditAsync(imagePath, fileName, imageHash ?? "unknown", new List<PredictionCandidate>(), false, stopwatch.ElapsedMilliseconds, false, $"Network error: {ex.Message}");

                return CreateEmptyResult(imagePath, null, $"Network error: Unable to connect to GeoCLIP service");
            }
            catch (TaskCanceledException ex)
            {
                Log.Warning(ex, "Pipeline cancelled or timed out");

                // Log cancelled processing to audit
                stopwatch.Stop();
                await LogAuditAsync(imagePath, fileName, imageHash ?? "unknown", new List<PredictionCandidate>(), false, stopwatch.ElapsedMilliseconds, false, "Operation cancelled or timed out");

                return CreateEmptyResult(imagePath, null, "Operation cancelled or timed out");
            }
            catch (OperationCanceledException ex)
            {
                Log.Warning(ex, "Pipeline operation cancelled");

                // Log cancelled processing to audit
                stopwatch.Stop();
                await LogAuditAsync(imagePath, fileName, imageHash ?? "unknown", new List<PredictionCandidate>(), false, stopwatch.ElapsedMilliseconds, false, "Operation cancelled");

                return CreateEmptyResult(imagePath, null, "Operation cancelled");
            }
            catch (IOException ex)
            {
                Log.Error(ex, "File I/O error in pipeline");

                // Log failed processing to audit
                stopwatch.Stop();
                await LogAuditAsync(imagePath, fileName, imageHash ?? "unknown", new List<PredictionCandidate>(), false, stopwatch.ElapsedMilliseconds, false, $"I/O error: {ex.Message}");

                return CreateEmptyResult(imagePath, null, $"File I/O error: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error(ex, "Access denied in pipeline");

                // Log failed processing to audit
                stopwatch.Stop();
                await LogAuditAsync(imagePath, fileName, imageHash ?? "unknown", new List<PredictionCandidate>(), false, stopwatch.ElapsedMilliseconds, false, $"Access denied: {ex.Message}");

                return CreateEmptyResult(imagePath, null, $"Access denied: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected pipeline error: {ExceptionType}", ex.GetType().Name);

                // Log failed processing to audit
                stopwatch.Stop();
                await LogAuditAsync(imagePath, fileName, imageHash ?? "unknown", new List<PredictionCandidate>(), false, stopwatch.ElapsedMilliseconds, false, $"Unexpected error: {ex.Message}");

                return CreateEmptyResult(imagePath, null, $"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Process multiple images in batch with progress reporting
        /// </summary>
        /// <param name="imagePaths">List of image paths to process</param>
        /// <param name="topK">Number of predictions per image (default: 5)</param>
        /// <param name="device">Device to use for inference (default: auto)</param>
        /// <param name="forceApiCall">Force API call even if cached results exist (default: false)</param>
        /// <param name="progress">Progress reporter (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of enhanced prediction results</returns>
        public async Task<List<EnhancedPredictionResult>> ProcessBatchAsync(
            List<string> imagePaths,
            int topK = 5,
            string device = "auto",
            bool forceApiCall = false,
            IProgress<BatchProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<EnhancedPredictionResult>();
            var totalImages = imagePaths.Count;
            var cachedCount = 0;

            Log.Information("Starting batch processing: {TotalImages} images", totalImages);

            try
            {
                // Process each image through the pipeline
                for (int i = 0; i < imagePaths.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log.Information("Batch processing cancelled at {ProcessedCount}/{TotalImages}", i, totalImages);
                        break;
                    }

                    var imagePath = imagePaths[i];
                    var fileName = System.IO.Path.GetFileName(imagePath);

                    // Report progress
                    progress?.Report(new BatchProgress
                    {
                        TotalImages = totalImages,
                        ProcessedImages = i,
                        CachedImages = cachedCount,
                        CurrentImage = fileName
                    });

                    // Process through pipeline
                    var result = await ProcessImageAsync(imagePath, topK, device, forceApiCall, cancellationToken);
                    results.Add(result);

                    if (result.FromCache)
                    {
                        cachedCount++;
                    }

                    Log.Debug("Batch progress: {ProcessedCount}/{TotalImages} ({CachedCount} cached)", i + 1, totalImages, cachedCount);
                }

                // Report final progress
                progress?.Report(new BatchProgress
                {
                    TotalImages = totalImages,
                    ProcessedImages = totalImages,
                    CachedImages = cachedCount,
                    CurrentImage = null
                });

                Log.Information("Batch complete: {ResultCount} results ({CachedCount} from cache)", results.Count, cachedCount);
            }
            catch (OperationCanceledException ex)
            {
                Log.Warning(ex, "Batch processing cancelled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected batch processing error: {ExceptionType}", ex.GetType().Name);
            }

            return results;
        }

        /// <summary>
        /// Build result from cached entry
        /// </summary>
        private Task<EnhancedPredictionResult> BuildResultFromCacheAsync(CachedPredictionEntry cached)
        {
            // Convert cached predictions to enhanced predictions
            var enhancedPredictions = ConvertToEnhancedPredictions(cached.Predictions);

            return Task.FromResult(new EnhancedPredictionResult
            {
                ImagePath = cached.FilePath,
                AiPredictions = enhancedPredictions,
                ExifGps = cached.ExifGps,
                ClusterInfo = null,
                FromCache = true,
                ProcessedAt = cached.CachedAt
            });
        }

        /// <summary>
        /// Convert API prediction candidates to enhanced predictions
        /// </summary>
        private List<EnhancedLocationPrediction> ConvertToEnhancedPredictions(List<PredictionCandidate> candidates)
        {
            var enhanced = new List<EnhancedLocationPrediction>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];

                var prediction = new EnhancedLocationPrediction
                {
                    Rank = i + 1,
                    Latitude = candidate.Latitude,
                    Longitude = candidate.Longitude,
                    Probability = candidate.Probability,
                    AdjustedProbability = candidate.Probability, // Will be updated by clustering
                    City = candidate.City ?? string.Empty,
                    State = candidate.State ?? string.Empty,
                    County = candidate.County ?? string.Empty,
                    Country = candidate.Country ?? string.Empty,
                    LocationSummary = BuildLocationSummary(candidate),
                    IsPartOfCluster = false,
                    ConfidenceLevel = ClassifyConfidence(candidate.Probability)
                };

                enhanced.Add(prediction);
            }

            return enhanced;
        }

        /// <summary>
        /// Build a location summary string from prediction components
        /// </summary>
        private string BuildLocationSummary(PredictionCandidate candidate)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(candidate.City))
                parts.Add(candidate.City);

            if (!string.IsNullOrEmpty(candidate.State))
                parts.Add(candidate.State);

            if (!string.IsNullOrEmpty(candidate.Country))
                parts.Add(candidate.Country);

            return parts.Count > 0 ? string.Join(", ", parts) : "Unknown Location";
        }

        /// <summary>
        /// Classify confidence level based on probability
        /// </summary>
        private ConfidenceLevel ClassifyConfidence(double probability)
        {
            return probability switch
            {
                >= 0.1 => ConfidenceLevel.High,
                >= 0.05 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            };
        }

        /// <summary>
        /// Create an empty result (used when errors occur or no predictions available)
        /// </summary>
        private EnhancedPredictionResult CreateEmptyResult(string imagePath, ExifGpsData? exifGps, string? error = null)
        {
            if (!string.IsNullOrEmpty(error))
            {
                Log.Warning("Creating empty result with error: {Error}", error);
            }

            return new EnhancedPredictionResult
            {
                ImagePath = imagePath,
                AiPredictions = new List<EnhancedLocationPrediction>(),
                ExifGps = exifGps,
                ClusterInfo = null,
                FromCache = false,
                ProcessedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Logs a processing operation to the audit log.
        /// </summary>
        private async Task LogAuditAsync(
            string imagePath,
            string fileName,
            string imageHash,
            List<PredictionCandidate> predictions,
            bool exifGpsPresent,
            long elapsedMs,
            bool success,
            string? errorMessage)
        {
            try
            {
                // Convert PredictionCandidates to PredictionResults for audit log
                var predictionResults = predictions.Select(p => new PredictionResult
                {
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Probability = p.Probability,
                    LocationName = BuildLocationSummaryFromCandidate(p),
                    City = p.City ?? string.Empty,
                    State = p.State ?? string.Empty,
                    County = p.County ?? string.Empty,
                    Country = p.Country ?? string.Empty
                }).ToList();

                var auditEntry = new AuditLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Filename = fileName,
                    Filepath = imagePath,
                    ImageHash = imageHash,
                    WindowsUser = Environment.UserName,
                    ProcessingTimeMs = (int)elapsedMs,
                    Predictions = predictionResults,
                    ExifGpsPresent = exifGpsPresent,
                    Success = success,
                    ErrorMessage = errorMessage
                };

                await App.AuditService.LogProcessingOperationAsync(auditEntry);
            }
            catch (Exception ex)
            {
                // Audit logging should never break the main flow
                Log.Warning(ex, "Failed to write audit log");
            }
        }

        /// <summary>
        /// Build a location summary string from prediction candidate (for audit log).
        /// </summary>
        private string BuildLocationSummaryFromCandidate(PredictionCandidate candidate)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(candidate.City))
                parts.Add(candidate.City);

            if (!string.IsNullOrEmpty(candidate.State))
                parts.Add(candidate.State);

            if (!string.IsNullOrEmpty(candidate.Country))
                parts.Add(candidate.Country);

            return parts.Count > 0 ? string.Join(", ", parts) : "Unknown Location";
        }
    }

    /// <summary>
    /// Progress information for batch processing
    /// </summary>
    public class BatchProgress
    {
        public int TotalImages { get; set; }
        public int ProcessedImages { get; set; }
        public int CachedImages { get; set; }
        public string? CurrentImage { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage => TotalImages > 0 ? (ProcessedImages * 100.0) / TotalImages : 0;

        /// <summary>
        /// Number of images processed via API (not from cache)
        /// </summary>
        public int ApiProcessedImages => ProcessedImages - CachedImages;

        /// <summary>
        /// Human-readable progress message
        /// </summary>
        public string ProgressMessage
        {
            get
            {
                if (ProcessedImages == TotalImages)
                {
                    return $"Complete: {TotalImages} images ({CachedImages} from cache, {ApiProcessedImages} via AI)";
                }

                if (!string.IsNullOrEmpty(CurrentImage))
                {
                    return $"Processing {CurrentImage} ({ProcessedImages + 1}/{TotalImages})...";
                }

                return $"Processing {ProcessedImages}/{TotalImages}...";
            }
        }
    }
}
