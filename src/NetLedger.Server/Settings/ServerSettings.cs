namespace NetLedger.Server.Settings
{
    using System;
    using NetLedger.Database;

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
        /// Database settings for the ledger.
        /// </summary>
        public DatabaseSettings Database { get; set; } = new DatabaseSettings();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ServerSettings()
        {
        }
    }
}
