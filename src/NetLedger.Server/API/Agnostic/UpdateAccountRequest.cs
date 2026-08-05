namespace NetLedger.Server.API.Agnostic
{
    using System.Collections.Generic;

    /// <summary>
    /// Request body for updating an existing account. The update replaces the editable fields of the account;
    /// a field that is omitted or null is cleared (labels and tags become empty). The account identifier, owning
    /// tenant, and creation timestamp cannot be changed.
    /// </summary>
    internal class UpdateAccountRequest
    {
        /// <summary>
        /// Account name. Required.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Account notes, or null to clear.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Unit of denomination for this account (for example, "USD" or "tokens"), or null to clear.
        /// </summary>
        public string? Units { get; set; }

        /// <summary>
        /// Account labels. Null or omitted clears all labels.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Account tags. Null or omitted clears all tags.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }

        /// <summary>
        /// Whether the account is active. Null leaves the current value unchanged.
        /// </summary>
        public bool? Active { get; set; }
    }
}
