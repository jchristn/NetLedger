namespace NetLedger.Sdk
{
    /// <summary>
    /// Create user request.
    /// </summary>
    public class CreateUserInfo
    {
        /// <summary>
        /// Email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// First name.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Last name.
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Whether the user is a system admin.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the user is a tenant admin.
        /// </summary>
        public bool IsTenantAdmin { get; set; }
    }
}
