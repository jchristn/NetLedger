namespace NetLedger.Server.Models
{
    using System;

    /// <summary>
    /// Request to replace account-specific automatic archival overrides.
    /// Null values inherit the global Archive.Automatic policy.
    /// </summary>
    public class UpdateAccountArchivalSettingsRequest
    {
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
        /// Optional account-level worker interval in seconds.
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
        /// Optional active cleanup override.
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

        private long? _MaxRetentionDays = null;
        private int? _IntervalSeconds = null;
        private int? _MaxBatchRows = null;
        private int? _RetryMaxAttempts = null;
        private int? _RetryInitialDelaySeconds = null;
        private int? _RetryMaxDelaySeconds = null;
    }
}
