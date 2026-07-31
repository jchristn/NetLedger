namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Archive migration model.
    /// </summary>
    public class ArchiveMigration
    {
        /// <summary>
        /// Migration identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.Migration);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Optional account identifier.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Entity type.
        /// </summary>
        public ArchiveEntityType EntityType { get; set; } = ArchiveEntityType.Entries;

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string StoragePoolId { get; set; } = String.Empty;

        /// <summary>
        /// Archive object format.
        /// </summary>
        public ArchiveFormat Format { get; set; } = ArchiveFormat.JsonlGzip;

        /// <summary>
        /// Archive object compression.
        /// </summary>
        public ArchiveCompression Compression { get; set; } = ArchiveCompression.Gzip;

        /// <summary>
        /// Requested range start UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Requested range end UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Migration status.
        /// </summary>
        public ArchiveMigrationStatus Status { get; set; } = ArchiveMigrationStatus.Pending;

        /// <summary>
        /// Idempotency key.
        /// </summary>
        public string IdempotencyKey { get; set; } = String.Empty;

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
