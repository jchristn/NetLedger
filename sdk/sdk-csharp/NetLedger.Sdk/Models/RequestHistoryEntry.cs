namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Captured REST request and response metadata.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// Request history identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant identifier associated with the request.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// User or credential principal identifier.
        /// </summary>
        public string? PrincipalId { get; set; }

        /// <summary>
        /// Principal type.
        /// </summary>
        public string? PrincipalType { get; set; }

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
        public int StatusCode { get; set; }

        /// <summary>
        /// Request duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// Source IP address.
        /// </summary>
        public string? SourceIp { get; set; }

        /// <summary>
        /// Redacted request headers.
        /// </summary>
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Captured request body.
        /// </summary>
        public string? RequestBody { get; set; }

        /// <summary>
        /// Original request body byte count.
        /// </summary>
        public long RequestBodyBytes { get; set; }

        /// <summary>
        /// Whether request body capture was truncated.
        /// </summary>
        public bool RequestBodyTruncated { get; set; }

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Captured response body.
        /// </summary>
        public string? ResponseBody { get; set; }

        /// <summary>
        /// Original response body byte count.
        /// </summary>
        public long ResponseBodyBytes { get; set; }

        /// <summary>
        /// Whether response body capture was truncated.
        /// </summary>
        public bool ResponseBodyTruncated { get; set; }

        /// <summary>
        /// UTC timestamp when request started.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// UTC timestamp when response completed.
        /// </summary>
        public DateTime? CompletedUtc { get; set; }
    }
}
