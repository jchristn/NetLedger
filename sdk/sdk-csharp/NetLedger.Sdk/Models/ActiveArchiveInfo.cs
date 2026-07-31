namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Active archive integration information returned by NetLedger Server.
    /// </summary>
    public class ActiveArchiveInfo
    {
        /// <summary>
        /// Whether archive integration is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Archive Server endpoint when archive integration is enabled.
        /// </summary>
        public string? ArchiveServerEndpoint { get; set; }

        /// <summary>
        /// Active data retention period in days for the resolved tenant.
        /// </summary>
        public int ActiveDataRetentionDays { get; set; }

        /// <summary>
        /// Oldest UTC timestamp retained by active APIs for the resolved tenant.
        /// </summary>
        public DateTime ActiveBoundaryUtc { get; set; }
    }
}
