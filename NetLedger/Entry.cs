using System;
using System.Collections.Generic;
using System.Text;
using Watson.ORM.Core;
using Newtonsoft.Json;

namespace NetLedger
{
    /// <summary>
    /// An entry in the ledger for a given account.
    /// </summary>
    [Table("entries")]
    public class Entry
    {
        #region Public-Members

        /// <summary>
        /// Database row ID.
        /// </summary>
        [JsonIgnore]
        [Column("id", true, DataTypes.Int, false)]
        public int Id { get; set; } = 0;

        /// <summary>
        /// Globally-unique identifier for the entry.
        /// </summary>
        [JsonProperty(Order = -4)]
        [Column("guid", false, DataTypes.Nvarchar, 64, false)]
        public string GUID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Globally-unique identifier for the account.
        /// </summary>
        [JsonProperty(Order = -3)]
        [Column("accountguid", false, DataTypes.Nvarchar, 64, false)]
        public string AccountGUID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The type of entry.
        /// </summary>
        [JsonProperty(Order = -2)]
        [Column("type", false, DataTypes.Nvarchar, 16, false)]
        public EntryType Type { get; set; } = EntryType.Balance;

        /// <summary>
        /// The amount/value of the entry.
        /// </summary>
        [JsonProperty(Order = -1)]
        [Column("amount", false, DataTypes.Decimal, 18, 8, false)]
        public decimal Amount { get; set; } = 0m;

        /// <summary>
        /// Description of the entry.
        /// </summary>
        [Column("description", false, DataTypes.Nvarchar, 256, true)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Specifies the GUID of the entry that this entry is replacing.  Used only by balance entries.
        /// </summary>
        [JsonProperty(Order = 995)]
        [Column("replaces", false, DataTypes.Nvarchar, 64, true)]
        public string Replaces { get; set; } = null;

        /// <summary>
        /// Indicates if the entry has been committed to the ledger and is reflected in the current balance.
        /// </summary>
        [JsonProperty(Order = 996)]
        [Column("committed", false, DataTypes.Boolean, false)]
        public bool IsCommitted { get; set; } = false;

        /// <summary>
        /// GUID of the entry that committed this entry.
        /// </summary>
        [JsonProperty(Order = 997)]
        [Column("committedbyguid", false, DataTypes.Nvarchar, 64, true)]
        public string CommittedByGUID { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the entry was committed.
        /// </summary>
        [JsonProperty(Order = 998)]
        [Column("committedutc", false, DataTypes.DateTime, true)]
        public DateTime? CommittedUtc { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the entry was created.
        /// </summary>
        [JsonProperty(Order = 999)]
        [Column("createdutc", false, DataTypes.DateTime, false)]
        public DateTime CreatedUtc { get; set; } = DateTime.Now.ToUniversalTime();

        #endregion

        #region Private-Members

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
        public Entry(string accountGuid, EntryType entryType, decimal amount, string notes = null, string summarizedBy = null, bool isCommitted = false)
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

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
