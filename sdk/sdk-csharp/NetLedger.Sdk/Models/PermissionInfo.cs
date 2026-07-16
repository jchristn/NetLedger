namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Permission information.
    /// </summary>
    public class PermissionInfo
    {
        /// <summary>
        /// Permission identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Permission name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Resource types.
        /// </summary>
        public List<string> ResourceTypes { get; set; } = new List<string>();

        /// <summary>
        /// Operation types.
        /// </summary>
        public List<string> OperationTypes { get; set; } = new List<string>();

        /// <summary>
        /// Permission type.
        /// </summary>
        public string PermissionType { get; set; } = "Permit";

        /// <summary>
        /// Whether the permission is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether the permission is protected.
        /// </summary>
        public bool IsProtected { get; set; }

        /// <summary>
        /// Created timestamp UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Last update timestamp UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }
    }
}
