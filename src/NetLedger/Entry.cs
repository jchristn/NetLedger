namespace NetLedger
{
    using System;

    /// <summary>
    /// An entry in the ledger for a given account.
    /// </summary>
    public class Entry
    {
        #region Public-Members

        /// <summary>
        /// Database row ID.
        /// </summary>
        public int Id { get; set; } = 0;

        /// <summary>
        /// Globally-unique identifier for the entry.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Globally-unique identifier for the account.
        /// </summary>
        public Guid AccountGUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The type of entry.
        /// </summary>
        public EntryType Type { get; set; } = EntryType.Balance;

        /// <summary>
        /// The amount/value of the entry.
        /// </summary>
        public decimal Amount { get; set; } = 0m;

        /// <summary>
        /// Description of the entry.
        /// </summary>
        public string? Description { get; set; } = null;

        /// <summary>
        /// Specifies the GUID of the entry that this entry is replacing.  Used only by balance entries.
        /// </summary>
        public Guid? Replaces { get; set; } = null;

        /// <summary>
        /// Indicates if the entry has been committed to the ledger and is reflected in the current balance.
        /// </summary>
        public bool IsCommitted { get; set; } = false;

        /// <summary>
        /// GUID of the entry that committed this entry.
        /// </summary>
        public Guid? CommittedByGUID { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the entry was committed.
        /// </summary>
        public DateTime? CommittedUtc { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the entry was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.Now.ToUniversalTime();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an entry.
        /// </summary>
        public Entry()
        {
        }

        /// <summary>
        /// Instantiate an entry.
        /// </summary>
        /// <param name="accountGuid">Account GUID.</param>
        /// <param name="entryType">Type of entry.</param>
        /// <param name="amount">Amount/value.</param>
        /// <param name="notes">Notes for the entry.</param>
        /// <param name="summarizedBy">GUID of the entry that summarized this entry.</param>
        /// <param name="isCommitted">Indicate whether or not the entry has already been included in the balance of the account.</param>
        /// <exception cref="ArgumentNullException">Thrown when accountGuid is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when amount is negative.</exception>
        public Entry(Guid accountGuid, EntryType entryType, decimal amount, string? notes = null, Guid? summarizedBy = null, bool isCommitted = false)
        {
            if (accountGuid == Guid.Empty) throw new ArgumentNullException(nameof(accountGuid));
            if (amount < 0) throw new ArgumentException("Amount must be zero or greater.");

            AccountGUID = accountGuid;
            Type = entryType;
            Amount = amount;
            Description = notes;
            CommittedByGUID = summarizedBy;

            if (isCommitted)
            {
                IsCommitted = true;
                CommittedUtc = CreatedUtc;
            }
        }

        #endregion
    }
}
