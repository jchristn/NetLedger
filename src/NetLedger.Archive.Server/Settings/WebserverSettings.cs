namespace NetLedger.Archive.Server.Settings
{
    /// <summary>
    /// Webserver settings.
    /// </summary>
    public class WebserverSettings
    {
        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// Port.
        /// </summary>
        public int Port { get; set; } = 8081;

        /// <summary>
        /// Enable SSL.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// CORS settings.
        /// </summary>
        public CorsSettings Cors { get; set; } = new CorsSettings();
    }
}
