namespace NetLedger.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using NetLedger.Telemetry;

    /// <summary>
    /// NetLedger server instruments on the shared NetLedger meter: authentication, authorization, background request
    /// history writes, the archive export pipeline (job, per-stage, rows, bytes, last success), and the automatic
    /// archival worker (runs, accounts, retries, last run, last success, running). Every member is best-effort.
    /// </summary>
    internal static class ServerTelemetry
    {
        #region Internal-Members

        internal const string EntityEntries = "entries";
        internal const string EntityRequestHistory = "request_history";
        internal const string TriggerApi = "api";
        internal const string TriggerAutomatic = "automatic";

        /// <summary>
        /// Logger exporting to OTLP and Loki with trace correlation, or null when telemetry export is unavailable.
        /// </summary>
        internal static ILogger? Logger { get; set; } = null;

        #endregion

        #region Private-Members

        private static readonly HashSet<string> _AuthzResources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Account", "Archive", "ArchiveSettings", "Assignment", "Audit", "Balance", "Credential", "ApiKey",
            "Entry", "Permission", "RequestHistory", "Role", "Session", "Tenant", "User"
        };

        private static readonly HashSet<string> _AuthzOperations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Create", "Read", "Update", "Delete", "Execute", "Admin"
        };

        private static readonly Counter<long> _Logins = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.AuthLogins, "{login}", "Password logins by outcome.");
        private static readonly Histogram<double> _AuthzDuration = NetLedgerTelemetry.Meter.CreateHistogram<double>(
            TelemetryNames.AuthzDuration, "s", "Authorization decision duration.");

        private static readonly Counter<long> _RequestHistoryWrites = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.RequestHistoryWrites, "{write}", "Background request history writes by outcome.");
        private static readonly Histogram<double> _RequestHistoryWriteDuration = NetLedgerTelemetry.Meter.CreateHistogram<double>(
            TelemetryNames.RequestHistoryWriteDuration, "s", "Background request history write duration.");
        private static long _RequestHistoryPending = 0;

        private static readonly Counter<long> _ExportJobs = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveExportJobs, "{job}", "Archive export jobs by entity, trigger, and outcome.");
        private static readonly Histogram<double> _ExportDuration = NetLedgerTelemetry.Meter.CreateHistogram<double>(
            TelemetryNames.ArchiveExportDuration, "s", "End-to-end archive export job duration.");
        private static readonly Histogram<double> _ExportStageDuration = NetLedgerTelemetry.Meter.CreateHistogram<double>(
            TelemetryNames.ArchiveExportStageDuration, "s", "Archive export duration per stage.");
        private static readonly Counter<long> _ExportStageEvents = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveExportStageEvents, "{event}", "Archive export stage executions by outcome.");
        private static readonly Counter<long> _ExportRows = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveExportRows, "{row}", "Rows exported to the archive server.");
        private static readonly Counter<long> _ExportBytes = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveExportBytes, "By", "Compressed bytes uploaded to the archive server.");
        private static readonly Counter<long> _ExportCleanupRows = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveExportCleanupRows, "{row}", "Active rows deleted after a committed archive export.");
        private static readonly ConcurrentDictionary<string, double> _ExportLastSuccess = new ConcurrentDictionary<string, double>(StringComparer.Ordinal);

        private static readonly Counter<long> _AutomaticRuns = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.AutomaticArchiveRuns, "{run}", "Automatic archival worker runs by outcome.");
        private static readonly Histogram<double> _AutomaticRunDuration = NetLedgerTelemetry.Meter.CreateHistogram<double>(
            TelemetryNames.AutomaticArchiveRunDuration, "s", "Automatic archival worker run duration.");
        private static readonly Counter<long> _AutomaticAccounts = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.AutomaticArchiveAccounts, "{account}", "Accounts evaluated by the automatic archival worker by result.");
        private static readonly Counter<long> _AutomaticRetries = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.AutomaticArchiveRetries, "{retry}", "Automatic archive export retry attempts.");
        private static double _AutomaticLastRun = 0;
        private static double _AutomaticLastSuccess = 0;
        private static long _AutomaticRunning = 0;

        #endregion

        #region Constructors-and-Factories

        static ServerTelemetry()
        {
            NetLedgerTelemetry.Meter.CreateObservableUpDownCounter<long>(
                TelemetryNames.RequestHistoryPending,
                () => new Measurement<long>(
                    Interlocked.Read(ref _RequestHistoryPending),
                    new TagList { { TelemetryNames.LabelComponent, TelemetryNames.ComponentServer } }),
                "{write}",
                "Background request history writes queued or in flight.");
            NetLedgerTelemetry.Meter.CreateObservableGauge<double>(
                TelemetryNames.ArchiveExportLastSuccess,
                ObserveExportLastSuccess,
                "s",
                "Unix time of the last successful archive export.");
            NetLedgerTelemetry.Meter.CreateObservableGauge<double>(
                TelemetryNames.AutomaticArchiveLastRun,
                ObserveAutomaticLastRun,
                "s",
                "Unix time of the last completed automatic archival run.");
            NetLedgerTelemetry.Meter.CreateObservableGauge<double>(
                TelemetryNames.AutomaticArchiveLastSuccess,
                ObserveAutomaticLastSuccess,
                "s",
                "Unix time of the last automatic archival run without export failures.");
            NetLedgerTelemetry.Meter.CreateObservableUpDownCounter<long>(
                TelemetryNames.AutomaticArchiveRunning,
                () => Interlocked.Read(ref _AutomaticRunning),
                "{run}",
                "Automatic archival runs currently executing.");
        }

        #endregion

        #region Internal-Methods

        internal static TelemetryScope StartAuthentication(string method)
        {
            return TelemetryScope.Start(
                TelemetryNames.SpanAuthenticate,
                ActivityKind.Internal,
                NetLedgerTelemetry.AuthDuration,
                NetLedgerTelemetry.AuthAttempts,
                new TagList
                {
                    { TelemetryNames.LabelComponent, TelemetryNames.ComponentServer },
                    { TelemetryNames.LabelAuthMethod, method }
                });
        }

        internal static TelemetryScope StartLogin()
        {
            return TelemetryScope.Start(TelemetryNames.SpanLogin, ActivityKind.Internal, null, _Logins, default);
        }

        internal static TelemetryScope StartAuthorization(string resourceType, string operationType)
        {
            TelemetryScope scope = TelemetryScope.Start(
                TelemetryNames.SpanAuthorize,
                ActivityKind.Internal,
                _AuthzDuration,
                null,
                new TagList { { TelemetryNames.LabelComponent, TelemetryNames.ComponentServer } });
            scope.SetTag(TelemetryNames.LabelResource, NetLedgerTelemetry.BoundLabel(resourceType, _AuthzResources));
            scope.SetTag(TelemetryNames.LabelOperation, NetLedgerTelemetry.BoundLabel(operationType, _AuthzOperations));
            return scope;
        }

        internal static void RecordAuthorizationDecision(TelemetryScope scope, string resourceType, string operationType, bool permitted, string? reason)
        {
            try
            {
                string decision = permitted ? "permit" : "deny";
                scope.AddLabel(TelemetryNames.LabelDecision, decision);
                scope.SetTag("netledger.authz.reason", reason);
                if (!NetLedgerTelemetry.AuthzDecisions.Enabled) return;
                NetLedgerTelemetry.AuthzDecisions.Add(1, new TagList
                {
                    { TelemetryNames.LabelComponent, TelemetryNames.ComponentServer },
                    { TelemetryNames.LabelResource, NetLedgerTelemetry.BoundLabel(resourceType, _AuthzResources) },
                    { TelemetryNames.LabelOperation, NetLedgerTelemetry.BoundLabel(operationType, _AuthzOperations) },
                    { TelemetryNames.LabelDecision, decision }
                });
            }
            catch (Exception)
            {
            }
        }

        internal static void RequestHistoryEnqueued()
        {
            Interlocked.Increment(ref _RequestHistoryPending);
        }

        internal static TelemetryScope StartRequestHistoryWrite(ActivityContext parent)
        {
            return TelemetryScope.Start(
                TelemetryNames.SpanRequestHistoryWrite,
                ActivityKind.Internal,
                _RequestHistoryWriteDuration,
                _RequestHistoryWrites,
                null,
                new TagList { { TelemetryNames.LabelComponent, TelemetryNames.ComponentServer } },
                default,
                parent);
        }

        internal static void RequestHistoryCompleted()
        {
            Interlocked.Decrement(ref _RequestHistoryPending);
        }

        internal static TelemetryScope StartExportJob(string entity, string trigger)
        {
            return TelemetryScope.Start(
                TelemetryNames.SpanArchiveExportPrefix + entity,
                ActivityKind.Internal,
                _ExportDuration,
                _ExportJobs,
                new TagList
                {
                    { TelemetryNames.LabelEntity, entity },
                    { TelemetryNames.LabelTrigger, trigger }
                });
        }

        internal static TelemetryScope StartExportStage(string entity, string stage)
        {
            return TelemetryScope.Start(
                TelemetryNames.SpanStagePrefix + stage,
                ActivityKind.Internal,
                _ExportStageDuration,
                _ExportStageEvents,
                new TagList
                {
                    { TelemetryNames.LabelEntity, entity },
                    { TelemetryNames.LabelStage, stage }
                });
        }

        internal static void RecordExportBatch(string entity, long rows, long bytes)
        {
            try
            {
                TagList tags = new TagList { { TelemetryNames.LabelEntity, entity } };
                if (rows > 0 && _ExportRows.Enabled) _ExportRows.Add(rows, tags);
                if (bytes > 0 && _ExportBytes.Enabled) _ExportBytes.Add(bytes, tags);
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordExportCleanup(string entity, long rows)
        {
            try
            {
                if (rows > 0 && _ExportCleanupRows.Enabled) _ExportCleanupRows.Add(rows, new TagList { { TelemetryNames.LabelEntity, entity } });
            }
            catch (Exception)
            {
            }
        }

        internal static void MarkExportSuccess(string entity)
        {
            _ExportLastSuccess[entity] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        }

        internal static double GetExportLastSuccess(string entity)
        {
            return _ExportLastSuccess.TryGetValue(entity, out double value) ? value : 0;
        }

        internal static TelemetryScope StartAutomaticRun()
        {
            Interlocked.Increment(ref _AutomaticRunning);
            return TelemetryScope.Start(
                TelemetryNames.SpanAutomaticArchiveRun,
                ActivityKind.Internal,
                _AutomaticRunDuration,
                _AutomaticRuns,
                default);
        }

        internal static void RecordAutomaticRunSkipped()
        {
            try
            {
                if (_AutomaticRuns.Enabled) _AutomaticRuns.Add(1, new TagList { { TelemetryNames.LabelOutcome, TelemetryNames.OutcomeSkipped } });
            }
            catch (Exception)
            {
            }
        }

        internal static void CompleteAutomaticRun(bool success)
        {
            Interlocked.Decrement(ref _AutomaticRunning);
            double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            Interlocked.Exchange(ref _AutomaticLastRun, now);
            if (success) Interlocked.Exchange(ref _AutomaticLastSuccess, now);
        }

        internal static double GetAutomaticLastRun()
        {
            return Interlocked.CompareExchange(ref _AutomaticLastRun, 0, 0);
        }

        internal static double GetAutomaticLastSuccess()
        {
            return Interlocked.CompareExchange(ref _AutomaticLastSuccess, 0, 0);
        }

        internal static TelemetryScope StartAutomaticAccount(string tenantId, string accountId)
        {
            TelemetryScope scope = TelemetryScope.Start(TelemetryNames.SpanAutomaticArchiveAccount, ActivityKind.Internal, null, null, default);
            scope.SetTag(TelemetryNames.AttributeTenantId, tenantId);
            scope.SetTag(TelemetryNames.AttributeAccountId, accountId);
            return scope;
        }

        internal static void RecordAutomaticAccount(string result)
        {
            try
            {
                if (_AutomaticAccounts.Enabled) _AutomaticAccounts.Add(1, new TagList { { TelemetryNames.LabelResult, result } });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordAutomaticRetry()
        {
            try
            {
                if (_AutomaticRetries.Enabled) _AutomaticRetries.Add(1);
            }
            catch (Exception)
            {
            }
        }

        internal static void LogInformation(string message, params object?[] args)
        {
            try
            {
                Logger?.LogInformation(message, args);
            }
            catch (Exception)
            {
            }
        }

        internal static void LogWarning(Exception? e, string message, params object?[] args)
        {
            try
            {
                Logger?.LogWarning(e, message, args);
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region Private-Methods

        private static IEnumerable<Measurement<double>> ObserveExportLastSuccess()
        {
            List<Measurement<double>> measurements = new List<Measurement<double>>();
            foreach (KeyValuePair<string, double> item in _ExportLastSuccess)
            {
                measurements.Add(new Measurement<double>(item.Value, new TagList { { TelemetryNames.LabelEntity, item.Key } }));
            }

            return measurements;
        }

        private static IEnumerable<Measurement<double>> ObserveAutomaticLastRun()
        {
            double value = GetAutomaticLastRun();
            if (value <= 0) return Array.Empty<Measurement<double>>();
            return new Measurement<double>[] { new Measurement<double>(value) };
        }

        private static IEnumerable<Measurement<double>> ObserveAutomaticLastSuccess()
        {
            double value = GetAutomaticLastSuccess();
            if (value <= 0) return Array.Empty<Measurement<double>>();
            return new Measurement<double>[] { new Measurement<double>(value) };
        }

        #endregion
    }
}
