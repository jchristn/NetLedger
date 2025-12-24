namespace NetLedger.Server.Models
{
    using System;
    using NetLedger;

    /// <summary>
    /// Object used to request API key enumeration.
    /// </summary>
    public class ApiKeyEnumerationQuery
    {
        #region Public-Members

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
        /// When provided, skip should not be used.
        /// </summary>
        public Guid? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Order by.
        /// Default is CreatedDescending.
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        /// <summary>
        /// Search term to filter API keys by name.
        /// </summary>
        public string? SearchTerm { get; set; } = null;

        /// <summary>
        /// Only include API keys created on or after this timestamp UTC.
        /// </summary>
        public DateTime? CreatedAfterUtc { get; set; } = null;

        /// <summary>
        /// Only include API keys created on or before this timestamp UTC.
        /// </summary>
        public DateTime? CreatedBeforeUtc { get; set; } = null;

        #endregion

        #region Private-Members

        private int _MaxResults = 1000;
        private int _Skip = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiKeyEnumerationQuery()
        {
        }

        #endregion
    }
}
