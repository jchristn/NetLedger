namespace NetLedger.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;

    /// <summary>
    /// Measures one unit of work: opens a span, times the work, and on disposal records a duration histogram and an
    /// outcome counter tagged with bounded labels. Instrumentation is best-effort: every member swallows its own
    /// failures so telemetry can never change application behavior. When no listener is attached to the span or the
    /// instruments, <see cref="Start(string, ActivityKind, Histogram{double}, Counter{long}, TagList)"/> returns a
    /// shared inert instance and the cost is a few flag checks.
    /// Usage: wrap the work in a using block, call <see cref="Fail(Exception)"/> from a catch block before rethrowing,
    /// and optionally <see cref="SetOutcome(string)"/> for non-failure outcomes such as no_rows. A scope that is
    /// disposed without a failure records outcome success.
    /// Thread safety: a scope is owned by one logical operation and is not safe for concurrent mutation.
    /// </summary>
    public sealed class TelemetryScope : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// The span opened for this scope, or null when no listener samples it. Use the null-conditional operator.
        /// </summary>
        public Activity? Activity
        {
            get { return _Activity; }
        }

        /// <summary>
        /// The outcome that will be recorded on disposal. Defaults to success.
        /// </summary>
        public string Outcome
        {
            get { return _Outcome; }
        }

        /// <summary>
        /// Whether this scope records anything. False for the shared inert instance.
        /// </summary>
        public bool IsRecording
        {
            get { return _Enabled; }
        }

        #endregion

        #region Private-Members

        private static readonly TelemetryScope _Inert = new TelemetryScope();
        private readonly bool _Enabled = false;
        private readonly Activity? _Activity = null;
        private readonly Histogram<double>? _Duration = null;
        private readonly Counter<long>? _Counter = null;
        private readonly UpDownCounter<long>? _Active = null;
        private readonly TagList _ActiveLabels;
        private readonly long _StartTimestamp = 0;
        private TagList _Labels;
        private string _Outcome = TelemetryNames.OutcomeSuccess;
        private string? _ErrorType = null;
        private bool _Failed = false;
        private bool _ActiveIncremented = false;
        private int _Disposed = 0;

        #endregion

        #region Constructors-and-Factories

        private TelemetryScope()
        {
        }

        private TelemetryScope(
            Activity? activity,
            Histogram<double>? duration,
            Counter<long>? counter,
            UpDownCounter<long>? active,
            TagList labels,
            TagList activeLabels)
        {
            _Enabled = true;
            _Activity = activity;
            _Duration = duration;
            _Counter = counter;
            _Active = active;
            _Labels = labels;
            _ActiveLabels = activeLabels;
            _StartTimestamp = Stopwatch.GetTimestamp();

            if (_Active != null && _Active.Enabled)
            {
                _Active.Add(1, _ActiveLabels);
                _ActiveIncremented = true;
            }
        }

        /// <summary>
        /// Start a measured scope.
        /// </summary>
        /// <param name="spanName">Span name. Must be low-cardinality (no identifiers).</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="duration">Duration histogram in seconds, or null.</param>
        /// <param name="counter">Outcome counter, or null.</param>
        /// <param name="labels">Bounded metric labels, also stamped on the span.</param>
        /// <returns>A recording scope, or the shared inert scope when nothing is listening. Never null.</returns>
        public static TelemetryScope Start(
            string spanName,
            ActivityKind kind,
            Histogram<double>? duration,
            Counter<long>? counter,
            TagList labels)
        {
            return Start(spanName, kind, duration, counter, null, labels, default, default);
        }

        /// <summary>
        /// Start a measured scope with an in-flight gauge and an explicit parent context.
        /// </summary>
        /// <param name="spanName">Span name. Must be low-cardinality (no identifiers).</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="duration">Duration histogram in seconds, or null.</param>
        /// <param name="counter">Outcome counter, or null.</param>
        /// <param name="active">In-flight up/down counter, or null. Incremented on start and decremented on disposal.</param>
        /// <param name="labels">Bounded metric labels, also stamped on the span.</param>
        /// <param name="activeLabels">Bounded labels for the in-flight counter.</param>
        /// <param name="parent">Explicit parent span context for background hand-offs; default uses the ambient span.</param>
        /// <returns>A recording scope, or the shared inert scope when nothing is listening. Never null.</returns>
        public static TelemetryScope Start(
            string spanName,
            ActivityKind kind,
            Histogram<double>? duration,
            Counter<long>? counter,
            UpDownCounter<long>? active,
            TagList labels,
            TagList activeLabels,
            ActivityContext parent)
        {
            try
            {
                bool metricsEnabled =
                    (duration != null && duration.Enabled) ||
                    (counter != null && counter.Enabled) ||
                    (active != null && active.Enabled);

                Activity? activity = null;
                if (NetLedgerTelemetry.ActivitySource.HasListeners())
                {
                    activity = parent == default
                        ? NetLedgerTelemetry.ActivitySource.StartActivity(spanName, kind)
                        : NetLedgerTelemetry.ActivitySource.StartActivity(spanName, kind, parent);
                }

                if (!metricsEnabled && activity == null)
                {
                    return _Inert;
                }

                if (activity != null)
                {
                    foreach (KeyValuePair<string, object?> label in labels)
                    {
                        activity.SetTag(label.Key, label.Value);
                    }
                }

                return new TelemetryScope(activity, duration, counter, active, labels, activeLabels);
            }
            catch (Exception)
            {
                return _Inert;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Set a span attribute. Use for high-cardinality detail such as identifiers. Never added to metrics.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value. Null values are ignored.</param>
        public void SetTag(string key, object? value)
        {
            if (!_Enabled || _Activity == null || String.IsNullOrEmpty(key) || value == null) return;
            try
            {
                _Activity.SetTag(key, value);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Add a bounded metric label that is also stamped on the span. The value set must be small and fixed.
        /// </summary>
        /// <param name="key">Label key.</param>
        /// <param name="value">Label value from a bounded set.</param>
        public void AddLabel(string key, string value)
        {
            if (!_Enabled || String.IsNullOrEmpty(key)) return;
            try
            {
                _Labels.Add(key, value);
                _Activity?.SetTag(key, value);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Set a non-failure outcome such as no_rows or skipped. Ignored after a failure has been recorded.
        /// </summary>
        /// <param name="outcome">Outcome value from <see cref="TelemetryNames"/>.</param>
        public void SetOutcome(string outcome)
        {
            if (!_Enabled || _Failed || String.IsNullOrEmpty(outcome)) return;
            _Outcome = outcome;
        }

        /// <summary>
        /// Record a failure caused by an exception. The span status becomes Error with an exception event, and the
        /// metrics record outcome failure (or canceled for cancellation) with the exception type as error.type.
        /// </summary>
        /// <param name="e">Exception. Null is treated as an unknown failure.</param>
        public void Fail(Exception? e)
        {
            if (!_Enabled) return;
            try
            {
                bool canceled = e is OperationCanceledException;
                _Failed = true;
                _Outcome = canceled ? TelemetryNames.OutcomeCanceled : TelemetryNames.OutcomeFailure;
                _ErrorType = NetLedgerTelemetry.GetErrorType(e);
                NetLedgerTelemetry.RecordException(_Activity, e);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a failure that did not throw, for example an HTTP 5xx from a downstream service.
        /// </summary>
        /// <param name="errorType">Bounded error type, for example "http_503".</param>
        /// <param name="description">Optional span status description. Must not contain secrets or payloads.</param>
        public void Fail(string errorType, string? description)
        {
            if (!_Enabled) return;
            try
            {
                _Failed = true;
                _Outcome = TelemetryNames.OutcomeFailure;
                _ErrorType = String.IsNullOrEmpty(errorType) ? "unknown" : errorType;
                _Activity?.SetTag(TelemetryNames.LabelErrorType, _ErrorType);
                _Activity?.SetStatus(ActivityStatusCode.Error, NetLedgerTelemetry.Truncate(description));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Elapsed seconds since the scope started. Zero for the inert scope.
        /// </summary>
        /// <returns>Elapsed seconds.</returns>
        public double GetElapsedSeconds()
        {
            if (!_Enabled) return 0;
            return NetLedgerTelemetry.GetElapsedSeconds(_StartTimestamp);
        }

        /// <summary>
        /// Record the duration and outcome, close the span, and release the in-flight count. Idempotent.
        /// </summary>
        public void Dispose()
        {
            if (!_Enabled) return;
            if (Interlocked.Exchange(ref _Disposed, 1) == 1) return;

            try
            {
                double seconds = NetLedgerTelemetry.GetElapsedSeconds(_StartTimestamp);
                TagList tags = _Labels;
                tags.Add(TelemetryNames.LabelOutcome, _Outcome);

                if (_Duration != null && _Duration.Enabled)
                {
                    _Duration.Record(seconds, tags);
                }

                if (_Counter != null && _Counter.Enabled)
                {
                    if (_ErrorType != null) tags.Add(TelemetryNames.LabelErrorType, _ErrorType);
                    _Counter.Add(1, tags);
                }

                if (_ActiveIncremented && _Active != null)
                {
                    _Active.Add(-1, _ActiveLabels);
                }

                if (_Activity != null)
                {
                    _Activity.SetTag(TelemetryNames.LabelOutcome, _Outcome);
                    if (!_Failed) _Activity.SetStatus(ActivityStatusCode.Ok);
                    _Activity.Dispose();
                }
            }
            catch (Exception)
            {
            }
        }

        #endregion
    }
}
