namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Query parameters for request history enumeration and summaries.
    /// </summary>
    public class RequestHistoryQuery
    {
        /// <summary>
        /// Optional tenant filter.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Optional principal filter.
        /// </summary>
        public string? PrincipalId { get; set; }

        /// <summary>
        /// Optional HTTP method filter.
        /// </summary>
        public string? Method { get; set; }

        /// <summary>
        /// Optional exact status code filter.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Optional request path substring filter.
        /// </summary>
        public string? PathContains { get; set; }

        /// <summary>
        /// Optional lower created timestamp bound.
        /// </summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>
        /// Optional upper created timestamp bound.
        /// </summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>
        /// Maximum records to return.
        /// </summary>
        public int MaxResults { get; set; } = 25;

        /// <summary>
        /// Number of records to skip.
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Summary bucket size in minutes.
        /// </summary>
        public int BucketMinutes { get; set; } = 15;
    }
}
