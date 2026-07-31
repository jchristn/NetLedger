namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Archive migration batch model.
    /// </summary>
    public class ArchiveMigrationBatch
    {
        /// <summary>
        /// Batch identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.MigrationBatch);

        /// <summary>
        /// Migration identifier.
        /// </summary>
        public string MigrationId { get; set; } = String.Empty;

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string StoragePoolId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Optional account identifier.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Sequence number within the migration.
        /// </summary>
        public long SequenceNumber { get; set; } = 0;

        /// <summary>
        /// Number of rows in the batch.
        /// </summary>
        public long RowCount { get; set; } = 0;

        /// <summary>
        /// Number of bytes in the batch object.
        /// </summary>
        public long ByteCount { get; set; } = 0;

        /// <summary>
        /// SHA-256 hash for the batch content.
        /// </summary>
        public string ContentHashSha256 { get; set; } = String.Empty;

        /// <summary>
        /// Temporary relative path for uploaded content.
        /// </summary>
        public string TemporaryRelativePath { get; set; } = String.Empty;

        /// <summary>
        /// Committed relative path for uploaded content.
        /// </summary>
        public string CommittedRelativePath { get; set; } = String.Empty;

        /// <summary>
        /// Batch status.
        /// </summary>
        public ArchiveMigrationBatchStatus Status { get; set; } = ArchiveMigrationBatchStatus.Pending;

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
