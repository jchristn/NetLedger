namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Archived NetLedger request-history coverage range.
    /// </summary>
    public class ArchiveRequestHistoryRange
    {
        /// <summary>
        /// Range identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.Range);

        /// <summary>
        /// Tenant identifier. Null represents records without tenant scope.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Archive manifest identifier.
        /// </summary>
        public string ManifestId { get; set; } = String.Empty;

        /// <summary>
        /// Range start UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Range end UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Row count.
        /// </summary>
        public long RowCount { get; set; } = 0;

        /// <summary>
        /// JSON object of HTTP method counts.
        /// </summary>
        public string? MethodCountsJson { get; set; } = null;

        /// <summary>
        /// JSON object of HTTP status code counts.
        /// </summary>
        public string? StatusCodeCountsJson { get; set; } = null;

        /// <summary>
        /// Created UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
