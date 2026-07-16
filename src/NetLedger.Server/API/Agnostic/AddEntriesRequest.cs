namespace NetLedger.Server.API.Agnostic
{
    using System.Collections.Generic;

    internal class AddEntriesRequest
    {
        /// <summary>
        /// Single-entry amount.
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// Single-entry notes.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Whether entries should be committed immediately.
        /// </summary>
        public bool? IsCommitted { get; set; }

        /// <summary>
        /// Batch entries.
        /// </summary>
        public List<EntryItem>? Entries { get; set; }

        /// <summary>
        /// Single-entry labels.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Single-entry tags.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }
    }
}
