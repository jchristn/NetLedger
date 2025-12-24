namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the balance state of an account at a point in time.
    /// </summary>
    public class Balance
    {
        #region Public-Members

        /// <summary>
        /// The GUID of the account.
        /// </summary>
        public Guid AccountGUID { get; set; }

        /// <summary>
        /// The committed balance (finalized from the latest balance entry).
        /// </summary>
        public decimal CommittedBalance { get; set; }

        /// <summary>
        /// The pending balance (committed balance plus/minus uncommitted entries).
        /// </summary>
        public decimal PendingBalance { get; set; }

        /// <summary>
        /// Summary of pending credit transactions.
        /// </summary>
        public PendingTransactionSummary? PendingCredits { get; set; }

        /// <summary>
        /// Summary of pending debit transactions.
        /// </summary>
        public PendingTransactionSummary? PendingDebits { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new balance.
        /// </summary>
        public Balance()
        {
        }

        #endregion
    }

    /// <summary>
    /// Summary of pending transactions (credits or debits).
    /// </summary>
    public class PendingTransactionSummary
    {
        #region Public-Members

        /// <summary>
        /// The number of pending transactions.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// The total amount of pending transactions.
        /// </summary>
        public decimal Total { get; set; }

        /// <summary>
        /// The list of pending entries.
        /// </summary>
        public List<Entry>? Entries { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new pending transaction summary.
        /// </summary>
        public PendingTransactionSummary()
        {
        }

        #endregion
    }
}
