namespace NetLedger.Sdk
{
    using System;

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
