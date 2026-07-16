namespace NetLedger.Database
{
    using System;
    using System.Threading.Tasks;

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
        }

        public async ValueTask DisposeAsync()
        {
            if (_Disposed) return;
            await _Driver.ReleaseAccountLockAsync(_AccountId, _OwnerId).ConfigureAwait(false);
            _Disposed = true;
        }
    }
}
