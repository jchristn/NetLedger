namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Archive balance checkpoint metadata.
    /// </summary>
    public class ArchiveBalanceCheckpointInfo
    {
        /// <summary>
        /// Checkpoint identifier.
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
        /// Manifest identifier.
        /// </summary>
        public string? ManifestId { get; set; }

        /// <summary>
        /// Timestamp represented by the checkpoint.
        /// </summary>
        public DateTime AsOfUtc { get; set; }

        /// <summary>
        /// Balance at the checkpoint.
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }
    }
}
