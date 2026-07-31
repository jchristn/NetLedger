namespace NetLedger.Archive.Server.Models
{
    /// <summary>
    /// Archive API error response.
    /// </summary>
    public class ArchiveApiErrorResponse
    {
        /// <summary>
        /// Error code.
        /// </summary>
        public ArchiveApiErrorCode Error { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// Error context.
        /// </summary>
        public object? Context { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ArchiveApiErrorResponse()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="error">Error code.</param>
        /// <param name="message">Error message.</param>
        /// <param name="context">Error context.</param>
        public ArchiveApiErrorResponse(ArchiveApiErrorCode error, string message, object? context = null)
        {
            Error = error;
            Message = message;
            Context = context;
        }
    }
}
