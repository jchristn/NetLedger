using System;
using System.Collections.Generic;
using System.Text;
using Watson.ORM.Core;
using Newtonsoft.Json;

namespace NetLedger
{
    /// <summary>
    /// Commit event arguments.
    /// </summary> 
    public class CommitEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Account.
        /// </summary>
        [JsonProperty(Order = -3)]
        public Account Account { get; private set; } = null;

        /// <summary>
        /// Balance from before the commit.
        /// </summary>
        [JsonProperty(Order = -2)]
        public Balance BalanceBefore { get; private set; } = null;

        /// <summary>
        /// Balance from after the commit.
        /// </summary>
        [JsonProperty(Order = -1)]
        public Balance BalanceAfter { get; private set; } = null;
        
        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories
         
        internal CommitEventArgs(Account account, Balance before, Balance after)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            Account = account;
            BalanceBefore = before;
            BalanceAfter = after;
        }
         
        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
