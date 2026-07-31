namespace NetLedger.Archive.Settings
{
    using NetLedger.Database;

    /// <summary>
    /// Archive catalog database settings.
    /// </summary>
    public class ArchiveCatalogSettings : DatabaseSettings
    {
        /// <summary>
        /// Instantiate.
        /// </summary>
        public ArchiveCatalogSettings()
        {
            Filename = "./netledger.archive.catalog.db";
        }
    }
}
