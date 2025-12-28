namespace NetLedger.Database.Postgresql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;

    /// <summary>
    /// PostgreSQL database transaction wrapper.
    /// </summary>
    internal class PostgresqlDatabaseTransaction : IDatabaseTransaction
    {
        #region Private-Members

        private readonly NpgsqlConnection _Connection;
        private readonly NpgsqlTransaction _Transaction;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the PostgreSQL database transaction.
        /// </summary>
        /// <param name="connection">PostgreSQL connection.</param>
        /// <param name="transaction">PostgreSQL transaction.</param>
        internal PostgresqlDatabaseTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            _Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(PostgresqlDatabaseTransaction));
            await _Transaction.CommitAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(PostgresqlDatabaseTransaction));
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
