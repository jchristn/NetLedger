namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// Tenant-specific active data archive retention settings.
    /// </summary>
    public class ArchiveTenantSettings
    {
        #region Public-Members

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Active data retention period in days. Default is 365, range is 1 through Int32.MaxValue.
        /// </summary>
        public long ActiveDataRetentionDays
        {
            get
            {
                return _ActiveDataRetentionDays;
            }
            set
            {
                _ActiveDataRetentionDays = Math.Clamp(value, 1L, Int32.MaxValue);
            }
        }

        #endregion

        #region Private-Members

        private long _ActiveDataRetentionDays = 365;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ArchiveTenantSettings()
        {
        }

        #endregion
    }
}
