namespace NetLedger.Database
{
    using System;

    /// <summary>
    /// Database schema migration record.
    /// </summary>
    public class SchemaMigration
    {
        /// <summary>
        /// Migration identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Migration);

        /// <summary>
        /// Migration name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Applied timestamp UTC.
        /// </summary>
        public DateTime AppliedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Migration checksum.
        /// </summary>
        public string Checksum { get; set; } = String.Empty;

        /// <summary>
        /// Whether the migration completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;
    }
}
