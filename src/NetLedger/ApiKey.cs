namespace NetLedger
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public class ApiKey
    {
        #region Public-Members

        /// <summary>
        /// Internal provider row ID.
        /// </summary>
        [JsonIgnore]
        public int RowId { get; set; } = 0;

        /// <summary>
        /// Unique identifier for the API key.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Credential);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// User identifier.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Legacy API key identifier alias.
        /// </summary>
        [JsonIgnore]
        public string GUID
        {
            get { return Id; }
            set { Id = value; }
        }

        /// <summary>
        /// Display name for the API key.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// The API key value. This is the Bearer token.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Secret key verifier material.
        /// </summary>
        [JsonIgnore]
        public string SecretKeySha256 { get; set; } = String.Empty;

        /// <summary>
        /// Last four characters of the secret key.
        /// </summary>
        public string SecretKeyLast4 { get; set; } = String.Empty;

        /// <summary>
        /// Raw secret key shown only at creation time.
        /// </summary>
        [JsonIgnore]
        public string? RawSecretKey { get; set; }

        /// <summary>
        /// Whether the API key is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether this is an admin API key with full permissions.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Creation timestamp (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiKey()
        {
        }

        /// <summary>
        /// Instantiate with name.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="isAdmin">Whether this is an admin key.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        public ApiKey(string name, bool isAdmin = false)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            Name = name;
            IsAdmin = isAdmin;
            Key = GenerateApiKey();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate a new random API key.
        /// </summary>
        /// <returns>API key string.</returns>
        public static string GenerateApiKey()
        {
            return NetLedgerId.Generate("acc_");
        }

        /// <summary>
        /// Create a redacted copy of the API key (for display purposes).
        /// </summary>
        /// <returns>Redacted copy.</returns>
        public ApiKey Redact()
        {
            return new ApiKey
            {
                RowId = RowId,
                Id = Id,
                TenantId = TenantId,
                UserId = UserId,
                GUID = GUID,
                Name = Name,
                Key = Key.Length >= 8 ? Key.Substring(0, 4) + "****" + Key.Substring(Key.Length - 4) : "****",
                SecretKeySha256 = String.Empty,
                SecretKeyLast4 = SecretKeyLast4,
                RawSecretKey = null,
                Active = Active,
                IsAdmin = IsAdmin,
                CreatedUtc = CreatedUtc
            };
        }

        #endregion
    }
}
