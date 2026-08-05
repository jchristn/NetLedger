namespace NetLedger.Server.API.Agnostic
{
    using System.Collections.Generic;

    internal class CreateAccountRequest
    {
        /// <summary>
        /// Account name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Optional initial balance.
        /// </summary>
        public decimal? InitialBalance { get; set; }

        /// <summary>
        /// Optional unit of denomination for this account (for example, "USD" or "tokens"). Null indicates no unit.
        /// </summary>
        public string? Units { get; set; }

        /// <summary>
        /// Optional account notes.
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Optional account labels.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Optional account tags.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }
    }
}
