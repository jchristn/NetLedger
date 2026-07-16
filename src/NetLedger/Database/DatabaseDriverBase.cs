namespace NetLedger.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
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
        /// Tenant methods.
        /// </summary>
        public ITenantMethods Tenants { get; protected set; }

        /// <summary>
        /// User methods.
        /// </summary>
        public IUserMethods Users { get; protected set; }

        /// <summary>
        /// Authentication session methods.
        /// </summary>
        public IAuthSessionMethods AuthSessions { get; protected set; }

        /// <summary>
        /// Account-user map methods.
        /// </summary>
        public IAccountUserMapMethods AccountUserMaps { get; protected set; }

        /// <summary>
        /// Audit record methods.
        /// </summary>
        public IAuditRecordMethods AuditRecords { get; protected set; }

        /// <summary>
        /// Request history methods.
        /// </summary>
        public IRequestHistoryMethods RequestHistory { get; protected set; }

        /// <summary>
        /// Role-based access control methods.
        /// </summary>
        public IRbacMethods Rbac { get; protected set; }

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
        /// Acquire a database-backed account lock shared by all ledger instances using the same database.
        /// </summary>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Account lock releaser.</returns>
        public async Task<IAsyncDisposable> AcquireAccountLockAsync(string accountId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(accountId)) throw new ArgumentNullException(nameof(accountId));

            string ownerId = NetLedgerId.Generate("lck_");
            Stopwatch sw = Stopwatch.StartNew();
            TimeSpan timeout = TimeSpan.FromSeconds(Math.Max(5, Settings.ConnectionTimeoutSeconds));
            TimeSpan delay = TimeSpan.FromMilliseconds(25);

            while (true)
            {
                token.ThrowIfCancellationRequested();
                await DeleteExpiredAccountLocksAsync(token).ConfigureAwait(false);

                try
                {
                    await ExecuteQueryAsync(BuildInsertAccountLockQuery(accountId, ownerId), true, token).ConfigureAwait(false);
                    return new DatabaseAccountLock(this, accountId, ownerId);
                }
                catch when (sw.Elapsed < timeout)
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                    if (delay < TimeSpan.FromMilliseconds(250)) delay += TimeSpan.FromMilliseconds(25);
                }
            }
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

        internal async Task ReleaseAccountLockAsync(string accountId, string ownerId, CancellationToken token = default)
        {
            await ExecuteQueryAsync(BuildDeleteAccountLockQuery(accountId, ownerId), true, token).ConfigureAwait(false);
        }

        private async Task DeleteExpiredAccountLocksAsync(CancellationToken token)
        {
            await ExecuteQueryAsync(BuildDeleteExpiredAccountLocksQuery(), true, token).ConfigureAwait(false);
        }

        private string BuildInsertAccountLockQuery(string accountId, string ownerId)
        {
            string expiresUtc = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-dd HH:mm:ss.ffffff");
            string createdUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff");

            switch (Settings.Type)
            {
                case DatabaseTypeEnum.Mysql:
                    return "INSERT INTO `accountlocks` (`accountid`, `ownerid`, `expiresutc`, `createdutc`) VALUES ('" + Sanitize(accountId) + "', '" + ownerId + "', '" + expiresUtc + "', '" + createdUtc + "');";
                case DatabaseTypeEnum.SqlServer:
                    return "INSERT INTO [accountlocks] ([accountid], [ownerid], [expiresutc], [createdutc]) VALUES ('" + Sanitize(accountId) + "', '" + ownerId + "', '" + expiresUtc + "', '" + createdUtc + "');";
                default:
                    return "INSERT INTO accountlocks (accountid, ownerid, expiresutc, createdutc) VALUES ('" + Sanitize(accountId) + "', '" + ownerId + "', '" + expiresUtc + "', '" + createdUtc + "');";
            }
        }

        private string BuildDeleteAccountLockQuery(string accountId, string ownerId)
        {
            switch (Settings.Type)
            {
                case DatabaseTypeEnum.Mysql:
                    return "DELETE FROM `accountlocks` WHERE `accountid` = '" + Sanitize(accountId) + "' AND `ownerid` = '" + ownerId + "';";
                case DatabaseTypeEnum.SqlServer:
                    return "DELETE FROM [accountlocks] WHERE [accountid] = '" + Sanitize(accountId) + "' AND [ownerid] = '" + ownerId + "';";
                default:
                    return "DELETE FROM accountlocks WHERE accountid = '" + Sanitize(accountId) + "' AND ownerid = '" + ownerId + "';";
            }
        }

        private string BuildDeleteExpiredAccountLocksQuery()
        {
            string now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
            switch (Settings.Type)
            {
                case DatabaseTypeEnum.Mysql:
                    return "DELETE FROM `accountlocks` WHERE `expiresutc` < '" + now + "';";
                case DatabaseTypeEnum.SqlServer:
                    return "DELETE FROM [accountlocks] WHERE [expiresutc] < '" + now + "';";
                default:
                    return "DELETE FROM accountlocks WHERE expiresutc < '" + now + "';";
            }
        }

        private string Sanitize(string input)
        {
            return input.Replace("'", "''");
        }

        #endregion
    }
}



