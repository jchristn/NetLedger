namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Role information.
    /// </summary>
    public class RoleInfo
    {
        /// <summary>
        /// Role identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Role name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether the role is built in.
        /// </summary>
        public bool IsBuiltIn { get; set; }

        /// <summary>
        /// Whether the role is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether the role is protected.
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
