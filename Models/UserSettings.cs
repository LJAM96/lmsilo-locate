using System;

namespace GeoLens.Models
{
    /// <summary>
    /// User settings model with all configurable application options
    /// </summary>
    public class UserSettings
    {
        // Cache Settings
        public bool EnableCache { get; set; } = true;
        public int CacheExpirationDays { get; set; } = 30; // 7, 30, 90, or 0 (never)

        // Prediction Settings
        public int TopKPredictions { get; set; } = 5;
        public bool ShowExifGpsFirst { get; set; } = true;
        public bool EnableClustering { get; set; } = true;

        // Map Settings
        public bool OfflineMode { get; set; } = false;
        public MapRenderMode RenderMode { get; set; } = MapRenderMode.Globe3D;

        // Interface Settings
        public bool ShowThumbnails { get; set; } = true;
        public ThumbnailSize ThumbnailSize { get; set; } = ThumbnailSize.Medium;
        public AppTheme Theme { get; set; } = AppTheme.Dark;
        public bool ShowSkeletonLoaders { get; set; } = true;

        // Export Settings
        public bool AlwaysShowExportPreview { get; set; } = true;
        public string DefaultExportTemplateId { get; set; } = "builtin-detailed"; // Default to Detailed template

        // Hardware (read-only, populated at runtime)
        public string? DetectedGpu { get; set; }
        public string? UsingRuntime { get; set; }
    }

    /// <summary>
    /// Map rendering mode options
    /// </summary>
    public enum MapRenderMode
    {
        Globe3D = 0,        // 3D Globe (WebGL)
        LeafletMap = 1,     // 2D Flat Map
        Win2DGlobe = 2      // 3D Globe (Win2D)
    }

    /// <summary>
    /// Thumbnail size options in pixels
    /// </summary>
    public enum ThumbnailSize
    {
        Small = 48,
        Medium = 72,
        Large = 96
    }

    /// <summary>
    /// Application theme options
    /// </summary>
    public enum AppTheme
    {
        Dark = 0,
        Light = 1,
        System = 2
    }
}
