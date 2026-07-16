namespace NetLedger.Server.API.Agnostic
{
    using System.Collections.Generic;

    internal class EntryItem
    {
        /// <summary>
        /// Entry amount.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Entry notes.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Entry labels.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Entry tags.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }
    }
}
