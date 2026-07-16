namespace NetLedger.Server.Models.Identity
{
    /// <summary>
    /// Create user request.
    /// </summary>
    internal class CreateUserRequest
    {
        /// <summary>
        /// First name.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Last name.
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Email address.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Password.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Whether the user is a system admin.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is a tenant admin.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}
