namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the type of a ledger entry.
    /// </summary>
    public enum EntryType
    {
        /// <summary>
        /// A credit entry (increases balance).
        /// </summary>
        Credit = 0,

        /// <summary>
        /// A debit entry (decreases balance).
        /// </summary>
        Debit = 1,

        /// <summary>
        /// A balance entry (snapshot of account state at commit time).
        /// </summary>
        Balance = 2
    }

    /// <summary>
    /// Represents a ledger entry (credit, debit, or balance snapshot).
    /// </summary>
    public class Entry
    {
        #region Public-Members

        /// <summary>
        /// The unique identifier for the entry.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The tenant identifier for the entry.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// The Id of the account this entry belongs to.
        /// </summary>
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// The type of entry (Credit, Debit, or Balance).
        /// </summary>
        public EntryType Type { get; set; }

        /// <summary>
        /// The monetary amount of the entry.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional description for the entry.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// For balance entries, the Id of the previous balance entry this replaces.
        /// </summary>
        public string? Replaces { get; set; }

        /// <summary>
        /// Indicates whether the entry has been committed.
        /// </summary>
        public bool IsCommitted { get; set; }

        /// <summary>
        /// The Id of the balance entry that committed this entry.
        /// </summary>
        public string? CommittedById { get; set; }

        /// <summary>
        /// Entry labels.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Entry tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The UTC timestamp when the entry was committed.
        /// </summary>
        public DateTime? CommittedUtc { get; set; }

        /// <summary>
        /// The UTC timestamp when the entry was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// The UTC timestamp when the entry was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new entry.
        /// </summary>
        public Entry()
        {
        }

        #endregion
    }
}

