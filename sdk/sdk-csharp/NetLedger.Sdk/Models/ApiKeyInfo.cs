namespace NetLedger.Sdk
{
    using System;

    /// <summary>
    /// Represents an API key for authentication.
    /// </summary>
    public class ApiKeyInfo
    {
        #region Public-Members

        /// <summary>
        /// The unique identifier for the API key.
        /// </summary>
        public Guid GUID { get; set; }

        /// <summary>
        /// The display name for the API key.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The API key value (only returned when created).
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// Indicates whether the API key is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Indicates whether the API key has admin privileges.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// The UTC timestamp when the API key was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a new API key info.
        /// </summary>
        public ApiKeyInfo()
        {
        }

        /// <summary>
        /// Instantiate a new API key info with a name.
        /// </summary>
        /// <param name="name">The display name for the API key.</param>
        public ApiKeyInfo(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Instantiate a new API key info with name and admin flag.
        /// </summary>
        /// <param name="name">The display name for the API key.</param>
        /// <param name="isAdmin">Whether the API key has admin privileges.</param>
        public ApiKeyInfo(string name, bool isAdmin)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsAdmin = isAdmin;
        }

        #endregion
    }
}
