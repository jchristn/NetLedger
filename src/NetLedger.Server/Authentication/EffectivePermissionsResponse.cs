namespace NetLedger.Server.Authentication
{
    using System.Collections.Generic;
    using NetLedger;

    /// <summary>
    /// Effective permissions response.
    /// </summary>
    public class EffectivePermissionsResponse
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Principal identifier.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Principal type.
        /// </summary>
        public string? PrincipalType { get; set; } = null;

        /// <summary>
        /// Whether the principal has system administrator access.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Whether the principal has tenant administrator access.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Effective permission tuples.
        /// </summary>
        public List<EffectivePermission> Permissions { get; set; } = new List<EffectivePermission>();
    }
}
