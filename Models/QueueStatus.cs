namespace GeoLens.Models
{
    /// <summary>
    /// Status of an image in the processing queue
    /// </summary>
    public enum QueueStatus
    {
        /// <summary>
        /// Image is queued but not yet processed
        /// </summary>
        Queued,

        /// <summary>
        /// Image is currently being processed
        /// </summary>
        Processing,

        /// <summary>
        /// Image processing completed successfully
        /// </summary>
        Done,

        /// <summary>
        /// Image processing failed with error
        /// </summary>
        Error,

        /// <summary>
        /// Image result was retrieved from cache
        /// </summary>
        Cached
    }
}
