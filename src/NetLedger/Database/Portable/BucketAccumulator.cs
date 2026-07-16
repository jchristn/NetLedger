namespace NetLedger.Database.Portable
{
    using System;

    /// <summary>
    /// Internal request history summary bucket accumulator.
    /// </summary>
    internal sealed class BucketAccumulator
    {
        /// <summary>
        /// Bucket start timestamp.
        /// </summary>
        internal DateTime BucketStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket end timestamp.
        /// </summary>
        internal DateTime BucketEndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total request count.
        /// </summary>
        internal long Count { get; set; } = 0;

        /// <summary>
        /// Successful request count.
        /// </summary>
        internal long SuccessCount { get; set; } = 0;

        /// <summary>
        /// Failed request count.
        /// </summary>
        internal long FailureCount { get; set; } = 0;

        /// <summary>
        /// Total duration in milliseconds.
        /// </summary>
        internal double DurationTotalMs { get; set; } = 0;
    }
}
