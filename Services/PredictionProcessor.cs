using GeoLens.Models;
using GeoLens.Services.DTOs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            try
            {
                Debug.WriteLine($"[PredictionProcessor] Starting pipeline for: {System.IO.Path.GetFileName(imagePath)}");

                // Step 1: Check cache first (unless force refresh)
                if (!forceApiCall)
                {
                    var cached = await _cacheService.GetCachedPredictionAsync(imagePath);
                    if (cached != null)
                    {
                        Debug.WriteLine($"[PredictionProcessor] Cache HIT for: {System.IO.Path.GetFileName(imagePath)}");
                        return await BuildResultFromCacheAsync(cached);
                    }
                }

                Debug.WriteLine($"[PredictionProcessor] Cache MISS - proceeding with full pipeline");

                // Step 2: Extract EXIF GPS data
                var exifGps = await _exifExtractor.ExtractGpsDataAsync(imagePath);
                if (exifGps?.HasGps == true)
                {
                    Debug.WriteLine($"[PredictionProcessor] Found GPS in EXIF: {exifGps.Latitude:F6}, {exifGps.Longitude:F6}");
                }
                else
                {
                    Debug.WriteLine($"[PredictionProcessor] No GPS data in EXIF");
                }

                // Step 3: Call API for AI predictions
                Debug.WriteLine($"[PredictionProcessor] Calling GeoCLIP API (device={device}, topK={topK})");
                var apiResult = await _apiClient.InferSingleAsync(imagePath, topK, device, cancellationToken);

                if (apiResult == null)
                {
                    Debug.WriteLine($"[PredictionProcessor] API call failed - no result returned");
                    return CreateEmptyResult(imagePath, exifGps);
                }

                if (!string.IsNullOrEmpty(apiResult.Error))
                {
                    Debug.WriteLine($"[PredictionProcessor] API returned error: {apiResult.Error}");
                    return CreateEmptyResult(imagePath, exifGps, apiResult.Error);
                }

                // Step 4: Convert API predictions to enhanced predictions
                var enhancedPredictions = ConvertToEnhancedPredictions(apiResult.Predictions);
                Debug.WriteLine($"[PredictionProcessor] Converted {enhancedPredictions.Count} predictions");

                // Step 5: Store in cache for future lookups
                try
                {
                    await _cacheService.StorePredictionAsync(imagePath, apiResult.Predictions, exifGps);
                    Debug.WriteLine($"[PredictionProcessor] Stored result in cache");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PredictionProcessor] Failed to store in cache (non-fatal): {ex.Message}");
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

                Debug.WriteLine($"[PredictionProcessor] Pipeline complete for: {System.IO.Path.GetFileName(imagePath)}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionProcessor] Pipeline error: {ex.Message}");
                return CreateEmptyResult(imagePath, null, $"Pipeline error: {ex.Message}");
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

            Debug.WriteLine($"[PredictionProcessor] Starting batch processing: {totalImages} images");

            try
            {
                // Process each image through the pipeline
                for (int i = 0; i < imagePaths.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Debug.WriteLine($"[PredictionProcessor] Batch processing cancelled at {i}/{totalImages}");
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

                    Debug.WriteLine($"[PredictionProcessor] Batch progress: {i + 1}/{totalImages} ({cachedCount} cached)");
                }

                // Report final progress
                progress?.Report(new BatchProgress
                {
                    TotalImages = totalImages,
                    ProcessedImages = totalImages,
                    CachedImages = cachedCount,
                    CurrentImage = null
                });

                Debug.WriteLine($"[PredictionProcessor] Batch complete: {results.Count} results ({cachedCount} from cache)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PredictionProcessor] Batch processing error: {ex.Message}");
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
                Debug.WriteLine($"[PredictionProcessor] Creating empty result with error: {error}");
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
