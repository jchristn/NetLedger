namespace NetLedger.Archive.Models
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
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Optional account identifier.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Manifest identifier.
        /// </summary>
        public string? ManifestId { get; set; } = null;

        /// <summary>
        /// Entity type.
        /// </summary>
        public ArchiveEntityType EntityType { get; set; } = ArchiveEntityType.Entries;

        /// <summary>
        /// Range start UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Range end UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Row count.
        /// </summary>
        public long RowCount { get; set; } = 0;
    }
}
