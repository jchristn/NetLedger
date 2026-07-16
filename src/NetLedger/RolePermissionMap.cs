namespace NetLedger
{
    using System;

    /// <summary>
    /// Role-to-permission mapping.
    /// </summary>
    public class RolePermissionMap
    {
        /// <summary>
        /// Mapping identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Assignment);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Role identifier.
        /// </summary>
        public string RoleId { get; set; } = String.Empty;

        /// <summary>
        /// Permission identifier.
        /// </summary>
        public string PermissionId { get; set; } = String.Empty;

        /// <summary>
        /// Whether this mapping is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether this mapping is protected from normal mutation.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// Created timestamp UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
