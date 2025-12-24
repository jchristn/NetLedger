namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// HTTP server configuration settings.
    /// </summary>
    public class HttpServerSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname to listen on.
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// Port to listen on.
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// Enable SSL/TLS.
        /// </summary>
        public bool Ssl { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HttpServerSettings()
        {
        }

        #endregion
    }
}
