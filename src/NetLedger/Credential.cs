namespace NetLedger
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tenant- and user-scoped credential.
    /// </summary>
    public class Credential
    {
        /// <summary>
        /// Credential identifier.
        /// </summary>
        public string Id { get; set; } = NetLedgerId.Generate(IdentifierPrefixes.Credential);

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Owning user identifier.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Credential name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Public access key.
        /// </summary>
        public string AccessKey { get; set; } = NetLedgerId.Generate("acc_");

        /// <summary>
        /// Secret key SHA-256 verifier material.
        /// </summary>
        [JsonIgnore]
        public string SecretKeySha256 { get; set; } = String.Empty;

        /// <summary>
        /// Last four characters of the raw secret key.
        /// </summary>
        public string SecretKeyLast4 { get; set; } = String.Empty;

        /// <summary>
        /// Authentication mode.
        /// </summary>
        public string AuthMode { get; set; } = "DirectHeader";

        /// <summary>
        /// Boolean indicating whether the credential is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Boolean indicating whether the credential is protected from accidental mutation.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// UTC timestamp when the credential was last used.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the credential expires.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the credential was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the credential was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Generate a raw secret key and store its verifier.
        /// </summary>
        /// <returns>Raw secret key to show once.</returns>
        public string GenerateSecret()
        {
            string secret = NetLedgerId.Generate("key_");
            SecretKeySha256 = HashSecret(secret);
            SecretKeyLast4 = secret.Substring(secret.Length - 4);
            return secret;
        }

        /// <summary>
        /// Hash a secret key for verifier storage.
        /// </summary>
        /// <param name="secret">Secret key.</param>
        /// <returns>Hex-encoded SHA-256 hash.</returns>
        public static string HashSecret(string secret)
        {
            if (String.IsNullOrEmpty(secret)) throw new ArgumentNullException(nameof(secret));

            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}

