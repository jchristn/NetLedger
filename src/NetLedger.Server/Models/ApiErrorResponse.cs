namespace NetLedger.Server.Models
{
    using System;

    /// <summary>
    /// API error response.
    /// </summary>
    public class ApiErrorResponse
    {
        #region Public-Members

        /// <summary>
        /// Error code.
        /// </summary>
        public ApiErrorEnum Error { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message
        {
            get
            {
                return Error switch
                {
                    ApiErrorEnum.BadRequest => "Bad request",
                    ApiErrorEnum.Unauthorized => "Unauthorized",
                    ApiErrorEnum.Forbidden => "Forbidden",
                    ApiErrorEnum.NotFound => "Not found",
                    ApiErrorEnum.Conflict => "Conflict",
                    ApiErrorEnum.Timeout => "Request timeout",
                    ApiErrorEnum.InternalError => "Internal server error",
                    ApiErrorEnum.ServiceUnavailable => "Service unavailable",
                    _ => "Unknown error"
                };
            }
        }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode
        {
            get
            {
                return (int)Error;
            }
        }

        /// <summary>
        /// Additional context for the error.
        /// </summary>
        public object? Context { get; set; }

        /// <summary>
        /// Detailed description of the error.
        /// </summary>
        public string? Description { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiErrorResponse()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="error">Error code.</param>
        /// <param name="context">Additional context.</param>
        /// <param name="description">Detailed description.</param>
        public ApiErrorResponse(ApiErrorEnum error, object? context = null, string? description = null)
        {
            Error = error;
            Context = context;
            Description = description;
        }

        #endregion
    }
}
