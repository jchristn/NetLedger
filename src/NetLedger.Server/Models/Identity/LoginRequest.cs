namespace NetLedger.Server.Models.Identity
{
    /// <summary>
    /// Tenant-scoped login request.
    /// </summary>
    internal class LoginRequest
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// User email address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// User password.
        /// </summary>
        public string? Password { get; set; }
    }
}
