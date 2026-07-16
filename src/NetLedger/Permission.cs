namespace NetLedger
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Permission tuple used by RBAC evaluation.
    /// </summary>
    public class Permission
    {
        /// <summary>
        /// Permission identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Permission);

        /// <summary>
        /// Tenant identifier. Empty for built-in global permissions.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Permission name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Resource types this permission applies to.
        /// </summary>
        public List<string> ResourceTypes { get; set; } = new List<string>();

        /// <summary>
        /// Operation types this permission applies to.
        /// </summary>
        public List<string> OperationTypes { get; set; } = new List<string>();

        /// <summary>
        /// Permission type, either Permit or Deny.
        /// </summary>
        public string PermissionType { get; set; } = "Permit";

        /// <summary>
        /// Whether this permission is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether this permission is protected from normal mutation.
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
