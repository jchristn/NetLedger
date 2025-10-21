namespace NetLedger
{
    using System;

    /// <summary>
    /// Entry event arguments.
    /// </summary>
    public class EntryEventArgs
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Account details.
        /// </summary>
        public Account Account { get; private set; } = null;

        /// <summary>
        /// Entry details.
        /// </summary>
        public Entry Entry { get; private set; } = null;

        internal EntryEventArgs(Account a, Entry e)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (e == null) throw new ArgumentNullException(nameof(e));

            Account = a;
            Entry = e;
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
