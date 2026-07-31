namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Balance checkpoint used to answer archived balance queries.
    /// </summary>
    public class ArchiveBalanceCheckpoint
    {
        /// <summary>
        /// Checkpoint identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.Checkpoint);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string AccountId { get; set; } = String.Empty;

        /// <summary>
        /// Manifest identifier.
        /// </summary>
        public string ManifestId { get; set; } = String.Empty;

        /// <summary>
        /// Timestamp represented by the checkpoint.
        /// </summary>
        public DateTime AsOfUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Balance at the checkpoint.
        /// </summary>
        public decimal Balance { get; set; } = 0m;

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
