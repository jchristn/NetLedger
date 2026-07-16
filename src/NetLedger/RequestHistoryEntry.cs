namespace NetLedger
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Captured HTTP request and response metadata.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// Request history identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.RequestHistory);

        /// <summary>
        /// Tenant identifier associated with the request.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User or credential principal identifier associated with the request.
        /// </summary>
        public string? PrincipalId { get; set; } = null;

        /// <summary>
        /// Principal type associated with the request.
        /// </summary>
        public string? PrincipalType { get; set; } = null;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Request path without query string.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Request URL including query string.
        /// </summary>
        public string Url { get; set; } = String.Empty;

        /// <summary>
        /// Response status code.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Request duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Source IP address.
        /// </summary>
        public string? SourceIp { get; set; } = null;

        /// <summary>
        /// Redacted request headers.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Captured request body.
        /// </summary>
        public string? RequestBody { get; set; } = null;

        /// <summary>
        /// Original request body byte count.
        /// </summary>
        public long RequestBodyBytes { get; set; } = 0;

        /// <summary>
        /// Whether the request body capture was truncated.
        /// </summary>
        public bool RequestBodyTruncated { get; set; } = false;

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Captured response body.
        /// </summary>
        public string? ResponseBody { get; set; } = null;

        /// <summary>
        /// Original response body byte count.
        /// </summary>
        public long ResponseBodyBytes { get; set; } = 0;

        /// <summary>
        /// Whether the response body capture was truncated.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; } = false;

        /// <summary>
        /// UTC timestamp when the request started.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the response completed.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;
    }
}
