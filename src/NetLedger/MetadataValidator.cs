namespace NetLedger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Metadata normalization and validation helper.
    /// </summary>
    public static class MetadataValidator
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of labels on one object.
        /// </summary>
        public const int MaxLabels = 64;

        /// <summary>
        /// Minimum label length.
        /// </summary>
        public const int MinLabelLength = 1;

        /// <summary>
        /// Maximum label length.
        /// </summary>
        public const int MaxLabelLength = 128;

        /// <summary>
        /// Maximum number of tags on one object.
        /// </summary>
        public const int MaxTags = 128;

        /// <summary>
        /// Minimum tag key length.
        /// </summary>
        public const int MinTagKeyLength = 1;

        /// <summary>
        /// Maximum tag key length.
        /// </summary>
        public const int MaxTagKeyLength = 128;

        /// <summary>
        /// Maximum tag value length.
        /// </summary>
        public const int MaxTagValueLength = 1024;

        /// <summary>
        /// Maximum length of an account units label.
        /// </summary>
        public const int MaxUnitsLength = 64;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Normalize and validate labels.
        /// </summary>
        /// <param name="labels">Labels.</param>
        /// <returns>Normalized labels.</returns>
        /// <exception cref="ArgumentException">Thrown when labels exceed supported limits.</exception>
        public static List<string> NormalizeLabels(IEnumerable<string>? labels)
        {
            if (labels == null) return new List<string>();

            List<string> normalized = labels
                .Where(label => label != null)
                .Select(label => label.Trim())
                .Where(label => label.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count > MaxLabels)
                throw new ArgumentException("Labels cannot contain more than " + MaxLabels + " values.", nameof(labels));

            foreach (string label in normalized)
            {
                if (label.Length < MinLabelLength || label.Length > MaxLabelLength)
                    throw new ArgumentException("Label length must be between " + MinLabelLength + " and " + MaxLabelLength + " characters.", nameof(labels));
            }

            return normalized;
        }

        /// <summary>
        /// Normalize and validate tags.
        /// </summary>
        /// <param name="tags">Tags.</param>
        /// <returns>Normalized tags.</returns>
        /// <exception cref="ArgumentException">Thrown when tags exceed supported limits.</exception>
        public static Dictionary<string, string> NormalizeTags(IDictionary<string, string>? tags)
        {
            Dictionary<string, string> normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (tags == null) return normalized;

            foreach (KeyValuePair<string, string> tag in tags)
            {
                string key = tag.Key?.Trim() ?? String.Empty;
                string value = tag.Value ?? String.Empty;

                if (key.Length < MinTagKeyLength || key.Length > MaxTagKeyLength)
                    throw new ArgumentException("Tag key length must be between " + MinTagKeyLength + " and " + MaxTagKeyLength + " characters.", nameof(tags));

                if (value.Length > MaxTagValueLength)
                    throw new ArgumentException("Tag value length must be " + MaxTagValueLength + " characters or less.", nameof(tags));

                normalized[key] = value;
            }

            if (normalized.Count > MaxTags)
                throw new ArgumentException("Tags cannot contain more than " + MaxTags + " values.", nameof(tags));

            return normalized;
        }

        /// <summary>
        /// Normalize and validate an account units label. Trims surrounding whitespace, treats empty input as no unit, and enforces the maximum length. Casing is preserved so labels such as "USD" or "tokens" are stored as entered.
        /// </summary>
        /// <param name="units">Units label, or null.</param>
        /// <returns>Normalized units label, or null when no unit is specified.</returns>
        /// <exception cref="ArgumentException">Thrown when the units label exceeds the maximum length.</exception>
        public static string? NormalizeUnits(string? units)
        {
            if (units == null) return null;

            string trimmed = units.Trim();
            if (trimmed.Length == 0) return null;

            if (trimmed.Length > MaxUnitsLength)
                throw new ArgumentException("Units length must be " + MaxUnitsLength + " characters or less.", nameof(units));

            return trimmed;
        }

        #endregion
    }
}

