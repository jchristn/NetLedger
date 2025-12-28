namespace NetLedger.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;

    /// <summary>
    /// Abstract base class for database drivers.
    /// Derived classes must initialize the implementation methods.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        #region Public-Members

        /// <summary>
        /// Account methods.
        /// </summary>
        public IAccountMethods Accounts { get; protected set; }

        /// <summary>
        /// Entry methods.
        /// </summary>
        public IEntryMethods Entries { get; protected set; }

        /// <summary>
        /// API key methods.
        /// </summary>
        public IApiKeyMethods ApiKeys { get; protected set; }

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Settings { get; protected set; }

        /// <summary>
        /// Enable or disable query logging.
        /// </summary>
        public bool LogQueries
        {
            get { return _LogQueries; }
            set { _LogQueries = value; }
        }

        /// <summary>
        /// Action to invoke when logging queries.
        /// </summary>
        public Action<string> LogQueryAction
        {
            get { return _LogQueryAction; }
            set { _LogQueryAction = value; }
        }

        #endregion

        #region Private-Members

        private bool _Disposed = false;
        private bool _LogQueries = false;
        private Action<string> _LogQueryAction = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Database driver base class.
        /// Derived classes must initialize the implementation methods.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        protected DatabaseDriverBase(DatabaseSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _LogQueries = settings.LogQueries;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Execute a raw SQL query.
        /// </summary>
        /// <param name="query">SQL query string.</param>
        /// <param name="isTransaction">Whether to execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable containing query results.</returns>
        /// <exception cref="ArgumentNullException">Thrown when query is null or empty.</exception>
        public abstract Task<DataTable> ExecuteQueryAsync(
            string query,
            bool isTransaction = false,
            CancellationToken token = default);

        /// <summary>
        /// Execute multiple raw SQL queries.
        /// </summary>
        /// <param name="queries">SQL query strings.</param>
        /// <param name="isTransaction">Whether to execute within a transaction.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>DataTable containing results of the last query.</returns>
        /// <exception cref="ArgumentNullException">Thrown when queries is null.</exception>
        public abstract Task<DataTable> ExecuteQueriesAsync(
            IEnumerable<string> queries,
            bool isTransaction = false,
            CancellationToken token = default);

        /// <summary>
        /// Begin a database transaction.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database transaction object.</returns>
        public abstract Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken token = default);

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
        public virtual async ValueTask DisposeAsync()
        {
            if (!_Disposed)
            {
                Dispose(true);
                _Disposed = true;
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">Whether disposing is occurring.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }
                _Disposed = true;
            }
        }

        /// <summary>
        /// Log a query if logging is enabled.
        /// </summary>
        /// <param name="query">Query to log.</param>
        protected void LogQuery(string query)
        {
            if (_LogQueries && _LogQueryAction != null)
            {
                _LogQueryAction.Invoke(query);
            }
        }

        #endregion
    }
}
