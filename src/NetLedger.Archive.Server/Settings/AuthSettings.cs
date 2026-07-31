namespace NetLedger.Archive.Server.Settings
{
    /// <summary>
    /// Archive authentication settings.
    /// </summary>
    public class AuthSettings
    {
        private int _IntrospectionCacheSeconds = 30;

        /// <summary>
        /// Enable Archive Server authentication.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Authentication mode.
        /// </summary>
        public string Mode { get; set; } = "NetLedgerIntrospection";

        /// <summary>
        /// NetLedger Server URL.
        /// </summary>
        public string NetLedgerServerUrl { get; set; } = "http://localhost:8080";

        /// <summary>
        /// Require TLS for secrets.
        /// </summary>
        public bool RequireTlsForSecrets { get; set; } = true;

        /// <summary>
        /// Introspection cache seconds.
        /// Default is 30 seconds. Minimum is 0 seconds. Maximum is 3600 seconds.
        /// </summary>
        public int IntrospectionCacheSeconds
        {
            get
            {
                return _IntrospectionCacheSeconds;
            }
            set
            {
                _IntrospectionCacheSeconds = System.Math.Clamp(value, 0, 3600);
            }
        }
    }
}
