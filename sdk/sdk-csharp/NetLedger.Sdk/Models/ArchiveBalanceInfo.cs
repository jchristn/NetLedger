namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Archived balance response for a tenant account at a point in time.
    /// </summary>
    public class ArchiveBalanceInfo
    {
        /// <summary>
        /// Account identifier.
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// As-of timestamp UTC.
        /// </summary>
        public DateTime AsOfUtc { get; set; }

        /// <summary>
        /// Archived balance.
        /// </summary>
        public decimal Balance { get; set; }

        /// <summary>
        /// Manifest containing the checkpoint used for the response.
        /// </summary>
        public string? ManifestId { get; set; }
    }
}
