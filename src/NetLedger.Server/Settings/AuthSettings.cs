namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// Authentication configuration settings.
    /// </summary>
    public class AuthSettings
    {
        /// <summary>
        /// Enable authentication. When disabled, all requests are allowed.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default admin API key. Auto-generated if not specified.
        /// </summary>
        public string DefaultAdminKey
        {
            get => _DefaultAdminKey;
            set => _DefaultAdminKey = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(DefaultAdminKey)));
        }

        private string _DefaultAdminKey = "netledgeradmin";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthSettings()
        {
        }
    }
}
