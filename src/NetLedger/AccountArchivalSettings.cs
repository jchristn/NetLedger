namespace NetLedger
{
    using System;

    /// <summary>
    /// Account-specific automatic archival policy overrides and execution state.
    /// Null override values inherit the global Archive.Automatic policy.
    /// </summary>
    public class AccountArchivalSettings
    {
        #region Public-Members

        /// <summary>
        /// Settings identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.AccountArchivalSettings);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string AccountId { get; set; } = String.Empty;

        /// <summary>
        /// Optional account-level automatic archival enabled override.
        /// </summary>
        public bool? Enabled { get; set; } = null;

        /// <summary>
        /// Optional account-level maximum active data retention in days.
        /// Range is 1 through Int32.MaxValue.
        /// </summary>
        public long? MaxRetentionDays
        {
            get
            {
                return _MaxRetentionDays;
            }
            set
            {
                _MaxRetentionDays = value.HasValue ? Math.Clamp(value.Value, 1L, Int32.MaxValue) : null;
            }
        }

        /// <summary>
        /// Optional account-level execution interval in seconds.
        /// Range is 1 through Int32.MaxValue.
        /// </summary>
        public int? IntervalSeconds
        {
            get
            {
                return _IntervalSeconds;
            }
            set
            {
                _IntervalSeconds = value.HasValue ? Math.Clamp(value.Value, 1, Int32.MaxValue) : null;
            }
        }

        /// <summary>
        /// Optional maximum rows per archive batch.
        /// Range is 1 through 50000.
        /// </summary>
        public int? MaxBatchRows
        {
            get
            {
                return _MaxBatchRows;
            }
            set
            {
                _MaxBatchRows = value.HasValue ? Math.Clamp(value.Value, 1, 50000) : null;
            }
        }

        /// <summary>
        /// Optional account-level active cleanup override.
        /// </summary>
        public bool? DeleteAfterCommit { get; set; } = null;

        /// <summary>
        /// Optional Archive Server storage pool override.
        /// </summary>
        public string? StoragePoolId { get; set; } = null;

        /// <summary>
        /// Optional account-level retry attempt count.
        /// Range is 1 through 100.
        /// </summary>
        public int? RetryMaxAttempts
        {
            get
            {
                return _RetryMaxAttempts;
            }
            set
            {
                _RetryMaxAttempts = value.HasValue ? Math.Clamp(value.Value, 1, 100) : null;
            }
        }

        /// <summary>
        /// Optional account-level initial retry delay in seconds.
        /// Range is 0 through Int32.MaxValue.
        /// </summary>
        public int? RetryInitialDelaySeconds
        {
            get
            {
                return _RetryInitialDelaySeconds;
            }
            set
            {
                _RetryInitialDelaySeconds = value.HasValue ? Math.Clamp(value.Value, 0, Int32.MaxValue) : null;
            }
        }

        /// <summary>
        /// Optional account-level maximum retry delay in seconds.
        /// Range is 0 through Int32.MaxValue.
        /// </summary>
        public int? RetryMaxDelaySeconds
        {
            get
            {
                return _RetryMaxDelaySeconds;
            }
            set
            {
                _RetryMaxDelaySeconds = value.HasValue ? Math.Clamp(value.Value, 0, Int32.MaxValue) : null;
            }
        }

        /// <summary>
        /// Last automatic archival attempt UTC.
        /// </summary>
        public DateTime? LastAttemptUtc { get; set; } = null;

        /// <summary>
        /// Last successful automatic archival UTC.
        /// </summary>
        public DateTime? LastSuccessUtc { get; set; } = null;

        /// <summary>
        /// Highest entry created timestamp archived successfully by the automatic worker.
        /// </summary>
        public DateTime? LastArchivedThroughUtc { get; set; } = null;

        /// <summary>
        /// Last failed automatic archival UTC.
        /// </summary>
        public DateTime? LastFailureUtc { get; set; } = null;

        /// <summary>
        /// Next time this account should be considered for automatic archival.
        /// </summary>
        public DateTime? NextAttemptUtc { get; set; } = null;

        /// <summary>
        /// Consecutive automatic archival failure count.
        /// </summary>
        public int FailureCount
        {
            get
            {
                return _FailureCount;
            }
            set
            {
                _FailureCount = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Last automatic archival error message.
        /// </summary>
        public string? LastError { get; set; } = null;

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private long? _MaxRetentionDays = null;
        private int? _IntervalSeconds = null;
        private int? _MaxBatchRows = null;
        private int? _RetryMaxAttempts = null;
        private int? _RetryInitialDelaySeconds = null;
        private int? _RetryMaxDelaySeconds = null;
        private int _FailureCount = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AccountArchivalSettings()
        {
        }

        #endregion
    }
}
