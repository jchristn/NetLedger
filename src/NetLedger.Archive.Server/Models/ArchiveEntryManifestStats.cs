namespace NetLedger.Archive.Server.Models
{
    using System.Collections.Generic;
    using NetLedger.Archive.Models;

    /// <summary>
    /// Entry manifest statistics computed from committed archive objects.
    /// </summary>
    internal sealed class ArchiveEntryManifestStats
    {
        /// <summary>
        /// Number of entry rows.
        /// </summary>
        public long RowCount { get; set; } = 0;

        /// <summary>
        /// Credit total.
        /// </summary>
        public decimal CreditTotal { get; set; } = 0m;

        /// <summary>
        /// Debit total.
        /// </summary>
        public decimal DebitTotal { get; set; } = 0m;

        /// <summary>
        /// Balance checkpoints discovered in archived entry objects.
        /// </summary>
        public List<ArchiveBalanceCheckpoint> BalanceCheckpoints { get; set; } = new List<ArchiveBalanceCheckpoint>();
    }
}
