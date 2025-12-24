namespace NetLedger
{
    using System;
    using System.Runtime.Serialization;

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
