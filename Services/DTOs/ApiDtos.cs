using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GeoLens.Services.DTOs
{
    /// <summary>
    /// Device choice for inference (matches Python backend)
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DeviceChoice
    {
        Auto,
        Cpu,
        Cuda,
        Rocm
    }

    /// <summary>
    /// Single image item for inference request
    /// </summary>
    public class InferenceItem
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("md5")]
        public string? Md5 { get; set; }
    }

    /// <summary>
    /// Request to infer location from images
    /// </summary>
    public class InferenceRequest
    {
        [JsonPropertyName("items")]
        public List<InferenceItem> Items { get; set; } = new();

        [JsonPropertyName("top_k")]
        public int TopK { get; set; } = 5;

        [JsonPropertyName("device")]
        public string Device { get; set; } = "auto";

        [JsonPropertyName("skip_missing")]
        public bool SkipMissing { get; set; } = false;

        [JsonPropertyName("hf_cache")]
        public string? HfCache { get; set; }
    }

    /// <summary>
    /// Single location prediction candidate
    /// </summary>
    public class PredictionCandidate
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("probability")]
        public double Probability { get; set; }

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("county")]
        public string County { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("location_summary")]
        public string LocationSummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Prediction result for a single image
    /// </summary>
    public class PredictionResult
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("md5")]
        public string? Md5 { get; set; }

        [JsonPropertyName("predictions")]
        public List<PredictionCandidate> Predictions { get; set; } = new();

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Complete inference response from the API
    /// </summary>
    public class InferenceResponse
    {
        [JsonPropertyName("device")]
        public string Device { get; set; } = string.Empty;

        [JsonPropertyName("results")]
        public List<PredictionResult> Results { get; set; } = new();
    }
}
