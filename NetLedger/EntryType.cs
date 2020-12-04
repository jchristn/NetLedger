using System;
using System.Collections.Generic;
using System.Text;
using Watson.ORM.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace NetLedger
{
    /// <summary>
    /// The type of entry in the ledger.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EntryType
    {
        /// <summary>
        /// Debit
        /// </summary>
        [EnumMember(Value = "Debit")]
        Debit,
        /// <summary>
        /// Credit
        /// </summary>
        [EnumMember(Value = "Credit")]
        Credit,
        /// <summary>
        /// Balance
        /// </summary>
        [EnumMember(Value = "Balance")]
        Balance
    }
}
