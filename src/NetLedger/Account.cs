namespace NetLedger
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Account information.
    /// </summary>
    public class Account
    {
        #region Public-Members

        /// <summary>
        /// Internal provider row ID.
        /// </summary>
        [JsonIgnore]
        public int RowId { get; set; } = 0;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Account);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Legacy account identifier alias.
        /// </summary>
        [JsonIgnore]
        public string GUID
        {
            get { return Id; }
            set { Id = value; }
        }

        /// <summary>
        /// Name of the account.
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Notes for the account.
        /// </summary>
        public string? Notes { get; set; } = null;

        /// <summary>
        /// Account labels.
        /// </summary>
        public List<string> Labels
        {
            get { return _Labels; }
            set { _Labels = MetadataValidator.NormalizeLabels(value); }
        }

        /// <summary>
        /// Account tags.
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get { return _Tags; }
            set { _Tags = MetadataValidator.NormalizeTags(value); }
        }

        /// <summary>
        /// UTC timestamp when the account was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// UTC timestamp when the account was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// Boolean indicating whether the account is active.
        /// </summary>
        public bool Active { get; set; } = true;

        #endregion

        #region Private-Members

        private List<string> _Labels = new List<string>();
        private Dictionary<string, string> _Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

