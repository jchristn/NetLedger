namespace NetLedger
{
    using System;

    /// <summary>
    /// Tenant-scoped or built-in user role.
    /// </summary>
    public class UserRole
    {
        /// <summary>
        /// Role identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Role);

        /// <summary>
        /// Tenant identifier. Empty for built-in global roles.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Role name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Whether this is a platform built-in role.
        /// </summary>
        public bool IsBuiltIn { get; set; } = false;

        /// <summary>
        /// Whether this role is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether this role is protected from normal mutation.
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
