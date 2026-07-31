namespace NetLedger.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Archive Server health response.
    /// </summary>
    public class ArchiveHealth
    {
        /// <summary>
        /// Indicates whether Archive Server is healthy.
        /// </summary>
        public bool Healthy { get; set; }

        /// <summary>
        /// Archive Server version.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Health details.
        /// </summary>
        public List<string>? Details { get; set; }
    }
}
