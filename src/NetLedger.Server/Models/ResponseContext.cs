namespace NetLedger.Server.Models
{
    using System;

    /// <summary>
    /// Response context for API responses.
    /// </summary>
    public class ResponseContext
    {
        #region Public-Members

        /// <summary>
        /// Request GUID.
        /// </summary>
        public Guid RequestGuid { get; set; }

        /// <summary>
        /// Whether the request was successful.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Error response, if any.
        /// </summary>
        public ApiErrorResponse? Error { get; set; }

        /// <summary>
        /// Response data.
        /// </summary>
        public object? Data { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ResponseContext()
        {
        }

        /// <summary>
        /// Instantiate with success response.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="data">Response data.</param>
        public ResponseContext(RequestContext req, object? data = null)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            RequestGuid = req.RequestGuid;
            Success = true;
            StatusCode = 200;
            Data = data;
        }

        /// <summary>
        /// Create an error response.
        /// </summary>
        /// <param name="req">Request context.</param>
        /// <param name="error">Error code.</param>
        /// <param name="context">Additional context.</param>
        /// <param name="description">Error description.</param>
        /// <returns>Response context.</returns>
        public static ResponseContext FromError(
            RequestContext req,
            ApiErrorEnum error,
            object? context = null,
            string? description = null)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            return new ResponseContext
            {
                RequestGuid = req.RequestGuid,
                Success = false,
                StatusCode = (int)error,
                Error = new ApiErrorResponse(error, context, description)
            };
        }

        /// <summary>
        /// Create an error response without request context.
        /// </summary>
        /// <param name="error">Error code.</param>
        /// <param name="context">Additional context.</param>
        /// <param name="description">Error description.</param>
        /// <returns>Response context.</returns>
        public static ResponseContext FromError(
            ApiErrorEnum error,
            object? context = null,
            string? description = null)
        {
            return new ResponseContext
            {
                RequestGuid = Guid.Empty,
                Success = false,
                StatusCode = (int)error,
                Error = new ApiErrorResponse(error, context, description)
            };
        }

        #endregion
    }
}
