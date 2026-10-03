namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One metric measurement captured by <see cref="TelemetryCapture"/>.
    /// </summary>
    internal sealed class CapturedMeasurement
    {
        internal CapturedMeasurement(string instrument, double value, Dictionary<string, string> tags)
        {
            Instrument = instrument ?? throw new ArgumentNullException(nameof(instrument));
            Value = value;
            Tags = tags ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        internal string Instrument { get; }

        internal double Value { get; }

        internal Dictionary<string, string> Tags { get; }

        internal bool Matches(params string[] keyValuePairs)
        {
            for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
            {
                if (!Tags.TryGetValue(keyValuePairs[i], out string? value)) return false;
                if (!String.Equals(value, keyValuePairs[i + 1], StringComparison.Ordinal)) return false;
            }

            return true;
        }
    }
}
