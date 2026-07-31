namespace NetLedger
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Paginated request history result.
    /// </summary>
    public class RequestHistoryResult
    {
        /// <summary>
        /// Request history records.
        /// </summary>
        public List<RequestHistoryEntry> Objects { get; set; } = new List<RequestHistoryEntry>();

        /// <summary>
        /// Total records matching the filter.
        /// </summary>
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Records remaining after the current page.
        /// </summary>
        public long RecordsRemaining { get; set; } = 0;

        /// <summary>
        /// Whether this page is the end of the result set.
        /// </summary>
        public bool EndOfResults { get; set; } = true;

        /// <summary>
        /// Opaque continuation token for fetching the next page.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Maximum records requested.
        /// </summary>
        public int MaxResults { get; set; } = 25;

        /// <summary>
        /// Number of records skipped.
        /// </summary>
        public int Skip { get; set; } = 0;
    }
}
