namespace NetLedger.Archive.Catalog.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Settings;
    using NetLedger.Database;

    /// <summary>
    /// SQL-backed archive catalog.
    /// </summary>
    public sealed class ArchiveSqlCatalog :
        IArchiveCatalog,
        IArchiveStoragePoolMethods,
        IArchiveManifestMethods,
        IArchiveObjectMethods,
        IArchiveRangeMethods,
        IArchiveRequestHistoryRangeMethods,
        IArchivedEntryMethods,
        IArchiveBalanceCheckpointMethods,
        IArchiveMigrationMethods,
        IArchiveAuditRecordMethods,
        IArchiveServerRequestHistoryMethods
    {
        private readonly ArchiveCatalogSettings _Settings;
        private readonly ArchiveSqlExecutor _Executor;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Catalog settings.</param>
        public ArchiveSqlCatalog(ArchiveCatalogSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Executor = new ArchiveSqlExecutor(settings);
        }

        /// <inheritdoc />
        public IArchiveStoragePoolMethods StoragePools
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchiveManifestMethods Manifests
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchiveObjectMethods Objects
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchiveRangeMethods Ranges
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchiveRequestHistoryRangeMethods RequestHistoryRanges
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchivedEntryMethods Entries
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchiveBalanceCheckpointMethods BalanceCheckpoints
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchiveMigrationMethods Migrations
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchiveAuditRecordMethods AuditRecords
        {
            get { return this; }
        }

        /// <inheritdoc />
        public IArchiveServerRequestHistoryMethods ServerRequestHistory
        {
            get { return this; }
        }

        /// <inheritdoc />
        public async Task InitializeAsync(CancellationToken token = default)
        {
            await _Executor.ExecuteManyAsync(ArchiveSqlSetupQueries.CreateTables(_Settings.Type), token).ConfigureAwait(false);
            await _Executor.ExecuteManyAsync(ArchiveSqlSetupQueries.CreateIndexes(_Settings.Type), token).ConfigureAwait(false);
            await RecordSchemaMigrationAsync(token).ConfigureAwait(false);
        }

        private async Task RecordSchemaMigrationAsync(CancellationToken token)
        {
            const string schemaVersion = "archive-catalog-v1";
            string insertColumns = "(" + Col("id") + ", " + Col("version") + ", " + Col("createdutc") + ")";
            string insertValues = "(" + Q(schemaVersion) + ", " + Q("4.0.0") + ", " + Q(Now()) + ")";
            string sql;

            switch (_Settings.Type)
            {
                case DatabaseTypeEnum.Mysql:
                    sql = "INSERT IGNORE INTO " + Table(ArchiveCatalogTables.SchemaMigrations) + " " + insertColumns + " VALUES " + insertValues + ";";
                    break;

                case DatabaseTypeEnum.Postgresql:
                    sql = "INSERT INTO " + Table(ArchiveCatalogTables.SchemaMigrations) + " " + insertColumns + " VALUES " + insertValues + " ON CONFLICT (" + Col("id") + ") DO NOTHING;";
                    break;

                case DatabaseTypeEnum.SqlServer:
                    sql = "IF NOT EXISTS (SELECT 1 FROM " + Table(ArchiveCatalogTables.SchemaMigrations) + " WHERE " + Col("id") + " = " + Q(schemaVersion) + ") INSERT INTO " + Table(ArchiveCatalogTables.SchemaMigrations) + " " + insertColumns + " VALUES " + insertValues + ";";
                    break;

                case DatabaseTypeEnum.Sqlite:
                default:
                    sql = "INSERT OR IGNORE INTO " + Table(ArchiveCatalogTables.SchemaMigrations) + " " + insertColumns + " VALUES " + insertValues + ";";
                    break;
            }

            await _Executor.ExecuteAsync(sql, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<ArchiveAuditRecord> CreateAsync(ArchiveAuditRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.AuditRecords) +
                " " + Cols("id", "tenantid", "principalid", "action", "targettype", "targetid", "metadata", "createdutc") + " VALUES (" +
                Q(record.Id) + ", " + Nullable(record.TenantId) + ", " + Nullable(record.PrincipalId) + ", " +
                Q(record.Action) + ", " + Q(record.TargetType) + ", " + Nullable(record.TargetId) + ", " +
                Nullable(record.Metadata) + ", " + Q(Time(record.CreatedUtc)) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return record;
        }

        /// <inheritdoc />
        public async Task<ArchiveServerRequestHistoryRecord> CreateAsync(ArchiveServerRequestHistoryRecord record, CancellationToken token = default)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.ServerRequestHistory) +
                " " + Cols("id", "tenantid", "principalid", "method", "path", "statuscode", "durationms", "createdutc") + " VALUES (" +
                Q(record.Id) + ", " + Nullable(record.TenantId) + ", " + Nullable(record.PrincipalId) + ", " +
                Q(record.Method) + ", " + Q(record.Path) + ", " + record.StatusCode.ToString(CultureInfo.InvariantCulture) + ", " +
                record.DurationMs.ToString(CultureInfo.InvariantCulture) + ", " + Q(Time(record.CreatedUtc)) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return record;
        }

        /// <inheritdoc />
        async Task<EnumerationResult<ArchiveServerRequestHistoryRecord>> IArchiveServerRequestHistoryMethods.EnumerateAsync(RequestHistoryFilter filter, CancellationToken token)
        {
            filter ??= new RequestHistoryFilter();
            string where = ServerRequestHistoryWhere(filter);
            EnumerationResult<ArchiveServerRequestHistoryRecord> result = new EnumerationResult<ArchiveServerRequestHistoryRecord>
            {
                MaxResults = filter.MaxResults,
                Skip = filter.Skip,
                Objects = new List<ArchiveServerRequestHistoryRecord>()
            };
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.ServerRequestHistory, where, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.ServerRequestHistory) + Where(where) +
                " ORDER BY " + Col("createdutc") + " DESC, " + Col("id") + " DESC" +
                ArchiveSqlDialect.LimitOffset(_Settings.Type, filter.MaxResults, filter.Skip) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), ServerRequestHistoryFromRow);
            result.RecordsRemaining = Math.Max(0, result.TotalRecords - filter.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
            return result;
        }

        /// <inheritdoc />
        async Task<ArchiveServerRequestHistoryRecord?> IArchiveServerRequestHistoryMethods.ReadAsync(string? tenantId, string id, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string where = Col("id") + " = " + Q(id);
            if (!String.IsNullOrWhiteSpace(tenantId))
            {
                where += " AND " + Col("tenantid") + " = " + Q(tenantId);
            }

            string sql = ArchiveSqlDialect.SelectOne(_Settings.Type, ArchiveCatalogTables.ServerRequestHistory) + Where(where) +
                ArchiveSqlDialect.SelectOneSuffix(_Settings.Type) + ";";
            DataTable table = await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false);
            return table.Rows.Count == 0 ? null : ServerRequestHistoryFromRow(table.Rows[0]);
        }

        /// <inheritdoc />
        async Task<RequestHistorySummary> IArchiveServerRequestHistoryMethods.SummarizeAsync(RequestHistoryFilter filter, CancellationToken token)
        {
            filter ??= new RequestHistoryFilter();
            string where = ServerRequestHistoryWhere(filter);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.ServerRequestHistory) + Where(where) +
                " ORDER BY " + Col("createdutc") + " ASC, " + Col("id") + " ASC;";
            DataTable table = await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false);
            List<ArchiveServerRequestHistoryRecord> records = Rows(table, ServerRequestHistoryFromRow);
            return BuildServerRequestHistorySummary(records, filter);
        }

        /// <inheritdoc />
        public async Task<ArchiveStoragePool> UpsertAsync(ArchiveStoragePool pool, CancellationToken token = default)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            ArchiveStoragePool? existing = await ReadStoragePoolByIdAsync(pool.Id, token).ConfigureAwait(false);

            if (existing == null)
            {
                string query = "INSERT INTO " + Table(ArchiveCatalogTables.StoragePools) +
                    " " + Cols("id", "name", "type", "basepath", "bucket", "prefix", "format", "compression", "createdutc", "lastupdateutc") + " VALUES (" +
                    Q(pool.Id) + ", " + Q(pool.Name) + ", " + Q(pool.Type.ToString()) + ", " + Nullable(pool.BasePath) + ", " +
                    Nullable(pool.Bucket) + ", " + Q(pool.Prefix) + ", " + Q(pool.Format.ToString()) + ", " +
                    Q(pool.Compression.ToString()) + ", " + Q(Now()) + ", " + Q(Now()) + ");";
                await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
                return pool;
            }

            string update = "UPDATE " + Table(ArchiveCatalogTables.StoragePools) + " SET " +
                Col("name") + " = " + Q(pool.Name) + ", " +
                Col("type") + " = " + Q(pool.Type.ToString()) + ", " +
                Col("basepath") + " = " + Nullable(pool.BasePath) + ", " +
                Col("bucket") + " = " + Nullable(pool.Bucket) + ", " +
                Col("prefix") + " = " + Q(pool.Prefix) + ", " +
                Col("format") + " = " + Q(pool.Format.ToString()) + ", " +
                Col("compression") + " = " + Q(pool.Compression.ToString()) + ", " +
                Col("lastupdateutc") + " = " + Q(Now()) + " " +
                "WHERE " + Col("id") + " = " + Q(pool.Id) + ";";
            await _Executor.ExecuteAsync(update, true, token).ConfigureAwait(false);
            return pool;
        }

        /// <inheritdoc />
        async Task<ArchiveStoragePool?> IArchiveStoragePoolMethods.ReadByIdAsync(string id, CancellationToken token)
        {
            return await ReadStoragePoolByIdAsync(id, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ArchiveStoragePool>> EnumerateAsync(ArchiveQuery query, CancellationToken token = default)
        {
            query ??= new ArchiveQuery();
            EnumerationResult<ArchiveStoragePool> result = NewResult<ArchiveStoragePool>(query);
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.StoragePools, null, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.StoragePools) + " ORDER BY " + Col("name") + Limit(query) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), StoragePoolFromRow);
            Complete(result, query);
            return result;
        }

        /// <inheritdoc />
        public async Task<ArchiveManifest> CreateAsync(ArchiveManifest manifest, CancellationToken token = default)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.Manifests) +
                " " + Cols("id", "tenantid", "accountid", "migrationid", "entitytype", "storagepoolid", "fromutc", "toutc", "rowcount", "credittotal", "debittotal", "contenthashsha256", "manifesthashsha256", "status", "createdutc", "lastupdateutc") + " VALUES (" +
                Q(manifest.Id) + ", " + Q(manifest.TenantId) + ", " + Nullable(manifest.AccountId) + ", " + Nullable(manifest.MigrationId) + ", " +
                Q(manifest.EntityType.ToString()) + ", " + Q(manifest.StoragePoolId) + ", " + Q(Time(manifest.FromUtc)) + ", " + Q(Time(manifest.ToUtc)) + ", " +
                manifest.RowCount.ToString(CultureInfo.InvariantCulture) + ", " + manifest.CreditTotal.ToString(CultureInfo.InvariantCulture) + ", " +
                manifest.DebitTotal.ToString(CultureInfo.InvariantCulture) + ", " + Q(manifest.ContentHashSha256) + ", " + Q(manifest.ManifestHashSha256) + ", " +
                Q(manifest.Status.ToString()) + ", " + Q(Time(manifest.CreatedUtc)) + ", " + Q(Time(manifest.LastUpdateUtc)) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return manifest;
        }

        /// <inheritdoc />
        async Task<ArchiveManifest?> IArchiveManifestMethods.ReadByIdAsync(string id, CancellationToken token)
        {
            DataTable table = await SelectByIdAsync(ArchiveCatalogTables.Manifests, id, token).ConfigureAwait(false);
            return table.Rows.Count == 0 ? null : ManifestFromRow(table.Rows[0]);
        }

        /// <inheritdoc />
        async Task<EnumerationResult<ArchiveManifest>> IArchiveManifestMethods.EnumerateAsync(ArchiveQuery query, CancellationToken token)
        {
            query ??= new ArchiveQuery();
            string where = ManifestWhere(query);
            EnumerationResult<ArchiveManifest> result = NewResult<ArchiveManifest>(query);
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.Manifests, where, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.Manifests) + Where(where) +
                " ORDER BY " + Col("fromutc") + " DESC, " + Col("id") + " DESC" + Limit(query) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), ManifestFromRow);
            Complete(result, query);
            return result;
        }

        /// <inheritdoc />
        public async Task<ArchiveManifest> UpdateStatusAsync(string id, ArchiveManifestStatus status, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string update = "UPDATE " + Table(ArchiveCatalogTables.Manifests) + " SET " +
                Col("status") + " = " + Q(status.ToString()) + ", " +
                Col("lastupdateutc") + " = " + Q(Now()) + " WHERE " + Col("id") + " = " + Q(id) + ";";
            await _Executor.ExecuteAsync(update, true, token).ConfigureAwait(false);
            ArchiveManifest? manifest = await ((IArchiveManifestMethods)this).ReadByIdAsync(id, token).ConfigureAwait(false);
            return manifest ?? throw new InvalidOperationException("Archive manifest was not found.");
        }

        /// <inheritdoc />
        public async Task<ArchiveObject> CreateAsync(ArchiveObject archiveObject, CancellationToken token = default)
        {
            if (archiveObject == null) throw new ArgumentNullException(nameof(archiveObject));
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.Objects) +
                " " + Cols("id", "manifestid", "storagepoolid", "relativepath", "rowcount", "bytecount", "contenthashsha256", "createdutc") + " VALUES (" +
                Q(archiveObject.Id) + ", " + Q(archiveObject.ManifestId) + ", " + Q(archiveObject.StoragePoolId) + ", " +
                Q(archiveObject.RelativePath) + ", " + archiveObject.RowCount.ToString(CultureInfo.InvariantCulture) + ", " +
                archiveObject.ByteCount.ToString(CultureInfo.InvariantCulture) + ", " + Q(archiveObject.ContentHashSha256) + ", " +
                Q(Time(archiveObject.CreatedUtc)) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return archiveObject;
        }

        /// <inheritdoc />
        async Task<ArchiveObject?> IArchiveObjectMethods.ReadByIdAsync(string id, CancellationToken token)
        {
            DataTable table = await SelectByIdAsync(ArchiveCatalogTables.Objects, id, token).ConfigureAwait(false);
            return table.Rows.Count == 0 ? null : ObjectFromRow(table.Rows[0]);
        }

        /// <inheritdoc />
        async Task<EnumerationResult<ArchiveObject>> IArchiveObjectMethods.EnumerateByManifestAsync(string manifestId, ArchiveQuery query, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(manifestId)) throw new ArgumentNullException(nameof(manifestId));
            query ??= new ArchiveQuery();
            string where = Col("manifestid") + " = " + Q(manifestId);
            EnumerationResult<ArchiveObject> result = NewResult<ArchiveObject>(query);
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.Objects, where, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.Objects) + Where(where) +
                " ORDER BY " + Col("createdutc") + " ASC, " + Col("id") + " ASC" + Limit(query) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), ObjectFromRow);
            Complete(result, query);
            return result;
        }

        /// <inheritdoc />
        public async Task<ArchiveRangeInfo> CreateAsync(ArchiveRangeInfo range, CancellationToken token = default)
        {
            if (range == null) throw new ArgumentNullException(nameof(range));
            string id = ArchiveId.Generate(ArchiveIdentifierPrefixes.Range);
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.AccountRanges) +
                " " + Cols("id", "tenantid", "accountid", "manifestid", "entitytype", "fromutc", "toutc", "rowcount") + " VALUES (" +
                Q(id) + ", " + Q(range.TenantId) + ", " + Nullable(range.AccountId) + ", " + Nullable(range.ManifestId) + ", " +
                Q(range.EntityType.ToString()) + ", " + Q(Time(range.FromUtc)) + ", " + Q(Time(range.ToUtc)) + ", " +
                range.RowCount.ToString(CultureInfo.InvariantCulture) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return range;
        }

        /// <inheritdoc />
        public async Task<ArchiveRequestHistoryRange> CreateAsync(ArchiveRequestHistoryRange range, CancellationToken token = default)
        {
            if (range == null) throw new ArgumentNullException(nameof(range));
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.RequestHistoryRanges) +
                " " + Cols("id", "tenantid", "manifestid", "fromutc", "toutc", "rowcount", "methodcountsjson", "statuscodecountsjson", "createdutc") + " VALUES (" +
                Q(range.Id) + ", " + Nullable(range.TenantId) + ", " + Q(range.ManifestId) + ", " +
                Q(Time(range.FromUtc)) + ", " + Q(Time(range.ToUtc)) + ", " +
                range.RowCount.ToString(CultureInfo.InvariantCulture) + ", " + Nullable(range.MethodCountsJson) + ", " +
                Nullable(range.StatusCodeCountsJson) + ", " + Q(Time(range.CreatedUtc)) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return range;
        }

        /// <inheritdoc />
        async Task<EnumerationResult<ArchiveRequestHistoryRange>> IArchiveRequestHistoryRangeMethods.EnumerateAsync(ArchiveQuery query, CancellationToken token)
        {
            query ??= new ArchiveQuery();
            string where = RequestHistoryRangeWhere(query);
            EnumerationResult<ArchiveRequestHistoryRange> result = NewResult<ArchiveRequestHistoryRange>(query);
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.RequestHistoryRanges, where, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.RequestHistoryRanges) + Where(where) +
                " ORDER BY " + Col("fromutc") + " DESC, " + Col("tenantid") + " ASC" + Limit(query) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), RequestHistoryRangeFromRow);
            Complete(result, query);
            return result;
        }

        /// <inheritdoc />
        async Task<EnumerationResult<ArchiveRangeInfo>> IArchiveRangeMethods.EnumerateAsync(ArchiveQuery query, CancellationToken token)
        {
            query ??= new ArchiveQuery();
            string where = RangeWhere(query);
            EnumerationResult<ArchiveRangeInfo> result = NewResult<ArchiveRangeInfo>(query);
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.AccountRanges, where, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.AccountRanges) + Where(where) +
                " ORDER BY " + Col("fromutc") + " DESC, " + Col("tenantid") + " ASC" + Limit(query) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), RangeFromRow);
            Complete(result, query);
            return result;
        }

        /// <inheritdoc />
        public Task<EnumerationResult<Entry>> EnumerateAsync(string tenantId, string accountId, EnumerationQuery query, CancellationToken token = default)
        {
            query ??= new EnumerationQuery();
            EnumerationResult<Entry> result = new EnumerationResult<Entry>
            {
                MaxResults = query.MaxResults,
                Skip = query.Skip,
                TotalRecords = 0,
                RecordsRemaining = 0,
                EndOfResults = true,
                Objects = new List<Entry>()
            };
            return Task.FromResult(result);
        }

        /// <inheritdoc />
        public async Task<ArchiveBalanceCheckpoint> CreateAsync(ArchiveBalanceCheckpoint checkpoint, CancellationToken token = default)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.BalanceCheckpoints) +
                " " + Cols("id", "tenantid", "accountid", "manifestid", "asofutc", "balance", "createdutc") + " VALUES (" +
                Q(checkpoint.Id) + ", " + Q(checkpoint.TenantId) + ", " + Q(checkpoint.AccountId) + ", " +
                Q(checkpoint.ManifestId) + ", " + Q(Time(checkpoint.AsOfUtc)) + ", " +
                checkpoint.Balance.ToString(CultureInfo.InvariantCulture) + ", " + Q(Time(checkpoint.CreatedUtc)) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return checkpoint;
        }

        /// <inheritdoc />
        public async Task<ArchiveBalanceCheckpoint?> ReadAsOfAsync(string tenantId, string accountId, DateTime asOfUtc, CancellationToken token = default)
        {
            string where = Col("tenantid") + " = " + Q(tenantId) + " AND " + Col("accountid") + " = " + Q(accountId) +
                " AND " + Col("asofutc") + " <= " + Q(Time(asOfUtc));
            string sql = ArchiveSqlDialect.SelectOne(_Settings.Type, ArchiveCatalogTables.BalanceCheckpoints) + Where(where) +
                " ORDER BY " + Col("asofutc") + " DESC" + ArchiveSqlDialect.SelectOneSuffix(_Settings.Type) + ";";
            DataTable table = await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false);
            return table.Rows.Count == 0 ? null : CheckpointFromRow(table.Rows[0]);
        }

        /// <inheritdoc />
        async Task<EnumerationResult<ArchiveBalanceCheckpoint>> IArchiveBalanceCheckpointMethods.EnumerateByManifestAsync(string manifestId, ArchiveQuery query, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(manifestId)) throw new ArgumentNullException(nameof(manifestId));
            query ??= new ArchiveQuery();
            string where = Col("manifestid") + " = " + Q(manifestId);
            EnumerationResult<ArchiveBalanceCheckpoint> result = NewResult<ArchiveBalanceCheckpoint>(query);
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.BalanceCheckpoints, where, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.BalanceCheckpoints) + Where(where) +
                " ORDER BY " + Col("asofutc") + " ASC" + Limit(query) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), CheckpointFromRow);
            Complete(result, query);
            return result;
        }

        /// <inheritdoc />
        public async Task<ArchiveMigration> CreateAsync(ArchiveMigration migration, CancellationToken token = default)
        {
            if (migration == null) throw new ArgumentNullException(nameof(migration));
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.Migrations) +
                " " + Cols("id", "tenantid", "accountid", "entitytype", "storagepoolid", "format", "compression", "fromutc", "toutc", "status", "idempotencykey", "createdutc", "lastupdateutc") + " VALUES (" +
                Q(migration.Id) + ", " + Q(migration.TenantId) + ", " + Nullable(migration.AccountId) + ", " +
                Q(migration.EntityType.ToString()) + ", " + Q(migration.StoragePoolId) + ", " + Q(migration.Format.ToString()) + ", " +
                Q(migration.Compression.ToString()) + ", " + Q(Time(migration.FromUtc)) + ", " + Q(Time(migration.ToUtc)) + ", " +
                Q(migration.Status.ToString()) + ", " +
                Q(migration.IdempotencyKey) + ", " + Q(Time(migration.CreatedUtc)) + ", " + Q(Time(migration.LastUpdateUtc)) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return migration;
        }

        /// <inheritdoc />
        async Task<ArchiveMigration?> IArchiveMigrationMethods.ReadByIdAsync(string id, CancellationToken token)
        {
            DataTable table = await SelectByIdAsync(ArchiveCatalogTables.Migrations, id, token).ConfigureAwait(false);
            return table.Rows.Count == 0 ? null : MigrationFromRow(table.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<ArchiveMigration?> ReadByIdempotencyKeyAsync(string idempotencyKey, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentNullException(nameof(idempotencyKey));
            string where = Col("idempotencykey") + " = " + Q(idempotencyKey);
            string sql = ArchiveSqlDialect.SelectOne(_Settings.Type, ArchiveCatalogTables.Migrations) + Where(where) +
                ArchiveSqlDialect.SelectOneSuffix(_Settings.Type) + ";";
            DataTable table = await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false);
            return table.Rows.Count == 0 ? null : MigrationFromRow(table.Rows[0]);
        }

        /// <inheritdoc />
        async Task<EnumerationResult<ArchiveMigration>> IArchiveMigrationMethods.EnumerateAsync(ArchiveQuery query, CancellationToken token)
        {
            query ??= new ArchiveQuery();
            string where = MigrationWhere(query);
            EnumerationResult<ArchiveMigration> result = NewResult<ArchiveMigration>(query);
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.Migrations, where, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.Migrations) + Where(where) +
                " ORDER BY " + Col("createdutc") + " DESC, " + Col("id") + " DESC" + Limit(query) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), MigrationFromRow);
            Complete(result, query);
            return result;
        }

        /// <inheritdoc />
        public async Task<ArchiveMigration> UpdateStatusAsync(string id, ArchiveMigrationStatus status, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            string update = "UPDATE " + Table(ArchiveCatalogTables.Migrations) + " SET " +
                Col("status") + " = " + Q(status.ToString()) + ", " +
                Col("lastupdateutc") + " = " + Q(Now()) + " WHERE " + Col("id") + " = " + Q(id) + ";";
            await _Executor.ExecuteAsync(update, true, token).ConfigureAwait(false);
            ArchiveMigration? migration = await ((IArchiveMigrationMethods)this).ReadByIdAsync(id, token).ConfigureAwait(false);
            return migration ?? throw new InvalidOperationException("Archive migration was not found.");
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationBatch> CreateBatchAsync(ArchiveMigrationBatch batch, CancellationToken token = default)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            string query = "INSERT INTO " + Table(ArchiveCatalogTables.MigrationBatches) +
                " " + Cols("id", "migrationid", "tenantid", "accountid", "storagepoolid", "sequencenumber", "rowcount", "bytecount", "contenthashsha256", "temporaryrelativepath", "committedrelativepath", "status", "createdutc", "lastupdateutc") + " VALUES (" +
                Q(batch.Id) + ", " + Q(batch.MigrationId) + ", " + Q(batch.TenantId) + ", " + Nullable(batch.AccountId) + ", " +
                Q(batch.StoragePoolId) + ", " + batch.SequenceNumber.ToString(CultureInfo.InvariantCulture) + ", " +
                batch.RowCount.ToString(CultureInfo.InvariantCulture) + ", " + batch.ByteCount.ToString(CultureInfo.InvariantCulture) + ", " +
                Q(batch.ContentHashSha256) + ", " + Q(batch.TemporaryRelativePath) + ", " + Q(batch.CommittedRelativePath) + ", " +
                Q(batch.Status.ToString()) + ", " + Q(Time(batch.CreatedUtc)) + ", " + Q(Time(batch.LastUpdateUtc)) + ");";
            await _Executor.ExecuteAsync(query, true, token).ConfigureAwait(false);
            return batch;
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationBatch?> ReadBatchAsync(string migrationId, string batchId, CancellationToken token = default)
        {
            string where = Col("migrationid") + " = " + Q(migrationId) + " AND " + Col("id") + " = " + Q(batchId);
            string sql = ArchiveSqlDialect.SelectOne(_Settings.Type, ArchiveCatalogTables.MigrationBatches) + Where(where) +
                ArchiveSqlDialect.SelectOneSuffix(_Settings.Type) + ";";
            DataTable table = await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false);
            return table.Rows.Count == 0 ? null : BatchFromRow(table.Rows[0]);
        }

        /// <inheritdoc />
        public async Task<ArchiveMigrationBatch> UpdateBatchAsync(ArchiveMigrationBatch batch, CancellationToken token = default)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            string update = "UPDATE " + Table(ArchiveCatalogTables.MigrationBatches) + " SET " +
                Col("storagepoolid") + " = " + Q(batch.StoragePoolId) + ", " +
                Col("rowcount") + " = " + batch.RowCount.ToString(CultureInfo.InvariantCulture) + ", " +
                Col("bytecount") + " = " + batch.ByteCount.ToString(CultureInfo.InvariantCulture) + ", " +
                Col("contenthashsha256") + " = " + Q(batch.ContentHashSha256) + ", " +
                Col("temporaryrelativepath") + " = " + Q(batch.TemporaryRelativePath) + ", " +
                Col("committedrelativepath") + " = " + Q(batch.CommittedRelativePath) + ", " +
                Col("status") + " = " + Q(batch.Status.ToString()) + ", " +
                Col("lastupdateutc") + " = " + Q(Now()) + " WHERE " + Col("id") + " = " + Q(batch.Id) + ";";
            await _Executor.ExecuteAsync(update, true, token).ConfigureAwait(false);
            return batch;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult<ArchiveMigrationBatch>> EnumerateBatchesAsync(string migrationId, ArchiveQuery query, CancellationToken token = default)
        {
            query ??= new ArchiveQuery();
            string where = Col("migrationid") + " = " + Q(migrationId);
            EnumerationResult<ArchiveMigrationBatch> result = NewResult<ArchiveMigrationBatch>(query);
            result.TotalRecords = await CountAsync(ArchiveCatalogTables.MigrationBatches, where, token).ConfigureAwait(false);
            string sql = "SELECT * FROM " + Table(ArchiveCatalogTables.MigrationBatches) + Where(where) +
                " ORDER BY " + Col("sequencenumber") + " ASC, " + Col("id") + " ASC" + Limit(query) + ";";
            result.Objects = Rows(await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false), BatchFromRow);
            Complete(result, query);
            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_Disposed)
            {
                _Executor.Dispose();
                _Disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (!_Disposed)
            {
                await _Executor.DisposeAsync().ConfigureAwait(false);
                _Disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        private async Task<ArchiveStoragePool?> ReadStoragePoolByIdAsync(string id, CancellationToken token)
        {
            DataTable table = await SelectByIdAsync(ArchiveCatalogTables.StoragePools, id, token).ConfigureAwait(false);
            return table.Rows.Count == 0 ? null : StoragePoolFromRow(table.Rows[0]);
        }

        private async Task<DataTable> SelectByIdAsync(string table, string id, CancellationToken token)
        {
            string sql = ArchiveSqlDialect.SelectOne(_Settings.Type, table) + " WHERE " + Col("id") + " = " + Q(id) +
                ArchiveSqlDialect.SelectOneSuffix(_Settings.Type) + ";";
            return await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false);
        }

        private async Task<long> CountAsync(string table, string? where, CancellationToken token)
        {
            string sql = "SELECT COUNT(*) FROM " + Table(table) + Where(where) + ";";
            DataTable result = await _Executor.ExecuteAsync(sql, false, token).ConfigureAwait(false);
            return result.Rows.Count == 0 ? 0 : ToLong(result.Rows[0][0]);
        }

        private string ManifestWhere(ArchiveQuery query)
        {
            List<string> conditions = CommonWhere(query);
            if (query.StoragePoolId != null) conditions.Add(Col("storagepoolid") + " = " + Q(query.StoragePoolId));
            if (query.MigrationId != null) conditions.Add(Col("migrationid") + " = " + Q(query.MigrationId));
            if (query.ManifestStatus.HasValue) conditions.Add(Col("status") + " = " + Q(query.ManifestStatus.Value.ToString()));
            return String.Join(" AND ", conditions);
        }

        private string RangeWhere(ArchiveQuery query)
        {
            return String.Join(" AND ", CommonWhere(query));
        }

        private string RequestHistoryRangeWhere(ArchiveQuery query)
        {
            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(query.TenantId)) conditions.Add(Col("tenantid") + " = " + Q(query.TenantId));
            if (!String.IsNullOrEmpty(query.MigrationId)) conditions.Add(Col("manifestid") + " = " + Q(query.MigrationId));
            if (query.FromUtc.HasValue) conditions.Add(Col("toutc") + " >= " + Q(Time(query.FromUtc.Value)));
            if (query.ToUtc.HasValue) conditions.Add(Col("fromutc") + " <= " + Q(Time(query.ToUtc.Value)));
            if (!String.IsNullOrEmpty(query.Search)) conditions.Add("(" + Col("id") + " LIKE " + Q("%" + query.Search + "%") + " OR " + Col("manifestid") + " LIKE " + Q("%" + query.Search + "%") + ")");
            return String.Join(" AND ", conditions);
        }

        private string MigrationWhere(ArchiveQuery query)
        {
            List<string> conditions = CommonWhere(query);
            if (query.MigrationStatus.HasValue) conditions.Add(Col("status") + " = " + Q(query.MigrationStatus.Value.ToString()));
            return String.Join(" AND ", conditions);
        }

        private string ServerRequestHistoryWhere(RequestHistoryFilter filter)
        {
            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(filter.TenantId)) conditions.Add(Col("tenantid") + " = " + Q(filter.TenantId));
            if (!String.IsNullOrEmpty(filter.PrincipalId)) conditions.Add(Col("principalid") + " = " + Q(filter.PrincipalId));
            if (!String.IsNullOrEmpty(filter.Method)) conditions.Add(Col("method") + " = " + Q(filter.Method));
            if (filter.StatusCode.HasValue) conditions.Add(Col("statuscode") + " = " + filter.StatusCode.Value.ToString(CultureInfo.InvariantCulture));
            if (!String.IsNullOrEmpty(filter.PathContains)) conditions.Add(Col("path") + " LIKE " + Q("%" + filter.PathContains + "%"));
            if (filter.FromUtc.HasValue) conditions.Add(Col("createdutc") + " >= " + Q(Time(filter.FromUtc.Value)));
            if (filter.ToUtc.HasValue) conditions.Add(Col("createdutc") + " <= " + Q(Time(filter.ToUtc.Value)));
            return String.Join(" AND ", conditions);
        }

        private List<string> CommonWhere(ArchiveQuery query)
        {
            List<string> conditions = new List<string>();
            if (!String.IsNullOrEmpty(query.TenantId)) conditions.Add(Col("tenantid") + " = " + Q(query.TenantId));
            if (!String.IsNullOrEmpty(query.AccountId)) conditions.Add(Col("accountid") + " = " + Q(query.AccountId));
            if (query.EntityType.HasValue) conditions.Add(Col("entitytype") + " = " + Q(query.EntityType.Value.ToString()));
            if (query.FromUtc.HasValue) conditions.Add(Col("toutc") + " >= " + Q(Time(query.FromUtc.Value)));
            if (query.ToUtc.HasValue) conditions.Add(Col("fromutc") + " <= " + Q(Time(query.ToUtc.Value)));
            if (!String.IsNullOrEmpty(query.Search)) conditions.Add("(" + Col("id") + " LIKE " + Q("%" + query.Search + "%") + ")");
            return conditions;
        }

        private static EnumerationResult<T> NewResult<T>(ArchiveQuery query)
        {
            return new EnumerationResult<T>
            {
                MaxResults = query.MaxResults,
                Skip = query.Skip,
                Objects = new List<T>()
            };
        }

        private static void Complete<T>(EnumerationResult<T> result, ArchiveQuery query)
        {
            result.RecordsRemaining = Math.Max(0, result.TotalRecords - query.Skip - result.Objects.Count);
            result.EndOfResults = result.RecordsRemaining == 0;
        }

        private static List<T> Rows<T>(DataTable table, Func<DataRow, T> mapper)
        {
            List<T> results = new List<T>();
            foreach (DataRow row in table.Rows)
            {
                results.Add(mapper(row));
            }
            return results;
        }

        private ArchiveStoragePool StoragePoolFromRow(DataRow row)
        {
            return new ArchiveStoragePool
            {
                Id = Str(row, "id"),
                Name = Str(row, "name"),
                Type = ParseEnum(Str(row, "type"), ArchiveStoragePoolType.FileSystem),
                BasePath = NullableStr(row, "basepath"),
                Bucket = NullableStr(row, "bucket"),
                Prefix = Str(row, "prefix"),
                Format = ParseEnum(Str(row, "format"), ArchiveFormat.JsonlGzip),
                Compression = ParseEnum(Str(row, "compression"), ArchiveCompression.Gzip)
            };
        }

        private ArchiveManifest ManifestFromRow(DataRow row)
        {
            return new ArchiveManifest
            {
                Id = Str(row, "id"),
                TenantId = Str(row, "tenantid"),
                AccountId = NullableStr(row, "accountid"),
                MigrationId = NullableStr(row, "migrationid"),
                EntityType = ParseEnum(Str(row, "entitytype"), ArchiveEntityType.Entries),
                StoragePoolId = Str(row, "storagepoolid"),
                FromUtc = ToDate(row, "fromutc"),
                ToUtc = ToDate(row, "toutc"),
                RowCount = ToLong(row["rowcount"]),
                CreditTotal = ToDecimal(row, "credittotal"),
                DebitTotal = ToDecimal(row, "debittotal"),
                ContentHashSha256 = Str(row, "contenthashsha256"),
                ManifestHashSha256 = Str(row, "manifesthashsha256"),
                Status = ParseEnum(Str(row, "status"), ArchiveManifestStatus.Committed),
                CreatedUtc = ToDate(row, "createdutc"),
                LastUpdateUtc = ToDate(row, "lastupdateutc")
            };
        }

        private ArchiveObject ObjectFromRow(DataRow row)
        {
            return new ArchiveObject
            {
                Id = Str(row, "id"),
                ManifestId = Str(row, "manifestid"),
                StoragePoolId = Str(row, "storagepoolid"),
                RelativePath = Str(row, "relativepath"),
                RowCount = ToLong(row["rowcount"]),
                ByteCount = ToLong(row["bytecount"]),
                ContentHashSha256 = Str(row, "contenthashsha256"),
                CreatedUtc = ToDate(row, "createdutc")
            };
        }

        private ArchiveRangeInfo RangeFromRow(DataRow row)
        {
            return new ArchiveRangeInfo
            {
                TenantId = Str(row, "tenantid"),
                AccountId = NullableStr(row, "accountid"),
                ManifestId = NullableStr(row, "manifestid"),
                EntityType = ParseEnum(Str(row, "entitytype"), ArchiveEntityType.Entries),
                FromUtc = ToDate(row, "fromutc"),
                ToUtc = ToDate(row, "toutc"),
                RowCount = ToLong(row["rowcount"])
            };
        }

        private ArchiveRequestHistoryRange RequestHistoryRangeFromRow(DataRow row)
        {
            return new ArchiveRequestHistoryRange
            {
                Id = Str(row, "id"),
                TenantId = NullableStr(row, "tenantid"),
                ManifestId = Str(row, "manifestid"),
                FromUtc = ToDate(row, "fromutc"),
                ToUtc = ToDate(row, "toutc"),
                RowCount = ToLong(row["rowcount"]),
                MethodCountsJson = NullableStr(row, "methodcountsjson"),
                StatusCodeCountsJson = NullableStr(row, "statuscodecountsjson"),
                CreatedUtc = ToDate(row, "createdutc")
            };
        }

        private ArchiveBalanceCheckpoint CheckpointFromRow(DataRow row)
        {
            return new ArchiveBalanceCheckpoint
            {
                Id = Str(row, "id"),
                TenantId = Str(row, "tenantid"),
                AccountId = Str(row, "accountid"),
                ManifestId = Str(row, "manifestid"),
                AsOfUtc = ToDate(row, "asofutc"),
                Balance = ToDecimal(row, "balance"),
                CreatedUtc = ToDate(row, "createdutc")
            };
        }

        private ArchiveServerRequestHistoryRecord ServerRequestHistoryFromRow(DataRow row)
        {
            return new ArchiveServerRequestHistoryRecord
            {
                Id = Str(row, "id"),
                TenantId = NullableStr(row, "tenantid"),
                PrincipalId = NullableStr(row, "principalid"),
                Method = Str(row, "method"),
                Path = Str(row, "path"),
                StatusCode = (int)ToLong(row["statuscode"]),
                DurationMs = ToDecimal(row, "durationms"),
                CreatedUtc = ToDate(row, "createdutc")
            };
        }

        private ArchiveMigration MigrationFromRow(DataRow row)
        {
            return new ArchiveMigration
            {
                Id = Str(row, "id"),
                TenantId = Str(row, "tenantid"),
                AccountId = NullableStr(row, "accountid"),
                EntityType = ParseEnum(Str(row, "entitytype"), ArchiveEntityType.Entries),
                StoragePoolId = Str(row, "storagepoolid"),
                Format = ParseEnum(Str(row, "format"), ArchiveFormat.JsonlGzip),
                Compression = ParseEnum(Str(row, "compression"), ArchiveCompression.Gzip),
                FromUtc = ToDate(row, "fromutc"),
                ToUtc = ToDate(row, "toutc"),
                Status = ParseEnum(Str(row, "status"), ArchiveMigrationStatus.Pending),
                IdempotencyKey = Str(row, "idempotencykey"),
                CreatedUtc = ToDate(row, "createdutc"),
                LastUpdateUtc = ToDate(row, "lastupdateutc")
            };
        }

        private ArchiveMigrationBatch BatchFromRow(DataRow row)
        {
            return new ArchiveMigrationBatch
            {
                Id = Str(row, "id"),
                MigrationId = Str(row, "migrationid"),
                TenantId = Str(row, "tenantid"),
                AccountId = NullableStr(row, "accountid"),
                StoragePoolId = Str(row, "storagepoolid"),
                SequenceNumber = ToLong(row["sequencenumber"]),
                RowCount = ToLong(row["rowcount"]),
                ByteCount = ToLong(row["bytecount"]),
                ContentHashSha256 = Str(row, "contenthashsha256"),
                TemporaryRelativePath = Str(row, "temporaryrelativepath"),
                CommittedRelativePath = Str(row, "committedrelativepath"),
                Status = ParseEnum(Str(row, "status"), ArchiveMigrationBatchStatus.Pending),
                CreatedUtc = ToDate(row, "createdutc"),
                LastUpdateUtc = ToDate(row, "lastupdateutc")
            };
        }

        private string Table(string name)
        {
            return ArchiveSqlDialect.Identifier(_Settings.Type, name);
        }

        private string Col(string name)
        {
            return ArchiveSqlDialect.Identifier(_Settings.Type, name);
        }

        private string Cols(params string[] names)
        {
            return "(" + String.Join(", ", Array.ConvertAll(names, name => Col(name))) + ")";
        }

        private string Limit(ArchiveQuery query)
        {
            return ArchiveSqlDialect.LimitOffset(_Settings.Type, query.MaxResults, query.Skip);
        }

        private static string Where(string? where)
        {
            return String.IsNullOrWhiteSpace(where) ? String.Empty : " WHERE " + where;
        }

        private static string Time(DateTime value)
        {
            return ArchiveSqlDialect.Timestamp(value);
        }

        private static string Now()
        {
            return ArchiveSqlDialect.Timestamp(DateTime.UtcNow);
        }

        private static string Q(string? value)
        {
            return "'" + ArchiveSqlDialect.Sanitize(value) + "'";
        }

        private static string Nullable(string? value)
        {
            return ArchiveSqlDialect.Nullable(value);
        }

        private static string Str(DataRow row, string column)
        {
            return row.Table.Columns.Contains(column) ? row[column]?.ToString() ?? String.Empty : String.Empty;
        }

        private static string? NullableStr(DataRow row, string column)
        {
            string value = Str(row, column);
            return String.IsNullOrEmpty(value) ? null : value;
        }

        private static DateTime ToDate(DataRow row, string column)
        {
            string value = Str(row, column);
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed)
                ? parsed.ToUniversalTime()
                : DateTime.MinValue.ToUniversalTime();
        }

        private static long ToLong(object? value)
        {
            return Int64.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out long parsed) ? parsed : 0;
        }

        private static decimal ToDecimal(DataRow row, string column)
        {
            return Decimal.TryParse(Str(row, column), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : 0m;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
        }

        private static RequestHistorySummary BuildServerRequestHistorySummary(List<ArchiveServerRequestHistoryRecord> records, RequestHistoryFilter filter)
        {
            RequestHistorySummary summary = new RequestHistorySummary();
            if (records == null || records.Count == 0) return summary;

            double totalDuration = 0d;
            foreach (ArchiveServerRequestHistoryRecord record in records)
            {
                summary.TotalCount++;
                if (record.StatusCode >= 200 && record.StatusCode <= 399)
                {
                    summary.TotalSuccess++;
                }
                else
                {
                    summary.TotalFailure++;
                }

                totalDuration += (double)record.DurationMs;
            }

            summary.AverageDurationMs = summary.TotalCount == 0 ? 0d : totalDuration / summary.TotalCount;

            DateTime fromUtc = filter.FromUtc ?? records[0].CreatedUtc;
            DateTime toUtc = filter.ToUtc ?? records[records.Count - 1].CreatedUtc;
            if (toUtc < fromUtc)
            {
                DateTime swap = fromUtc;
                fromUtc = toUtc;
                toUtc = swap;
            }

            Dictionary<long, RequestHistorySummaryBucket> buckets = new Dictionary<long, RequestHistorySummaryBucket>();
            Dictionary<long, double> bucketDurations = new Dictionary<long, double>();
            Dictionary<long, long> bucketCounts = new Dictionary<long, long>();
            int bucketMinutes = filter.BucketMinutes <= 0 ? 15 : filter.BucketMinutes;
            foreach (ArchiveServerRequestHistoryRecord record in records)
            {
                long bucketTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
                long startTicks = record.CreatedUtc.Ticks - (record.CreatedUtc.Ticks % bucketTicks);
                if (!buckets.TryGetValue(startTicks, out RequestHistorySummaryBucket? bucket))
                {
                    bucket = new RequestHistorySummaryBucket
                    {
                        BucketStartUtc = new DateTime(startTicks, DateTimeKind.Utc),
                        BucketEndUtc = new DateTime(startTicks + bucketTicks, DateTimeKind.Utc)
                    };
                    buckets[startTicks] = bucket;
                    bucketDurations[startTicks] = 0d;
                    bucketCounts[startTicks] = 0;
                }

                if (record.StatusCode >= 200 && record.StatusCode <= 399)
                {
                    bucket.SuccessCount++;
                }
                else
                {
                    bucket.FailureCount++;
                }

                bucketDurations[startTicks] += (double)record.DurationMs;
                bucketCounts[startTicks]++;
            }

            List<long> keys = new List<long>(buckets.Keys);
            keys.Sort();
            foreach (long key in keys)
            {
                RequestHistorySummaryBucket bucket = buckets[key];
                long bucketCount = bucketCounts[key];
                bucket.AverageDurationMs = bucketCount == 0 ? 0d : bucketDurations[key] / bucketCount;
                summary.Buckets.Add(bucket);
            }

            return summary;
        }
    }
}
