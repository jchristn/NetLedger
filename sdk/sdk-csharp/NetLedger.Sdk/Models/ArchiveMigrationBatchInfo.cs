namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Archive migration batch metadata.
    /// </summary>
    public class ArchiveMigrationBatchInfo
    {
        /// <summary>
        /// Batch identifier.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Migration identifier.
        /// </summary>
        public string? MigrationId { get; set; }

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Optional account identifier.
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// Batch sequence number.
        /// </summary>
        public long SequenceNumber { get; set; }

        /// <summary>
        /// Number of rows in the batch.
        /// </summary>
        public long RowCount { get; set; }

        /// <summary>
        /// Number of bytes in the batch object.
        /// </summary>
        public long ByteCount { get; set; }

        /// <summary>
        /// SHA-256 content hash.
        /// </summary>
        public string? ContentHashSha256 { get; set; }

        /// <summary>
        /// Temporary relative path for uploaded content.
        /// </summary>
        public string? TemporaryRelativePath { get; set; }

        /// <summary>
        /// Committed relative path for uploaded content.
        /// </summary>
        public string? CommittedRelativePath { get; set; }

        /// <summary>
        /// Batch status.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Last update UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }
    }
}
