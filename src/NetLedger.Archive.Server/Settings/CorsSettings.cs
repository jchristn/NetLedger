namespace NetLedger.Archive.Server.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// CORS settings.
    /// </summary>
    public class CorsSettings
    {
        /// <summary>
        /// Enable CORS.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Allowed origins.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new List<string> { "*" };

        /// <summary>
        /// Allowed methods.
        /// </summary>
        public List<string> AllowedMethods { get; set; } = new List<string> { "OPTIONS", "HEAD", "GET", "PUT", "POST", "DELETE" };

        /// <summary>
        /// Allowed headers.
        /// </summary>
        public List<string> AllowedHeaders { get; set; } = new List<string> { "*" };

        /// <summary>
        /// Exposed headers.
        /// </summary>
        public List<string> ExposedHeaders { get; set; } = new List<string> { "Content-Type", "x-netledger-data-scope", "x-request-id", "x-hostname", "x-api-version" };

        /// <summary>
        /// Allow credentialed requests.
        /// </summary>
        public bool AllowCredentials { get; set; } = false;

        /// <summary>
        /// Preflight max age seconds.
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

        private int _MaxAgeSeconds = 600;
    }
}
