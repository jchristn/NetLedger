namespace NetLedger
{
    using System;

    /// <summary>
    /// Represents input data for a batch entry operation (credit or debit).
    /// </summary>
    public class BatchEntryInput
    {
        #region Public-Members

        /// <summary>
        /// Amount for the entry.
        /// Must be a non-negative value.
        /// </summary>
        public decimal Amount
        {
            get => _Amount;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Amount), "Amount cannot be negative.");
                _Amount = value;
            }
        }

        /// <summary>
        /// Optional notes or description for the entry.
        /// Can be null.
        /// </summary>
        public string? Notes { get; set; } = null;

        #endregion

        #region Private-Members

        private decimal _Amount = 0m;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a batch entry input with default values.
        /// </summary>
        public BatchEntryInput()
        {
        }

        /// <summary>
        /// Instantiate a batch entry input with the specified amount and notes.
        /// </summary>
        /// <param name="amount">Amount for the entry. Must be non-negative.</param>
        /// <param name="notes">Optional notes or description for the entry.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when amount is negative.</exception>
        public BatchEntryInput(decimal amount, string? notes = null)
        {
            Amount = amount;
            Notes = notes;
        }

        #endregion
    }
}
