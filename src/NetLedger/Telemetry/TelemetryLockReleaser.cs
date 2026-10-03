namespace NetLedger.Telemetry
{
    using System;
    using System.Threading;

    /// <summary>
    /// Wraps an in-process account lock releaser so the held-lock gauge is decremented exactly once on release.
    /// </summary>
    internal sealed class TelemetryLockReleaser : IDisposable
    {
        private readonly IDisposable _Inner;
        private readonly string _LockKind;
        private int _Disposed = 0;

        internal TelemetryLockReleaser(IDisposable inner, string lockKind)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _LockKind = lockKind ?? throw new ArgumentNullException(nameof(lockKind));
            LedgerTelemetry.LockAcquired(_LockKind);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _Disposed, 1) == 1) return;
            try
            {
                _Inner.Dispose();
            }
            finally
            {
                LedgerTelemetry.LockReleased(_LockKind);
            }
        }
    }
}
