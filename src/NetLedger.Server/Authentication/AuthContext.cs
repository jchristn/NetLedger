namespace NetLedger.Server.Authentication
{
    using System;

    /// <summary>
    /// Authentication result.
    /// </summary>
    public enum AuthResult
    {
        /// <summary>
        /// Authentication successful.
        /// </summary>
        Success,

        /// <summary>
        /// No credentials provided.
        /// </summary>
        NoCredentials,

        /// <summary>
        /// Invalid API key.
        /// </summary>
        InvalidApiKey,

        /// <summary>
        /// API key is inactive.
        /// </summary>
        InactiveApiKey,

        /// <summary>
        /// Authentication not required.
        /// </summary>
        NotRequired
    }

    /// <summary>
    /// Authentication context containing the result of authentication.
    /// </summary>
    public class AuthContext
    {
        #region Public-Members

        /// <summary>
        /// Authentication result.
        /// </summary>
        public AuthResult Result { get; set; } = AuthResult.NoCredentials;

        /// <summary>
        /// Whether authentication was successful.
        /// </summary>
        public bool IsAuthenticated
        {
            get
            {
                return Result == AuthResult.Success || Result == AuthResult.NotRequired;
            }
        }

        /// <summary>
        /// The authenticated API key, if any.
        /// </summary>
        public ApiKey? ApiKey { get; set; }

        /// <summary>
        /// Whether the authenticated user has admin privileges.
        /// </summary>
        public bool IsAdmin
        {
            get
            {
                return ApiKey?.IsAdmin ?? false;
            }
        }

        /// <summary>
        /// Error message if authentication failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthContext()
        {
        }

        /// <summary>
        /// Create a successful authentication context.
        /// </summary>
        /// <param name="apiKey">Authenticated API key.</param>
        /// <returns>Authentication context.</returns>
        public static AuthContext Success(ApiKey apiKey)
        {
            return new AuthContext
            {
                Result = AuthResult.Success,
                ApiKey = apiKey
            };
        }

        /// <summary>
        /// Create a failed authentication context.
        /// </summary>
        /// <param name="result">Failure reason.</param>
        /// <param name="errorMessage">Error message.</param>
        /// <returns>Authentication context.</returns>
        public static AuthContext Failed(AuthResult result, string? errorMessage = null)
        {
            return new AuthContext
            {
                Result = result,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Create an authentication context for when auth is not required.
        /// </summary>
        /// <returns>Authentication context.</returns>
        public static AuthContext NotRequired()
        {
            return new AuthContext
            {
                Result = AuthResult.NotRequired
            };
        }

        #endregion
    }
}
