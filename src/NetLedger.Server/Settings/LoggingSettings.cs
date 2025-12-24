namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// Logging configuration settings.
    /// </summary>
    public class LoggingSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable console logging.
        /// </summary>
        public bool EnableConsole { get; set; } = true;

        /// <summary>
        /// Minimum log level. Valid values: Debug, Info, Warn, Alert.
        /// </summary>
        public string MinimumLevel { get; set; } = "Debug";

        /// <summary>
        /// Log HTTP requests.
        /// </summary>
        public bool LogRequests { get; set; } = true;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LoggingSettings()
        {
        }

        #endregion
    }
}
