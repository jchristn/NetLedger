namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Request to commit pending entries for an account.
    /// </summary>
    public class CommitRequest
    {
        #region Public-Members

        /// <summary>
        /// The GUIDs of specific entries to commit. If null or empty, all pending entries will be committed.
        /// </summary>
        public List<Guid>? EntryGuids { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new commit request that will commit all pending entries.
        /// </summary>
        public CommitRequest()
        {
        }

        /// <summary>
        /// Instantiate a new commit request for specific entries.
        /// </summary>
        /// <param name="entryGuids">The GUIDs of entries to commit.</param>
        public CommitRequest(List<Guid>? entryGuids)
        {
            EntryGuids = entryGuids;
        }

        /// <summary>
        /// Instantiate a new commit request for specific entries.
        /// </summary>
        /// <param name="entryGuids">The GUIDs of entries to commit.</param>
        public CommitRequest(params Guid[] entryGuids)
        {
            EntryGuids = new List<Guid>(entryGuids);
        }

        #endregion
    }

    /// <summary>
    /// Result of a commit operation.
    /// </summary>
    public class CommitResult
    {
        #region Public-Members

        /// <summary>
        /// The number of entries that were committed.
        /// </summary>
        public int EntriesCommitted { get; set; }

        /// <summary>
        /// The balance entry created by the commit operation.
        /// </summary>
        public Entry? BalanceEntry { get; set; }

        /// <summary>
        /// The new balance after the commit.
        /// </summary>
        public Balance? Balance { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new commit result.
        /// </summary>
        public CommitResult()
        {
        }

        #endregion
    }
}
