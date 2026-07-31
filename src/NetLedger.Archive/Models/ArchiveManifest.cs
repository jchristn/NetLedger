namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Archive manifest model.
    /// </summary>
    public class ArchiveManifest
    {
        /// <summary>
        /// Manifest identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.Manifest);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Optional account identifier.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Source migration identifier.
        /// </summary>
        public string? MigrationId { get; set; } = null;

        /// <summary>
        /// Entity type.
        /// </summary>
        public ArchiveEntityType EntityType { get; set; } = ArchiveEntityType.Entries;

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string StoragePoolId { get; set; } = String.Empty;

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

        /// <summary>
        /// Total credit amount in the archived range.
        /// </summary>
        public decimal CreditTotal { get; set; } = 0m;

        /// <summary>
        /// Total debit amount in the archived range.
        /// </summary>
        public decimal DebitTotal { get; set; } = 0m;

        /// <summary>
        /// Content hash.
        /// </summary>
        public string ContentHashSha256 { get; set; } = String.Empty;

        /// <summary>
        /// Manifest hash.
        /// </summary>
        public string ManifestHashSha256 { get; set; } = String.Empty;

        /// <summary>
        /// Manifest status.
        /// </summary>
        public ArchiveManifestStatus Status { get; set; } = ArchiveManifestStatus.Committed;

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }
}
