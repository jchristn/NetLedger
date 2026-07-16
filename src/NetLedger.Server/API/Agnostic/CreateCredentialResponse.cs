namespace NetLedger.Server.API.Agnostic
{
    using NetLedger;

    internal class CreateCredentialResponse
    {
        /// <summary>
        /// Created credential.
        /// </summary>
        public ApiKey? Credential { get; set; }

        /// <summary>
        /// Secret key shown only at creation time.
        /// </summary>
        public string? SecretKey { get; set; }
    }
}
