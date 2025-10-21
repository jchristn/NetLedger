namespace NetLedger
{
    using System;

    /// <summary>
    /// Commit event arguments.
    /// </summary>
    public class CommitEventArgs : EventArgs
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Account.
        /// </summary>
        public Account Account { get; private set; } = null;

        /// <summary>
        /// Balance from before the commit.
        /// </summary>
        public Balance BalanceBefore { get; private set; } = null;

        /// <summary>
        /// Balance from after the commit.
        /// </summary>
        public Balance BalanceAfter { get; private set; } = null;

        internal CommitEventArgs(Account account, Balance before, Balance after)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            Account = account;
            BalanceBefore = before;
            BalanceAfter = after;
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
