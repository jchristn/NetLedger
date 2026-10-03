namespace NetLedger.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using NetLedger.Database;

    /// <summary>
    /// The NetLedger meter and activity source plus the instruments shared by every NetLedger assembly
    /// (database, outbound integration, errors, build info, configuration, and uptime).
    /// Emission uses only System.Diagnostics, takes no exporter dependency, and costs a few nanoseconds when no
    /// collector is subscribed. Subscribe a host to <see cref="TelemetryNames.MeterName"/> and
    /// <see cref="TelemetryNames.ActivitySourceName"/>, for example with Radiant:
    /// settings.Sources.AddMeter("NetLedger"); settings.Sources.AddActivitySource("NetLedger").
    /// Thread safety: all members are thread-safe. Every recording method is best-effort and never throws.
    /// </summary>
    public static class NetLedgerTelemetry
    {
        #region Public-Members

        /// <summary>
        /// The NetLedger meter. Never null.
        /// </summary>
        public static Meter Meter
        {
            get { return _Meter; }
        }

        /// <summary>
        /// The NetLedger activity source. Never null.
        /// </summary>
        public static ActivitySource ActivitySource
        {
            get { return _ActivitySource; }
        }

        /// <summary>
        /// The NetLedger library version stamped on the meter and activity source.
        /// </summary>
        public static string Version
        {
            get { return _Version; }
        }

        /// <summary>
        /// Duration histogram for database round trips, in seconds.
        /// </summary>
        public static Histogram<double> DbOperationDuration
        {
            get { return _DbOperationDuration; }
        }

        /// <summary>
        /// Counter for database round trips by outcome.
        /// </summary>
        public static Counter<long> DbOperations
        {
            get { return _DbOperations; }
        }

        /// <summary>
        /// In-flight database round trips.
        /// </summary>
        public static UpDownCounter<long> DbOperationsActive
        {
            get { return _DbOperationsActive; }
        }

        /// <summary>
        /// Duration histogram for outbound integration calls, in seconds.
        /// </summary>
        public static Histogram<double> IntegrationRequestDuration
        {
            get { return _IntegrationRequestDuration; }
        }

        /// <summary>
        /// Counter for outbound integration calls by outcome.
        /// </summary>
        public static Counter<long> IntegrationRequests
        {
            get { return _IntegrationRequests; }
        }

        /// <summary>
        /// Counter for bytes moved to or from archive object storage.
        /// </summary>
        public static Counter<long> StorageBytes
        {
            get { return _StorageBytes; }
        }

        /// <summary>
        /// Counter for errors observed by NetLedger components.
        /// </summary>
        public static Counter<long> Errors
        {
            get { return _Errors; }
        }

        /// <summary>
        /// Counter for authentication attempts, shared by the NetLedger server and archive server.
        /// </summary>
        public static Counter<long> AuthAttempts
        {
            get { return _AuthAttempts; }
        }

        /// <summary>
        /// Duration histogram for authentication, in seconds.
        /// </summary>
        public static Histogram<double> AuthDuration
        {
            get { return _AuthDuration; }
        }

        /// <summary>
        /// Counter for authorization decisions, shared by the NetLedger server and archive server.
        /// </summary>
        public static Counter<long> AuthzDecisions
        {
            get { return _AuthzDecisions; }
        }

        #endregion

        #region Private-Members

        private const int MaxDescriptionLength = 512;
        private static readonly string _Version = ResolveVersion();
        private static readonly Meter _Meter = new Meter(TelemetryNames.MeterName, _Version);
        private static readonly ActivitySource _ActivitySource = new ActivitySource(TelemetryNames.ActivitySourceName, _Version);

        private static readonly Histogram<double> _DbOperationDuration = _Meter.CreateHistogram<double>(
            TelemetryNames.DbOperationDuration, "s", "Duration of NetLedger database round trips.");
        private static readonly Counter<long> _DbOperations = _Meter.CreateCounter<long>(
            TelemetryNames.DbOperations, "{operation}", "NetLedger database round trips by outcome.");
        private static readonly UpDownCounter<long> _DbOperationsActive = _Meter.CreateUpDownCounter<long>(
            TelemetryNames.DbOperationsActive, "{operation}", "NetLedger database round trips in flight.");
        private static readonly Histogram<double> _IntegrationRequestDuration = _Meter.CreateHistogram<double>(
            TelemetryNames.IntegrationRequestDuration, "s", "Duration of outbound NetLedger integration calls.");
        private static readonly Counter<long> _IntegrationRequests = _Meter.CreateCounter<long>(
            TelemetryNames.IntegrationRequests, "{request}", "Outbound NetLedger integration calls by outcome.");
        private static readonly Counter<long> _StorageBytes = _Meter.CreateCounter<long>(
            TelemetryNames.StorageBytes, "By", "Bytes moved to or from archive object storage.");
        private static readonly Counter<long> _Errors = _Meter.CreateCounter<long>(
            TelemetryNames.Errors, "{error}", "Errors observed by NetLedger components.");
        private static readonly Counter<long> _AuthAttempts = _Meter.CreateCounter<long>(
            TelemetryNames.AuthAttempts, "{attempt}", "Authentication attempts by component, method, and result.");
        private static readonly Histogram<double> _AuthDuration = _Meter.CreateHistogram<double>(
            TelemetryNames.AuthDuration, "s", "Authentication duration.");
        private static readonly Counter<long> _AuthzDecisions = _Meter.CreateCounter<long>(
            TelemetryNames.AuthzDecisions, "{decision}", "Authorization decisions by component, resource, operation, and decision.");

        private static readonly ConcurrentDictionary<string, string> _ServiceVersions = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, long> _ServiceStartTimestamps = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, Func<IReadOnlyDictionary<string, double>>> _ConfigProviders =
            new ConcurrentDictionary<string, Func<IReadOnlyDictionary<string, double>>>(StringComparer.Ordinal);

        private static readonly HashSet<string> _KnownDbOperations = new HashSet<string>(StringComparer.Ordinal)
        {
            "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "ALTER", "DROP", "PRAGMA",
            "BEGIN", "COMMIT", "ROLLBACK", "WITH", "MERGE", "TRUNCATE", "EXEC", "SET", "IF", "DECLARE"
        };

        #endregion

        #region Constructors-and-Factories

        static NetLedgerTelemetry()
        {
            _Meter.CreateObservableGauge<long>(
                TelemetryNames.BuildInfo,
                ObserveBuildInfo,
                "1",
                "Build information for NetLedger services; always 1.");
            _Meter.CreateObservableGauge<double>(
                TelemetryNames.Uptime,
                ObserveUptime,
                "s",
                "Seconds since the NetLedger service started.");
            _Meter.CreateObservableGauge<double>(
                TelemetryNames.ConfigValue,
                ObserveConfig,
                "1",
                "Safe numeric NetLedger configuration values. Booleans are 1 or 0.");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Register a running service so it reports build info, uptime, and safe configuration gauges.
        /// Registering the same component again replaces its previous registration.
        /// </summary>
        /// <param name="component">Bounded component name, for example "server" or "archive_server".</param>
        /// <param name="version">Service version.</param>
        /// <param name="configProvider">Callback returning safe numeric configuration values keyed by a bounded setting name. Must never return secrets. May be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when component is null or empty.</exception>
        public static void RegisterService(string component, string version, Func<IReadOnlyDictionary<string, double>>? configProvider)
        {
            if (String.IsNullOrEmpty(component)) throw new ArgumentNullException(nameof(component));
            _ServiceVersions[component] = String.IsNullOrEmpty(version) ? "unknown" : version;
            _ServiceStartTimestamps[component] = Stopwatch.GetTimestamp();
            if (configProvider != null) _ConfigProviders[component] = configProvider;
            else _ConfigProviders.TryRemove(component, out _);
        }

        /// <summary>
        /// Remove a service registration so its gauges stop reporting.
        /// </summary>
        /// <param name="component">Component name.</param>
        public static void UnregisterService(string component)
        {
            if (String.IsNullOrEmpty(component)) return;
            _ServiceVersions.TryRemove(component, out _);
            _ServiceStartTimestamps.TryRemove(component, out _);
            _ConfigProviders.TryRemove(component, out _);
        }

        /// <summary>
        /// Start a client span and timing scope for one database round trip.
        /// The query text is never recorded because it can contain user data.
        /// </summary>
        /// <param name="component">Bounded component, for example "active_db" or "archive_catalog".</param>
        /// <param name="dbSystem">Database system name from <see cref="GetDbSystemName(DatabaseTypeEnum)"/>.</param>
        /// <param name="operation">Database operation verb from <see cref="GetDbOperationName(string)"/>, or "BATCH".</param>
        /// <returns>Telemetry scope. Never null.</returns>
        public static TelemetryScope StartDbOperation(string component, string dbSystem, string operation)
        {
            TagList labels = new TagList
            {
                { TelemetryNames.LabelComponent, component },
                { TelemetryNames.LabelDbSystem, dbSystem },
                { TelemetryNames.LabelDbOperation, operation }
            };

            TagList activeLabels = new TagList
            {
                { TelemetryNames.LabelComponent, component },
                { TelemetryNames.LabelDbSystem, dbSystem }
            };

            return TelemetryScope.Start(
                dbSystem + " " + operation,
                ActivityKind.Client,
                _DbOperationDuration,
                _DbOperations,
                _DbOperationsActive,
                labels,
                activeLabels,
                default);
        }

        /// <summary>
        /// Start a client span and timing scope for one outbound integration call. Span name is "service operation".
        /// </summary>
        /// <param name="service">Bounded downstream service name from <see cref="TelemetryNames"/>.</param>
        /// <param name="operation">Bounded operation name.</param>
        /// <returns>Telemetry scope. Never null.</returns>
        public static TelemetryScope StartIntegration(string service, string operation)
        {
            TagList labels = new TagList
            {
                { TelemetryNames.LabelService, service },
                { TelemetryNames.LabelOperation, operation }
            };

            return TelemetryScope.Start(
                service + " " + operation,
                ActivityKind.Client,
                _IntegrationRequestDuration,
                _IntegrationRequests,
                labels);
        }

        /// <summary>
        /// Count an error observed by a component.
        /// </summary>
        /// <param name="component">Bounded component name.</param>
        /// <param name="e">Exception.</param>
        public static void RecordError(string component, Exception? e)
        {
            RecordError(component, GetErrorType(e));
        }

        /// <summary>
        /// Count an error observed by a component.
        /// </summary>
        /// <param name="component">Bounded component name.</param>
        /// <param name="errorType">Bounded error type.</param>
        public static void RecordError(string component, string errorType)
        {
            try
            {
                if (!_Errors.Enabled) return;
                _Errors.Add(1, new TagList
                {
                    { TelemetryNames.LabelComponent, component ?? "unknown" },
                    { TelemetryNames.LabelErrorType, String.IsNullOrEmpty(errorType) ? "unknown" : errorType }
                });
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Count bytes moved to or from archive object storage.
        /// </summary>
        /// <param name="service">Storage service, "s3" or "filesystem".</param>
        /// <param name="direction">"read" or "write".</param>
        /// <param name="bytes">Byte count. Values below one are ignored.</param>
        public static void RecordStorageBytes(string service, string direction, long bytes)
        {
            try
            {
                if (bytes < 1 || !_StorageBytes.Enabled) return;
                _StorageBytes.Add(bytes, new TagList
                {
                    { TelemetryNames.LabelService, service },
                    { TelemetryNames.LabelDirection, direction }
                });
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Mark a span as failed with an exception event (type and truncated message). Safe with a null span.
        /// </summary>
        /// <param name="activity">Span, or null.</param>
        /// <param name="e">Exception, or null.</param>
        public static void RecordException(Activity? activity, Exception? e)
        {
            if (activity == null) return;
            try
            {
                string errorType = GetErrorType(e);
                string? message = Truncate(e?.Message);
                activity.SetTag(TelemetryNames.LabelErrorType, errorType);
                activity.SetStatus(ActivityStatusCode.Error, message);
                ActivityTagsCollection tags = new ActivityTagsCollection
                {
                    { TelemetryNames.AttributeExceptionType, e?.GetType().FullName ?? errorType }
                };
                if (message != null) tags.Add(TelemetryNames.AttributeExceptionMessage, message);
                activity.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, tags));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Bounded error type for an exception: the exception's short type name, or "unknown".
        /// </summary>
        /// <param name="e">Exception, or null.</param>
        /// <returns>Error type.</returns>
        public static string GetErrorType(Exception? e)
        {
            if (e == null) return "unknown";
            if (e is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
            {
                return aggregate.InnerExceptions[0].GetType().Name;
            }

            return e.GetType().Name;
        }

        /// <summary>
        /// OpenTelemetry db.system.name value for a NetLedger database type.
        /// </summary>
        /// <param name="type">Database type.</param>
        /// <returns>Database system name.</returns>
        public static string GetDbSystemName(DatabaseTypeEnum type)
        {
            switch (type)
            {
                case DatabaseTypeEnum.Sqlite: return "sqlite";
                case DatabaseTypeEnum.Postgresql: return "postgresql";
                case DatabaseTypeEnum.Mysql: return "mysql";
                case DatabaseTypeEnum.SqlServer: return "microsoft.sql_server";
                default: return "other_sql";
            }
        }

        /// <summary>
        /// Bounded database operation verb parsed from the first keyword of a SQL statement, for example SELECT.
        /// Unknown verbs return OTHER so the label set stays bounded.
        /// </summary>
        /// <param name="query">SQL text. Only the first keyword is inspected and nothing is retained.</param>
        /// <returns>Operation verb.</returns>
        public static string GetDbOperationName(string? query)
        {
            if (String.IsNullOrEmpty(query)) return "OTHER";
            int start = 0;
            while (start < query.Length && (Char.IsWhiteSpace(query[start]) || query[start] == '(' || query[start] == ';')) start++;
            int end = start;
            while (end < query.Length && Char.IsLetter(query[end])) end++;
            if (end <= start) return "OTHER";
            string verb = query.Substring(start, Math.Min(end - start, 16)).ToUpperInvariant();
            return _KnownDbOperations.Contains(verb) ? verb : "OTHER";
        }

        /// <summary>
        /// Bound a caller-supplied value to a fixed allow-list before it is used as a metric label.
        /// Returns the lower-cased value when allowed, otherwise "other". Keeps label cardinality bounded.
        /// </summary>
        /// <param name="value">Candidate label value.</param>
        /// <param name="allowed">Allowed values. Comparison follows the set's comparer.</param>
        /// <returns>Bounded label value.</returns>
        public static string BoundLabel(string? value, ISet<string> allowed)
        {
            if (String.IsNullOrEmpty(value) || allowed == null || !allowed.Contains(value)) return "other";
            return value.ToLowerInvariant();
        }

        /// <summary>
        /// Seconds elapsed since a <see cref="Stopwatch.GetTimestamp"/> value.
        /// </summary>
        /// <param name="startTimestamp">Start timestamp.</param>
        /// <returns>Elapsed seconds.</returns>
        public static double GetElapsedSeconds(long startTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
        }

        /// <summary>
        /// Inject the W3C trace context of a span into outbound headers through the supplied setter.
        /// Uses the current <see cref="DistributedContextPropagator"/>. Safe with a null span.
        /// </summary>
        /// <param name="activity">Span whose context is propagated, or null to use the ambient span.</param>
        /// <param name="carrier">Header carrier passed to the setter.</param>
        /// <param name="setter">Callback that writes one header to the carrier.</param>
        public static void InjectTraceContext(Activity? activity, object carrier, Action<object, string, string> setter)
        {
            if (carrier == null || setter == null) return;
            try
            {
                Activity? source = activity ?? Activity.Current;
                if (source == null) return;
                DistributedContextPropagator.Current.Inject(source, carrier, (object? c, string name, string value) =>
                {
                    if (c != null) setter(c, name, value);
                });
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Truncate free text before it is attached to a span, limiting accidental payload capture.
        /// </summary>
        /// <param name="value">Text, or null.</param>
        /// <returns>Truncated text, or null.</returns>
        public static string? Truncate(string? value)
        {
            if (value == null) return null;
            if (value.Length <= MaxDescriptionLength) return value;
            return value.Substring(0, MaxDescriptionLength);
        }

        #endregion

        #region Private-Methods

        private static string ResolveVersion()
        {
            try
            {
                Assembly assembly = typeof(NetLedgerTelemetry).Assembly;
                AssemblyInformationalVersionAttribute? info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string? version = info?.InformationalVersion;
                if (!String.IsNullOrEmpty(version))
                {
                    int plus = version.IndexOf('+');
                    return plus > 0 ? version.Substring(0, plus) : version;
                }

                return assembly.GetName().Version?.ToString() ?? "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private static IEnumerable<Measurement<long>> ObserveBuildInfo()
        {
            List<Measurement<long>> measurements = new List<Measurement<long>>();
            try
            {
                foreach (KeyValuePair<string, string> service in _ServiceVersions)
                {
                    measurements.Add(new Measurement<long>(1, new TagList
                    {
                        { TelemetryNames.LabelComponent, service.Key },
                        { TelemetryNames.LabelVersion, service.Value },
                        { TelemetryNames.LabelRuntime, RuntimeInformation.FrameworkDescription }
                    }));
                }
            }
            catch (Exception)
            {
            }

            return measurements;
        }

        private static IEnumerable<Measurement<double>> ObserveUptime()
        {
            List<Measurement<double>> measurements = new List<Measurement<double>>();
            try
            {
                foreach (KeyValuePair<string, long> service in _ServiceStartTimestamps)
                {
                    measurements.Add(new Measurement<double>(
                        GetElapsedSeconds(service.Value),
                        new TagList { { TelemetryNames.LabelComponent, service.Key } }));
                }
            }
            catch (Exception)
            {
            }

            return measurements;
        }

        private static IEnumerable<Measurement<double>> ObserveConfig()
        {
            List<Measurement<double>> measurements = new List<Measurement<double>>();
            foreach (KeyValuePair<string, Func<IReadOnlyDictionary<string, double>>> provider in _ConfigProviders)
            {
                try
                {
                    IReadOnlyDictionary<string, double> values = provider.Value();
                    if (values == null) continue;
                    foreach (KeyValuePair<string, double> value in values)
                    {
                        measurements.Add(new Measurement<double>(value.Value, new TagList
                        {
                            { TelemetryNames.LabelComponent, provider.Key },
                            { TelemetryNames.LabelSetting, value.Key }
                        }));
                    }
                }
                catch (Exception)
                {
                }
            }

            return measurements;
        }

        #endregion
    }
}
