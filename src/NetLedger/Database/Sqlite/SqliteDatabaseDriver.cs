namespace NetLedger.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AsyncKeyedLock;
    using Microsoft.Data.Sqlite;
    using NetLedger.Database.Sqlite.Implementations;
    using NetLedger.Database.Sqlite.Queries;

    /// <summary>
    /// SQLite database driver implementation.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        #region Private-Members

        private readonly AsyncNonKeyedLocker _Lock = new();
        private readonly string _ConnectionString;
        private readonly int _MaxStatementLength = 1024 * 1024;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the SQLite database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when filename is null or empty.</exception>
        public SqliteDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            if (String.IsNullOrEmpty(settings.Filename))
                throw new ArgumentException("Filename must be specified for SQLite database.", nameof(settings));

            _ConnectionString = "Data Source=" + settings.Filename + ";";

            // Initialize implementation methods
            Accounts = new AccountMethods(this);
            Entries = new EntryMethods(this);
            ApiKeys = new ApiKeyMethods(this);

            // Initialize database
            InitializeDatabaseAsync(CancellationToken.None).Wait();

            // Create tables and indices
            ExecuteQueryAsync(SetupQueries.CreateTables()).Wait();
            ExecuteQueryAsync(SetupQueries.CreateIndices()).Wait();
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueryAsync(
            string query,
            bool isTransaction = false,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));
            if (query.Length > _MaxStatementLength)
                throw new ArgumentException("Query exceeds maximum statement length of " + _MaxStatementLength + " characters.");

            token.ThrowIfCancellationRequested();
            LogQuery(query);

            DataTable result = new();

            using var _ = await _Lock.LockAsync(token).ConfigureAwait(false);
            using var conn = new SqliteConnection(_ConnectionString);
            await conn.OpenAsync(token).ConfigureAwait(false);

            DbTransaction transaction = null;

            try
            {
                if (isTransaction)
                {
                    transaction = await conn.BeginTransactionAsync(token).ConfigureAwait(false);
                }

                using (SqliteCommand cmd = new(query, conn))
                {
                    if (transaction != null)
                    {
                        cmd.Transaction = (SqliteTransaction)transaction;
                    }

                    using (SqliteDataReader rdr = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (result.Columns.Count == 0)
                        {
                            for (int i = 0; i < rdr.FieldCount; i++)
                            {
                                result.Columns.Add(rdr.GetName(i), typeof(string));
                            }
                        }

                        while (await rdr.ReadAsync(token).ConfigureAwait(false))
                        {
                            DataRow row = result.NewRow();
                            for (int i = 0; i < rdr.FieldCount; i++)
                            {
                                if (!rdr.IsDBNull(i))
                                {
                                    row[i] = rdr.GetValue(i)?.ToString() ?? String.Empty;
                                }
                            }
                            result.Rows.Add(row);
                        }
                    }
                }

                if (transaction != null)
                {
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                }

                e.Data["IsTransaction"] = isTransaction;
                e.Data["Query"] = query;
                throw;
            }
            finally
            {
                transaction?.Dispose();
                await conn.CloseAsync().ConfigureAwait(false);
            }

            return result;
        }

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueriesAsync(
            IEnumerable<string> queries,
            bool isTransaction = false,
            CancellationToken token = default)
        {
            if (queries == null) throw new ArgumentNullException(nameof(queries));

            List<string> queryList = queries.ToList();
            if (queryList.Count == 0) return new DataTable();

            // For single query, delegate to ExecuteQueryAsync
            if (queryList.Count == 1)
            {
                return await ExecuteQueryAsync(queryList[0], isTransaction, token).ConfigureAwait(false);
            }

            token.ThrowIfCancellationRequested();

            DataTable result = new DataTable();

            await _DatabaseSemaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                using (SqliteConnection conn = new SqliteConnection(_ConnectionString))
                {
                    await conn.OpenAsync(token).ConfigureAwait(false);

                    DbTransaction transaction = null;

                    try
                    {
                        // Always use transaction for multiple queries
                        transaction = await conn.BeginTransactionAsync(token).ConfigureAwait(false);

                        foreach (string query in queryList)
                        {
                            if (String.IsNullOrEmpty(query)) continue;

                            token.ThrowIfCancellationRequested();
                            LogQuery(query);

                            using (SqliteCommand cmd = new SqliteCommand(query, conn))
                            {
                                cmd.Transaction = (SqliteTransaction)transaction;

                                using (SqliteDataReader rdr = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
                                {
                                    // Only capture results from last query
                                    result = new DataTable();

                                    if (result.Columns.Count == 0)
                                    {
                                        for (int i = 0; i < rdr.FieldCount; i++)
                                        {
                                            result.Columns.Add(rdr.GetName(i), typeof(string));
                                        }
                                    }

                                    while (await rdr.ReadAsync(token).ConfigureAwait(false))
                                    {
                                        DataRow row = result.NewRow();
                                        for (int i = 0; i < rdr.FieldCount; i++)
                                        {
                                            if (!rdr.IsDBNull(i))
                                            {
                                                row[i] = rdr.GetValue(i)?.ToString() ?? String.Empty;
                                            }
                                        }
                                        result.Rows.Add(row);
                                    }
                                }
                            }
                        }

                        await transaction.CommitAsync(token).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        if (transaction != null)
                        {
                            await transaction.RollbackAsync(token).ConfigureAwait(false);
                        }

                        e.Data["IsTransaction"] = isTransaction;
                        throw;
                    }
                    finally
                    {
                        transaction?.Dispose();
                        await conn.CloseAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _DatabaseSemaphore.Release();
            }

            return result;
        }

        /// <inheritdoc />
        public override async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            SqliteConnection conn = new SqliteConnection(_ConnectionString);
            await conn.OpenAsync(token).ConfigureAwait(false);

            SqliteTransaction transaction = (SqliteTransaction)await conn.BeginTransactionAsync(token).ConfigureAwait(false);

            return new SqliteDatabaseTransaction(conn, transaction);
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    _DatabaseSemaphore?.Dispose();
                }
                _Disposed = true;
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Private-Methods

        private async Task InitializeDatabaseAsync(CancellationToken token)
        {
            await ExecuteQueryAsync("PRAGMA journal_mode = WAL;", false, token).ConfigureAwait(false);
            await ExecuteQueryAsync("PRAGMA synchronous = NORMAL;", false, token).ConfigureAwait(false);
            await ExecuteQueryAsync("PRAGMA cache_size = -1000000;", false, token).ConfigureAwait(false);
            await ExecuteQueryAsync("PRAGMA temp_store = MEMORY;", false, token).ConfigureAwait(false);
            await ExecuteQueryAsync("PRAGMA page_size = 4096;", false, token).ConfigureAwait(false);
            await ExecuteQueryAsync("PRAGMA mmap_size = 2147483648;", false, token).ConfigureAwait(false);
            await ExecuteQueryAsync("PRAGMA wal_autocheckpoint = 1000;", false, token).ConfigureAwait(false);
            await ExecuteQueryAsync("PRAGMA foreign_keys = ON;", false, token).ConfigureAwait(false);
        }

        #endregion
    }
}
