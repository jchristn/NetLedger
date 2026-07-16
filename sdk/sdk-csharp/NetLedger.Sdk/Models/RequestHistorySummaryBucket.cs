namespace NetLedger.Sdk
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
        public DateTime BucketStartUtc { get; set; }

        /// <summary>
        /// Bucket end timestamp in UTC.
        /// </summary>
        public DateTime BucketEndUtc { get; set; }

        /// <summary>
        /// Successful request count.
        /// </summary>
        public long SuccessCount { get; set; }

        /// <summary>
        /// Failed request count.
        /// </summary>
        public long FailureCount { get; set; }

        /// <summary>
        /// Average request duration in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; }
    }
}
