namespace NetLedger.Archive
{
    /// <summary>
    /// Archive manifest status.
    /// </summary>
    public enum ArchiveManifestStatus
    {
        /// <summary>
        /// Manifest is committed and visible.
        /// </summary>
        Committed,

        /// <summary>
        /// Manifest was superseded by another manifest.
        /// </summary>
        Superseded,

        /// <summary>
        /// Manifest is quarantined.
        /// </summary>
        Quarantined
    }
}
