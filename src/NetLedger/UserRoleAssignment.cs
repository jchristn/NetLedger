namespace NetLedger
{
    using System;

    /// <summary>
    /// User role assignment with tenant or resource scope.
    /// </summary>
    public class UserRoleAssignment
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
        /// User identifier.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

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
        /// Whether tenant-scoped grants inherit to child resources.
        /// </summary>
        public bool InheritsToChildren { get; set; } = true;

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
