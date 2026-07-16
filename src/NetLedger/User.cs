namespace NetLedger
{
    using System;

    /// <summary>
    /// Tenant-scoped user.
    /// </summary>
    public class User
    {
        /// <summary>
        /// User identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.User);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// First name.
        /// </summary>
        public string? FirstName { get; set; } = null;

        /// <summary>
        /// Last name.
        /// </summary>
        public string? LastName { get; set; } = null;

        /// <summary>
        /// Email address, unique within the tenant.
        /// </summary>
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Password SHA-256 hash.
        /// </summary>
        public string PasswordSha256 { get; set; } = String.Empty;

        /// <summary>
        /// Boolean indicating whether the user has system-wide administrator access.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether the user has tenant administrator access.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Boolean indicating whether the user is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Boolean indicating whether the user is protected from accidental mutation.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// UTC timestamp when the user was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the user was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }
}

