namespace NetLedger.Server.Models.Identity
{
    /// <summary>
    /// Tenant discovery request.
    /// </summary>
    internal class TenantDiscoveryRequest
    {
        /// <summary>
        /// Email address to discover tenant memberships for.
        /// </summary>
        public string? Email { get; set; }
    }
}
