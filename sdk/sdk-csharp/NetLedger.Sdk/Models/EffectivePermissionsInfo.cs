namespace NetLedger.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Effective permissions response.
    /// </summary>
    public class EffectivePermissionsInfo
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Principal identifier.
        /// </summary>
        public string? PrincipalId { get; set; }

        /// <summary>
        /// Principal type.
        /// </summary>
        public string? PrincipalType { get; set; }

        /// <summary>
        /// Permission tuples.
        /// </summary>
        public List<EffectivePermissionInfo> Permissions { get; set; } = new List<EffectivePermissionInfo>();
    }
}
