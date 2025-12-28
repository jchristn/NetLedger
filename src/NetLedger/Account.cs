namespace NetLedger
{
    using System;

    /// <summary>
    /// Account information.
    /// </summary>
    public class Account
    {
        #region Public-Members

        /// <summary>
        /// Database row ID.
        /// </summary>
        public int Id { get; set; } = 0;

        /// <summary>
        /// Globally-unique identifier for the account.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Name of the account.
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Notes for the account.
        /// </summary>
        public string? Notes { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the account was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.Now.ToUniversalTime();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an account.
        /// </summary>
        public Account()
        {
        }

        /// <summary>
        /// Instantiate an account with the specified name.
        /// </summary>
        /// <param name="name">Name of the account.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        public Account(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Name = name;
        }

        #endregion
    }
}
