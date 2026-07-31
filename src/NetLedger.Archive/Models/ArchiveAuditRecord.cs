namespace NetLedger.Archive.Models
{
    using System;

    /// <summary>
    /// Archive Server audit event.
    /// </summary>
    public class ArchiveAuditRecord
    {
        /// <summary>
        /// Audit record identifier.
        /// </summary>
        public string Id { get; set; } = ArchiveId.Generate(ArchiveIdentifierPrefixes.Audit);

        /// <summary>
        /// Tenant identifier associated with the event, if known.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Principal identifier associated with the event, if known.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Action name.
        /// </summary>
        public string Action { get; set; } = String.Empty;

        /// <summary>
        /// Target resource type.
        /// </summary>
        public string TargetType { get; set; } = String.Empty;

        /// <summary>
        /// Target resource identifier, if any.
        /// </summary>
        public string? TargetId { get; set; } = null;

        /// <summary>
        /// Redacted structured metadata serialized as JSON.
        /// </summary>
        public string? Metadata { get; set; } = null;

        /// <summary>
        /// UTC creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
