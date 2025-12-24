namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Base exception class for all NetLedger SDK exceptions.
    /// </summary>
    public abstract class NetLedgerException : Exception
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new NetLedger exception.
        /// </summary>
        protected NetLedgerException()
        {
        }

        /// <summary>
        /// Instantiate a new NetLedger exception with a message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        protected NetLedgerException(string message) : base(message)
        {
        }

        /// <summary>
        /// Instantiate a new NetLedger exception with a message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        protected NetLedgerException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown when the SDK cannot connect to the NetLedger server.
    /// </summary>
    public class NetLedgerConnectionException : NetLedgerException
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new connection exception.
        /// </summary>
        public NetLedgerConnectionException()
        {
        }

        /// <summary>
        /// Instantiate a new connection exception with a message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public NetLedgerConnectionException(string message) : base(message)
        {
        }

        /// <summary>
        /// Instantiate a new connection exception with a message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public NetLedgerConnectionException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown when the NetLedger API returns an error response.
    /// </summary>
    public class NetLedgerApiException : NetLedgerException
    {
        #region Public-Members

        /// <summary>
        /// The HTTP status code returned by the server.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Additional details about the error.
        /// </summary>
        public string? Details { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new API exception.
        /// </summary>
        public NetLedgerApiException() : base()
        {
        }

        /// <summary>
        /// Instantiate a new API exception with a message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public NetLedgerApiException(string message) : base(message)
        {
        }

        /// <summary>
        /// Instantiate a new API exception with status code and message.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="message">The exception message.</param>
        public NetLedgerApiException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// Instantiate a new API exception with status code, message, and details.
        /// </summary>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="message">The exception message.</param>
        /// <param name="details">Additional error details.</param>
        public NetLedgerApiException(int statusCode, string message, string? details)
            : base(message)
        {
            StatusCode = statusCode;
            Details = details;
        }

        /// <summary>
        /// Instantiate a new API exception with message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public NetLedgerApiException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown when input validation fails.
    /// </summary>
    public class NetLedgerValidationException : NetLedgerException
    {
        #region Public-Members

        /// <summary>
        /// The name of the parameter that failed validation.
        /// </summary>
        public string? ParameterName { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new validation exception.
        /// </summary>
        public NetLedgerValidationException()
        {
        }

        /// <summary>
        /// Instantiate a new validation exception with a message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public NetLedgerValidationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Instantiate a new validation exception with message and parameter name.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="parameterName">The name of the invalid parameter.</param>
        public NetLedgerValidationException(string message, string parameterName)
            : base(message)
        {
            ParameterName = parameterName;
        }

        /// <summary>
        /// Instantiate a new validation exception with message and inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public NetLedgerValidationException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }
}
