namespace NetLedger.Archive.Server
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Archive server constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// JSON serializer options.
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// JSON content type.
        /// </summary>
        public const string JsonContentType = "application/json";

        /// <summary>
        /// Hostname header.
        /// </summary>
        public const string HostnameHeader = "x-hostname";

        /// <summary>
        /// Request identifier header.
        /// </summary>
        public const string RequestIdHeader = "x-request-id";

        /// <summary>
        /// API version header.
        /// </summary>
        public const string ApiVersionHeader = "x-api-version";

        /// <summary>
        /// Data scope header.
        /// </summary>
        public const string DataScopeHeader = "x-netledger-data-scope";

        /// <summary>
        /// Current API version.
        /// </summary>
        public const string CurrentApiVersion = "v1";
    }
}
