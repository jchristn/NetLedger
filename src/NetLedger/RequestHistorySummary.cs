namespace NetLedger
{
    using System.Collections.Generic;

    /// <summary>
    /// Request history summary response.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>
        /// Total matching request count.
        /// </summary>
        public long TotalCount { get; set; } = 0;

        /// <summary>
        /// Total successful request count.
        /// </summary>
        public long TotalSuccess { get; set; } = 0;

        /// <summary>
        /// Total failed request count.
        /// </summary>
        public long TotalFailure { get; set; } = 0;

        /// <summary>
        /// Average request duration in milliseconds.
        /// </summary>
        public double AverageDurationMs { get; set; } = 0;

        /// <summary>
        /// Summary buckets.
        /// </summary>
        public List<RequestHistorySummaryBucket> Buckets { get; set; } = new List<RequestHistorySummaryBucket>();
    }
}
