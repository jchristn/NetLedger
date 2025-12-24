namespace NetLedger.Server.Models
{
    using System;

    /// <summary>
    /// API error codes.
    /// </summary>
    public enum ApiErrorEnum
    {
        /// <summary>
        /// Bad request (400).
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// Unauthorized (401).
        /// </summary>
        Unauthorized = 401,

        /// <summary>
        /// Forbidden (403).
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// Not found (404).
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// Conflict (409).
        /// </summary>
        Conflict = 409,

        /// <summary>
        /// Request timeout (408).
        /// </summary>
        Timeout = 408,

        /// <summary>
        /// Internal server error (500).
        /// </summary>
        InternalError = 500,

        /// <summary>
        /// Service unavailable (503).
        /// </summary>
        ServiceUnavailable = 503
    }
}
