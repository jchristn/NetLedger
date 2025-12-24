namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Represents a standard API response from the NetLedger server.
    /// </summary>
    /// <typeparam name="T">The type of data contained in the response.</typeparam>
    public class ApiResponse<T>
    {
        #region Public-Members

        /// <summary>
        /// The response data payload.
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// The HTTP status code of the response.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// The unique identifier for this request.
        /// </summary>
        public Guid? RequestGuid { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new API response.
        /// </summary>
        public ApiResponse()
        {
        }

        /// <summary>
        /// Instantiate a new API response with data and status code.
        /// </summary>
        /// <param name="data">The response data.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        public ApiResponse(T? data, int statusCode)
        {
            Data = data;
            StatusCode = statusCode;
        }

        #endregion
    }

    /// <summary>
    /// Represents an error response from the NetLedger server.
    /// </summary>
    public class ErrorResponse
    {
        #region Public-Members

        /// <summary>
        /// The error code.
        /// </summary>
        public int Error { get; set; }

        /// <summary>
        /// A brief error message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Additional context about the error.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// A detailed description of the error.
        /// </summary>
        public string? Description { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new error response.
        /// </summary>
        public ErrorResponse()
        {
        }

        #endregion
    }
}
