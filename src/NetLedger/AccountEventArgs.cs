namespace NetLedger
{
    using System;

    /// <summary>
    /// Account event arguments.
    /// </summary>
    public class AccountEventArgs : EventArgs
    {
        /// <summary>
        /// Globally-unique identifier for the account.
        /// </summary>
        public Guid GUID { get; private set; }

        /// <summary>
        /// Name of the account.
        /// </summary>
        public string Name { get; private set; } = null;

        /// <summary>
        /// Notes for the account.
        /// </summary>
        public string Notes { get; private set; } = null;

        /// <summary>
        /// UTC timestamp when the account was created.
        /// </summary>
        public DateTime CreatedUtc { get; private set; } = DateTime.Now.ToUniversalTime();

        internal AccountEventArgs(Account a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));

            GUID = a.GUID;
            Name = a.Name;
            Notes = a.Notes;
            CreatedUtc = a.CreatedUtc;
        }
    }
}
