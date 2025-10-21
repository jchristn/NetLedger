namespace NetLedger
{
    using System.Collections.Generic;

    /// <summary>
    /// Summary of pending transactions.
    /// </summary>
    public class PendingTransactionSummary
    {
        #region Public-Members

        /// <summary>
        /// Number of pending transactions of this type.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// The total of amounts for all pending transactions of this type.
        /// </summary>
        public decimal Total { get; set; } = 0m;

        /// <summary>
        /// The entries associated with the pending transactions of this type.
        /// </summary>
        public List<Entry> Entries { get; set; } = new List<Entry>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a pending transaction summary.
        /// </summary>
        public PendingTransactionSummary()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
