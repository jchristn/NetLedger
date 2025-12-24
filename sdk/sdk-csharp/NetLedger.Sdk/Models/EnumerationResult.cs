namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the result of an enumeration query with pagination support.
    /// </summary>
    /// <typeparam name="T">The type of objects in the result.</typeparam>
    public class EnumerationResult<T>
    {
        #region Public-Members

        /// <summary>
        /// The total number of records matching the query.
        /// </summary>
        public int TotalRecords { get; set; }

        /// <summary>
        /// The number of records remaining after the current page.
        /// </summary>
        public int RecordsRemaining { get; set; }

        /// <summary>
        /// Indicates whether this is the last page of results.
        /// </summary>
        public bool EndOfResults { get; set; }

        /// <summary>
        /// Continuation token for fetching the next page of results.
        /// </summary>
        public string? ContinuationToken { get; set; }

        /// <summary>
        /// The objects in the current page of results.
        /// </summary>
        public List<T>? Objects { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new enumeration result.
        /// </summary>
        public EnumerationResult()
        {
        }

        #endregion
    }
}
