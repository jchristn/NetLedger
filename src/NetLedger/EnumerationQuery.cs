namespace NetLedger
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Object used to request enumeration of accounts or entries.
    /// When enumerating accounts, AccountId should be null.
    /// When enumerating entries, AccountId must be specified.
    /// </summary>
    public class EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Account identifier.
        /// Required when enumerating entries within an account.
        /// Should be null when enumerating accounts.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User identifier used to restrict account enumeration to mapped accounts.
        /// Applicable only when enumerating accounts.
        /// </summary>
        public string? MappedUserId { get; set; } = null;

        /// <summary>
        /// User identifier used to restrict user-owned resources.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Maximum number of results to retrieve.
        /// Minimum value is 1, maximum value is 1000.
        /// Default value is 1000.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                if (value < 1) throw new ArgumentException("MaxResults must be greater than zero.");
                if (value > 1000) throw new ArgumentException("MaxResults must be one thousand or less.");
                _MaxResults = value;
            }
        }

        /// <summary>
        /// The number of records to skip.
        /// Minimum value is 0.
        /// Default value is 0.
        /// </summary>
        public int Skip
        {
            get
            {
                return _Skip;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Skip));
                _Skip = value;
            }
        }

        /// <summary>
        /// Continuation token for pagination.
        /// When provided, Skip should not be used.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Order by.
        /// Default is CreatedDescending.
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        /// <summary>
        /// Search term to filter results.
        /// When enumerating accounts, filters by account name.
        /// When enumerating entries, filters by entry description.
        /// </summary>
        public string? SearchTerm { get; set; } = null;

        /// <summary>
        /// Only include records created on or after this timestamp UTC.
        /// </summary>
        public DateTime? CreatedAfterUtc { get; set; } = null;

        /// <summary>
        /// Only include records created on or before this timestamp UTC.
        /// </summary>
        public DateTime? CreatedBeforeUtc { get; set; } = null;

        /// <summary>
        /// Only include entries with an amount greater than or equal to this value.
        /// Applicable only when enumerating entries.
        /// </summary>
        public decimal? AmountMinimum { get; set; } = null;

        /// <summary>
        /// Only include entries with an amount less than or equal to this value.
        /// Applicable only when enumerating entries.
        /// </summary>
        public decimal? AmountMaximum { get; set; } = null;

        /// <summary>
        /// Only include credit entries with an amount greater than or equal to this value.
        /// </summary>
        public decimal? CreditMinimum { get; set; } = null;

        /// <summary>
        /// Only include credit entries with an amount less than or equal to this value.
        /// </summary>
        public decimal? CreditMaximum { get; set; } = null;

        /// <summary>
        /// Only include debit entries with an amount greater than or equal to this value.
        /// </summary>
        public decimal? DebitMinimum { get; set; } = null;

        /// <summary>
        /// Only include debit entries with an amount less than or equal to this value.
        /// </summary>
        public decimal? DebitMaximum { get; set; } = null;

        /// <summary>
        /// Labels that must all exist on the returned object.
        /// </summary>
        public List<string> Labels
        {
            get { return _Labels; }
            set { _Labels = MetadataValidator.NormalizeLabels(value); }
        }

        /// <summary>
        /// Tags that must all match on the returned object.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get { return _Tags; }
            set { _Tags = MetadataValidator.NormalizeTags(value); }
        }

        /// <summary>
        /// Only include accounts with a committed balance greater than or equal to this value.
        /// Applicable only when enumerating accounts.
        /// </summary>
        public decimal? BalanceMinimum { get; set; } = null;

        /// <summary>
        /// Only include accounts with a committed balance less than or equal to this value.
        /// Applicable only when enumerating accounts.
        /// </summary>
        public decimal? BalanceMaximum { get; set; } = null;

        #endregion

        #region Private-Members

        private int _MaxResults = 1000;
        private int _Skip = 0;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationQuery()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}

