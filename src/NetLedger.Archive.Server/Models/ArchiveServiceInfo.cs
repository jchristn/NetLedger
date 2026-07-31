namespace NetLedger.Archive.Server.Models
{
    using System;

    /// <summary>
    /// Archive service information.
    /// </summary>
    public class ArchiveServiceInfo
    {
        /// <summary>
        /// Service name.
        /// </summary>
        public string Name { get; set; } = "NetLedger.Archive.Server";

        /// <summary>
        /// Service version.
        /// </summary>
        public string Version { get; set; } = "4.0.0";

        /// <summary>
        /// Start time UTC.
        /// </summary>
        public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Uptime in seconds.
        /// </summary>
        public long UptimeSeconds { get; set; } = 0;

        /// <summary>
        /// Data scope.
        /// </summary>
        public string DataScope { get; set; } = "archive";
    }
}
