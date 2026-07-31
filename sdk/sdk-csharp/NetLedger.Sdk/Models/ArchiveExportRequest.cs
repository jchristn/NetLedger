namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Request to export active NetLedger entries to NetLedger Archive Server.
    /// </summary>
    public class ArchiveExportRequest
    {
        /// <summary>
        /// Tenant identifier. Route tenant or authenticated tenant is used when omitted.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Account identifier. Route account is used when omitted.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Inclusive UTC lower bound for exported rows.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Inclusive UTC upper bound for exported rows.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Archive Server storage pool identifier. Archive Server default is used when omitted.
        /// </summary>
        public string? StoragePoolId { get; set; } = null;

        /// <summary>
        /// Idempotency key. NetLedger Server generates a stable scope key when omitted.
        /// </summary>
        public string? IdempotencyKey { get; set; } = null;

        /// <summary>
        /// Maximum rows per migration batch. Default is 50000, minimum is 1, maximum is 50000.
        /// </summary>
        public int MaxBatchRows
        {
            get
            {
                return _MaxBatchRows;
            }
            set
            {
                _MaxBatchRows = Math.Clamp(value, 1, 50000);
            }
        }

        /// <summary>
        /// Whether active rows should be deleted after archive commit. v4.0.0 supports opt-in cleanup after Archive Server commit succeeds.
        /// </summary>
        public bool DeleteAfterCommit { get; set; } = false;

        private int _MaxBatchRows = 50000;
    }
}
