namespace NetLedger.Server.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// CORS configuration settings.
    /// </summary>
    public class CorsSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable CORS headers.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Allowed origins. Use "*" only when credentials are not allowed.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new List<string> { "*" };

        /// <summary>
        /// Allowed methods.
        /// </summary>
        public List<string> AllowedMethods { get; set; } = new List<string> { "OPTIONS", "HEAD", "GET", "PUT", "POST", "DELETE" };

        /// <summary>
        /// Allowed request headers.
        /// </summary>
        public List<string> AllowedHeaders { get; set; } = new List<string> { "*" };

        /// <summary>
        /// Headers exposed to browsers.
        /// </summary>
        public List<string> ExposedHeaders { get; set; } = new List<string> { "Content-Type", "X-Requested-With", "x-hostname", "x-api-version", "x-request-id", "x-netledger-data-scope" };

        /// <summary>
        /// Allow credentialed browser requests.
        /// </summary>
        public bool AllowCredentials { get; set; } = false;

        /// <summary>
        /// Preflight cache duration in seconds. Range is 0 through 86400.
        /// </summary>
        public int MaxAgeSeconds
        {
            get
            {
                return _MaxAgeSeconds;
            }
            set
            {
                _MaxAgeSeconds = Math.Clamp(value, 0, 86400);
            }
        }

        #endregion

        #region Private-Members

        private int _MaxAgeSeconds = 600;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CorsSettings()
        {
        }

        #endregion
    }
}
