using GeoLens.Services.DTOs;
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

        public GeoCLIPApiClient(string baseUrl = "http://localhost:8899")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromMinutes(5) // GeoCLIP inference can take time
            };
        }

        /// <summary>
        /// Check if the service is healthy
        /// </summary>
        public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("/health", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Infer location from a single image
        /// </summary>
        public async Task<PredictionResult?> InferSingleAsync(
            string imagePath,
            int topK = 5,
            string device = "auto",
            CancellationToken cancellationToken = default)
        {
            var results = await InferBatchAsync(new[] { imagePath }, topK, device, cancellationToken);
            return results?.FirstOrDefault();
        }

        /// <summary>
        /// Infer location from multiple images
        /// </summary>
        public async Task<List<PredictionResult>?> InferBatchAsync(
            IEnumerable<string> imagePaths,
            int topK = 5,
            string device = "auto",
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate image paths
                var validPaths = imagePaths
                    .Where(path => File.Exists(path))
                    .ToList();

                if (validPaths.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("No valid image paths provided");
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
                    TopK = topK,
                    Device = device.ToLowerInvariant(),
                    SkipMissing = true
                };

                // Send request
                var response = await _httpClient.PostAsJsonAsync("/infer", request, cancellationToken);
                response.EnsureSuccessStatusCode();

                // Parse response
                var inferenceResponse = await response.Content.ReadFromJsonAsync<InferenceResponse>(cancellationToken: cancellationToken);

                if (inferenceResponse == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to parse inference response");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"Inference completed on device: {inferenceResponse.Device}");
                return inferenceResponse.Results;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP error during inference: {ex.Message}");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Inference request timed out: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error during inference: {ex.Message}");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error computing MD5 for {filePath}: {ex.Message}");
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
