namespace NetLedger.Server.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Active server archive integration settings.
    /// </summary>
    public class ArchiveSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether active data archival is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Archive server endpoint.
        /// </summary>
        public string ArchiveServerEndpoint { get; set; } = "http://localhost:8081";

        /// <summary>
        /// Access key used by automatic archival background tasks when calling Archive Server.
        /// </summary>
        public string? ServiceAccessKey { get; set; } = null;

        /// <summary>
        /// Secret key used by automatic archival background tasks when calling Archive Server.
        /// </summary>
        public string? ServiceSecretKey { get; set; } = null;

        /// <summary>
        /// Default active data retention period in days. Default is 365, range is 1 through Int32.MaxValue.
        /// </summary>
        public long DefaultActiveDataRetentionDays
        {
            get
            {
                return _DefaultActiveDataRetentionDays;
            }
            set
            {
                _DefaultActiveDataRetentionDays = Math.Clamp(value, 1L, Int32.MaxValue);
            }
        }

        /// <summary>
        /// Tenant-specific archive retention settings.
        /// </summary>
        public List<ArchiveTenantSettings> Tenants { get; set; } = new List<ArchiveTenantSettings>();

        /// <summary>
        /// Automatic archival worker settings.
        /// </summary>
        public ArchiveAutomaticSettings Automatic { get; set; } = new ArchiveAutomaticSettings();

        #endregion

        #region Private-Members

        private long _DefaultActiveDataRetentionDays = 365;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ArchiveSettings()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get the active data retention days for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>Retention period in days.</returns>
        public int GetActiveDataRetentionDays(string tenantId)
        {
            if (!String.IsNullOrEmpty(tenantId) && Tenants != null)
            {
                foreach (ArchiveTenantSettings tenant in Tenants)
                {
                    if (tenant == null)
                    {
                        continue;
                    }

                    if (String.Equals(tenant.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToInt32(tenant.ActiveDataRetentionDays);
                    }
                }
            }

            return Convert.ToInt32(DefaultActiveDataRetentionDays);
        }

        #endregion
    }
}
