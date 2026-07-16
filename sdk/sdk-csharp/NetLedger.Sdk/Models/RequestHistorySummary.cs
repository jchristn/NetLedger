namespace NetLedger.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Request history summary.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>
        /// Total matching request count.
        /// </summary>
        public long TotalCount { get; set; }

        /// <summary>
        /// Total successful request count.
        /// </summary>
        public long TotalSuccess { get; set; }

        /// <summary>
        /// Total failed request count.
        /// </summary>
        public long TotalFailure { get; set; }

        /// <summary>
        /// Average request duration in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; }

        /// <summary>
        /// Summary buckets.
        /// </summary>
        public List<RequestHistorySummaryBucket> Buckets { get; set; } = new List<RequestHistorySummaryBucket>();
    }
}
