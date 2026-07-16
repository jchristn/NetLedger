namespace NetLedger
{
    using System;

    /// <summary>
    /// Revocable authentication session.
    /// </summary>
    public class AuthSession
    {
        /// <summary>
        /// Session identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Session);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// User identifier.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential identifier.
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// Session bearer token.
        /// </summary>
        public string Token { get; set; } = NetLedgerId.Generate("tok_");

        /// <summary>
        /// Boolean indicating whether the session is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC timestamp when the session expires.
        /// </summary>
        public DateTime ExpiresUtc { get; set; } = DateTime.UtcNow.AddHours(12);

        /// <summary>
        /// UTC timestamp when the session was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}

