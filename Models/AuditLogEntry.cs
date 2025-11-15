using System;
using System.Collections.Generic;

namespace GeoLens.Models;

/// <summary>
/// Represents a single audit log entry for image processing operations.
/// Tracks comprehensive information for compliance and review purposes.
/// </summary>
public class AuditLogEntry
{
    /// <summary>
    /// Unique identifier for this audit log entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// UTC timestamp when the processing operation occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Original filename (without path) for privacy.
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// Full absolute path to the source image file.
    /// </summary>
    public string Filepath { get; set; } = string.Empty;

    /// <summary>
    /// XXHash64 fingerprint of the image file (same as cache).
    /// </summary>
    public string ImageHash { get; set; } = string.Empty;

    /// <summary>
    /// Windows username of the user who initiated the processing.
    /// </summary>
    public string WindowsUser { get; set; } = string.Empty;

    /// <summary>
    /// Time taken to process the image in milliseconds.
    /// </summary>
    public int ProcessingTimeMs { get; set; }

    /// <summary>
    /// All predictions returned by GeoCLIP for this image.
    /// </summary>
    public List<PredictionResult> Predictions { get; set; } = new();

    /// <summary>
    /// Whether EXIF GPS data was present in the source image.
    /// </summary>
    public bool ExifGpsPresent { get; set; }

    /// <summary>
    /// Whether the processing operation was successful.
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When this audit entry was created (database timestamp).
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
