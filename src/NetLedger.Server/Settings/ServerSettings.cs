namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// Main server configuration settings.
    /// </summary>
    public class ServerSettings
    {
        /// <summary>
        /// HTTP server settings.
        /// </summary>
        public HttpServerSettings Webserver { get; set; } = new HttpServerSettings();

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        /// <summary>
        /// Authentication settings.
        /// </summary>
        public AuthSettings Authentication { get; set; } = new AuthSettings();

        /// <summary>
        /// Path to the ledger database file.
        /// </summary>
        public string LedgerDatabase { get; set; } = "./netledger.db";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ServerSettings()
        {
        }
    }
}
