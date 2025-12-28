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
        /// Database row ID.
        /// </summary>
        [JsonIgnore]
        public int Id { get; set; } = 0;

        /// <summary>
        /// Unique identifier for the API key.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Display name for the API key.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// The API key value. This is the Bearer token.
        /// </summary>
        public string Key { get; set; } = String.Empty;

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
        /// <returns>API key string as a GUID.</returns>
        public static string GenerateApiKey()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Create a redacted copy of the API key (for display purposes).
        /// </summary>
        /// <returns>Redacted copy.</returns>
        public ApiKey Redact()
        {
            return new ApiKey
            {
                Id = Id,
                GUID = GUID,
                Name = Name,
                Key = Key.Length >= 36 ? Key.Substring(0, 8) + "-****-****-****-" + Key.Substring(Key.Length - 12) : "****",
                Active = Active,
                IsAdmin = IsAdmin,
                CreatedUtc = CreatedUtc
            };
        }

        #endregion
    }
}
