namespace NetLedger
{
    using System;

    /// <summary>
    /// Request history search and summary filter.
    /// </summary>
    public class RequestHistoryFilter
    {
        /// <summary>
        /// Optional tenant scope.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Optional principal identifier scope.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Optional HTTP method filter.
        /// </summary>
        public string? Method { get; set; } = null;

        /// <summary>
        /// Optional exact status code filter.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Optional path substring filter.
        /// </summary>
        public string? PathContains { get; set; } = null;

        /// <summary>
        /// Optional lower created timestamp bound.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Optional upper created timestamp bound.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Maximum records to return. Default 25, minimum 1, maximum 1000.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                _MaxResults = Math.Clamp(value, 1, 1000);
            }
        }

        /// <summary>
        /// Number of records to skip. Default 0, minimum 0.
        /// </summary>
        public int Skip
        {
            get
            {
                return _Skip;
            }
            set
            {
                _Skip = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Summary bucket size in minutes. Default 15, minimum 1, maximum 1440.
        /// </summary>
        public int BucketMinutes
        {
            get
            {
                return _BucketMinutes;
            }
            set
            {
                _BucketMinutes = Math.Clamp(value, 1, 1440);
            }
        }

        private int _MaxResults = 25;
        private int _Skip = 0;
        private int _BucketMinutes = 15;
    }
}
