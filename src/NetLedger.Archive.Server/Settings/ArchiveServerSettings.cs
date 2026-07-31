namespace NetLedger.Archive.Server.Settings
{
    using System.Collections.Generic;
    using NetLedger.Archive.Settings;

    /// <summary>
    /// Archive server settings.
    /// </summary>
    public class ArchiveServerSettings
    {
        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings Webserver { get; set; } = new WebserverSettings();

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging { get; set; } = new LoggingSettings();

        /// <summary>
        /// Authentication settings.
        /// </summary>
        public AuthSettings Authentication { get; set; } = new AuthSettings();

        /// <summary>
        /// Catalog settings.
        /// </summary>
        public ArchiveCatalogSettings Catalog { get; set; } = new ArchiveCatalogSettings();

        /// <summary>
        /// Archive runtime settings.
        /// </summary>
        public ArchiveRuntimeSettings Archive { get; set; } = new ArchiveRuntimeSettings();

        /// <summary>
        /// Storage pools.
        /// </summary>
        public List<ArchiveStoragePoolSettings> StoragePools { get; set; } = new List<ArchiveStoragePoolSettings> { new ArchiveStoragePoolSettings() };

        /// <summary>
        /// Request history settings.
        /// </summary>
        public RequestHistorySettings RequestHistory { get; set; } = new RequestHistorySettings();
    }
}
