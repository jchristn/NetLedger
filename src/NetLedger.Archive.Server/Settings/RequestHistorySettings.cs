namespace NetLedger.Archive.Server.Settings
{
    using System;

    /// <summary>
    /// Archive server request history settings.
    /// </summary>
    public class RequestHistorySettings
    {
        /// <summary>
        /// Enable request history capture.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Retention days.
        /// </summary>
        public int RetentionDays
        {
            get
            {
                return _RetentionDays;
            }
            set
            {
                _RetentionDays = Math.Clamp(value, 1, 3650);
            }
        }

        /// <summary>
        /// Maximum request body bytes.
        /// </summary>
        public int MaxRequestBodyBytes { get; set; } = 65536;

        /// <summary>
        /// Maximum response body bytes.
        /// </summary>
        public int MaxResponseBodyBytes { get; set; } = 65536;

        private int _RetentionDays = 30;
    }
}
