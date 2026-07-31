namespace NetLedger.Archive.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Archive verification result for a tenant/account range.
    /// </summary>
    public class ArchiveVerificationResult
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string AccountId { get; set; } = String.Empty;

        /// <summary>
        /// Whether all checked archive metadata and objects are valid.
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Number of checked manifests.
        /// </summary>
        public long CheckedManifests { get; set; } = 0;

        /// <summary>
        /// Number of checked archive objects.
        /// </summary>
        public long CheckedObjects { get; set; } = 0;

        /// <summary>
        /// Number of checked balance checkpoints.
        /// </summary>
        public long CheckedBalanceCheckpoints { get; set; } = 0;

        /// <summary>
        /// Informational verification details.
        /// </summary>
        public List<string> Details { get; set; } = new List<string>();

        /// <summary>
        /// Verification errors.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }
}
