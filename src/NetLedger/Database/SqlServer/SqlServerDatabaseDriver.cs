namespace NetLedger.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NetLedger.Database.SqlServer.Queries;
    using NetLedger.Database.Portable;

    /// <summary>
    /// SQL Server database driver.
    /// </summary>
    public class SqlServerDatabaseDriver : DatabaseDriverBase
    {
        #region Private-Members

        private readonly string _ConnectionString;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the SQL Server database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when required settings are missing.</exception>
        public SqlServerDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            if (String.IsNullOrEmpty(settings.Hostname))
                throw new ArgumentException("Hostname is required for SQL Server.");
            if (String.IsNullOrEmpty(settings.DatabaseName))
                throw new ArgumentException("DatabaseName is required for SQL Server.");

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = settings.GetEffectivePort() != 1433 ? $"{settings.Hostname},{settings.GetEffectivePort()}" : settings.Hostname,
                InitialCatalog = settings.DatabaseName,
                ConnectTimeout = settings.ConnectionTimeoutSeconds,
                Pooling = true,
                MaxPoolSize = settings.MaxPoolSize,
                MinPoolSize = 1,
                TrustServerCertificate = !settings.RequireEncryption,
                Encrypt = settings.RequireEncryption
            };

            if (!String.IsNullOrEmpty(settings.Username))
            {
                builder.UserID = settings.Username;
                builder.Password = settings.Password ?? String.Empty;
            }
            else
            {
                builder.IntegratedSecurity = true;
            }

            _ConnectionString = builder.ConnectionString;

            Accounts = new PortableSqlAccountMethods(this, DatabaseTypeEnum.SqlServer);
            Entries = new PortableSqlEntryMethods(this, DatabaseTypeEnum.SqlServer);
            ApiKeys = new PortableSqlApiKeyMethods(this, DatabaseTypeEnum.SqlServer);
            Tenants = new PortableSqlTenantMethods(this, DatabaseTypeEnum.SqlServer);
            Users = new PortableSqlUserMethods(this, DatabaseTypeEnum.SqlServer);
            AuthSessions = new PortableSqlAuthSessionMethods(this, DatabaseTypeEnum.SqlServer);
            AccountUserMaps = new PortableSqlAccountUserMapMethods(this, DatabaseTypeEnum.SqlServer);
            AuditRecords = new PortableSqlAuditRecordMethods(this, DatabaseTypeEnum.SqlServer);
            RequestHistory = new PortableSqlRequestHistoryMethods(this, DatabaseTypeEnum.SqlServer);
            Rbac = new PortableSqlRbacMethods(this, DatabaseTypeEnum.SqlServer);

            InitializeDatabaseAsync().GetAwaiter().GetResult();
            Rbac.SeedBuiltInsAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isTransaction = false, CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SqlServerDatabaseDriver));
            if (String.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            if (Settings.LogQueries)
            {
                LogQuery($"[SQL Server] {query}");
            }

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = Settings.ConnectionTimeoutSeconds;

                    bool isWrite = !query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);

                    if (isWrite && !query.Contains("OUTPUT", StringComparison.OrdinalIgnoreCase))
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
                        using (SqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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
            if (_Disposed) throw new ObjectDisposedException(nameof(SqlServerDatabaseDriver));
            if (queries == null || !queries.Any()) return new DataTable();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                DataTable lastResult = new DataTable();
                SqlTransaction? transaction = null;

                try
                {
                    if (isTransaction)
                    {
                        transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false);
                    }

                    foreach (string query in queries)
                    {
                        if (String.IsNullOrEmpty(query)) continue;

                        if (Settings.LogQueries)
                        {
                            LogQuery($"[SQL Server] {query}");
                        }

                        using (SqlCommand command = new SqlCommand(query, connection))
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
            if (_Disposed) throw new ObjectDisposedException(nameof(SqlServerDatabaseDriver));

            SqlConnection connection = new SqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false);

            return new SqlServerDatabaseTransaction(connection, transaction);
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



