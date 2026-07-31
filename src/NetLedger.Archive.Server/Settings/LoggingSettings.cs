namespace NetLedger.Archive.Server.Settings
{
    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Enable console logging.
        /// </summary>
        public bool EnableConsole { get; set; } = true;

        /// <summary>
        /// Minimum log level.
        /// </summary>
        public string MinimumLevel { get; set; } = "Debug";

        /// <summary>
        /// Log requests.
        /// </summary>
        public bool LogRequests { get; set; } = true;
    }
}
