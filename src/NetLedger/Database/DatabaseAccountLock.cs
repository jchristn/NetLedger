namespace NetLedger.Database
{
    using System;
    using System.Threading.Tasks;
    using NetLedger.Telemetry;

    internal sealed class DatabaseAccountLock : IAsyncDisposable
    {
        private readonly DatabaseDriverBase _Driver;
        private readonly string _AccountId;
        private readonly string _OwnerId;
        private bool _Disposed;

        internal DatabaseAccountLock(DatabaseDriverBase driver, string accountId, string ownerId)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _AccountId = accountId ?? throw new ArgumentNullException(nameof(accountId));
            _OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
            LedgerTelemetry.LockAcquired(LedgerTelemetry.LockDatabase);
        }

        public async ValueTask DisposeAsync()
        {
            if (_Disposed) return;
            _Disposed = true;
            try
            {
                await _Driver.ReleaseAccountLockAsync(_AccountId, _OwnerId).ConfigureAwait(false);
            }
            finally
            {
                LedgerTelemetry.LockReleased(LedgerTelemetry.LockDatabase);
            }
        }
    }
}
