using System;
using System.Collections.Generic;
using System.Text;
using Watson.ORM.Core;
using System.Runtime.Serialization;

namespace NetLedger
{
    /// <summary>
    /// The type of entry in the ledger.
    /// </summary>
    public enum EntryType
    {
        /// <summary>
        /// Debit
        /// </summary>
        Debit,
        /// <summary>
        /// Credit
        /// </summary>
        Credit,
        /// <summary>
        /// Balance
        /// </summary>
        Balance
    }
}
