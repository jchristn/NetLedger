namespace NetLedger
{
    using System;

    /// <summary>
    /// Tenant information.
    /// </summary>
    public class Tenant
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Tenant);

        /// <summary>
        /// Parent tenant identifier.
        /// </summary>
        public string? ParentId { get; set; } = null;

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Geographic region.
        /// </summary>
        public string? Region { get; set; } = null;

        /// <summary>
        /// Boolean indicating whether the tenant is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Boolean indicating whether the tenant is protected from accidental mutation.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// UTC timestamp when the tenant was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the tenant was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }
}

