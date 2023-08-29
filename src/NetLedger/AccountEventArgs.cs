using System;
using System.Collections.Generic;
using System.Text;
using Watson.ORM.Core;

namespace NetLedger
{
    /// <summary>
    /// Account event arguments.
    /// </summary> 
    public class AccountEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Globally-unique identifier for the account.
        /// </summary>
        public string GUID { get; private set; } = null;

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

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories
         
        internal AccountEventArgs(Account a)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));

            GUID = a.GUID;
            Name = a.Name;
            Notes = a.Notes;
            CreatedUtc = a.CreatedUtc;
        }
         
        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
