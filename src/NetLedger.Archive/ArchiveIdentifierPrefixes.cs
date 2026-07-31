namespace NetLedger.Archive
{
    /// <summary>
    /// Identifier prefixes used by archive entities.
    /// </summary>
    public static class ArchiveIdentifierPrefixes
    {
        /// <summary>
        /// Archive storage pool identifier prefix.
        /// </summary>
        public const string StoragePool = "asp_";

        /// <summary>
        /// Archive migration identifier prefix.
        /// </summary>
        public const string Migration = "amg_";

        /// <summary>
        /// Archive migration batch identifier prefix.
        /// </summary>
        public const string MigrationBatch = "amb_";

        /// <summary>
        /// Archive manifest identifier prefix.
        /// </summary>
        public const string Manifest = "amf_";

        /// <summary>
        /// Archive range identifier prefix.
        /// </summary>
        public const string Range = "arn_";

        /// <summary>
        /// Archive object identifier prefix.
        /// </summary>
        public const string Object = "aob_";

        /// <summary>
        /// Archive checkpoint identifier prefix.
        /// </summary>
        public const string Checkpoint = "ach_";

        /// <summary>
        /// Archive audit record identifier prefix.
        /// </summary>
        public const string Audit = "aad_";

        /// <summary>
        /// Archive request history identifier prefix.
        /// </summary>
        public const string RequestHistory = "arq_";

        /// <summary>
        /// Archive object lock identifier prefix.
        /// </summary>
        public const string ObjectLock = "aol_";
    }
}
