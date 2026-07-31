namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Archive migration metadata.
    /// </summary>
    public class ArchiveMigrationInfo
    {
        /// <summary>
        /// Migration identifier.
        /// </summary>
        public string? Id { get; set; }

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
        /// Storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; }

        /// <summary>
        /// Archive object format.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Archive object compression.
        /// </summary>
        public string? Compression { get; set; }

        /// <summary>
        /// Requested range start UTC.
        /// </summary>
        public DateTime FromUtc { get; set; }

        /// <summary>
        /// Requested range end UTC.
        /// </summary>
        public DateTime ToUtc { get; set; }

        /// <summary>
        /// Migration status.
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Idempotency key.
        /// </summary>
        public string? IdempotencyKey { get; set; }

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
