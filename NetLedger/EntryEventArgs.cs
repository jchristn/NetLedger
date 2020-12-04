using System;
using System.Collections.Generic;
using System.Text;
using Watson.ORM.Core;
using Newtonsoft.Json;

namespace NetLedger
{
    /// <summary>
    /// Entry event arguments.
    /// </summary>
    public class EntryEventArgs
    {
        #region Public-Members

        /// <summary>
        /// Account details.
        /// </summary>
        [JsonProperty(Order = -1)]
        public Account Account { get; private set; } = null;

        /// <summary>
        /// Entry details.
        /// </summary>
        public Entry Entry { get; private set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        internal EntryEventArgs(Account a, Entry e)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (e == null) throw new ArgumentNullException(nameof(e));

            Account = a;
            Entry = e;
        }
         
        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
