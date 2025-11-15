using GeoLens.Services.DTOs;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace GeoLens.Services
{
    /// <summary>
    /// Client for communicating with the GeoCLIP FastAPI service
    /// </summary>
    public class GeoCLIPApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private bool _isDisposed;

        public GeoCLIPApiClient(string? baseUrl = null)
        {
            _baseUrl = (baseUrl ?? ConfigurationService.Instance.Config.GeoLens.Api.BaseUrl).TrimEnd('/');

            var timeoutSeconds = ConfigurationService.Instance.Config.GeoLens.Api.RequestTimeoutSeconds;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        /// <summary>
        /// Check if the service is healthy
        /// </summary>
        public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var healthEndpoint = ConfigurationService.Instance.Config.GeoLens.Api.HealthCheckEndpoint;
                var response = await _httpClient.GetAsync(healthEndpoint, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException ex)
            {
                Log.Debug(ex, "Health check failed - network error");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                Log.Debug(ex, "Health check timed out");
                return false;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Health check failed - unexpected error");
                return false;
            }
        }

        /// <summary>
        /// Infer location from a single image
        /// </summary>
        public async Task<PredictionResult?> InferSingleAsync(
            string imagePath,
            int? topK = null,
            string device = "auto",
            CancellationToken cancellationToken = default)
        {
            var actualTopK = topK ?? ConfigurationService.Instance.Config.GeoLens.Api.DefaultTopK;
            var results = await InferBatchAsync(new[] { imagePath }, actualTopK, device, cancellationToken);
            return results?.FirstOrDefault();
        }

        /// <summary>
        /// Infer location from multiple images
        /// </summary>
        public async Task<List<PredictionResult>?> InferBatchAsync(
            IEnumerable<string> imagePaths,
            int? topK = null,
            string device = "auto",
            CancellationToken cancellationToken = default)
        {
            try
            {
                var actualTopK = topK ?? ConfigurationService.Instance.Config.GeoLens.Api.DefaultTopK;

                // Validate image paths
                var validPaths = imagePaths
                    .Where(path => File.Exists(path))
                    .ToList();

                if (validPaths.Count == 0)
                {
                    Log.Warning("No valid image paths provided");
                    return new List<PredictionResult>();
                }

                // Create request
                var request = new InferenceRequest
                {
                    Items = validPaths.Select(path => new InferenceItem
                    {
                        Path = path,
                        Md5 = ComputeMd5Hash(path)
                    }).ToList(),
                    TopK = actualTopK,
                    Device = device.ToLowerInvariant(),
                    SkipMissing = true
                };

                // Send request
                var inferEndpoint = ConfigurationService.Instance.Config.GeoLens.Api.InferEndpoint;
                var response = await _httpClient.PostAsJsonAsync(inferEndpoint, request, cancellationToken);
                response.EnsureSuccessStatusCode();

                // Parse response
                var inferenceResponse = await response.Content.ReadFromJsonAsync<InferenceResponse>(cancellationToken: cancellationToken);

                if (inferenceResponse == null)
                {
                    Log.Error("Failed to parse inference response");
                    return null;
                }

                Log.Information("Inference completed on device: {Device}", inferenceResponse.Device);
                return inferenceResponse.Results;
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "HTTP error during inference");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                Log.Warning(ex, "Inference request timed out");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during inference");
                return null;
            }
        }

        /// <summary>
        /// Infer location with progress reporting
        /// NOTE: This processes images sequentially to provide granular progress updates.
        /// For batch processing without progress tracking, use InferBatchAsync() instead,
        /// which sends all images to the server in a single request for better performance.
        /// </summary>
        public async Task<List<PredictionResult>> InferBatchWithProgressAsync(
            IEnumerable<string> imagePaths,
            int topK,
            string device,
            IProgress<(int current, int total)>? progress,
            CancellationToken cancellationToken = default)
        {
            var paths = imagePaths.ToList();
            var results = new List<PredictionResult>();

            for (int i = 0; i < paths.Count; i++)
            {
                var result = await InferSingleAsync(paths[i], topK, device, cancellationToken);
                if (result != null)
                {
                    results.Add(result);
                }

                progress?.Report((i + 1, paths.Count));
            }

            return results;
        }

        /// <summary>
        /// Compute MD5 hash of an image file for caching purposes
        /// </summary>
        private string? ComputeMd5Hash(string filePath)
        {
            try
            {
                using var md5 = MD5.Create();
                using var stream = File.OpenRead(filePath);
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            catch (IOException ex)
            {
                Log.Warning(ex, "I/O error computing MD5 for {FilePath}", filePath);
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warning(ex, "Access denied computing MD5 for {FilePath}", filePath);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error computing MD5 for {FilePath}", filePath);
                return null;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _httpClient?.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
