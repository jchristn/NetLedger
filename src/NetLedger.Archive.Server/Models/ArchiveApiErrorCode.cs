namespace NetLedger.Archive.Server.Models
{
    /// <summary>
    /// Archive API error code.
    /// </summary>
    public enum ArchiveApiErrorCode
    {
        /// <summary>
        /// Bad request.
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// Unauthorized.
        /// </summary>
        Unauthorized = 401,

        /// <summary>
        /// Forbidden.
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// Not found.
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// Method not allowed.
        /// </summary>
        MethodNotAllowed = 405,

        /// <summary>
        /// Conflict.
        /// </summary>
        Conflict = 409,

        /// <summary>
        /// Not implemented.
        /// </summary>
        NotImplemented = 501,

        /// <summary>
        /// Internal server error.
        /// </summary>
        InternalError = 500
    }
}
