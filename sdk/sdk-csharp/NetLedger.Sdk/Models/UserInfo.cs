namespace NetLedger.Sdk
{
    /// <summary>
    /// User information.
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// User identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

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

        /// <summary>
        /// Whether the user is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}
