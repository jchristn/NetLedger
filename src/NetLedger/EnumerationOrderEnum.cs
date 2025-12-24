namespace NetLedger
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Enumeration order.
    /// </summary>
    public enum EnumerationOrderEnum
    {
        /// <summary>
        /// CreatedAscending.
        /// </summary>
        [EnumMember(Value = "CreatedAscending")]
        CreatedAscending,
        /// <summary>
        /// CreatedDescending.
        /// </summary>
        [EnumMember(Value = "CreatedDescending")]
        CreatedDescending,
        /// <summary>
        /// AmountAscending.
        /// </summary>
        [EnumMember(Value = "AmountAscending")]
        AmountAscending,
        /// <summary>
        /// AmountDescending.
        /// </summary>
        [EnumMember(Value = "AmountDescending")]
        AmountDescending
    }
}
