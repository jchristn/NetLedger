namespace NetLedger.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Interfaces;
    using NetLedger.Database.Postgresql.Implementations;
    using NetLedger.Database.Postgresql.Queries;
    using Npgsql;

    /// <summary>
    /// PostgreSQL database driver.
    /// </summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        #region Private-Members

        private readonly string _ConnectionString;
        private readonly SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the PostgreSQL database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when required settings are missing.</exception>
        public PostgresqlDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            if (String.IsNullOrEmpty(settings.Hostname))
                throw new ArgumentException("Hostname is required for PostgreSQL.");
            if (String.IsNullOrEmpty(settings.DatabaseName))
                throw new ArgumentException("DatabaseName is required for PostgreSQL.");

            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder
            {
                Host = settings.Hostname,
                Port = settings.Port,
                Database = settings.DatabaseName,
                Username = settings.Username ?? String.Empty,
                Password = settings.Password ?? String.Empty,
                SslMode = settings.RequireEncryption ? SslMode.Require : SslMode.Prefer,
                Timeout = settings.ConnectionTimeoutSeconds,
                Pooling = true,
                MaxPoolSize = settings.MaxPoolSize,
                MinPoolSize = 1
            };

            if (!String.IsNullOrEmpty(settings.Schema))
            {
                builder.SearchPath = settings.Schema;
            }

            _ConnectionString = builder.ConnectionString;

            Accounts = new AccountMethods(this);
            Entries = new EntryMethods(this);
            ApiKeys = new ApiKeyMethods(this);

            InitializeDatabaseAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(PostgresqlDatabaseDriver));
            if (String.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            if (Settings.LogQueries)
            {
                LogQuery($"[PostgreSQL] {query}");
            }

            await _Lock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
                {
                    await connection.OpenAsync(token).ConfigureAwait(false);

                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        command.CommandTimeout = Settings.ConnectionTimeoutSeconds;

                        bool isWrite = !query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);

                        if (isWrite && !query.Contains("RETURNING"))
                        {
                            int affected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                            DataTable resultTable = new DataTable();
                            resultTable.Columns.Add("affected", typeof(int));
                            DataRow affectedRow = resultTable.NewRow();
                            affectedRow["affected"] = affected;
                            resultTable.Rows.Add(affectedRow);
                            return resultTable;
                        }
                        else
                        {
                            using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                            {
                                DataTable table = new DataTable();

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    table.Columns.Add(reader.GetName(i), typeof(string));
                                }

                                while (await reader.ReadAsync(token).ConfigureAwait(false))
                                {
                                    DataRow row = table.NewRow();
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        if (reader.IsDBNull(i))
                                            row[i] = DBNull.Value;
                                        else
                                            row[i] = reader.GetValue(i)?.ToString() ?? String.Empty;
                                    }
                                    table.Rows.Add(row);
                                }

                                return table;
                            }
                        }
                    }
                }
            }
            finally
            {
                _Lock.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(PostgresqlDatabaseDriver));
            if (queries == null || !queries.Any()) return new DataTable();

            await _Lock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
                {
                    await connection.OpenAsync(token).ConfigureAwait(false);

                    DataTable lastResult = new DataTable();

                    foreach (string query in queries)
                    {
                        if (String.IsNullOrEmpty(query)) continue;

                        if (Settings.LogQueries)
                        {
                            LogQuery($"[PostgreSQL] {query}");
                        }

                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.CommandTimeout = Settings.ConnectionTimeoutSeconds;
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    return lastResult;
                }
            }
            finally
            {
                _Lock.Release();
            }
        }

        /// <inheritdoc />
        public override async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(PostgresqlDatabaseDriver));

            NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);

            return new PostgresqlDatabaseTransaction(connection, transaction);
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
                    _Lock.Dispose();
                }
                _Disposed = true;
            }
            base.Dispose(disposing);
        }

        private async Task InitializeDatabaseAsync()
        {
            string[] tableQueries = SetupQueries.CreateTables();
            await ExecuteQueriesAsync(tableQueries).ConfigureAwait(false);

            string[] indexQueries = SetupQueries.CreateIndices();
            await ExecuteQueriesAsync(indexQueries).ConfigureAwait(false);
        }

        #endregion
    }
}
