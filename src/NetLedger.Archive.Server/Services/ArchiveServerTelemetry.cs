namespace NetLedger.Archive.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using NetLedger.Telemetry;

    /// <summary>
    /// NetLedger Archive Server instruments on the shared NetLedger meter: migration lifecycle events, per-stage
    /// workflow latency (upload, commit, verify, query), uploaded bytes, archived query rows, manifest verification
    /// results, introspection authentication and cache efficiency, and archive authorization decisions.
    /// Every member is best-effort and never throws.
    /// </summary>
    internal static class ArchiveServerTelemetry
    {
        #region Internal-Members

        internal const string EventCreated = "created";
        internal const string EventReused = "reused";
        internal const string EventBatchCreated = "batch_created";
        internal const string EventBatchUploaded = "batch_uploaded";
        internal const string EventBatchRejected = "batch_rejected";
        internal const string EventSealed = "sealed";
        internal const string EventCommitted = "committed";
        internal const string EventAborted = "aborted";

        /// <summary>
        /// Logger exporting to OTLP and Loki with trace correlation, or null when telemetry export is unavailable.
        /// </summary>
        internal static ILogger? Logger { get; set; } = null;

        #endregion

        #region Private-Members

        private static readonly HashSet<string> _Resources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Archive", "ArchiveMigration", "ArchiveMigrationBatch", "ArchiveManifest", "ArchiveObject", "ArchiveRange",
            "ArchiveCheckpoint", "ArchiveStoragePool", "ArchiveRequestHistory", "ArchiveAudit", "RequestHistory",
            "Account", "Entry", "Balance"
        };

        private static readonly HashSet<string> _Operations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Read", "Create", "Update", "Delete", "Admin", "Execute"
        };

        private static readonly Counter<long> _MigrationEvents = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveMigrationEvents, "{event}", "Archive migration lifecycle events.");
        private static readonly Histogram<double> _WorkflowStageDuration = NetLedgerTelemetry.Meter.CreateHistogram<double>(
            TelemetryNames.ArchiveWorkflowStageDuration, "s", "Archive server workflow duration per stage.");
        private static readonly Counter<long> _UploadBytes = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveUploadBytes, "By", "Migration batch bytes received by the archive server.");
        private static readonly Counter<long> _QueryRows = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveQueryRows, "{row}", "Archived rows returned by archive queries.");
        private static readonly Counter<long> _Verifications = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.ArchiveVerifications, "{verification}", "Archive verifications by result.");
        private static readonly Counter<long> _CacheLookups = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.IntrospectionCacheLookups, "{lookup}", "Archive server introspection cache lookups by result.");
        private static Func<int>? _CacheSizeProvider = null;

        #endregion

        #region Constructors-and-Factories

        static ArchiveServerTelemetry()
        {
            NetLedgerTelemetry.Meter.CreateObservableGauge<int>(
                TelemetryNames.IntrospectionCacheSize,
                ObserveCacheSize,
                "{entry}",
                "Archive server introspection cache entries.");
        }

        #endregion

        #region Internal-Methods

        internal static void SetCacheSizeProvider(Func<int>? provider)
        {
            Volatile.Write(ref _CacheSizeProvider, provider);
        }

        internal static void RecordMigrationEvent(string eventName, string entity)
        {
            try
            {
                if (!_MigrationEvents.Enabled) return;
                _MigrationEvents.Add(1, new TagList
                {
                    { TelemetryNames.LabelEvent, eventName },
                    { TelemetryNames.LabelEntity, entity }
                });
            }
            catch (Exception)
            {
            }
        }

        internal static TelemetryScope StartWorkflowStage(string workflow, string stage)
        {
            return TelemetryScope.Start(
                TelemetryNames.SpanStagePrefix + stage,
                ActivityKind.Internal,
                _WorkflowStageDuration,
                null,
                new TagList
                {
                    { TelemetryNames.LabelWorkflow, workflow },
                    { TelemetryNames.LabelStage, stage }
                });
        }

        internal static void RecordUploadBytes(long bytes)
        {
            try
            {
                if (bytes > 0 && _UploadBytes.Enabled) _UploadBytes.Add(bytes);
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordQueryRows(string entity, long rows)
        {
            try
            {
                if (rows > 0 && _QueryRows.Enabled) _QueryRows.Add(rows, new TagList { { TelemetryNames.LabelEntity, entity } });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordVerification(bool valid)
        {
            try
            {
                if (_Verifications.Enabled) _Verifications.Add(1, new TagList { { TelemetryNames.LabelResult, valid ? "valid" : "invalid" } });
            }
            catch (Exception)
            {
            }
        }

        internal static TelemetryScope StartAuthentication(string method)
        {
            return TelemetryScope.Start(
                TelemetryNames.SpanAuthenticate,
                ActivityKind.Internal,
                NetLedgerTelemetry.AuthDuration,
                NetLedgerTelemetry.AuthAttempts,
                new TagList
                {
                    { TelemetryNames.LabelComponent, TelemetryNames.ComponentArchiveServer },
                    { TelemetryNames.LabelAuthMethod, method }
                });
        }

        internal static void RecordCacheLookup(bool hit)
        {
            try
            {
                if (_CacheLookups.Enabled) _CacheLookups.Add(1, new TagList { { TelemetryNames.LabelResult, hit ? "hit" : "miss" } });
            }
            catch (Exception)
            {
            }
        }

        internal static void RecordAuthorizationDecision(string resourceType, string operationType, bool permitted)
        {
            try
            {
                Activity.Current?.AddEvent(new ActivityEvent("authz." + (permitted ? "permit" : "deny")));
                if (!NetLedgerTelemetry.AuthzDecisions.Enabled) return;
                NetLedgerTelemetry.AuthzDecisions.Add(1, new TagList
                {
                    { TelemetryNames.LabelComponent, TelemetryNames.ComponentArchiveServer },
                    { TelemetryNames.LabelResource, NetLedgerTelemetry.BoundLabel(resourceType, _Resources) },
                    { TelemetryNames.LabelOperation, NetLedgerTelemetry.BoundLabel(operationType, _Operations) },
                    { TelemetryNames.LabelDecision, permitted ? "permit" : "deny" }
                });
            }
            catch (Exception)
            {
            }
        }

        internal static string EntityLabel(NetLedger.Archive.ArchiveEntityType entityType)
        {
            return entityType == NetLedger.Archive.ArchiveEntityType.RequestHistory ? "request_history" : "entries";
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

        private static IEnumerable<Measurement<int>> ObserveCacheSize()
        {
            Func<int>? provider = Volatile.Read(ref _CacheSizeProvider);
            if (provider == null) return Array.Empty<Measurement<int>>();
            try
            {
                return new Measurement<int>[] { new Measurement<int>(provider()) };
            }
            catch (Exception)
            {
                return Array.Empty<Measurement<int>>();
            }
        }

        #endregion
    }
}
