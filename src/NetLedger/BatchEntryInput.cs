namespace NetLedger
{
    using System;
    using System.Collections.Generic;

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

        /// <summary>
        /// Labels for the entry.
        /// </summary>
        public List<string> Labels
        {
            get { return _Labels; }
            set { _Labels = MetadataValidator.NormalizeLabels(value); }
        }

        /// <summary>
        /// Tags for the entry.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get { return _Tags; }
            set { _Tags = MetadataValidator.NormalizeTags(value); }
        }

        #endregion

        #region Private-Members

        private decimal _Amount = 0m;
        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

