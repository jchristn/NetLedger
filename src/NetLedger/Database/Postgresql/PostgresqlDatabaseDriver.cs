namespace NetLedger.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Database.Postgresql.Queries;
    using NetLedger.Database.Portable;
    using Npgsql;

    /// <summary>
    /// PostgreSQL database driver.
    /// </summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        #region Private-Members

        private readonly string _ConnectionString;
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
                Port = settings.GetEffectivePort(),
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

            Accounts = new PortableSqlAccountMethods(this, DatabaseTypeEnum.Postgresql);
            Entries = new PortableSqlEntryMethods(this, DatabaseTypeEnum.Postgresql);
            ApiKeys = new PortableSqlApiKeyMethods(this, DatabaseTypeEnum.Postgresql);
            Tenants = new PortableSqlTenantMethods(this, DatabaseTypeEnum.Postgresql);
            Users = new PortableSqlUserMethods(this, DatabaseTypeEnum.Postgresql);
            AuthSessions = new PortableSqlAuthSessionMethods(this, DatabaseTypeEnum.Postgresql);
            AccountUserMaps = new PortableSqlAccountUserMapMethods(this, DatabaseTypeEnum.Postgresql);
            AuditRecords = new PortableSqlAuditRecordMethods(this, DatabaseTypeEnum.Postgresql);
            RequestHistory = new PortableSqlRequestHistoryMethods(this, DatabaseTypeEnum.Postgresql);
            Rbac = new PortableSqlRbacMethods(this, DatabaseTypeEnum.Postgresql);

            InitializeDatabaseAsync().GetAwaiter().GetResult();
            Rbac.SeedBuiltInsAsync(CancellationToken.None).GetAwaiter().GetResult();
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

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(PostgresqlDatabaseDriver));
            if (queries == null || !queries.Any()) return new DataTable();

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                DataTable lastResult = new DataTable();
                NpgsqlTransaction? transaction = null;

                try
                {
                    if (isTransaction)
                    {
                        transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                    }

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
                            if (transaction != null) command.Transaction = transaction;
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    if (transaction != null)
                    {
                        await transaction.CommitAsync(token).ConfigureAwait(false);
                    }

                    return lastResult;
                }
                catch
                {
                    if (transaction != null)
                    {
                        await transaction.RollbackAsync(token).ConfigureAwait(false);
                    }
                    throw;
                }
                finally
                {
                    if (transaction != null)
                    {
                        await transaction.DisposeAsync().ConfigureAwait(false);
                    }
                }
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
                _Disposed = true;
            }
            base.Dispose(disposing);
        }

        private async Task InitializeDatabaseAsync()
        {
            string[] tableQueries = SetupQueries.CreateTables();
            await ExecuteQueriesAsync(tableQueries).ConfigureAwait(false);

            await PrettyIdPrimaryKeyMigration.ApplyAsync(this, CancellationToken.None).ConfigureAwait(false);

            string[] indexQueries = SetupQueries.CreateIndices();
            await ExecuteQueriesAsync(indexQueries).ConfigureAwait(false);
        }

        #endregion
    }
}



