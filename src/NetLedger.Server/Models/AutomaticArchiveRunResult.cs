namespace NetLedger.Server.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Result from one automatic archival worker run.
    /// </summary>
    public class AutomaticArchiveRunResult
    {
        /// <summary>
        /// Run start UTC.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Run completion UTC.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Whether Archive integration was enabled for the run.
        /// </summary>
        public bool ArchiveEnabled { get; set; } = false;

        /// <summary>
        /// Global automatic archival default enabled value.
        /// </summary>
        public bool AutomaticEnabled { get; set; } = false;

        /// <summary>
        /// Number of accounts scanned.
        /// </summary>
        public long AccountsScanned { get; set; } = 0;

        /// <summary>
        /// Number of accounts skipped.
        /// </summary>
        public long AccountsSkipped { get; set; } = 0;

        /// <summary>
        /// Number of entry export attempts.
        /// </summary>
        public long EntryExportsAttempted { get; set; } = 0;

        /// <summary>
        /// Number of successful entry exports.
        /// </summary>
        public long EntryExportsSucceeded { get; set; } = 0;

        /// <summary>
        /// Number of failed entry exports.
        /// </summary>
        public long EntryExportsFailed { get; set; } = 0;

        /// <summary>
        /// Number of rows exported.
        /// </summary>
        public long RowsExported { get; set; } = 0;

        /// <summary>
        /// Number of uploaded bytes.
        /// </summary>
        public long BytesUploaded { get; set; } = 0;

        /// <summary>
        /// Number of active rows deleted after archive commit.
        /// </summary>
        public long ActiveRowsDeleted { get; set; } = 0;

        /// <summary>
        /// Errors observed during the run.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }
}
