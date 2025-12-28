namespace NetLedger.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for database transaction operations.
    /// </summary>
    public interface IDatabaseTransaction : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Commit the transaction.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task CommitAsync(CancellationToken token = default);

        /// <summary>
        /// Rollback the transaction.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task RollbackAsync(CancellationToken token = default);
    }
}
