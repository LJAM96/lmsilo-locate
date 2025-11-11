namespace GeoLens.Models
{
    /// <summary>
    /// Confidence level classification for predictions
    /// </summary>
    public enum ConfidenceLevel
    {
        /// <summary>
        /// Very High confidence - from EXIF GPS data (cyan)
        /// </summary>
        VeryHigh,

        /// <summary>
        /// High confidence - probability > 0.1 or clustered (green)
        /// </summary>
        High,

        /// <summary>
        /// Medium confidence - probability 0.05-0.1 (yellow)
        /// </summary>
        Medium,

        /// <summary>
        /// Low confidence - probability < 0.05 (red)
        /// </summary>
        Low
    }
}
