namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Request to create an Archive Server migration.
    /// </summary>
    public class ArchiveMigrationRequest
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Optional account identifier.
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// Entity type.
        /// </summary>
        public string EntityType { get; set; } = "Entries";

        /// <summary>
        /// Optional storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; }

        /// <summary>
        /// Archive object format.
        /// </summary>
        public string? Format { get; set; } = "JsonlGzip";

        /// <summary>
        /// Archive object compression.
        /// </summary>
        public string? Compression { get; set; } = "Gzip";

        /// <summary>
        /// Requested range start UTC.
        /// </summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>
        /// Requested range end UTC.
        /// </summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>
        /// Idempotency key.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }
}
