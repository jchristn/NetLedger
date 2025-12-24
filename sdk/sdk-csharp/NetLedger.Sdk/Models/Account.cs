namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Represents a ledger account.
    /// </summary>
    public class Account
    {
        #region Public-Members

        /// <summary>
        /// The unique identifier for the account.
        /// </summary>
        public Guid GUID { get; set; }

        /// <summary>
        /// The name of the account.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional notes associated with the account.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// The UTC timestamp when the account was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

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
