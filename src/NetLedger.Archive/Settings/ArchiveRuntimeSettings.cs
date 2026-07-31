namespace NetLedger.Archive.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Archive runtime settings.
    /// </summary>
    public class ArchiveRuntimeSettings
    {
        /// <summary>
        /// Default storage pool identifier.
        /// </summary>
        public string DefaultStoragePoolId { get; set; } = "asp_default";

        /// <summary>
        /// Require complete coverage for archive queries.
        /// </summary>
        public bool RequireCompleteCoverage { get; set; } = true;

        /// <summary>
        /// Maximum enumeration results.
        /// </summary>
        public int MaxEnumerationResults
        {
            get
            {
                return _MaxEnumerationResults;
            }
            set
            {
                _MaxEnumerationResults = Math.Clamp(value, 1, 100000);
            }
        }

        /// <summary>
        /// Maximum migration batch rows.
        /// </summary>
        public int MaxMigrationBatchRows
        {
            get
            {
                return _MaxMigrationBatchRows;
            }
            set
            {
                _MaxMigrationBatchRows = Math.Clamp(value, 1, 1000000);
            }
        }

        /// <summary>
        /// Maximum migration batch bytes.
        /// </summary>
        public long MaxMigrationBatchBytes
        {
            get
            {
                return _MaxMigrationBatchBytes;
            }
            set
            {
                _MaxMigrationBatchBytes = Math.Clamp(value, 1, Int32.MaxValue);
            }
        }

        /// <summary>
        /// Accepted archive formats.
        /// </summary>
        public List<ArchiveFormat> AcceptedFormats { get; set; } = new List<ArchiveFormat> { ArchiveFormat.JsonlGzip };

        /// <summary>
        /// Preferred archive format.
        /// </summary>
        public ArchiveFormat PreferredFormat { get; set; } = ArchiveFormat.JsonlGzip;

        private int _MaxEnumerationResults = 1000;
        private int _MaxMigrationBatchRows = 50000;
        private long _MaxMigrationBatchBytes = 134217728;
    }
}
