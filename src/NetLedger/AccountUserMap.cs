namespace NetLedger
{
    using System;

    /// <summary>
    /// Many-to-many mapping between users and accounts.
    /// </summary>
    public class AccountUserMap
    {
        /// <summary>
        /// Mapping identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Assignment);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string AccountId { get; set; } = String.Empty;

        /// <summary>
        /// User identifier.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// UTC timestamp when the mapping was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}

