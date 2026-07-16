namespace NetLedger
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// An entry in the ledger for a given account.
    /// </summary>
    public class Entry
    {
        #region Public-Members

        /// <summary>
        /// Internal provider row ID.
        /// </summary>
        [JsonIgnore]
        public int RowId { get; set; } = 0;

        /// <summary>
        /// Entry identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Entry);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string AccountId { get; set; } = String.Empty;

        /// <summary>
        /// Legacy entry identifier alias.
        /// </summary>
        [JsonIgnore]
        public string GUID
        {
            get { return Id; }
            set { Id = value; }
        }

        /// <summary>
        /// Legacy account identifier alias.
        /// </summary>
        [JsonIgnore]
        public string AccountGUID
        {
            get { return AccountId; }
            set { AccountId = value; }
        }

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
        public string? Replaces { get; set; } = null;

        /// <summary>
        /// Indicates if the entry has been committed to the ledger and is reflected in the current balance.
        /// </summary>
        public bool IsCommitted { get; set; } = false;

        /// <summary>
        /// GUID of the entry that committed this entry.
        /// </summary>
        public string? CommittedById { get; set; } = null;

        /// <summary>
        /// Legacy committed-by identifier alias.
        /// </summary>
        [JsonIgnore]
        public string? CommittedByGUID
        {
            get { return CommittedById; }
            set { CommittedById = value; }
        }

        /// <summary>
        /// Entry labels.
        /// </summary>
        public List<string> Labels
        {
            get { return _Labels; }
            set { _Labels = MetadataValidator.NormalizeLabels(value); }
        }

        /// <summary>
        /// Entry tags.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get { return _Tags; }
            set { _Tags = MetadataValidator.NormalizeTags(value); }
        }

        /// <summary>
        /// UTC timestamp when the entry was committed.
        /// </summary>
        public DateTime? CommittedUtc { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the entry was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// UTC timestamp when the entry was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.Now.ToUniversalTime();

        #endregion

        #region Private-Members

        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        public Entry(string accountGuid, EntryType entryType, decimal amount, string? notes = null, string? summarizedBy = null, bool isCommitted = false)
        {
            if (String.IsNullOrEmpty(accountGuid)) throw new ArgumentNullException(nameof(accountGuid));
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

