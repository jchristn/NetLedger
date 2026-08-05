namespace NetLedger.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a ledger account.
    /// </summary>
    public class Account
    {
        #region Public-Members

        /// <summary>
        /// The unique identifier for the account.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// The tenant identifier for the account.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// The name of the account.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional notes associated with the account.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Optional unit or currency label for the account (e.g. "USD" or "tokens").
        /// </summary>
        public string? Units { get; set; }

        /// <summary>
        /// Account labels.
        /// </summary>
        public List<string> Labels { get; set; } = new List<string>();

        /// <summary>
        /// Account tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The UTC timestamp when the account was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// The UTC timestamp when the account was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; }

        /// <summary>
        /// Indicates whether the account is active.
        /// </summary>
        public bool Active { get; set; } = true;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new account.
        /// </summary>
        public Account()
        {
        }

        /// <summary>
        /// Instantiate a new account with a name.
        /// </summary>
        /// <param name="name">The account name.</param>
        public Account(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Instantiate a new account with name and notes.
        /// </summary>
        /// <param name="name">The account name.</param>
        /// <param name="notes">Optional notes.</param>
        public Account(string name, string? notes)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Notes = notes;
        }

        #endregion
    }
}

