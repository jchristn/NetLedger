namespace NetLedger
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Serializes and deserializes metadata fields stored in database JSON columns.
    /// </summary>
    public static class MetadataSerializer
    {
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        /// <summary>
        /// Serialize labels to JSON.
        /// </summary>
        /// <param name="labels">Labels.</param>
        /// <returns>JSON array.</returns>
        public static string SerializeLabels(IEnumerable<string>? labels)
        {
            List<string> normalized = MetadataValidator.NormalizeLabels(labels);
            return JsonSerializer.Serialize(normalized, _JsonOptions);
        }

        /// <summary>
        /// Serialize tags to JSON.
        /// </summary>
        /// <param name="tags">Tags.</param>
        /// <returns>JSON object.</returns>
        public static string SerializeTags(IDictionary<string, string>? tags)
        {
            Dictionary<string, string> normalized = MetadataValidator.NormalizeTags(tags);
            return JsonSerializer.Serialize(normalized, _JsonOptions);
        }

        /// <summary>
        /// Deserialize labels from JSON.
        /// </summary>
        /// <param name="json">JSON array.</param>
        /// <returns>Labels.</returns>
        public static List<string> DeserializeLabels(string? json)
        {
            if (String.IsNullOrEmpty(json)) return new List<string>();

            try
            {
                List<string>? labels = JsonSerializer.Deserialize<List<string>>(json, _JsonOptions);
                return MetadataValidator.NormalizeLabels(labels);
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Deserialize tags from JSON.
        /// </summary>
        /// <param name="json">JSON object.</param>
        /// <returns>Tags.</returns>
        public static Dictionary<string, string> DeserializeTags(string? json)
        {
            if (String.IsNullOrEmpty(json)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                Dictionary<string, string>? tags = JsonSerializer.Deserialize<Dictionary<string, string>>(json, _JsonOptions);
                return MetadataValidator.NormalizeTags(tags);
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
