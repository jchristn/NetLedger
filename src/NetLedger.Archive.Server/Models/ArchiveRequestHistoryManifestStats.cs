namespace NetLedger.Archive.Server.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Request-history archive manifest statistics.
    /// </summary>
    internal sealed class ArchiveRequestHistoryManifestStats
    {
        /// <summary>
        /// Row count.
        /// </summary>
        public long RowCount { get; set; } = 0;

        /// <summary>
        /// Minimum request timestamp.
        /// </summary>
        public DateTime? MinCreatedUtc { get; set; } = null;

        /// <summary>
        /// Maximum request timestamp.
        /// </summary>
        public DateTime? MaxCreatedUtc { get; set; } = null;

        /// <summary>
        /// HTTP method counts.
        /// </summary>
        public Dictionary<string, long> MethodCounts { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// HTTP status code counts.
        /// </summary>
        public Dictionary<string, long> StatusCodeCounts { get; set; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    }
}
