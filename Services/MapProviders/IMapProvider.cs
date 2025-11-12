using GeoLens.Models;
using System.Threading.Tasks;

namespace GeoLens.Services.MapProviders
{
    /// <summary>
    /// Interface for map visualization providers
    /// </summary>
    public interface IMapProvider
    {
        /// <summary>
        /// Initialize the map provider
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Add a location pin to the map
        /// </summary>
        /// <param name="latitude">Latitude in degrees</param>
        /// <param name="longitude">Longitude in degrees</param>
        /// <param name="label">Location label (e.g., "Paris, France")</param>
        /// <param name="confidence">Confidence level (0.0 to 1.0)</param>
        /// <param name="rank">Prediction rank (1-based)</param>
        /// <param name="isExif">Whether this is from EXIF GPS data</param>
        Task AddPinAsync(double latitude, double longitude, string label, double confidence, int rank, bool isExif = false);

        /// <summary>
        /// Clear all pins from the map
        /// </summary>
        Task ClearPinsAsync();

        /// <summary>
        /// Rotate/zoom the map to focus on a specific location
        /// </summary>
        /// <param name="latitude">Latitude in degrees</param>
        /// <param name="longitude">Longitude in degrees</param>
        /// <param name="durationMs">Animation duration in milliseconds</param>
        Task RotateToLocationAsync(double latitude, double longitude, int durationMs = 1000);

        /// <summary>
        /// Enable or disable heatmap visualization mode
        /// </summary>
        Task SetHeatmapModeAsync(bool enabled);

        /// <summary>
        /// Show a heatmap visualization from multiple image predictions
        /// </summary>
        /// <param name="heatmap">Heatmap data to visualize</param>
        Task ShowHeatmapAsync(HeatmapData heatmap);

        /// <summary>
        /// Hide the current heatmap visualization
        /// </summary>
        Task HideHeatmapAsync();

        /// <summary>
        /// Toggle between heatmap and individual pins mode
        /// </summary>
        /// <param name="showHeatmap">True to show heatmap, false to show pins</param>
        Task ToggleHeatmapModeAsync(bool showHeatmap);

        /// <summary>
        /// Check if the provider is ready to use
        /// </summary>
        bool IsReady { get; }
    }
}
