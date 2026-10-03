namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Linq;
    using NetLedger.Telemetry;

    /// <summary>
    /// In-memory collector for NetLedger telemetry used by the shared tests. Subscribes a BCL MeterListener and
    /// ActivityListener to the NetLedger meter and activity source, exactly as an exporter host would.
    /// </summary>
    internal sealed class TelemetryCapture : IDisposable
    {
        private readonly MeterListener _MeterListener;
        private readonly ActivityListener _ActivityListener;
        private readonly ConcurrentQueue<CapturedMeasurement> _Measurements = new ConcurrentQueue<CapturedMeasurement>();
        private readonly ConcurrentQueue<Activity> _Activities = new ConcurrentQueue<Activity>();
        private bool _Disposed = false;

        internal TelemetryCapture()
        {
            _MeterListener = new MeterListener();
            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (String.Equals(instrument.Meter.Name, TelemetryNames.MeterName, StringComparison.Ordinal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.Start();

            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = source => String.Equals(source.Name, TelemetryNames.ActivitySourceName, StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _Activities.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(_ActivityListener);
        }

        internal IReadOnlyList<Activity> Activities
        {
            get { return _Activities.ToList(); }
        }

        internal void CollectObservables()
        {
            _MeterListener.RecordObservableInstruments();
        }

        internal IReadOnlyList<CapturedMeasurement> Measurements(string instrument, params string[] keyValuePairs)
        {
            return _Measurements
                .Where(m => String.Equals(m.Instrument, instrument, StringComparison.Ordinal) && m.Matches(keyValuePairs))
                .ToList();
        }

        internal double Sum(string instrument, params string[] keyValuePairs)
        {
            return Measurements(instrument, keyValuePairs).Sum(m => m.Value);
        }

        internal int Count(string instrument, params string[] keyValuePairs)
        {
            return Measurements(instrument, keyValuePairs).Count;
        }

        internal List<Activity> Spans(string name)
        {
            return _Activities.Where(a => String.Equals(a.DisplayName, name, StringComparison.Ordinal)).ToList();
        }

        internal string Describe(string instrument)
        {
            List<string> lines = new List<string>();
            foreach (CapturedMeasurement m in _Measurements.Where(m => m.Instrument == instrument).Take(25))
            {
                lines.Add(m.Value.ToString(CultureInfo.InvariantCulture) + " {" + String.Join(", ", m.Tags.Select(t => t.Key + "=" + t.Value)) + "}");
            }

            return instrument + ": " + (lines.Count == 0 ? "(none)" : String.Join("; ", lines));
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _MeterListener.Dispose();
            _ActivityListener.Dispose();
        }

        private void Add<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags) where T : struct
        {
            Dictionary<string, string> captured = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                captured[tag.Key] = Convert.ToString(tag.Value, CultureInfo.InvariantCulture) ?? String.Empty;
            }

            _Measurements.Enqueue(new CapturedMeasurement(instrument.Name, Convert.ToDouble(value, CultureInfo.InvariantCulture), captured));
        }
    }
}
