namespace NetLedger.Database.Mysql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MySqlConnector;

    /// <summary>
    /// MySQL database transaction wrapper.
    /// </summary>
    internal class MysqlDatabaseTransaction : IDatabaseTransaction
    {
        #region Private-Members

        private readonly MySqlConnection _Connection;
        private readonly MySqlTransaction _Transaction;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the MySQL database transaction.
        /// </summary>
        /// <param name="connection">MySQL connection.</param>
        /// <param name="transaction">MySQL transaction.</param>
        internal MysqlDatabaseTransaction(MySqlConnection connection, MySqlTransaction transaction)
        {
            _Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task CommitAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(MysqlDatabaseTransaction));
            await _Transaction.CommitAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(MysqlDatabaseTransaction));
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
