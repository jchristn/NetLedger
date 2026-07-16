namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Input for creating a single entry (credit or debit).
    /// </summary>
    public class EntryInput
    {
        #region Public-Members

        /// <summary>
        /// The monetary amount of the entry. Must be greater than zero.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional notes for the entry.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Labels to attach to the entry.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Tags to attach to the entry.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new entry input.
        /// </summary>
        public EntryInput()
        {
        }

        /// <summary>
        /// Instantiate a new entry input with amount.
        /// </summary>
        /// <param name="amount">The monetary amount.</param>
        public EntryInput(decimal amount)
        {
            Amount = amount;
        }

        /// <summary>
        /// Instantiate a new entry input with amount and notes.
        /// </summary>
        /// <param name="amount">The monetary amount.</param>
        /// <param name="notes">Optional notes.</param>
        public EntryInput(decimal amount, string? notes)
        {
            Amount = amount;
            Notes = notes;
        }

        #endregion
    }
}

