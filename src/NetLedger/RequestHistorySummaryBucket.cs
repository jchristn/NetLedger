namespace NetLedger
{
    using System;

    /// <summary>
    /// Request history summary bucket.
    /// </summary>
    public class RequestHistorySummaryBucket
    {
        /// <summary>
        /// Bucket start timestamp in UTC.
        /// </summary>
        public DateTime BucketStartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket end timestamp in UTC.
        /// </summary>
        public DateTime BucketEndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Successful request count.
        /// </summary>
        public long SuccessCount { get; set; } = 0;

        /// <summary>
        /// Failed request count.
        /// </summary>
        public long FailureCount { get; set; } = 0;

        /// <summary>
        /// Average request duration in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; } = 0;
    }
}
