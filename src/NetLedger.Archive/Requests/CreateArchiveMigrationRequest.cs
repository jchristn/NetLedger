namespace NetLedger.Archive.Requests
{
    using System;

    /// <summary>
    /// Request to create an archive migration.
    /// </summary>
    public class CreateArchiveMigrationRequest
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Optional account identifier.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Entity type.
        /// </summary>
        public ArchiveEntityType EntityType { get; set; } = ArchiveEntityType.Entries;

        /// <summary>
        /// Optional storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; } = null;

        /// <summary>
        /// Optional archive object format.
        /// </summary>
        public ArchiveFormat? Format { get; set; } = null;

        /// <summary>
        /// Optional archive object compression.
        /// </summary>
        public ArchiveCompression? Compression { get; set; } = null;

        /// <summary>
        /// Requested range start UTC.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Requested range end UTC.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Optional idempotency key when the header is not used.
        /// </summary>
        public string? IdempotencyKey { get; set; } = null;
    }
}
