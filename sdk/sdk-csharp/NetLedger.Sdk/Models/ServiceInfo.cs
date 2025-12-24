namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Information about the NetLedger service.
    /// </summary>
    public class ServiceInfo
    {
        #region Public-Members

        /// <summary>
        /// The name of the service.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The version of the service.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// The hostname of the server.
        /// </summary>
        public string Hostname { get; set; } = string.Empty;

        /// <summary>
        /// The server's uptime in a human-readable format.
        /// </summary>
        public string Uptime { get; set; } = string.Empty;

        /// <summary>
        /// The UTC timestamp when the server started.
        /// </summary>
        public DateTime StartTimeUtc { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new service info.
        /// </summary>
        public ServiceInfo()
        {
        }

        #endregion
    }
}
