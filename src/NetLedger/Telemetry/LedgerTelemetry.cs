namespace NetLedger.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;

    /// <summary>
    /// Ledger-domain instruments: operation latency and outcome, entry and commit counters, balance chain
    /// verification results, and account lock wait and occupancy.
    /// </summary>
    internal static class LedgerTelemetry
    {
        #region Internal-Members

        internal const string LockProcess = "process";
        internal const string LockDatabase = "database";

        #endregion

        #region Private-Members

        private static readonly Histogram<double> _OperationDuration = NetLedgerTelemetry.Meter.CreateHistogram<double>(
            TelemetryNames.LedgerOperationDuration, "s", "Duration of public NetLedger Ledger operations.");
        private static readonly Counter<long> _Operations = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.LedgerOperations, "{operation}", "NetLedger Ledger operations by outcome.");
        private static readonly Counter<long> _EntriesCreated = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.LedgerEntriesCreated, "{entry}", "Ledger entries created.");
        private static readonly Counter<long> _EntriesCommitted = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.LedgerEntriesCommitted, "{entry}", "Ledger entries committed into a balance.");
        private static readonly Counter<long> _EntriesCanceled = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.LedgerEntriesCanceled, "{entry}", "Pending ledger entries canceled.");
        private static readonly Histogram<long> _CommitSize = NetLedgerTelemetry.Meter.CreateHistogram<long>(
            TelemetryNames.LedgerCommitSize, "{entry}", "Entries committed per commit.");
        private static readonly Counter<long> _Verifications = NetLedgerTelemetry.Meter.CreateCounter<long>(
            TelemetryNames.LedgerBalanceChainVerifications, "{verification}", "Balance chain verifications by result.");
        private static readonly Histogram<double> _LockWait = NetLedgerTelemetry.Meter.CreateHistogram<double>(
            TelemetryNames.LedgerLockWaitDuration, "s", "Time spent waiting for an account lock.");
        private static long _ProcessLocksHeld = 0;
        private static long _DatabaseLocksHeld = 0;
        private static readonly ObservableUpDownCounter<long> _LocksActive = NetLedgerTelemetry.Meter.CreateObservableUpDownCounter<long>(
            TelemetryNames.LedgerLocksActive,
            ObserveLocksHeld,
            "{lock}",
            "Account locks currently held.");

        #endregion

        #region Internal-Methods

        internal static TelemetryScope StartOperation(string operation, string? accountId)
        {
            TelemetryScope scope = TelemetryScope.Start(
                TelemetryNames.SpanLedgerPrefix + operation,
                ActivityKind.Internal,
                _OperationDuration,
                _Operations,
                new TagList { { TelemetryNames.LabelOperation, operation } });
            scope.SetTag(TelemetryNames.AttributeAccountId, accountId);
            return scope;
        }

        internal static TelemetryScope StartLockWait(string lockKind, string accountId)
        {
            TelemetryScope scope = TelemetryScope.Start(
                TelemetryNames.SpanLockWait,
                ActivityKind.Internal,
                _LockWait,
                null,
                new TagList { { TelemetryNames.LabelLock, lockKind } });
            scope.SetTag(TelemetryNames.AttributeAccountId, accountId);
            return scope;
        }

        internal static void LockAcquired(string lockKind)
        {
            if (lockKind == LockDatabase) Interlocked.Increment(ref _DatabaseLocksHeld);
            else Interlocked.Increment(ref _ProcessLocksHeld);
        }

        internal static void LockReleased(string lockKind)
        {
            if (lockKind == LockDatabase) Interlocked.Decrement(ref _DatabaseLocksHeld);
            else Interlocked.Decrement(ref _ProcessLocksHeld);
        }

        internal static void EntriesCreated(EntryType type, long count)
        {
            if (count < 1) return;
            string entryType = type == EntryType.Credit ? "credit" : type == EntryType.Debit ? "debit" : "balance";
            Add(_EntriesCreated, count, TelemetryNames.LabelEntryType, entryType);
        }

        internal static void EntriesCommitted(long count)
        {
            try
            {
                if (_CommitSize.Enabled) _CommitSize.Record(count);
                if (count > 0 && _EntriesCommitted.Enabled) _EntriesCommitted.Add(count);
            }
            catch (Exception)
            {
            }
        }

        internal static void EntryCanceled()
        {
            try
            {
                if (_EntriesCanceled.Enabled) _EntriesCanceled.Add(1);
            }
            catch (Exception)
            {
            }
        }

        internal static void BalanceChainVerified(bool valid)
        {
            Add(_Verifications, 1, TelemetryNames.LabelResult, valid ? "valid" : "invalid");
        }

        #endregion

        #region Private-Methods

        private static void Add(Counter<long> counter, long value, string key, string label)
        {
            try
            {
                if (counter.Enabled) counter.Add(value, new TagList { { key, label } });
            }
            catch (Exception)
            {
            }
        }

        private static IEnumerable<Measurement<long>> ObserveLocksHeld()
        {
            return new List<Measurement<long>>
            {
                new Measurement<long>(Interlocked.Read(ref _ProcessLocksHeld), new TagList { { TelemetryNames.LabelLock, LockProcess } }),
                new Measurement<long>(Interlocked.Read(ref _DatabaseLocksHeld), new TagList { { TelemetryNames.LabelLock, LockDatabase } })
            };
        }

        #endregion
    }
}
