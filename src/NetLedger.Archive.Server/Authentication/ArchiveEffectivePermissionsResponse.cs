namespace NetLedger.Archive.Server.Authentication
{
    using System.Collections.Generic;
    using NetLedger;

    /// <summary>
    /// Effective permissions response returned by NetLedger Server.
    /// </summary>
    internal sealed class ArchiveEffectivePermissionsResponse
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

        /// <summary>
        /// Account identifiers mapped to the principal inside the tenant.
        /// </summary>
        public List<string> MappedAccountIds { get; set; } = new List<string>();
    }
}
