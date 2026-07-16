namespace NetLedger.Server.API.Agnostic
{
    internal class CreateApiKeyRequest
    {
        /// <summary>
        /// Credential name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Owning user identifier.
        /// </summary>
        public string? UserId { get; set; }
    }
}
