using System;
using System.Collections.Generic;
using System.Text;
using Watson.ORM.Core;

namespace NetLedger
{
    /// <summary>
    /// Account balance information.
    /// </summary> 
    public class Balance
    {
        #region Public-Members

        /// <summary>
        /// GUID of the account.
        /// </summary>
        public string AccountGUID { get; set; } = null;

        /// <summary>
        /// Entry GUID containing the latest balance entry.
        /// </summary>
        public string EntryGUID { get; set; } = null;

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
        public List<string> Committed { get; set; } = new List<string>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a balance object.
        /// </summary>
        public Balance()
        {

        }
         
        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion

        #region Public-Embedded-Classes

        /// <summary>
        /// Summary of pending transactions.
        /// </summary>
        public class PendingTransactionSummary
        {
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
        }

        #endregion
    }
}
