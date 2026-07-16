namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Tenant information.
    /// </summary>
    public class TenantInfo
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether the tenant is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Created timestamp UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; }
    }
}
