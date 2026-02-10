namespace NetLedger.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AsyncKeyedLock;
    using Microsoft.Data.SqlClient;
    using NetLedger.Database.SqlServer.Implementations;
    using NetLedger.Database.SqlServer.Queries;

    /// <summary>
    /// SQL Server database driver.
    /// </summary>
    public class SqlServerDatabaseDriver : DatabaseDriverBase
    {
        #region Private-Members

        private readonly string _ConnectionString;
        private readonly AsyncNonKeyedLocker _Lock = new();
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
                DataSource = settings.Port != 1433 ? $"{settings.Hostname},{settings.Port}" : settings.Hostname,
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
            if (_Disposed) throw new ObjectDisposedException(nameof(SqlServerDatabaseDriver));
            if (String.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            if (Settings.LogQueries)
            {
                LogQuery($"[SQL Server] {query}");
            }

            using var _ = await _Lock.LockAsync(token).ConfigureAwait(false);
            using var connection = new SqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            using var command = new SqlCommand(query, connection);
            command.CommandTimeout = Settings.ConnectionTimeoutSeconds;

            bool isWrite = !query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);

            if (isWrite && !query.Contains("OUTPUT INSERTED"))
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
                using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                DataTable table = new();

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

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueriesAsync(IEnumerable<string> queries, bool isTransaction = false, CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SqlServerDatabaseDriver));
            if (queries == null || !queries.Any()) return new DataTable();

            using var _ = await _Lock.LockAsync(token).ConfigureAwait(false);
            using var connection = new SqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            DataTable lastResult = new();

            foreach (string query in queries)
            {
                if (String.IsNullOrEmpty(query)) continue;

                if (Settings.LogQueries)
                {
                    LogQuery($"[SQL Server] {query}");
                }

                using var command = new SqlCommand(query, connection);
                command.CommandTimeout = Settings.ConnectionTimeoutSeconds;
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            return lastResult;
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
