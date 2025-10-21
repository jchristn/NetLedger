namespace NetLedger
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Account balance information.
    /// </summary>
    public class Balance
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// GUID of the account.
        /// </summary>
        public Guid AccountGUID { get; set; }

        /// <summary>
        /// Entry GUID containing the latest balance entry.
        /// </summary>
        public Guid EntryGUID { get; set; }

        /// <summary>
        /// Name of the account.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the account was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// UTC timestamp when the most recent balance was calculated.
        /// </summary>
        public DateTime BalanceTimestampUtc { get; set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// Committed balance available in the account.
        /// This balance does not include pending debits and credits.
        /// </summary>
        public decimal CommittedBalance { get; set; } = 0m;

        /// <summary>
        /// Pending balance in the account.
        /// This balance includes pending credits and debits.
        /// </summary>
        public decimal PendingBalance { get; set; } = 0m;

        /// <summary>
        /// Pending credits.
        /// These transactions are not reflected in the committed balance.
        /// </summary>
        public PendingTransactionSummary PendingCredits { get; set; } = new PendingTransactionSummary();

        /// <summary>
        /// Pending debits.
        /// These transactions are not reflected in the committed balance.
        /// </summary>
        public PendingTransactionSummary PendingDebits { get; set; } = new PendingTransactionSummary();

        /// <summary>
        /// GUIDs of committed entries.
        /// </summary>
        public List<Guid> Committed { get; set; } = new List<Guid>();

        /// <summary>
        /// Instantiate a balance object.
        /// </summary>
        public Balance()
        {

        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
