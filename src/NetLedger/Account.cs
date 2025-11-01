namespace NetLedger
{
    using System;
    using Durable;

    /// <summary>
    /// Account information.
    /// </summary>
    [Entity("accounts")]
    public class Account
    {
        /// <summary>
        /// Database row ID.
        /// </summary>
        [Property("id", Flags.PrimaryKey | Flags.AutoIncrement)]
        public int Id { get; set; } = 0;

        /// <summary>
        /// Globally-unique identifier for the account.
        /// </summary>
        [Property("guid", Flags.String, 64)]
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Name of the account.
        /// </summary>
        [Property("name", Flags.String, 256)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Notes for the account.
        /// </summary>
        [Property("notes", Flags.String, 256)]
        public string? Notes { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the account was created.
        /// </summary>
        [Property("createdutc")]
        public DateTime CreatedUtc { get; set; } = DateTime.Now.ToUniversalTime();

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
        public Account(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Name = name;
        }
    }
}
