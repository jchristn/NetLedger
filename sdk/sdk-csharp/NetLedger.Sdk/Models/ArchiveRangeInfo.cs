namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Archive coverage range.
    /// </summary>
    public class ArchiveRangeInfo
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// Entity type.
        /// </summary>
        public string? EntityType { get; set; }

        /// <summary>
        /// Range start UTC.
        /// </summary>
        public DateTime FromUtc { get; set; }

        /// <summary>
        /// Range end UTC.
        /// </summary>
        public DateTime ToUtc { get; set; }

        /// <summary>
        /// Row count.
        /// </summary>
        public long RowCount { get; set; }
    }
}
