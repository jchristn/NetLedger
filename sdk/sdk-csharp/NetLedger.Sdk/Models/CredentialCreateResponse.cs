namespace NetLedger.Sdk
{
    /// <summary>
    /// Credential creation response with one-time secret.
    /// </summary>
    public class CredentialCreateResponse
    {
        /// <summary>
        /// Created credential.
        /// </summary>
        public ApiKeyInfo? Credential { get; set; }

        /// <summary>
        /// Raw secret key shown only once.
        /// </summary>
        public string? SecretKey { get; set; }
    }
}
