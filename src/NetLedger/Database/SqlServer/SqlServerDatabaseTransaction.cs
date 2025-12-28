namespace NetLedger.Database.SqlServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;

    /// <summary>
    /// SQL Server database transaction wrapper.
    /// </summary>
    internal class SqlServerDatabaseTransaction : IDatabaseTransaction
    {
        #region Private-Members

        private readonly SqlConnection _Connection;
        private readonly SqlTransaction _Transaction;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the SQL Server database transaction.
        /// </summary>
        /// <param name="connection">SQL Server connection.</param>
        /// <param name="transaction">SQL Server transaction.</param>
        internal SqlServerDatabaseTransaction(SqlConnection connection, SqlTransaction transaction)
        {
            _Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SqlServerDatabaseTransaction));
            await _Transaction.CommitAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SqlServerDatabaseTransaction));
            await _Transaction.RollbackAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_Disposed)
            {
                _Transaction.Dispose();
                _Connection.Dispose();
                _Disposed = true;
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (!_Disposed)
            {
                await _Transaction.DisposeAsync().ConfigureAwait(false);
                await _Connection.DisposeAsync().ConfigureAwait(false);
                _Disposed = true;
            }
        }

        #endregion
    }
}
