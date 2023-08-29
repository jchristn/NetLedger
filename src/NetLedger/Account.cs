using System;
using System.Collections.Generic;
using System.Text;
using Watson.ORM.Core;

namespace NetLedger
{
    /// <summary>
    /// Account information.
    /// </summary>
    [Table("accounts")]
    public class Account
    {
        #region Public-Members

        /// <summary>
        /// Database row ID.
        /// </summary>
        [Column("id", true, DataTypes.Int, false)]
        public int Id { get; set; } = 0;

        /// <summary>
        /// Globally-unique identifier for the account.
        /// </summary>
        [Column("guid", false, DataTypes.Nvarchar, 64, false)]
        public string GUID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Name of the account.
        /// </summary>
        [Column("name", false, DataTypes.Nvarchar, 256, false)]
        public string Name { get; set; } = null;

        /// <summary>
        /// Notes for the account.
        /// </summary>
        [Column("notes", false, DataTypes.Nvarchar, 256, true)]
        public string Notes { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the account was created.
        /// </summary>
        [Column("createdutc", false, DataTypes.DateTime, false)]
        public DateTime CreatedUtc { get; set; } = DateTime.Now.ToUniversalTime();

        #endregion

        #region Private-Members

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
        public Account(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Name = name;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
