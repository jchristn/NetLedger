namespace NetLedger
{
    using System;

    /// <summary>
    /// Append-only audit record for authentication and authorization events.
    /// </summary>
    public class AuditRecord
    {
        /// <summary>
        /// Audit record identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Audit);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Principal identifier.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Principal type.
        /// </summary>
        public string? PrincipalType { get; set; } = null;

        /// <summary>
        /// Event type.
        /// </summary>
        public string EventType { get; set; } = String.Empty;

        /// <summary>
        /// Resource type.
        /// </summary>
        public string? ResourceType { get; set; } = null;

        /// <summary>
        /// Operation type.
        /// </summary>
        public string? OperationType { get; set; } = null;

        /// <summary>
        /// Resource identifier.
        /// </summary>
        public string? ResourceId { get; set; } = null;

        /// <summary>
        /// Event result.
        /// </summary>
        public string Result { get; set; } = String.Empty;

        /// <summary>
        /// Human-readable reason.
        /// </summary>
        public string? Reason { get; set; } = null;

        /// <summary>
        /// Request identifier.
        /// </summary>
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the record was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
