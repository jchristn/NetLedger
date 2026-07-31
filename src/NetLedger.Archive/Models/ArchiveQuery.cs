namespace NetLedger.Archive.Models
{
    using System;
    using System.Collections.Generic;
    using NetLedger;

    /// <summary>
    /// Archive catalog query.
    /// </summary>
    public class ArchiveQuery
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Entity type.
        /// </summary>
        public ArchiveEntityType? EntityType { get; set; } = null;

        /// <summary>
        /// Storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; } = null;

        /// <summary>
        /// Migration identifier.
        /// </summary>
        public string? MigrationId { get; set; } = null;

        /// <summary>
        /// Manifest status.
        /// </summary>
        public ArchiveManifestStatus? ManifestStatus { get; set; } = null;

        /// <summary>
        /// Migration status.
        /// </summary>
        public ArchiveMigrationStatus? MigrationStatus { get; set; } = null;

        /// <summary>
        /// Range start UTC.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Range end UTC.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Search term.
        /// </summary>
        public string? Search { get; set; } = null;

        /// <summary>
        /// Whether the caller accepts partial archive coverage.
        /// </summary>
        public bool AllowPartial { get; set; } = false;

        /// <summary>
        /// Opaque continuation token returned by a previous archive enumeration.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Entry ordering.
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        /// <summary>
        /// Minimum entry amount.
        /// </summary>
        public decimal? AmountMinimum { get; set; } = null;

        /// <summary>
        /// Maximum entry amount.
        /// </summary>
        public decimal? AmountMaximum { get; set; } = null;

        /// <summary>
        /// Minimum credit amount.
        /// </summary>
        public decimal? CreditMinimum { get; set; } = null;

        /// <summary>
        /// Maximum credit amount.
        /// </summary>
        public decimal? CreditMaximum { get; set; } = null;

        /// <summary>
        /// Minimum debit amount.
        /// </summary>
        public decimal? DebitMinimum { get; set; } = null;

        /// <summary>
        /// Maximum debit amount.
        /// </summary>
        public decimal? DebitMaximum { get; set; } = null;

        /// <summary>
        /// Labels that must all be present on an archived entry.
        /// </summary>
        public List<string> Labels
        {
            get { return _Labels; }
            set { _Labels = MetadataValidator.NormalizeLabels(value); }
        }

        /// <summary>
        /// Tags that must all match on an archived entry.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get { return _Tags; }
            set { _Tags = MetadataValidator.NormalizeTags(value); }
        }

        /// <summary>
        /// Maximum number of results.
        /// </summary>
        public int MaxResults
        {
            get { return _MaxResults; }
            set { _MaxResults = Math.Clamp(value, 1, 1000); }
        }

        /// <summary>
        /// Number of rows to skip.
        /// </summary>
        public int Skip
        {
            get { return _Skip; }
            set { _Skip = Math.Max(0, value); }
        }

        private int _MaxResults = 100;
        private int _Skip = 0;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
