namespace NetLedger.Server.Models.Identity
{
    using NetLedger;

    /// <summary>
    /// Tenant-scoped login response.
    /// </summary>
    internal class LoginResponse
    {
        /// <summary>
        /// Issued session.
        /// </summary>
        public AuthSession? Session { get; set; }

        /// <summary>
        /// Authenticated user.
        /// </summary>
        public User? User { get; set; }

        /// <summary>
        /// Selected tenant.
        /// </summary>
        public Tenant? Tenant { get; set; }
    }
}
