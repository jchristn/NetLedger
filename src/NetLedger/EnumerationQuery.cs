namespace NetLedger
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Timestamps;

    /// <summary>
    /// Object used to request enumeration.
    /// </summary>
    public class EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Bucket GUID.
        /// </summary>
        public Guid AccountGUID { get; set; }

        /// <summary>
        /// Maximum number of results to retrieve.
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
        /// Continuation token.
        /// </summary>
        public Guid? ContinuationToken { get; set; } = null;

        /// <summary>
        /// Order by.
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        /// <summary>
        /// Only include entries created on or after this timestamp UTC.
        /// </summary>
        public DateTime? CreatedAfterUtc { get; set; } = null;

        /// <summary>
        /// Only include entries created on or before this timestamp UTC.
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
