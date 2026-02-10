namespace NetLedger.Database.Mysql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AsyncKeyedLock;
    using MySqlConnector;
    using NetLedger.Database.Mysql.Implementations;
    using NetLedger.Database.Mysql.Queries;

    /// <summary>
    /// MySQL database driver.
    /// </summary>
    public class MysqlDatabaseDriver : DatabaseDriverBase
    {
        #region Private-Members

        private readonly string _ConnectionString;
        private readonly AsyncNonKeyedLocker _Lock = new();
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the MySQL database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        /// <exception cref="ArgumentException">Thrown when required settings are missing.</exception>
        public MysqlDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
            if (String.IsNullOrEmpty(settings.Hostname))
                throw new ArgumentException("Hostname is required for MySQL.");
            if (String.IsNullOrEmpty(settings.DatabaseName))
                throw new ArgumentException("DatabaseName is required for MySQL.");

            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = settings.Hostname,
                Port = (uint)settings.Port,
                Database = settings.DatabaseName,
                UserID = settings.Username ?? String.Empty,
                Password = settings.Password ?? String.Empty,
                SslMode = settings.RequireEncryption ? MySqlSslMode.Required : MySqlSslMode.Preferred,
                ConnectionTimeout = (uint)settings.ConnectionTimeoutSeconds,
                Pooling = true,
                MaximumPoolSize = (uint)settings.MaxPoolSize,
                MinimumPoolSize = 1
            };

            _ConnectionString = builder.ConnectionString;

            Accounts = new AccountMethods(this);
            Entries = new EntryMethods(this);
            ApiKeys = new ApiKeyMethods(this);

            InitializeDatabaseAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task<DataTable> ExecuteQueryAsync(string query, bool isWrite, CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(MysqlDatabaseDriver));
            if (String.IsNullOrEmpty(query)) throw new ArgumentNullException(nameof(query));

            if (Settings.LogQueries)
            {
                LogQuery($"[MySQL] {query}");
            }

            using var _ = await _Lock.LockAsync(token).ConfigureAwait(false);
            using var connection = new MySqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            using var command = new MySqlCommand(query, connection);
            command.CommandTimeout = Settings.ConnectionTimeoutSeconds;

            if (isWrite)
            {
                int affected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                // Handle INSERT with LAST_INSERT_ID
                if (query.Contains("LAST_INSERT_ID()"))
                {
                    using var idCommand = new MySqlCommand("SELECT LAST_INSERT_ID();", connection);
                    object result = await idCommand.ExecuteScalarAsync(token).ConfigureAwait(false);
                    DataTable idTable = new();
                    idTable.Columns.Add("id", typeof(long));
                    DataRow row = idTable.NewRow();
                    row["id"] = result;
                    idTable.Rows.Add(row);
                    return idTable;
                }

                DataTable resultTable = new();
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
            if (_Disposed) throw new ObjectDisposedException(nameof(MysqlDatabaseDriver));
            if (queries == null || !queries.Any()) return new DataTable();

            using var _ = await _Lock.LockAsync(token).ConfigureAwait(false);
            using var connection = new MySqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            DataTable lastResult = new();

            foreach (string query in queries)
            {
                if (String.IsNullOrEmpty(query)) continue;

                if (Settings.LogQueries)
                {
                    LogQuery($"[MySQL] {query}");
                }

                using var command = new MySqlCommand(query, connection);
                command.CommandTimeout = Settings.ConnectionTimeoutSeconds;
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            return lastResult;
        }

        /// <inheritdoc />
        public override async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(MysqlDatabaseDriver));

            MySqlConnection connection = new MySqlConnection(_ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);

            MySqlTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false);

            return new MysqlDatabaseTransaction(connection, transaction);
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
            await CreateIndicesAsync(indexQueries).ConfigureAwait(false);
        }

        private async Task CreateIndicesAsync(string[] indexQueries)
        {
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync().ConfigureAwait(false);

                foreach (string query in indexQueries)
                {
                    if (String.IsNullOrEmpty(query)) continue;

                    try
                    {
                        if (Settings.LogQueries)
                        {
                            LogQuery($"[MySQL] {query}");
                        }

                        using (MySqlCommand command = new MySqlCommand(query, connection))
                        {
                            command.CommandTimeout = Settings.ConnectionTimeoutSeconds;
                            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }
                    catch (MySqlException ex) when (ex.Number == 1061)
                    {
                        // Error 1061: Duplicate key name - index already exists, ignore
                    }
                }
            }
        }

        #endregion
    }
}
