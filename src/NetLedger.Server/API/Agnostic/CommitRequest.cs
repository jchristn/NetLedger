namespace NetLedger.Server.API.Agnostic
{
    using System.Collections.Generic;

    internal class CommitRequest
    {
        /// <summary>
        /// Optional entry identifiers to commit.
        /// </summary>
        public List<string>? EntryIds { get; set; }

    }
}
