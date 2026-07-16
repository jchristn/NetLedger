namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// Request history capture settings.
    /// </summary>
    public class RequestHistorySettings
    {
        /// <summary>
        /// Whether request history capture is enabled. Default is true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum captured request body bytes. Default is 65536, range is 0 through 1048576.
        /// </summary>
        public int MaxRequestBodyBytes
        {
            get
            {
                return _MaxRequestBodyBytes;
            }
            set
            {
                _MaxRequestBodyBytes = Math.Clamp(value, 0, 1024 * 1024);
            }
        }

        /// <summary>
        /// Maximum captured response body bytes. Default is 65536, range is 0 through 1048576.
        /// </summary>
        public int MaxResponseBodyBytes
        {
            get
            {
                return _MaxResponseBodyBytes;
            }
            set
            {
                _MaxResponseBodyBytes = Math.Clamp(value, 0, 1024 * 1024);
            }
        }

        /// <summary>
        /// Retention period in days. Default is 30, range is 1 through 3650.
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

        private int _MaxRequestBodyBytes = 65536;
        private int _MaxResponseBodyBytes = 65536;
        private int _RetentionDays = 30;
    }
}
