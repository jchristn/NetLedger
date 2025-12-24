namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Specifies the ordering for enumeration results.
    /// </summary>
    public enum EnumerationOrder
    {
        /// <summary>
        /// Order by creation date, ascending (oldest first).
        /// </summary>
        CreatedAscending = 0,

        /// <summary>
        /// Order by creation date, descending (newest first).
        /// </summary>
        CreatedDescending = 1,

        /// <summary>
        /// Order by amount, ascending (smallest first).
        /// </summary>
        AmountAscending = 2,

        /// <summary>
        /// Order by amount, descending (largest first).
        /// </summary>
        AmountDescending = 3
    }

    /// <summary>
    /// Query parameters for enumerating entries.
    /// </summary>
    public class EntryEnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of results to return. Default is 100. Range: 1-1000.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Number of results to skip for pagination.
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Continuation token from a previous query for pagination.
        /// </summary>
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// Filter for entries created after this UTC timestamp.
        /// </summary>
        public DateTime? CreatedAfterUtc { get; set; }

        /// <summary>
        /// Filter for entries created before this UTC timestamp.
        /// </summary>
        public DateTime? CreatedBeforeUtc { get; set; }

        /// <summary>
        /// Filter for entries with amount greater than or equal to this value.
        /// </summary>
        public decimal? AmountMinimum { get; set; }

        /// <summary>
        /// Filter for entries with amount less than or equal to this value.
        /// </summary>
        public decimal? AmountMaximum { get; set; }

        /// <summary>
        /// The order in which to return results.
        /// </summary>
        public EnumerationOrder Ordering { get; set; } = EnumerationOrder.CreatedDescending;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new entry enumeration query with default values.
        /// </summary>
        public EntryEnumerationQuery()
        {
        }

        #endregion
    }

    /// <summary>
    /// Query parameters for enumerating accounts.
    /// </summary>
    public class AccountEnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of results to return. Default is 100. Range: 1-1000.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Number of results to skip for pagination.
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Search term to filter accounts by name.
        /// </summary>
        public string? SearchTerm { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new account enumeration query with default values.
        /// </summary>
        public AccountEnumerationQuery()
        {
        }

        #endregion
    }

    /// <summary>
    /// Query parameters for enumerating API keys.
    /// </summary>
    public class ApiKeyEnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of results to return. Default is 100. Range: 1-1000.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Number of results to skip for pagination.
        /// </summary>
        public int Skip { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new API key enumeration query with default values.
        /// </summary>
        public ApiKeyEnumerationQuery()
        {
        }

        #endregion
    }
}
