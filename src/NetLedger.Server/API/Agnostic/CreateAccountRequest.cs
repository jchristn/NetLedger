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
        /// Optional account labels.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Optional account tags.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }
    }
}
