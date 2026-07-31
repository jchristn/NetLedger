namespace NetLedger.Server
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Server constants including header names and content types.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Default JSON serializer options (PascalCase property naming, string enums).
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
        public static readonly string JsonContentType = "application/json";

        /// <summary>
        /// Authorization header name.
        /// </summary>
        public static readonly string AuthorizationHeader = "authorization";

        /// <summary>
        /// Bearer token prefix.
        /// </summary>
        public static readonly string BearerPrefix = "Bearer ";

        /// <summary>
        /// Hostname header name.
        /// </summary>
        public static readonly string HostnameHeader = "x-hostname";

        /// <summary>
        /// Request identifier header name.
        /// </summary>
        public static readonly string RequestIdHeader = "x-request-id";

        /// <summary>
        /// API version header name.
        /// </summary>
        public static readonly string ApiVersionHeader = "x-api-version";

        /// <summary>
        /// Data scope header name.
        /// </summary>
        public static readonly string DataScopeHeader = "x-netledger-data-scope";

        /// <summary>
        /// Current API version.
        /// </summary>
        public static readonly string CurrentApiVersion = "v1";
    }
}




