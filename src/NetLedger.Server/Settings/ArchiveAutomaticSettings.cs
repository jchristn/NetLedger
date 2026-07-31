namespace NetLedger.Server.Settings
{
    using System;

    /// <summary>
    /// Global automatic archival worker settings.
    /// </summary>
    public class ArchiveAutomaticSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the background automatic archival worker is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Maximum active data retention in days before automatic archival.
        /// Range is 1 through Int32.MaxValue.
        /// </summary>
        public long MaxRetentionDays
        {
            get
            {
                return _MaxRetentionDays;
            }
            set
            {
                _MaxRetentionDays = Math.Clamp(value, 1L, Int32.MaxValue);
            }
        }

        /// <summary>
        /// Worker run interval in seconds.
        /// Range is 1 through Int32.MaxValue.
        /// </summary>
        public int IntervalSeconds
        {
            get
            {
                return _IntervalSeconds;
            }
            set
            {
                _IntervalSeconds = Math.Clamp(value, 1, Int32.MaxValue);
            }
        }

        /// <summary>
        /// Initial worker startup delay in seconds.
        /// Range is 0 through Int32.MaxValue.
        /// </summary>
        public int InitialDelaySeconds
        {
            get
            {
                return _InitialDelaySeconds;
            }
            set
            {
                _InitialDelaySeconds = Math.Clamp(value, 0, Int32.MaxValue);
            }
        }

        /// <summary>
        /// Maximum accounts to consider per worker run.
        /// Range is 1 through 10000.
        /// </summary>
        public int MaxAccountsPerRun
        {
            get
            {
                return _MaxAccountsPerRun;
            }
            set
            {
                _MaxAccountsPerRun = Math.Clamp(value, 1, 10000);
            }
        }

        /// <summary>
        /// Maximum rows per archive migration batch.
        /// Range is 1 through 50000.
        /// </summary>
        public int MaxBatchRows
        {
            get
            {
                return _MaxBatchRows;
            }
            set
            {
                _MaxBatchRows = Math.Clamp(value, 1, 50000);
            }
        }

        /// <summary>
        /// Whether active rows should be deleted after Archive Server commit confirmation.
        /// </summary>
        public bool DeleteAfterCommit { get; set; } = false;

        /// <summary>
        /// Optional Archive Server storage pool identifier.
        /// </summary>
        public string? StoragePoolId { get; set; } = null;

        /// <summary>
        /// Retry policy for automatic archival attempts.
        /// </summary>
        public ArchiveRetrySettings Retry { get; set; } = new ArchiveRetrySettings();

        #endregion

        #region Private-Members

        private long _MaxRetentionDays = 365;
        private int _IntervalSeconds = 3600;
        private int _InitialDelaySeconds = 30;
        private int _MaxAccountsPerRun = 100;
        private int _MaxBatchRows = 50000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ArchiveAutomaticSettings()
        {
        }

        #endregion
    }
}
