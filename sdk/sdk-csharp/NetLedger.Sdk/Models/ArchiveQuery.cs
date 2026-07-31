namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Archive Server query.
    /// </summary>
    public class ArchiveQuery
    {
        /// <summary>
        /// Maximum results.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Number of records to skip.
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Opaque continuation token returned by Archive Server.
        /// </summary>
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// Search term.
        /// </summary>
        public string? Search { get; set; }

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
        /// Migration identifier.
        /// </summary>
        public string? MigrationId { get; set; }

        /// <summary>
        /// Manifest status.
        /// </summary>
        public string? ManifestStatus { get; set; }

        /// <summary>
        /// Migration status.
        /// </summary>
        public string? MigrationStatus { get; set; }

        /// <summary>
        /// Start time UTC.
        /// </summary>
        public DateTime? StartTimeUtc { get; set; }

        /// <summary>
        /// End time UTC.
        /// </summary>
        public DateTime? EndTimeUtc { get; set; }

        /// <summary>
        /// Start time UTC using Archive Server route naming.
        /// </summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>
        /// End time UTC using Archive Server route naming.
        /// </summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>
        /// Ordering value.
        /// </summary>
        public string? Ordering { get; set; }

        /// <summary>
        /// Minimum entry amount.
        /// </summary>
        public decimal? AmountMinimum { get; set; }

        /// <summary>
        /// Maximum entry amount.
        /// </summary>
        public decimal? AmountMaximum { get; set; }

        /// <summary>
        /// Minimum credit amount.
        /// </summary>
        public decimal? CreditMinimum { get; set; }

        /// <summary>
        /// Maximum credit amount.
        /// </summary>
        public decimal? CreditMaximum { get; set; }

        /// <summary>
        /// Minimum debit amount.
        /// </summary>
        public decimal? DebitMinimum { get; set; }

        /// <summary>
        /// Maximum debit amount.
        /// </summary>
        public decimal? DebitMaximum { get; set; }

        /// <summary>
        /// Labels that must all match.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Tags that must all match.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Allow partially covered archive ranges.
        /// </summary>
        public bool? AllowPartial { get; set; }
    }
}
