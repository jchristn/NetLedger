namespace NetLedger.Database.Sqlite
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;

    /// <summary>
    /// SQLite database transaction wrapper.
    /// </summary>
    internal class SqliteDatabaseTransaction : IDatabaseTransaction
    {
        #region Private-Members

        private SqliteConnection _Connection = null;
        private SqliteTransaction _Transaction = null;
        private bool _Disposed = false;
        private bool _Completed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the SQLite database transaction.
        /// </summary>
        /// <param name="connection">Open SQLite connection.</param>
        /// <param name="transaction">SQLite transaction.</param>
        internal SqliteDatabaseTransaction(SqliteConnection connection, SqliteTransaction transaction)
        {
            _Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        #endregion

        #region Internal-Properties

        /// <summary>
        /// The underlying SQLite connection.
        /// </summary>
        internal SqliteConnection Connection
        {
            get { return _Connection; }
        }

        /// <summary>
        /// The underlying SQLite transaction.
        /// </summary>
        internal SqliteTransaction Transaction
        {
            get { return _Transaction; }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Commit the transaction.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task CommitAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SqliteDatabaseTransaction));
            if (_Completed) throw new InvalidOperationException("Transaction has already been completed.");

            token.ThrowIfCancellationRequested();
            await _Transaction.CommitAsync(token).ConfigureAwait(false);
            _Completed = true;
        }

        /// <summary>
        /// Rollback the transaction.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RollbackAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SqliteDatabaseTransaction));
            if (_Completed) throw new InvalidOperationException("Transaction has already been completed.");

            token.ThrowIfCancellationRequested();
            await _Transaction.RollbackAsync(token).ConfigureAwait(false);
            _Completed = true;
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources asynchronously.
        /// </summary>
        /// <returns>ValueTask.</returns>
        public async ValueTask DisposeAsync()
        {
            if (!_Disposed)
            {
                if (!_Completed && _Transaction != null)
                {
                    try
                    {
                        await _Transaction.RollbackAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore rollback errors during disposal
                    }
                }

                if (_Transaction != null)
                {
                    await _Transaction.DisposeAsync().ConfigureAwait(false);
                    _Transaction = null;
                }

                if (_Connection != null)
                {
                    await _Connection.CloseAsync().ConfigureAwait(false);
                    await _Connection.DisposeAsync().ConfigureAwait(false);
                    _Connection = null;
                }

                _Disposed = true;
            }
        }

        #endregion

        #region Private-Methods

        private void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    if (!_Completed && _Transaction != null)
                    {
                        try
                        {
                            _Transaction.Rollback();
                        }
                        catch
                        {
                            // Ignore rollback errors during disposal
                        }
                    }

                    _Transaction?.Dispose();
                    _Transaction = null;

                    _Connection?.Close();
                    _Connection?.Dispose();
                    _Connection = null;
                }
                _Disposed = true;
            }
        }

        #endregion
    }
}
