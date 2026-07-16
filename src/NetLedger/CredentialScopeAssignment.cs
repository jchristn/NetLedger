namespace NetLedger
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Credential role or direct permission assignment.
    /// </summary>
    public class CredentialScopeAssignment
    {
        /// <summary>
        /// Assignment identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Assignment);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Credential identifier.
        /// </summary>
        public string CredentialId { get; set; } = String.Empty;

        /// <summary>
        /// Optional role identifier.
        /// </summary>
        public string? RoleId { get; set; }

        /// <summary>
        /// Optional role name fallback.
        /// </summary>
        public string? RoleName { get; set; }

        /// <summary>
        /// Scope, either Tenant or Resource.
        /// </summary>
        public string ResourceScope { get; set; } = "Tenant";

        /// <summary>
        /// Resource identifier for resource-scoped assignments.
        /// </summary>
        public string? ResourceId { get; set; }

        /// <summary>
        /// Optional direct operation grants.
        /// </summary>
        public List<string> OperationTypes { get; set; } = new List<string>();

        /// <summary>
        /// Optional direct resource grants.
        /// </summary>
        public List<string> ResourceTypes { get; set; } = new List<string>();

        /// <summary>
        /// Whether this assignment is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether this assignment is protected from normal mutation.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// Created timestamp UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }
}
