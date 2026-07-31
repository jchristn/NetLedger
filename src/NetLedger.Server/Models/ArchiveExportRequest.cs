namespace NetLedger.Server.Models
{
    using System;

    /// <summary>
    /// Request to export active NetLedger data to NetLedger Archive Server.
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
        /// Idempotency key. A stable key is generated from the export scope when omitted.
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
        /// Whether active rows should be deleted after Archive Server commit confirmation.
        /// </summary>
        public bool DeleteAfterCommit { get; set; } = false;

        /// <summary>
        /// Internal active retention override used by automatic archival.
        /// </summary>
        internal long? ActiveDataRetentionDaysOverride
        {
            get
            {
                return _ActiveDataRetentionDaysOverride;
            }
            set
            {
                _ActiveDataRetentionDaysOverride = value.HasValue ? Math.Clamp(value.Value, 1L, Int32.MaxValue) : null;
            }
        }

        private int _MaxBatchRows = 50000;
        private long? _ActiveDataRetentionDaysOverride = null;
    }
}
