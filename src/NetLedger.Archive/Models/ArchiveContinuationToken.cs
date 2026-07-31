namespace NetLedger.Archive.Models
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Opaque continuation token state for Archive Server enumerations.
    /// </summary>
    public class ArchiveContinuationToken
    {
        /// <summary>
        /// Token schema version.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Token purpose, such as entries or request-history.
        /// </summary>
        public string Purpose { get; set; } = String.Empty;

        /// <summary>
        /// Stable fingerprint for the query filters this token belongs to.
        /// </summary>
        public string FilterHashSha256 { get; set; } = String.Empty;

        /// <summary>
        /// Manifest cursor reserved for future object-level seek pagination.
        /// </summary>
        public string? ManifestCursor { get; set; } = null;

        /// <summary>
        /// Object cursor reserved for future object-level seek pagination.
        /// </summary>
        public string? ObjectCursor { get; set; } = null;

        /// <summary>
        /// Matched-row cursor.
        /// </summary>
        public int RowCursor { get; set; } = 0;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string? AccountId { get; set; } = null;

        /// <summary>
        /// Entity type.
        /// </summary>
        public string? EntityType { get; set; } = null;

        /// <summary>
        /// Ordering value.
        /// </summary>
        public string? Ordering { get; set; } = null;

        /// <summary>
        /// Create an opaque continuation token.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="purpose">Token purpose.</param>
        /// <param name="rowCursor">Next matched-row offset.</param>
        /// <param name="filterHash">Optional precomputed filter hash.</param>
        /// <returns>Opaque token string.</returns>
        public static string Create(ArchiveQuery query, string purpose, int rowCursor, string? filterHash = null)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (String.IsNullOrWhiteSpace(purpose)) throw new ArgumentNullException(nameof(purpose));
            if (rowCursor < 0) throw new ArgumentOutOfRangeException(nameof(rowCursor));

            ArchiveContinuationToken token = new ArchiveContinuationToken
            {
                Purpose = purpose,
                FilterHashSha256 = filterHash ?? BuildFilterHash(query, purpose),
                RowCursor = rowCursor,
                TenantId = query.TenantId,
                AccountId = query.AccountId,
                EntityType = query.EntityType?.ToString(),
                Ordering = query.Ordering.ToString()
            };

            string json = JsonSerializer.Serialize(token);
            return Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        }

        /// <summary>
        /// Resolve the starting row cursor from an archive query.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="purpose">Token purpose.</param>
        /// <param name="filterHash">Optional precomputed filter hash.</param>
        /// <returns>Start row cursor.</returns>
        /// <exception cref="InvalidDataException">Thrown when the continuation token is invalid.</exception>
        public static int ResolveRowCursor(ArchiveQuery query, string purpose, string? filterHash = null)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (String.IsNullOrWhiteSpace(query.ContinuationToken)) return query.Skip;
            if (query.Skip > 0) throw new InvalidDataException("Skip and continuationToken cannot be used together.");

            ArchiveContinuationToken token = Decode(query.ContinuationToken!);
            string expectedHash = filterHash ?? BuildFilterHash(query, purpose);
            if (token.Version != 1 ||
                !String.Equals(token.Purpose, purpose, StringComparison.Ordinal) ||
                !String.Equals(token.FilterHashSha256, expectedHash, StringComparison.Ordinal) ||
                token.RowCursor < 0)
            {
                throw new InvalidDataException("Continuation token does not match the archive query.");
            }

            return token.RowCursor;
        }

        /// <summary>
        /// Build a stable hash over query filters.
        /// </summary>
        /// <param name="query">Archive query.</param>
        /// <param name="purpose">Token purpose.</param>
        /// <returns>SHA-256 hash.</returns>
        public static string BuildFilterHash(ArchiveQuery query, string purpose)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            SortedDictionary<string, string> values = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["purpose"] = purpose,
                ["tenantId"] = query.TenantId ?? String.Empty,
                ["accountId"] = query.AccountId ?? String.Empty,
                ["entityType"] = query.EntityType?.ToString() ?? String.Empty,
                ["storagePoolId"] = query.StoragePoolId ?? String.Empty,
                ["migrationId"] = query.MigrationId ?? String.Empty,
                ["manifestStatus"] = query.ManifestStatus?.ToString() ?? String.Empty,
                ["migrationStatus"] = query.MigrationStatus?.ToString() ?? String.Empty,
                ["fromUtc"] = query.FromUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? String.Empty,
                ["toUtc"] = query.ToUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? String.Empty,
                ["search"] = query.Search ?? String.Empty,
                ["allowPartial"] = query.AllowPartial ? "true" : "false",
                ["ordering"] = query.Ordering.ToString(),
                ["amountMinimum"] = query.AmountMinimum?.ToString(CultureInfo.InvariantCulture) ?? String.Empty,
                ["amountMaximum"] = query.AmountMaximum?.ToString(CultureInfo.InvariantCulture) ?? String.Empty,
                ["creditMinimum"] = query.CreditMinimum?.ToString(CultureInfo.InvariantCulture) ?? String.Empty,
                ["creditMaximum"] = query.CreditMaximum?.ToString(CultureInfo.InvariantCulture) ?? String.Empty,
                ["debitMinimum"] = query.DebitMinimum?.ToString(CultureInfo.InvariantCulture) ?? String.Empty,
                ["debitMaximum"] = query.DebitMaximum?.ToString(CultureInfo.InvariantCulture) ?? String.Empty,
                ["labels"] = String.Join(",", query.Labels.OrderBy(label => label, StringComparer.OrdinalIgnoreCase)),
                ["tags"] = String.Join(",", query.Tags.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value))
            };

            string canonical = JsonSerializer.Serialize(values);
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
            }
        }

        private static ArchiveContinuationToken Decode(string token)
        {
            try
            {
                byte[] bytes = Base64UrlDecode(token);
                ArchiveContinuationToken? decoded = JsonSerializer.Deserialize<ArchiveContinuationToken>(Encoding.UTF8.GetString(bytes));
                return decoded ?? throw new InvalidDataException("Continuation token is empty.");
            }
            catch (Exception e) when (e is FormatException || e is JsonException || e is ArgumentException)
            {
                throw new InvalidDataException("Continuation token is invalid.", e);
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string value)
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            return Convert.FromBase64String(padded);
        }

        private static string ToHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
