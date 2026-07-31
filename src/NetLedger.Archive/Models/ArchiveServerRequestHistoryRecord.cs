namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Archive Server operational request history record.
    /// </summary>
    public class ArchiveServerRequestHistoryRecord
    {
        /// <summary>
        /// Request history identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.RequestHistory);

        /// <summary>
        /// Tenant identifier associated with the request, if known.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Principal identifier associated with the request, if known.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Request path.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Response status code.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Request duration in milliseconds.
        /// </summary>
        public decimal DurationMs { get; set; } = 0m;

        /// <summary>
        /// UTC creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
