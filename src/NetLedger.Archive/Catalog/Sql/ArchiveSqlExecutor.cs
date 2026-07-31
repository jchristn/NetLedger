namespace NetLedger.Archive.Catalog.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Microsoft.Data.Sqlite;
    using MySqlConnector;
    using NetLedger.Archive.Settings;
    using NetLedger.Database;
    using Npgsql;

    /// <summary>
    /// Archive-only SQL executor.
    /// </summary>
    internal sealed class ArchiveSqlExecutor : IDisposable, IAsyncDisposable
    {
        private readonly ArchiveCatalogSettings _Settings;
        private readonly string _ConnectionString;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Catalog settings.</param>
        internal ArchiveSqlExecutor(ArchiveCatalogSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _ConnectionString = BuildConnectionString(settings);
            if (settings.Type == DatabaseTypeEnum.Sqlite)
            {
                SQLitePCL.Batteries_V2.Init();
            }
        }

        /// <summary>
        /// Execute one query.
        /// </summary>
        /// <param name="query">Query.</param>
        /// <param name="isWrite">Whether this is a write.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Data table.</returns>
        internal async Task<DataTable> ExecuteAsync(string query, bool isWrite, CancellationToken token = default)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(ArchiveSqlExecutor));
            if (String.IsNullOrWhiteSpace(query)) throw new ArgumentNullException(nameof(query));

            token.ThrowIfCancellationRequested();

            using (DbConnection connection = CreateConnection())
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    command.CommandTimeout = _Settings.ConnectionTimeoutSeconds;

                    if (isWrite)
                    {
                        int affected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        DataTable affectedTable = new DataTable();
                        affectedTable.Columns.Add("affected", typeof(string));
                        DataRow affectedRow = affectedTable.NewRow();
                        affectedRow["affected"] = affected.ToString();
                        affectedTable.Rows.Add(affectedRow);
                        return affectedTable;
                    }

                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        return await ReaderToTableAsync(reader, token).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Execute multiple queries.
        /// </summary>
        /// <param name="queries">Queries.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Data table from last query.</returns>
        internal async Task<DataTable> ExecuteManyAsync(IEnumerable<string> queries, CancellationToken token = default)
        {
            if (queries == null) throw new ArgumentNullException(nameof(queries));

            DataTable result = new DataTable();
            foreach (string query in queries)
            {
                if (String.IsNullOrWhiteSpace(query)) continue;
                try
                {
                    result = await ExecuteAsync(query, true, token).ConfigureAwait(false);
                }
                catch (Exception e) when (IsIgnorableSetupException(query, e))
                {
                    result = new DataTable();
                }
            }

            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            _Disposed = true;
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }

        private DbConnection CreateConnection()
        {
            return _Settings.Type switch
            {
                DatabaseTypeEnum.Mysql => new MySqlConnection(_ConnectionString),
                DatabaseTypeEnum.Postgresql => new NpgsqlConnection(_ConnectionString),
                DatabaseTypeEnum.SqlServer => new SqlConnection(_ConnectionString),
                _ => new SqliteConnection(_ConnectionString)
            };
        }

        private static bool IsIgnorableSetupException(string query, Exception e)
        {
            if (String.IsNullOrWhiteSpace(query) || e == null) return false;
            string normalized = query.TrimStart();
            if (!normalized.StartsWith("CREATE INDEX ", StringComparison.OrdinalIgnoreCase)) return false;

            string message = e.Message ?? String.Empty;
            return message.IndexOf("Duplicate key name", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async Task<DataTable> ReaderToTableAsync(DbDataReader reader, CancellationToken token)
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
                    if (!await reader.IsDBNullAsync(i, token).ConfigureAwait(false))
                    {
                        row[i] = reader.GetValue(i)?.ToString() ?? String.Empty;
                    }
                }
                table.Rows.Add(row);
            }

            return table;
        }

        private static string BuildConnectionString(ArchiveCatalogSettings settings)
        {
            switch (settings.Type)
            {
                case DatabaseTypeEnum.Mysql:
                    MySqlConnectionStringBuilder mysql = new MySqlConnectionStringBuilder
                    {
                        Server = settings.Hostname,
                        Port = (uint)settings.GetEffectivePort(),
                        Database = settings.DatabaseName,
                        UserID = settings.Username ?? String.Empty,
                        Password = settings.Password ?? String.Empty,
                        SslMode = settings.RequireEncryption ? MySqlSslMode.Required : MySqlSslMode.Preferred,
                        ConnectionTimeout = (uint)settings.ConnectionTimeoutSeconds,
                        Pooling = true,
                        MaximumPoolSize = (uint)settings.MaxPoolSize,
                        MinimumPoolSize = 1
                    };
                    return mysql.ConnectionString;

                case DatabaseTypeEnum.Postgresql:
                    NpgsqlConnectionStringBuilder postgres = new NpgsqlConnectionStringBuilder
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
                        SearchPath = settings.Schema
                    };
                    return postgres.ConnectionString;

                case DatabaseTypeEnum.SqlServer:
                    SqlConnectionStringBuilder sqlServer = new SqlConnectionStringBuilder
                    {
                        DataSource = String.IsNullOrWhiteSpace(settings.Instance)
                            ? settings.Hostname + "," + settings.GetEffectivePort()
                            : settings.Hostname + "\\" + settings.Instance,
                        InitialCatalog = settings.DatabaseName,
                        UserID = settings.Username ?? String.Empty,
                        Password = settings.Password ?? String.Empty,
                        Encrypt = settings.RequireEncryption,
                        TrustServerCertificate = !settings.RequireEncryption,
                        ConnectTimeout = settings.ConnectionTimeoutSeconds,
                        Pooling = true,
                        MaxPoolSize = settings.MaxPoolSize
                    };
                    return sqlServer.ConnectionString;

                case DatabaseTypeEnum.Sqlite:
                default:
                    return "Data Source=" + settings.Filename + ";";
            }
        }
    }
}
