namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Archive manifest.
    /// </summary>
    public class ArchiveManifestInfo
    {
        /// <summary>
        /// Manifest identifier.
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

        /// <summary>
        /// Content hash.
        /// </summary>
        public string? ContentHashSha256 { get; set; }

        /// <summary>
        /// Manifest hash.
        /// </summary>
        public string? ManifestHashSha256 { get; set; }

        /// <summary>
        /// Manifest status.
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
