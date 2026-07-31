namespace NetLedger.Archive.Responses
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Archive health response.
    /// </summary>
    public class ArchiveHealthResponse
    {
        /// <summary>
        /// Whether the archive server is healthy.
        /// </summary>
        public bool Healthy { get; set; } = true;

        /// <summary>
        /// Service name.
        /// </summary>
        public string Name { get; set; } = "NetLedger.Archive.Server";

        /// <summary>
        /// Service version.
        /// </summary>
        public string Version { get; set; } = "4.0.0";

        /// <summary>
        /// Detail messages.
        /// </summary>
        public List<string> Details { get; set; } = new List<string>();

        /// <summary>
        /// Current UTC timestamp.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
