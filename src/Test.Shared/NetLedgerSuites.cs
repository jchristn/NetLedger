namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Microsoft.Data.Sqlite;
    using MySqlConnector;
    using NetLedger;
    using NetLedger.Archive;
    using NetLedger.Archive.Catalog.Sql;
    using NetLedger.Archive.Models;
    using NetLedger.Archive.Settings;
    using NetLedger.Archive.Storage;
    using NetLedger.Database;
    using NetLedger.Server.API.Agnostic;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Models.Identity;
    using NetLedger.Server.Services;
    using NetLedger.Server.Settings;
    using Npgsql;
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// Shared Touchstone suites for NetLedger.
    /// </summary>
    public static class NetLedgerSuites
    {
        #region Private-Members

        private static readonly string _RunScope = UniqueSuffix(12);
        private static DatabaseSettings? _ConfiguredSettings = null;

        #endregion

        /// <summary>
        /// All shared suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    IdentifierSuite(),
                    ArchiveSuite(),
                    MetadataSuite(),
                    LedgerSuite(),
                    CredentialSuite(),
                    IdentitySuite(),
                    RequestHistorySuite(),
                    SecurityBoundarySuite(),
                    ProviderMatrixSuite()
                };
            }
        }

        /// <summary>
        /// Configure the database provider used by every database-backed shared test.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public static void Configure(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _ConfiguredSettings = NetLedgerTestConfiguration.CloneDatabaseSettings(settings);
        }

        /// <summary>
        /// Parse a database type name.
        /// </summary>
        /// <param name="value">Database type name.</param>
        /// <returns>Database type.</returns>
        public static DatabaseTypeEnum ParseDatabaseType(string value)
        {
            return NetLedgerTestConfiguration.ParseDatabaseType(value);
        }

        private static TestSuiteDescriptor IdentifierSuite()
        {
            string suiteId = "identifiers";
            return new TestSuiteDescriptor(
                suiteId,
                "Identifier contract",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "account_id_prefix_length", "Account IDs use acct_ and 32 characters", _ =>
                    {
                        Account account = new Account("checking");
                        Assert(account.Id.StartsWith(IdentifierPrefixes.Account, StringComparison.Ordinal), "Account ID prefix mismatch.");
                        Assert(account.Id.Length == NetLedgerId.Length, "Account ID length mismatch.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "entry_id_prefix_length", "Entry IDs use ent_ and 32 characters", _ =>
                    {
                        Entry entry = new Entry();
                        Assert(entry.Id.StartsWith(IdentifierPrefixes.Entry, StringComparison.Ordinal), "Entry ID prefix mismatch.");
                        Assert(entry.Id.Length == NetLedgerId.Length, "Entry ID length mismatch.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "ids_sort", "K-sortable IDs preserve generation order", _ =>
                    {
                        string first = NetLedgerId.Generate(IdentifierPrefixes.Entry);
                        Thread.Sleep(2);
                        string second = NetLedgerId.Generate(IdentifierPrefixes.Entry);
                        Assert(String.CompareOrdinal(first, second) < 0, "Generated IDs did not sort by generation order.");
                        return Task.CompletedTask;
                    })
                });
        }

        private static TestSuiteDescriptor ArchiveSuite()
        {
            string suiteId = "archive";
            return new TestSuiteDescriptor(
                suiteId,
                "Archive catalog and storage contracts",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "archive_id_prefix_length_sort", "Archive IDs use configured prefixes and remain K-sortable", _ =>
                    {
                        List<string> prefixes = new List<string>
                        {
                            ArchiveIdentifierPrefixes.StoragePool,
                            ArchiveIdentifierPrefixes.Migration,
                            ArchiveIdentifierPrefixes.MigrationBatch,
                            ArchiveIdentifierPrefixes.Manifest,
                            ArchiveIdentifierPrefixes.Range,
                            ArchiveIdentifierPrefixes.Object,
                            ArchiveIdentifierPrefixes.Checkpoint,
                            ArchiveIdentifierPrefixes.Audit,
                            ArchiveIdentifierPrefixes.RequestHistory,
                            ArchiveIdentifierPrefixes.ObjectLock
                        };

                        foreach (string prefix in prefixes)
                        {
                            string id = ArchiveId.Generate(prefix);
                            Assert(id.StartsWith(prefix, StringComparison.Ordinal), "Archive ID prefix mismatch for " + prefix + ".");
                            Assert(id.Length == NetLedgerId.Length, "Archive ID length mismatch for " + prefix + ".");
                        }

                        string first = ArchiveId.Generate(ArchiveIdentifierPrefixes.Manifest);
                        Thread.Sleep(2);
                        string second = ArchiveId.Generate(ArchiveIdentifierPrefixes.Manifest);
                        Assert(String.CompareOrdinal(first, second) < 0, "Archive IDs did not sort by generation order.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "archive_continuation_token_filter_hash", "Archive continuation tokens are opaque and bound to their query filters", _ =>
                    {
                        ArchiveQuery query = new ArchiveQuery
                        {
                            TenantId = "ten_archive_token",
                            AccountId = "acct_archive_token",
                            EntityType = ArchiveEntityType.Entries,
                            ManifestStatus = ArchiveManifestStatus.Committed,
                            FromUtc = new DateTime(2026, 01, 01, 00, 00, 00, DateTimeKind.Utc),
                            ToUtc = new DateTime(2026, 02, 01, 00, 00, 00, DateTimeKind.Utc),
                            Search = "invoice",
                            Ordering = EnumerationOrderEnum.AmountDescending,
                            AmountMinimum = 10m,
                            MaxResults = 25
                        };
                        query.Labels = new List<string> { "paid", "external" };
                        query.Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["source"] = "api"
                        };

                        string token = ArchiveContinuationToken.Create(query, "entries", 37);
                        Assert(!token.Contains("acct_archive_token", StringComparison.Ordinal), "Continuation token exposed raw account identifiers.");

                        query.ContinuationToken = token;
                        int cursor = ArchiveContinuationToken.ResolveRowCursor(query, "entries");
                        Assert(cursor == 37, "Continuation token did not resolve the expected row cursor.");

                        bool rejectedChangedFilter = false;
                        query.Search = "other";
                        try
                        {
                            ArchiveContinuationToken.ResolveRowCursor(query, "entries");
                        }
                        catch (InvalidDataException)
                        {
                            rejectedChangedFilter = true;
                        }

                        Assert(rejectedChangedFilter, "Continuation token was accepted after filters changed.");

                        bool rejectedSkip = false;
                        query.Search = "invoice";
                        query.Skip = 1;
                        try
                        {
                            ArchiveContinuationToken.ResolveRowCursor(query, "entries");
                        }
                        catch (InvalidDataException)
                        {
                            rejectedSkip = true;
                        }

                        Assert(rejectedSkip, "Continuation token was accepted together with skip.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "archive_runtime_accepts_jsonl_only", "Archive runtime defaults accept only JSONL.Gzip in v4", _ =>
                    {
                        ArchiveRuntimeSettings settings = new ArchiveRuntimeSettings();
                        Assert(settings.PreferredFormat == ArchiveFormat.JsonlGzip, "Archive runtime preferred format was not JSONL.Gzip.");
                        Assert(settings.AcceptedFormats.Count == 1 && settings.AcceptedFormats[0] == ArchiveFormat.JsonlGzip, "Archive runtime accepted an unsupported v4 format.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "archive_catalog_table_isolation", "Archive catalog creates only approved archive table names", async token =>
                    {
                        ArchiveCatalogSettings settings = CreateArchiveCatalogSettings();
                        HashSet<string> beforeTables = await ReadSqlTableNamesAsync(settings, token).ConfigureAwait(false);

                        await using ArchiveSqlCatalog catalog = new ArchiveSqlCatalog(settings);
                        await catalog.InitializeAsync(token).ConfigureAwait(false);

                        HashSet<string> tableNames = await ReadSqlTableNamesAsync(settings, token).ConfigureAwait(false);
                        HashSet<string> createdTables = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
                        createdTables.ExceptWith(beforeTables);

                        foreach (string tableName in createdTables)
                        {
                            Assert(ArchiveCatalogTables.IsApproved(tableName), settings.Type + " archive catalog created unexpected table name: " + tableName + ".");
                        }

                        foreach (string tableName in ArchiveCatalogTables.Approved)
                        {
                            Assert(tableNames.Contains(tableName), settings.Type + " archive catalog table was not created: " + tableName + ".");
                        }

                        foreach (string tableName in ActiveLedgerTableNames())
                        {
                            Assert(!ArchiveCatalogTables.IsApproved(tableName), "Archive catalog approved active table name: " + tableName + ".");
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "archive_catalog_metadata_crud", "Archive catalog persists migration, manifest, object, range, and checkpoint metadata", async token =>
                    {
                        ArchiveCatalogSettings settings = CreateArchiveCatalogSettings();

                        await using ArchiveSqlCatalog catalog = new ArchiveSqlCatalog(settings);
                        await catalog.InitializeAsync(token).ConfigureAwait(false);

                        string tenantId = "ten_archive_" + UniqueSuffix(12);
                        string accountId = "acct_archive_" + UniqueSuffix(12);
                        ArchiveStoragePool pool = await catalog.StoragePools.UpsertAsync(new ArchiveStoragePool
                        {
                            Id = "asp_test_" + UniqueSuffix(8),
                            Name = "Archive test pool",
                            Type = ArchiveStoragePoolType.FileSystem,
                            BasePath = Path.Combine(Path.GetTempPath(), "netledger-archive-test-" + UniqueSuffix(8)),
                            Prefix = "test",
                            Format = ArchiveFormat.JsonlGzip,
                            Compression = ArchiveCompression.Gzip
                        }, token).ConfigureAwait(false);

                        DateTime fromUtc = DateTime.UtcNow.AddDays(-7);
                        DateTime toUtc = DateTime.UtcNow.AddDays(-1);
                        ArchiveMigration migration = await catalog.Migrations.CreateAsync(new ArchiveMigration
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            EntityType = ArchiveEntityType.Entries,
                            StoragePoolId = pool.Id,
                            Format = ArchiveFormat.JsonlGzip,
                            Compression = ArchiveCompression.Gzip,
                            FromUtc = fromUtc,
                            ToUtc = toUtc,
                            Status = ArchiveMigrationStatus.Pending,
                            IdempotencyKey = "idem_" + UniqueSuffix(16)
                        }, token).ConfigureAwait(false);

                        ArchiveMigration? readByIdempotency = await catalog.Migrations.ReadByIdempotencyKeyAsync(migration.IdempotencyKey, token).ConfigureAwait(false);
                        Assert(readByIdempotency != null && readByIdempotency.Id == migration.Id, "Migration idempotency lookup failed.");

                        ArchiveMigrationBatch batch = await catalog.Migrations.CreateBatchAsync(new ArchiveMigrationBatch
                        {
                            MigrationId = migration.Id,
                            StoragePoolId = pool.Id,
                            TenantId = tenantId,
                            AccountId = accountId,
                            SequenceNumber = 0,
                            RowCount = 2,
                            ByteCount = 128,
                            ContentHashSha256 = "abcd",
                            TemporaryRelativePath = "_tmp/" + migration.Id + "/part.jsonl.gz",
                            CommittedRelativePath = "test/v1/entity=entries/tenantid=" + tenantId + "/part.jsonl.gz",
                            Status = ArchiveMigrationBatchStatus.Uploaded
                        }, token).ConfigureAwait(false);

                        ArchiveMigrationBatch? readBatch = await catalog.Migrations.ReadBatchAsync(migration.Id, batch.Id, token).ConfigureAwait(false);
                        Assert(readBatch != null && readBatch.SequenceNumber == 0, "Migration batch read failed.");

                        ArchiveManifest manifest = await catalog.Manifests.CreateAsync(new ArchiveManifest
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            MigrationId = migration.Id,
                            EntityType = ArchiveEntityType.Entries,
                            StoragePoolId = pool.Id,
                            FromUtc = fromUtc,
                            ToUtc = toUtc,
                            RowCount = 2,
                            CreditTotal = 10m,
                            DebitTotal = 1m,
                            ContentHashSha256 = "content",
                            ManifestHashSha256 = "manifest",
                            Status = ArchiveManifestStatus.Committed
                        }, token).ConfigureAwait(false);

                        await catalog.Objects.CreateAsync(new ArchiveObject
                        {
                            ManifestId = manifest.Id,
                            StoragePoolId = pool.Id,
                            RelativePath = batch.CommittedRelativePath,
                            RowCount = 2,
                            ByteCount = 128,
                            ContentHashSha256 = "abcd"
                        }, token).ConfigureAwait(false);

                        await catalog.Ranges.CreateAsync(new ArchiveRangeInfo
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            ManifestId = manifest.Id,
                            EntityType = ArchiveEntityType.Entries,
                            FromUtc = fromUtc,
                            ToUtc = toUtc,
                            RowCount = 2
                        }, token).ConfigureAwait(false);

                        await catalog.BalanceCheckpoints.CreateAsync(new ArchiveBalanceCheckpoint
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            ManifestId = manifest.Id,
                            AsOfUtc = toUtc,
                            Balance = 9m
                        }, token).ConfigureAwait(false);

                        ArchiveRequestHistoryRange requestHistoryRange = await catalog.RequestHistoryRanges.CreateAsync(new ArchiveRequestHistoryRange
                        {
                            TenantId = tenantId,
                            ManifestId = manifest.Id,
                            FromUtc = fromUtc,
                            ToUtc = toUtc,
                            RowCount = 2,
                            MethodCountsJson = "{\"GET\":2}",
                            StatusCodeCountsJson = "{\"200\":2}"
                        }, token).ConfigureAwait(false);

                        EnumerationResult<ArchiveManifest> manifests = await catalog.Manifests.EnumerateAsync(new ArchiveQuery
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            MigrationId = migration.Id,
                            MaxResults = 10
                        }, token).ConfigureAwait(false);
                        Assert(manifests.Objects.Count == 1 && manifests.Objects[0].Id == manifest.Id, "Manifest enumeration did not respect tenant/account/migration filters.");

                        EnumerationResult<ArchiveObject> objects = await catalog.Objects.EnumerateByManifestAsync(manifest.Id, new ArchiveQuery { MaxResults = 10 }, token).ConfigureAwait(false);
                        Assert(objects.Objects.Count == 1 && objects.Objects[0].RelativePath == batch.CommittedRelativePath, "Object enumeration by manifest failed.");

                        ArchiveBalanceCheckpoint? checkpoint = await catalog.BalanceCheckpoints.ReadAsOfAsync(tenantId, accountId, DateTime.UtcNow, token).ConfigureAwait(false);
                        Assert(checkpoint != null && checkpoint.Balance == 9m, "Balance checkpoint read-as-of failed.");

                        EnumerationResult<ArchiveRequestHistoryRange> requestHistoryRanges = await catalog.RequestHistoryRanges.EnumerateAsync(new ArchiveQuery
                        {
                            TenantId = tenantId,
                            MigrationId = manifest.Id,
                            MaxResults = 10
                        }, token).ConfigureAwait(false);
                        Assert(requestHistoryRanges.Objects.Count == 1 && requestHistoryRanges.Objects[0].Id == requestHistoryRange.Id, "Request history range enumeration failed.");

                        ArchiveManifest quarantined = await catalog.Manifests.UpdateStatusAsync(manifest.Id, ArchiveManifestStatus.Quarantined, token).ConfigureAwait(false);
                        Assert(quarantined.Status == ArchiveManifestStatus.Quarantined, "Manifest status update failed.");
                    }),
                    new TestCaseDescriptor(suiteId, "manual_migration_recovery_abort_cleans_temporary_payload", "Manual migration recovery abort removes temporary payloads and exposes no manifest", async token =>
                    {
                        ArchiveCatalogSettings settings = CreateArchiveCatalogSettings();
                        string directory = Path.Combine(Path.GetTempPath(), "netledger-archive-recovery-" + UniqueSuffix(16));
                        try
                        {
                            await using ArchiveSqlCatalog catalog = new ArchiveSqlCatalog(settings);
                            await catalog.InitializeAsync(token).ConfigureAwait(false);
                            FileSystemArchiveObjectStore store = new FileSystemArchiveObjectStore(directory);

                            string tenantId = "ten_archive_recovery_" + UniqueSuffix(8);
                            string accountId = "acct_archive_recovery_" + UniqueSuffix(8);
                            ArchiveStoragePool pool = await catalog.StoragePools.UpsertAsync(new ArchiveStoragePool
                            {
                                Id = "asp_recovery_" + UniqueSuffix(8),
                                Name = "Recovery drill pool",
                                Type = ArchiveStoragePoolType.FileSystem,
                                BasePath = directory,
                                Format = ArchiveFormat.JsonlGzip,
                                Compression = ArchiveCompression.Gzip
                            }, token).ConfigureAwait(false);

                            ArchiveMigration migration = await catalog.Migrations.CreateAsync(new ArchiveMigration
                            {
                                TenantId = tenantId,
                                AccountId = accountId,
                                EntityType = ArchiveEntityType.Entries,
                                StoragePoolId = pool.Id,
                                Format = ArchiveFormat.JsonlGzip,
                                Compression = ArchiveCompression.Gzip,
                                FromUtc = DateTime.UtcNow.AddDays(-3),
                                ToUtc = DateTime.UtcNow.AddDays(-2),
                                IdempotencyKey = "recovery-" + UniqueSuffix(12),
                                Status = ArchiveMigrationStatus.Receiving
                            }, token).ConfigureAwait(false);

                            string temporaryPath = "_tmp/" + migration.Id + "/part-000000000000.jsonl.gz";
                            string committedPath = "v1/entity=entries/tenantid=" + tenantId + "/accountid=" + accountId + "/manifest=pending/part-000000000000.jsonl.gz";
                            byte[] payload = Encoding.UTF8.GetBytes("interrupted archive payload");
                            using (MemoryStream stream = new MemoryStream(payload))
                            {
                                await store.WriteTemporaryAsync(temporaryPath, stream, token).ConfigureAwait(false);
                            }

                            ArchiveMigrationBatch batch = await catalog.Migrations.CreateBatchAsync(new ArchiveMigrationBatch
                            {
                                MigrationId = migration.Id,
                                StoragePoolId = pool.Id,
                                TenantId = tenantId,
                                AccountId = accountId,
                                SequenceNumber = 0,
                                RowCount = 1,
                                ByteCount = payload.Length,
                                ContentHashSha256 = "manual-recovery-drill",
                                TemporaryRelativePath = temporaryPath,
                                CommittedRelativePath = committedPath,
                                Status = ArchiveMigrationBatchStatus.Uploaded
                            }, token).ConfigureAwait(false);

                            ArchiveObjectMetadata beforeAbort = await store.ReadMetadataAsync(temporaryPath, token).ConfigureAwait(false);
                            Assert(beforeAbort.Exists, "Recovery drill did not create the temporary archive payload.");

                            await store.DeleteTemporaryAsync(batch.TemporaryRelativePath, token).ConfigureAwait(false);
                            batch.Status = ArchiveMigrationBatchStatus.Failed;
                            await catalog.Migrations.UpdateBatchAsync(batch, token).ConfigureAwait(false);
                            migration = await catalog.Migrations.UpdateStatusAsync(migration.Id, ArchiveMigrationStatus.Aborted, token).ConfigureAwait(false);
                            await catalog.AuditRecords.CreateAsync(new ArchiveAuditRecord
                            {
                                TenantId = tenantId,
                                PrincipalId = "manual-recovery-drill",
                                Action = "MigrationRecoveryAborted",
                                TargetType = "ArchiveMigration",
                                TargetId = migration.Id,
                                Metadata = "{\"Decision\":\"Permit\",\"Reason\":\"Shared recovery drill aborted interrupted migration.\"}"
                            }, token).ConfigureAwait(false);

                            ArchiveObjectMetadata afterAbort = await store.ReadMetadataAsync(temporaryPath, token).ConfigureAwait(false);
                            Assert(!afterAbort.Exists, "Recovery drill left a temporary archive payload behind.");
                            ArchiveMigration? aborted = await catalog.Migrations.ReadByIdAsync(migration.Id, token).ConfigureAwait(false);
                            Assert(aborted != null && aborted.Status == ArchiveMigrationStatus.Aborted, "Recovery drill did not mark the migration aborted.");
                            ArchiveMigrationBatch? recoveredBatch = await catalog.Migrations.ReadBatchAsync(migration.Id, batch.Id, token).ConfigureAwait(false);
                            Assert(recoveredBatch != null && recoveredBatch.Status == ArchiveMigrationBatchStatus.Failed, "Recovery drill did not mark the interrupted batch failed.");

                            EnumerationResult<ArchiveManifest> manifests = await catalog.Manifests.EnumerateAsync(new ArchiveQuery
                            {
                                TenantId = tenantId,
                                AccountId = accountId,
                                MigrationId = migration.Id,
                                MaxResults = 10
                            }, token).ConfigureAwait(false);
                            Assert(manifests.Objects.Count == 0, "Recovery drill exposed a manifest for an aborted migration.");
                        }
                        finally
                        {
                            if (Directory.Exists(directory))
                            {
                                Directory.Delete(directory, true);
                            }
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "archive_catalog_audit_and_request_history_capture", "Archive catalog persists archive audit and server request history rows", async token =>
                    {
                        ArchiveCatalogSettings settings = CreateArchiveCatalogSettings();

                        await using ArchiveSqlCatalog catalog = new ArchiveSqlCatalog(settings);
                        await catalog.InitializeAsync(token).ConfigureAwait(false);

                        ArchiveAuditRecord audit = await catalog.AuditRecords.CreateAsync(new ArchiveAuditRecord
                        {
                            TenantId = "ten_archive_audit",
                            PrincipalId = "usr_archive_audit",
                            Action = "Denied",
                            TargetType = "ArchiveManifest",
                            TargetId = "amf_test",
                            Metadata = "{\"Result\":\"Denied\"}"
                        }, token).ConfigureAwait(false);

                        ArchiveServerRequestHistoryRecord requestHistory = await catalog.ServerRequestHistory.CreateAsync(new ArchiveServerRequestHistoryRecord
                        {
                            TenantId = "ten_archive_audit",
                            PrincipalId = "usr_archive_audit",
                            Method = "GET",
                            Path = "/v1/archive/manifests",
                            StatusCode = 403,
                            DurationMs = 3.5m
                        }, token).ConfigureAwait(false);

                        long auditCount = await CountRowsByIdAsync(settings, ArchiveCatalogTables.AuditRecords, audit.Id, token).ConfigureAwait(false);
                        Assert(auditCount == 1, "Archive audit record was not persisted.");

                        ArchiveServerRequestHistoryRecord? readHistory = await catalog.ServerRequestHistory.ReadAsync("ten_archive_audit", requestHistory.Id, token).ConfigureAwait(false);
                        Assert(readHistory != null && readHistory.Id == requestHistory.Id, "Archive server request history record was not persisted.");
                    }),
                    new TestCaseDescriptor(suiteId, "active_archive_boundary_conflict_and_partial", "Active server archive boundary rejects archived ranges unless partial results are explicit", _ =>
                    {
                        ServerSettings settings = new ServerSettings();
                        settings.Archive.Enabled = true;
                        settings.Archive.ArchiveServerEndpoint = "http://archive.example";
                        settings.Archive.DefaultActiveDataRetentionDays = 30;

                        ActiveArchiveBoundaryService service = new ActiveArchiveBoundaryService(settings);
                        RequestContext req = new RequestContext
                        {
                            TenantId = "ten_boundary",
                            AllowPartial = false
                        };

                        DateTime boundaryUtc = service.GetBoundaryUtc(req);
                        DateTime? fromUtc = boundaryUtc.AddDays(-2);
                        ResponseContext? conflict = service.ApplyActiveRange(req, ref fromUtc, DateTime.UtcNow, "Entry");
                        Assert(conflict != null && conflict.StatusCode == 409, "Boundary did not reject mixed active/archive range.");

                        req.AllowPartial = true;
                        DateTime? partialFromUtc = boundaryUtc.AddDays(-2);
                        ResponseContext? partial = service.ApplyActiveRange(req, ref partialFromUtc, DateTime.UtcNow, "Entry");
                        Assert(partial == null, "Boundary rejected explicit partial range.");
                        Assert(partialFromUtc.HasValue && partialFromUtc.Value > boundaryUtc.AddSeconds(-5), "Boundary did not clamp partial active range.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "active_cleanup_retains_balance_anchor", "Active cleanup preserves a balance anchor and current balance after deletion", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("cleanup");
                        string accountId = await ledger.CreateAccountAsync("archive-cleanup", 100m, null, null, tenantId, token).ConfigureAwait(false);
                        await ledger.AddCreditAsync(accountId, 25m, "committed credit", null, true, null, null, tenantId, token).ConfigureAwait(false);
                        await ledger.AddDebitAsync(accountId, 5m, "committed debit", null, true, null, null, tenantId, token).ConfigureAwait(false);

                        Balance beforeCleanup = await ledger.GetBalanceAsync(accountId, false, token).ConfigureAwait(false);
                        int rowsBeforeCleanup = await ledger.Driver.Entries.GetCountByAccountIdAsync(accountId, token).ConfigureAwait(false);
                        DateTime cutoffUtc = DateTime.UtcNow.AddSeconds(1);

                        RequestContext req = new RequestContext
                        {
                            TenantId = tenantId,
                            Auth = AuthContext.NotRequired()
                        };

                        using ArchiveExportService service = new ArchiveExportService(new ServerSettings(), ledger, new LoggingModule());
                        long rowsDeleted = await service.CleanupArchivedEntriesAsync(req, tenantId, accountId, cutoffUtc, token).ConfigureAwait(false);

                        Balance afterCleanup = await ledger.GetBalanceAsync(accountId, false, token).ConfigureAwait(false);
                        int rowsAfterCleanup = await ledger.Driver.Entries.GetCountByAccountIdAsync(accountId, token).ConfigureAwait(false);
                        Entry anchor = await ledger.Driver.Entries.ReadLatestBalanceAsync(accountId, token).ConfigureAwait(false);
                        string anchorDescription = anchor?.Description ?? String.Empty;

                        Assert(rowsDeleted > 0, "Active cleanup did not delete committed rows.");
                        Assert(rowsAfterCleanup < rowsBeforeCleanup, "Active cleanup did not reduce active entry rows.");
                        Assert(afterCleanup.CommittedBalance == beforeCleanup.CommittedBalance, "Active cleanup changed the committed balance.");
                        Assert(anchor != null && anchor.Type == EntryType.Balance, "Active cleanup did not leave a balance anchor.");
                        Assert(anchorDescription.Contains("Archive balance anchor", StringComparison.Ordinal), "Retained balance entry was not marked as an archive anchor.");
                        Assert(await ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false), "Active balance chain failed verification after cleanup.");
                    }),
                    new TestCaseDescriptor(suiteId, "account_archival_settings_crud", "Account archival settings persist overrides, state, and clamped values", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("accountarchivalsettings");
                        string accountId = await ledger.CreateAccountAsync("archive-settings-" + UniqueSuffix(8), 0m, null, null, tenantId, token).ConfigureAwait(false);

                        AccountArchivalSettings saved = await ledger.Driver.AccountArchivalSettings.UpsertAsync(new AccountArchivalSettings
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            Enabled = true,
                            MaxRetentionDays = 0,
                            IntervalSeconds = 0,
                            MaxBatchRows = 100000,
                            DeleteAfterCommit = false,
                            StoragePoolId = "pool-a",
                            RetryMaxAttempts = 0,
                            RetryInitialDelaySeconds = -1,
                            RetryMaxDelaySeconds = -1,
                            LastArchivedThroughUtc = DateTime.UtcNow.AddDays(-3),
                            FailureCount = -1
                        }, token).ConfigureAwait(false);

                        Assert(saved.MaxRetentionDays == 1, "MaxRetentionDays was not clamped.");
                        Assert(saved.IntervalSeconds == 1, "IntervalSeconds was not clamped.");
                        Assert(saved.MaxBatchRows == 50000, "MaxBatchRows was not clamped.");
                        Assert(saved.RetryMaxAttempts == 1, "RetryMaxAttempts was not clamped.");
                        Assert(saved.RetryInitialDelaySeconds == 0, "RetryInitialDelaySeconds was not clamped.");
                        Assert(saved.RetryMaxDelaySeconds == 0, "RetryMaxDelaySeconds was not clamped.");
                        Assert(saved.FailureCount == 0, "FailureCount was not clamped.");

                        AccountArchivalSettings? read = await ledger.Driver.AccountArchivalSettings.ReadByAccountAsync(tenantId, accountId, token).ConfigureAwait(false);
                        Assert(read != null && read.StoragePoolId == "pool-a" && read.LastArchivedThroughUtc.HasValue, "Account archival settings were not persisted.");

                        read!.Enabled = false;
                        read.StoragePoolId = "pool-b";
                        AccountArchivalSettings updated = await ledger.Driver.AccountArchivalSettings.UpsertAsync(read, token).ConfigureAwait(false);
                        Assert(updated.Id == saved.Id && updated.Enabled == false && updated.StoragePoolId == "pool-b", "Account archival settings update failed.");

                        EnumerationResult<AccountArchivalSettings> enumerated = await ledger.Driver.AccountArchivalSettings.EnumerateAsync(new EnumerationQuery
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            MaxResults = 10
                        }, token).ConfigureAwait(false);
                        Assert(enumerated.Objects.Count == 1 && enumerated.Objects[0].Id == saved.Id, "Account archival settings enumeration failed.");

                        bool deleted = await ledger.Driver.AccountArchivalSettings.DeleteByAccountAsync(tenantId, accountId, token).ConfigureAwait(false);
                        AccountArchivalSettings? afterDelete = await ledger.Driver.AccountArchivalSettings.ReadByAccountAsync(tenantId, accountId, token).ConfigureAwait(false);
                        Assert(deleted && afterDelete == null, "Account archival settings delete failed.");
                    }),
                    new TestCaseDescriptor(suiteId, "automatic_archive_account_override_exports_old_entries", "Account override enables automatic archival and avoids duplicate exports by watermark", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        using (TestArchiveServer archiveServer = new TestArchiveServer())
                        {
                            string tenantId = ScopedTenantId("autoarchive");
                            string accountId = await ledger.CreateAccountAsync("auto-archive-" + UniqueSuffix(8), 0m, null, null, tenantId, token).ConfigureAwait(false);
                            await CreateOldCommittedCreditAsync(ledger, tenantId, accountId, DateTime.UtcNow.AddDays(-10), token).ConfigureAwait(false);
                            await UpsertAutomaticAccountOverrideAsync(ledger, tenantId, accountId, true, token).ConfigureAwait(false);

                            ServerSettings settings = CreateAutomaticArchiveServerSettings(archiveServer.Endpoint, false);
                            using (ArchiveExportService exportService = new ArchiveExportService(settings, ledger, new LoggingModule()))
                            using (AutomaticArchiveService worker = new AutomaticArchiveService(settings, ledger, exportService, new LoggingModule()))
                            {
                                AutomaticArchiveRunResult result = await worker.RunOnceAsync(token).ConfigureAwait(false);
                                Assert(result.EntryExportsSucceeded >= 1, "Automatic archive export did not succeed.");
                                Assert(result.RowsExported > 0, "Automatic archive export did not export rows.");
                                Assert(archiveServer.MigrationCreateAttempts >= 1, "Automatic archive export did not create a migration.");
                                Assert(archiveServer.BatchCreateAttempts >= 1, "Automatic archive export did not create a batch.");
                                Assert(archiveServer.UploadAttempts >= 1, "Automatic archive export did not upload a batch.");
                                Assert(archiveServer.SealAttempts >= 1, "Automatic archive export did not seal the migration.");
                                Assert(archiveServer.CommitAttempts >= 1, "Automatic archive export did not commit the migration.");
                                Assert(archiveServer.UploadedBytes > 0, "Automatic archive export uploaded no bytes.");
                                Assert(archiveServer.ValidatedJsonlRows == archiveServer.ExpectedBatchRows, "Automatic archive export uploaded invalid JSONL.Gzip rows.");

                                AccountArchivalSettings? state = await ledger.Driver.AccountArchivalSettings.ReadByAccountAsync(tenantId, accountId, token).ConfigureAwait(false);
                                Assert(state != null && state.LastSuccessUtc.HasValue && state.LastArchivedThroughUtc.HasValue, "Automatic archive state was not persisted.");

                                int migrationsBeforeSecondRun = archiveServer.MigrationCreateAttempts;
                                state!.LastAttemptUtc = DateTime.UtcNow.AddHours(-2);
                                state.NextAttemptUtc = DateTime.UtcNow.AddSeconds(-1);
                                await ledger.Driver.AccountArchivalSettings.UpsertAsync(state, token).ConfigureAwait(false);

                                AutomaticArchiveRunResult second = await worker.RunOnceAsync(token).ConfigureAwait(false);
                                Assert(second.EntryExportsAttempted >= 1, "Second automatic archive run did not evaluate the account.");
                                Assert(archiveServer.MigrationCreateAttempts == migrationsBeforeSecondRun, "Automatic archive watermark allowed duplicate migration creation.");
                            }
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "automatic_archive_retries_transient_archive_server_failure", "Automatic archival retries transient Archive Server failures", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        using (TestArchiveServer archiveServer = new TestArchiveServer(1))
                        {
                            string tenantId = ScopedTenantId("autoretry");
                            string accountId = await ledger.CreateAccountAsync("auto-retry-" + UniqueSuffix(8), 0m, null, null, tenantId, token).ConfigureAwait(false);
                            await CreateOldCommittedCreditAsync(ledger, tenantId, accountId, DateTime.UtcNow.AddDays(-10), token).ConfigureAwait(false);
                            AccountArchivalSettings overrideSettings = await UpsertAutomaticAccountOverrideAsync(ledger, tenantId, accountId, true, token).ConfigureAwait(false);
                            overrideSettings.RetryMaxAttempts = 2;
                            overrideSettings.RetryInitialDelaySeconds = 0;
                            overrideSettings.RetryMaxDelaySeconds = 0;
                            await ledger.Driver.AccountArchivalSettings.UpsertAsync(overrideSettings, token).ConfigureAwait(false);

                            ServerSettings settings = CreateAutomaticArchiveServerSettings(archiveServer.Endpoint, false);
                            using (ArchiveExportService exportService = new ArchiveExportService(settings, ledger, new LoggingModule()))
                            using (AutomaticArchiveService worker = new AutomaticArchiveService(settings, ledger, exportService, new LoggingModule()))
                            {
                                AutomaticArchiveRunResult result = await worker.RunOnceAsync(token).ConfigureAwait(false);
                                Assert(result.EntryExportsSucceeded >= 1, "Automatic archive retry did not recover.");
                                Assert(result.EntryExportsFailed == 0, "Automatic archive retry left a failed export.");
                                Assert(archiveServer.MigrationCreateAttempts >= 2, "Automatic archive retry did not retry the failed migration create.");
                            }
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "automatic_archive_disabled_account_override_skips", "Disabled account override prevents automatic archival", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        using (TestArchiveServer archiveServer = new TestArchiveServer())
                        {
                            string tenantId = ScopedTenantId("autodisabled");
                            string accountId = await ledger.CreateAccountAsync("auto-disabled-" + UniqueSuffix(8), 0m, null, null, tenantId, token).ConfigureAwait(false);
                            await CreateOldCommittedCreditAsync(ledger, tenantId, accountId, DateTime.UtcNow.AddDays(-10), token).ConfigureAwait(false);
                            await UpsertAutomaticAccountOverrideAsync(ledger, tenantId, accountId, false, token).ConfigureAwait(false);

                            ServerSettings settings = CreateAutomaticArchiveServerSettings(archiveServer.Endpoint, false);
                            using (ArchiveExportService exportService = new ArchiveExportService(settings, ledger, new LoggingModule()))
                            using (AutomaticArchiveService worker = new AutomaticArchiveService(settings, ledger, exportService, new LoggingModule()))
                            {
                                AutomaticArchiveRunResult result = await worker.RunOnceAsync(token).ConfigureAwait(false);
                                Assert(result.EntryExportsAttempted == 0, "Disabled account override still attempted export.");
                                Assert(archiveServer.MigrationCreateAttempts == 0, "Disabled account override created an archive migration.");
                            }
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "filesystem_archive_store_commit_and_traversal", "Filesystem archive object store commits readable objects and rejects traversal", async token =>
                    {
                        string directory = Path.Combine(Path.GetTempPath(), "netledger-archive-store-" + UniqueSuffix(16));
                        try
                        {
                            FileSystemArchiveObjectStore store = new FileSystemArchiveObjectStore(directory);
                            byte[] bytes = Encoding.UTF8.GetBytes("archive payload");
                            using MemoryStream writeStream = new MemoryStream(bytes);
                            await store.WriteTemporaryAsync("_tmp/object.jsonl.gz", writeStream, token).ConfigureAwait(false);
                            await store.CommitAsync("_tmp/object.jsonl.gz", "committed/object.jsonl.gz", token).ConfigureAwait(false);

                            using Stream readStream = await store.ReadAsync("committed/object.jsonl.gz", token).ConfigureAwait(false);
                            using MemoryStream readBuffer = new MemoryStream();
                            await readStream.CopyToAsync(readBuffer, token).ConfigureAwait(false);
                            string readText = Encoding.UTF8.GetString(readBuffer.ToArray());
                            Assert(readText == "archive payload", "Committed archive object content was not readable.");

                            ArchiveObjectMetadata metadata = await store.ReadMetadataAsync("committed/object.jsonl.gz", token).ConfigureAwait(false);
                            Assert(metadata.Exists, "Committed archive object metadata did not report the object.");
                            Assert(metadata.ByteCount == bytes.Length, "Committed archive object metadata byte count was incorrect.");
                            Assert(metadata.IsReadOnly == true, "Committed archive object was not marked read-only.");

                            using MemoryStream metadataWriteStream = new MemoryStream(bytes);
                            await store.WriteTemporaryAsync("_tmp/object-with-metadata.jsonl.gz", metadataWriteStream, new Dictionary<string, string>
                            {
                                ["netledger-schema-version"] = "archive-object-v1"
                            }, token).ConfigureAwait(false);
                            await store.UpdateMetadataAsync("committed/object.jsonl.gz", new Dictionary<string, string>
                            {
                                ["netledger-manifest-id"] = "manifest-test"
                            }, token).ConfigureAwait(false);

                            bool rejectedTraversal = false;
                            try
                            {
                                using MemoryStream traversalStream = new MemoryStream(bytes);
                                await store.WriteTemporaryAsync("../escape.json", traversalStream, token).ConfigureAwait(false);
                            }
                            catch (InvalidOperationException)
                            {
                                rejectedTraversal = true;
                            }

                            Assert(rejectedTraversal, "Filesystem archive store did not reject path traversal.");
                        }
                        finally
                        {
                            if (Directory.Exists(directory))
                            {
                                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                                {
                                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                                }

                                Directory.Delete(directory, true);
                            }
                        }
                    }),
                    new TestCaseDescriptor(suiteId, "s3_archive_store_commit_metadata_and_cleanup", "S3-compatible archive object store commits readable objects when configured", async token =>
                    {
                        ArchiveStoragePoolSettings? settings = CreateS3ArchiveStorageSettings();
                        if (settings == null)
                        {
                            return;
                        }

                        using S3ArchiveObjectStore store = new S3ArchiveObjectStore(settings);
                        byte[] bytes = Encoding.UTF8.GetBytes("less3 archive payload " + UniqueSuffix(8));
                        string prefix = settings.Prefix.Trim('/').Trim();
                        if (!String.IsNullOrWhiteSpace(prefix))
                        {
                            prefix += "/";
                        }

                        string objectRoot = prefix + "touchstone/" + UniqueSuffix(16);
                        string temporaryPath = objectRoot + "/_tmp/object.txt";
                        string committedPath = objectRoot + "/committed/object.txt";

                        try
                        {
                            using MemoryStream writeStream = new MemoryStream(bytes);
                            await store.WriteTemporaryAsync(temporaryPath, writeStream, new Dictionary<string, string>
                            {
                                ["netledger-schema-version"] = "archive-object-v1"
                            }, token).ConfigureAwait(false);

                            await store.CommitAsync(temporaryPath, committedPath, token).ConfigureAwait(false);

                            using Stream readStream = await store.ReadAsync(committedPath, token).ConfigureAwait(false);
                            using MemoryStream readBuffer = new MemoryStream();
                            await readStream.CopyToAsync(readBuffer, token).ConfigureAwait(false);
                            string readText = Encoding.UTF8.GetString(readBuffer.ToArray());
                            Assert(readText == Encoding.UTF8.GetString(bytes), "Committed S3 archive object content was not readable.");

                            ArchiveObjectMetadata metadata = await store.ReadMetadataAsync(committedPath, token).ConfigureAwait(false);
                            Assert(metadata.Exists, "Committed S3 archive object metadata did not report the object.");
                            Assert(metadata.ByteCount == bytes.Length, "Committed S3 archive object metadata byte count was incorrect.");
                            Assert(metadata.Properties.ContainsKey("Provider"), "Committed S3 archive object metadata did not include provider context.");

                            await store.UpdateMetadataAsync(committedPath, new Dictionary<string, string>
                            {
                                ["netledger-manifest-id"] = "manifest-test"
                            }, token).ConfigureAwait(false);
                            ArchiveObjectMetadata updatedMetadata = await store.ReadMetadataAsync(committedPath, token).ConfigureAwait(false);
                            Assert(updatedMetadata.Properties.ContainsKey("netledger-manifest-id"), "Committed S3 archive object metadata update was not visible.");

                            bool rejectedTraversal = false;
                            try
                            {
                                using MemoryStream traversalStream = new MemoryStream(bytes);
                                await store.WriteTemporaryAsync("../escape.json", traversalStream, token).ConfigureAwait(false);
                            }
                            catch (InvalidOperationException)
                            {
                                rejectedTraversal = true;
                            }

                            Assert(rejectedTraversal, "S3 archive store did not reject path traversal.");
                        }
                        finally
                        {
                            await store.DeleteTemporaryAsync(temporaryPath, token).ConfigureAwait(false);
                            await store.DeleteTemporaryAsync(committedPath, token).ConfigureAwait(false);
                        }
                    })
                });
        }

        private static TestSuiteDescriptor MetadataSuite()
        {
            string suiteId = "metadata";
            return new TestSuiteDescriptor(
                suiteId,
                "Metadata validation",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "labels_normalize", "Labels trim and de-duplicate", _ =>
                    {
                        List<string> labels = MetadataValidator.NormalizeLabels(new[] { " debit ", "blue", "DEBIT" });
                        Assert(labels.Count == 2, "Labels were not de-duplicated.");
                        Assert(labels.Contains("debit"), "Trimmed label missing.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "tags_normalize", "Tags trim keys and preserve values", _ =>
                    {
                        Dictionary<string, string> tags = MetadataValidator.NormalizeTags(new Dictionary<string, string> { { " user ", "foo" } });
                        Assert(tags.ContainsKey("user"), "Tag key was not trimmed.");
                        Assert(tags["user"] == "foo", "Tag value mismatch.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "tag_limit_rejects", "Too many tags are rejected", _ =>
                    {
                        Dictionary<string, string> tags = Enumerable.Range(0, MetadataValidator.MaxTags + 1)
                            .ToDictionary(i => "k" + i, i => "v" + i);
                        AssertThrows<ArgumentException>(() => MetadataValidator.NormalizeTags(tags), "Too many tags were accepted.");
                        return Task.CompletedTask;
                    })
                });
        }

        private static TestSuiteDescriptor LedgerSuite()
        {
            string suiteId = "ledger";
            return new TestSuiteDescriptor(
                suiteId,
                "Ledger metadata round trip",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "account_metadata_round_trip", "Account metadata persists", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string accountId = await ledger.CreateAccountAsync(
                            "metadata-account",
                            10m,
                            new List<string> { "operating", "usd" },
                            new Dictionary<string, string> { { "department", "finance" } },
                            "ten_test",
                            token).ConfigureAwait(false);

                        Account account = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                        Assert(account.TenantId == "ten_test", "Tenant ID did not persist.");
                        Assert(account.Labels.Contains("operating"), "Account label did not persist.");
                        Assert(account.Tags["department"] == "finance", "Account tag did not persist.");
                    }),
                    new TestCaseDescriptor(suiteId, "entry_metadata_round_trip", "Entry metadata persists", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string accountId = await ledger.CreateAccountAsync("entry-account", null, null, null, "ten_test", token).ConfigureAwait(false);
                        string entryId = await ledger.AddCreditAsync(
                            accountId,
                            25m,
                            "payment",
                            null,
                            false,
                            new List<string> { "credit", "blue" },
                            new Dictionary<string, string> { { "user", "foo" } },
                            "ten_test",
                            token).ConfigureAwait(false);

                        Entry entry = await ledger.GetEntryAsync(entryId, token).ConfigureAwait(false);
                        Assert(entry.TenantId == "ten_test", "Entry tenant ID did not persist.");
                        Assert(entry.Labels.Contains("credit"), "Entry label did not persist.");
                        Assert(entry.Tags["user"] == "foo", "Entry tag did not persist.");
                    }),
                    new TestCaseDescriptor(suiteId, "committed_batch_entries_are_summarized_once_per_batch", "Committed credit and debit batches use one summarizing balance entry per batch", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string accountId = await ledger.CreateAccountAsync("batch-commit-optimized", 100m, null, null, "ten_finance", token).ConfigureAwait(false);

                        List<string> creditIds = await ledger.AddCreditsAsync(accountId, new List<BatchEntryInput>
                        {
                            new BatchEntryInput(10m, "batch credit 1"),
                            new BatchEntryInput(15m, "batch credit 2"),
                            new BatchEntryInput(20m, "batch credit 3")
                        }, true, token).ConfigureAwait(false);

                        List<string> debitIds = await ledger.AddDebitsAsync(accountId, new List<BatchEntryInput>
                        {
                            new BatchEntryInput(4m, "batch debit 1"),
                            new BatchEntryInput(6m, "batch debit 2")
                        }, true, token).ConfigureAwait(false);

                        await AssertBalanceAsync(ledger, accountId, 135m, 135m, 0m, 0, 0, token, "after committed batch credits and debits").ConfigureAwait(false);

                        List<Entry> entries = await ledger.Driver.Entries.ReadByAccountIdAsync(accountId, token).ConfigureAwait(false);
                        List<Entry> balanceEntries = entries.Where(entry => entry.Type == EntryType.Balance).ToList();
                        List<Entry> journalEntries = entries.Where(entry => entry.Type != EntryType.Balance).ToList();

                        Assert(creditIds.Count == 3 && debitIds.Count == 2, "Batch APIs returned the wrong number of entry identifiers.");
                        Assert(journalEntries.Count == 5, "Committed batch journal entry count mismatch.");
                        Assert(journalEntries.All(entry => entry.IsCommitted && !String.IsNullOrEmpty(entry.CommittedById)), "Committed batch entries were not summarized.");
                        Assert(balanceEntries.Count == 3, "Committed credit and debit batches should create one balance entry per batch plus the initial balance.");
                        Assert(journalEntries.Where(entry => entry.Type == EntryType.Credit).Select(entry => entry.CommittedById).Distinct(StringComparer.Ordinal).Count() == 1, "Credit batch was not summarized by a single balance entry.");
                        Assert(journalEntries.Where(entry => entry.Type == EntryType.Debit).Select(entry => entry.CommittedById).Distinct(StringComparer.Ordinal).Count() == 1, "Debit batch was not summarized by a single balance entry.");
                        Assert(await ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false), "Balance chain failed verification after committed batch workflow.");
                    }),
                    new TestCaseDescriptor(suiteId, "balance_reporting_tracks_committed_and_pending_transaction_mix", "Balance reporting is exact after each committed and uncommitted debit/credit step", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string accountId = await ledger.CreateAccountAsync("finance-step-balance", 100m, null, null, "ten_finance", token).ConfigureAwait(false);

                        await AssertBalanceAsync(ledger, accountId, 100m, 100m, 0m, 0, 0, token, "initial account creation").ConfigureAwait(false);

                        string pendingCreditId = await ledger.AddCreditAsync(accountId, 25.75m, "pending invoice payment", null, false, null, null, "ten_finance", token).ConfigureAwait(false);
                        await AssertBalanceAsync(ledger, accountId, 100m, 125.75m, 25.75m, 1, 0, token, "after pending credit").ConfigureAwait(false);

                        string pendingDebitId = await ledger.AddDebitAsync(accountId, 10.25m, "pending fee", null, false, null, null, "ten_finance", token).ConfigureAwait(false);
                        await AssertBalanceAsync(ledger, accountId, 100m, 115.50m, 15.50m, 1, 1, token, "after pending debit").ConfigureAwait(false);

                        Balance afterPartialCommit = await ledger.CommitEntriesAsync(accountId, new List<string> { pendingCreditId }, true, token).ConfigureAwait(false);
                        Assert(afterPartialCommit.Committed.Count == 1 && afterPartialCommit.Committed[0] == pendingCreditId, "Partial commit did not report the committed credit.");
                        await AssertBalanceAsync(ledger, accountId, 125.75m, 115.50m, -10.25m, 0, 1, token, "after committing credit only").ConfigureAwait(false);

                        string immediateDebitId = await ledger.AddDebitAsync(accountId, 5.50m, "immediate debit", null, true, null, null, "ten_finance", token).ConfigureAwait(false);
                        await AssertBalanceAsync(ledger, accountId, 120.25m, 110.00m, -10.25m, 0, 1, token, "after immediate debit commit").ConfigureAwait(false);

                        Balance afterFinalCommit = await ledger.CommitEntriesAsync(accountId, null!, true, token).ConfigureAwait(false);
                        Assert(afterFinalCommit.Committed.Count == 1 && afterFinalCommit.Committed[0] == pendingDebitId, "Final commit did not report the remaining pending debit.");
                        await AssertBalanceAsync(ledger, accountId, 110.00m, 110.00m, 0m, 0, 0, token, "after final pending commit").ConfigureAwait(false);

                        Entry pendingCredit = await ledger.GetEntryAsync(pendingCreditId, token).ConfigureAwait(false);
                        Entry pendingDebit = await ledger.GetEntryAsync(pendingDebitId, token).ConfigureAwait(false);
                        Entry immediateDebit = await ledger.GetEntryAsync(immediateDebitId, token).ConfigureAwait(false);
                        Assert(pendingCredit.IsCommitted && !String.IsNullOrEmpty(pendingCredit.CommittedById), "Committed credit was not linked to a balance entry.");
                        Assert(pendingDebit.IsCommitted && !String.IsNullOrEmpty(pendingDebit.CommittedById), "Committed debit was not linked to a balance entry.");
                        Assert(immediateDebit.IsCommitted && !String.IsNullOrEmpty(immediateDebit.CommittedById), "Immediate debit was not linked to a balance entry.");
                        Assert(await ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false), "Balance chain failed verification after stepwise accounting workflow.");
                    }),
                    new TestCaseDescriptor(suiteId, "parallel_writes_to_same_account_preserve_exact_pending_balance", "Concurrent debit and credit writes to one account preserve exact pending totals", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string accountId = await ledger.CreateAccountAsync("finance-parallel-writes", 500m, null, null, "ten_finance", token).ConfigureAwait(false);

                        List<Task> writes = new List<Task>();
                        decimal expectedCredits = 0m;
                        decimal expectedDebits = 0m;
                        int creditCount = 40;
                        int debitCount = 35;

                        for (int i = 1; i <= creditCount; i++)
                        {
                            decimal amount = i / 4m;
                            expectedCredits += amount;
                            writes.Add(ledger.AddCreditAsync(accountId, amount, "parallel credit " + i, null, false, null, null, "ten_finance", token));
                        }

                        for (int i = 1; i <= debitCount; i++)
                        {
                            decimal amount = i / 5m;
                            expectedDebits += amount;
                            writes.Add(ledger.AddDebitAsync(accountId, amount, "parallel debit " + i, null, false, null, null, "ten_finance", token));
                        }

                        await Task.WhenAll(writes).ConfigureAwait(false);

                        decimal expectedPendingBalance = 500m + expectedCredits - expectedDebits;
                        await AssertBalanceAsync(ledger, accountId, 500m, expectedPendingBalance, expectedCredits - expectedDebits, creditCount, debitCount, token, "after parallel pending writes").ConfigureAwait(false);

                        List<Entry> pendingEntries = await ledger.GetPendingEntriesAsync(accountId, token).ConfigureAwait(false);
                        Assert(pendingEntries.Count == creditCount + debitCount, "Pending entry count mismatch after parallel writes.");
                        Assert(pendingEntries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() == pendingEntries.Count, "Parallel writes produced duplicate entry identifiers.");
                        Assert(pendingEntries.All(entry => !entry.IsCommitted), "Parallel pending write test found an unexpectedly committed entry.");
                    }),
                    new TestCaseDescriptor(suiteId, "parallel_commits_to_same_account_are_sequential_and_idempotent", "Concurrent commits to one account produce one exact committed balance and no duplicate summarization", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string accountId = await ledger.CreateAccountAsync("finance-parallel-commits", 1000m, null, null, "ten_finance", token).ConfigureAwait(false);

                        decimal expectedCredits = 0m;
                        decimal expectedDebits = 0m;
                        for (int i = 1; i <= 30; i++)
                        {
                            decimal credit = i * 1.25m;
                            decimal debit = i * 0.75m;
                            expectedCredits += credit;
                            expectedDebits += debit;
                            await ledger.AddCreditAsync(accountId, credit, "commit credit " + i, null, false, null, null, "ten_finance", token).ConfigureAwait(false);
                            await ledger.AddDebitAsync(accountId, debit, "commit debit " + i, null, false, null, null, "ten_finance", token).ConfigureAwait(false);
                        }

                        decimal expectedFinalBalance = 1000m + expectedCredits - expectedDebits;
                        await AssertBalanceAsync(ledger, accountId, 1000m, expectedFinalBalance, expectedCredits - expectedDebits, 30, 30, token, "before parallel commits").ConfigureAwait(false);

                        List<Task<Balance>> commits = Enumerable.Range(0, 12)
                            .Select(_ => ledger.CommitEntriesAsync(accountId, null!, true, token))
                            .ToList();
                        Balance[] commitResults = await Task.WhenAll(commits).ConfigureAwait(false);

                        await AssertBalanceAsync(ledger, accountId, expectedFinalBalance, expectedFinalBalance, 0m, 0, 0, token, "after parallel commits").ConfigureAwait(false);
                        Assert(commitResults.Count(result => result.Committed.Count > 0) == 1, "More than one parallel commit summarized pending entries.");
                        Assert(commitResults.Sum(result => result.Committed.Count) == 60, "Parallel commits did not summarize every pending entry exactly once.");

                        List<Entry> allEntries = await ledger.Driver.Entries.ReadByAccountIdAsync(accountId, token).ConfigureAwait(false);
                        List<Entry> journalEntries = allEntries.Where(entry => entry.Type != EntryType.Balance).ToList();
                        Assert(journalEntries.Count == 60, "Journal entry count changed during parallel commit.");
                        Assert(journalEntries.All(entry => entry.IsCommitted && !String.IsNullOrEmpty(entry.CommittedById)), "Committed journal entries were not all summarized.");
                        Assert(journalEntries.Select(entry => entry.CommittedById).Distinct(StringComparer.Ordinal).Count() == 1, "Parallel commits produced multiple summarizing balance entries for one pending batch.");
                        Assert(await ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false), "Balance chain failed verification after parallel commits.");
                    }),
                    new TestCaseDescriptor(suiteId, "parallel_immediate_committed_writes_preserve_final_balance_and_journal_order", "Concurrent immediately committed writes preserve final balance and sequential journal integrity", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string accountId = await ledger.CreateAccountAsync("finance-parallel-immediate", 250m, null, null, "ten_finance", token).ConfigureAwait(false);

                        List<Task> writes = new List<Task>();
                        decimal expectedCredits = 0m;
                        decimal expectedDebits = 0m;

                        for (int i = 1; i <= 20; i++)
                        {
                            decimal credit = 2m + i;
                            decimal debit = 1m + (i / 2m);
                            expectedCredits += credit;
                            expectedDebits += debit;
                            writes.Add(ledger.AddCreditAsync(accountId, credit, "immediate credit " + i, null, true, null, null, "ten_finance", token));
                            writes.Add(ledger.AddDebitAsync(accountId, debit, "immediate debit " + i, null, true, null, null, "ten_finance", token));
                        }

                        await Task.WhenAll(writes).ConfigureAwait(false);

                        decimal expectedFinalBalance = 250m + expectedCredits - expectedDebits;
                        await AssertBalanceAsync(ledger, accountId, expectedFinalBalance, expectedFinalBalance, 0m, 0, 0, token, "after parallel immediate committed writes").ConfigureAwait(false);

                        List<Entry> entries = await ledger.Driver.Entries.ReadByAccountIdAsync(accountId, token).ConfigureAwait(false);
                        List<Entry> balanceEntries = entries.Where(entry => entry.Type == EntryType.Balance).ToList();
                        List<Entry> journalEntries = entries.Where(entry => entry.Type != EntryType.Balance).ToList();
                        Assert(journalEntries.Count == 40, "Immediate write journal entry count mismatch.");
                        Assert(journalEntries.All(entry => entry.IsCommitted && !String.IsNullOrEmpty(entry.CommittedById)), "Immediate write journal entries were not committed and summarized.");
                        Assert(balanceEntries.Count == 41, "Each immediate committed write should create one sequential balance entry plus the initial balance.");
                        Assert(balanceEntries.Count(entry => !String.IsNullOrEmpty(entry.Replaces)) == 40, "Balance chain does not contain one replacement link per immediate committed write.");
                        Assert(balanceEntries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() == balanceEntries.Count, "Balance entries contain duplicate identifiers.");
                        Assert(await ledger.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false), "Balance chain failed verification after parallel immediate writes.");
                    }),
                    new TestCaseDescriptor(suiteId, "multiple_ledger_instances_serialize_same_account_writes", "Independent ledger instances serialize writes to the same account through database-backed locking", async token =>
                    {
                        DatabaseSettings settings = CreateDatabaseSettings();
                        await using Ledger ledgerA = new Ledger(settings);
                        await using Ledger ledgerB = new Ledger(CloneDatabaseSettings(settings));
                        string accountId = await ledgerA.CreateAccountAsync("finance-multi-instance", 1000m, null, null, "ten_finance", token).ConfigureAwait(false);

                        List<Task> workload = new List<Task>();
                        decimal expectedCredits = 0m;
                        decimal expectedDebits = 0m;

                        for (int i = 1; i <= 25; i++)
                        {
                            decimal creditA = 3m + i;
                            decimal debitA = 1m + (i / 10m);
                            decimal creditB = 2m + (i / 2m);
                            decimal debitB = 0.5m + (i / 20m);

                            expectedCredits += creditA + creditB;
                            expectedDebits += debitA + debitB;

                            workload.Add(ledgerA.AddCreditAsync(accountId, creditA, "ledger-a credit " + i, null, true, null, null, "ten_finance", token));
                            workload.Add(ledgerA.AddDebitAsync(accountId, debitA, "ledger-a debit " + i, null, true, null, null, "ten_finance", token));
                            workload.Add(ledgerB.AddCreditAsync(accountId, creditB, "ledger-b credit " + i, null, true, null, null, "ten_finance", token));
                            workload.Add(ledgerB.AddDebitAsync(accountId, debitB, "ledger-b debit " + i, null, true, null, null, "ten_finance", token));
                        }

                        await Task.WhenAll(workload).ConfigureAwait(false);

                        decimal expectedFinalBalance = 1000m + expectedCredits - expectedDebits;
                        await AssertBalanceAsync(ledgerA, accountId, expectedFinalBalance, expectedFinalBalance, 0m, 0, 0, token, "after multi-instance immediate writes").ConfigureAwait(false);
                        await AssertBalanceAsync(ledgerB, accountId, expectedFinalBalance, expectedFinalBalance, 0m, 0, 0, token, "after multi-instance readback").ConfigureAwait(false);

                        List<Entry> entries = await ledgerA.Driver.Entries.ReadByAccountIdAsync(accountId, token).ConfigureAwait(false);
                        List<Entry> journalEntries = entries.Where(entry => entry.Type != EntryType.Balance).ToList();
                        List<Entry> balanceEntries = entries.Where(entry => entry.Type == EntryType.Balance).ToList();
                        Assert(journalEntries.Count == 100, "Multi-instance journal entry count mismatch.");
                        Assert(journalEntries.All(entry => entry.IsCommitted && !String.IsNullOrEmpty(entry.CommittedById)), "Multi-instance journal entries were not all committed.");
                        Assert(balanceEntries.Count == 101, "Multi-instance writes should produce one balance entry per committed write plus the initial balance.");
                        Assert(await ledgerA.VerifyBalanceChainAsync(accountId, token).ConfigureAwait(false), "Balance chain failed verification after multi-instance writes.");
                    })
                });
        }

        private static TestSuiteDescriptor CredentialSuite()
        {
            string suiteId = "credentials";
            return new TestSuiteDescriptor(
                suiteId,
                "Credential persistence",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "credential_scope_round_trip", "Credential tenant and user scope persists", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        ApiKey credential = new ApiKey("worker", false)
                        {
                            TenantId = ScopedTenantId("credentials"),
                            UserId = ScopedUserId("credentials"),
                            SecretKeySha256 = Credential.HashSecret("sk_test_secret"),
                            SecretKeyLast4 = "cret"
                        };

                        ApiKey created = await ledger.Driver.ApiKeys.CreateAsync(credential, token).ConfigureAwait(false);
                        ApiKey read = await ledger.Driver.ApiKeys.ReadByIdAsync(created.Id, token).ConfigureAwait(false);
                        Assert(read.TenantId == credential.TenantId, "Credential tenant ID did not persist.");
                        Assert(read.UserId == credential.UserId, "Credential user ID did not persist.");
                        Assert(read.SecretKeySha256 == credential.SecretKeySha256, "Credential secret verifier did not persist.");
                        Assert(read.SecretKeyLast4 == "cret", "Credential secret last-four did not persist.");
                    })
                });
        }

        private static TestSuiteDescriptor IdentitySuite()
        {
            string suiteId = "identity";
            return new TestSuiteDescriptor(
                suiteId,
                "Identity persistence",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "tenant_user_session_round_trip", "Tenant, user, and session persist", async token =>
                    {
                        await using Ledger ledger = CreateLedger();

                        Tenant tenant = await ledger.Driver.Tenants.CreateAsync(new Tenant { Name = "Acme" }, token).ConfigureAwait(false);
                        User user = await ledger.Driver.Users.CreateAsync(new User
                        {
                            TenantId = tenant.Id,
                            Email = "admin@example.com",
                            PasswordSha256 = Credential.HashSecret("password"),
                            IsTenantAdmin = true
                        }, token).ConfigureAwait(false);
                        AuthSession session = await ledger.Driver.AuthSessions.CreateAsync(new AuthSession
                        {
                            TenantId = tenant.Id,
                            UserId = user.Id
                        }, token).ConfigureAwait(false);

                        Tenant? readTenant = await ledger.Driver.Tenants.ReadAsync(tenant.Id, token).ConfigureAwait(false);
                        User? readUser = await ledger.Driver.Users.ReadByEmailAsync(tenant.Id, "admin@example.com", token).ConfigureAwait(false);
                        AuthSession? readSession = await ledger.Driver.AuthSessions.ReadByTokenAsync(session.Token, token).ConfigureAwait(false);

                        Assert(readTenant != null && readTenant.Name == "Acme", "Tenant did not persist.");
                        Assert(readUser != null && readUser.IsTenantAdmin, "User did not persist.");
                        Assert(readSession != null && readSession.UserId == user.Id, "Session did not persist.");
                    }),
                    new TestCaseDescriptor(suiteId, "account_user_map_round_trip", "Account user mapping persists", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("account_user_map");
                        string accountId = await ledger.CreateAccountAsync("mapped", null, null, null, tenantId, token).ConfigureAwait(false);
                        AccountUserMap map = await ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            UserId = ScopedUserId("account_user_map")
                        }, token).ConfigureAwait(false);

                        bool exists = await ledger.Driver.AccountUserMaps.ExistsAsync(tenantId, accountId, map.UserId, token).ConfigureAwait(false);
                        Assert(exists, "Account user map did not persist.");
                    }),
                    new TestCaseDescriptor(suiteId, "account_user_map_enumeration_user_filter", "Account user map enumeration respects user filters", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("account_user_map_filter");
                        string firstUserId = ScopedUserId("account_user_map_filter_a");
                        string secondUserId = ScopedUserId("account_user_map_filter_b");
                        string firstAccountId = await ledger.CreateAccountAsync("mapped-a", null, null, null, tenantId, token).ConfigureAwait(false);
                        string secondAccountId = await ledger.CreateAccountAsync("mapped-b", null, null, null, tenantId, token).ConfigureAwait(false);

                        AccountUserMap firstMap = await ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
                        {
                            TenantId = tenantId,
                            AccountId = firstAccountId,
                            UserId = firstUserId
                        }, token).ConfigureAwait(false);

                        await ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
                        {
                            TenantId = tenantId,
                            AccountId = secondAccountId,
                            UserId = secondUserId
                        }, token).ConfigureAwait(false);

                        EnumerationResult<AccountUserMap> result = await ledger.Driver.AccountUserMaps.EnumerateAsync(new EnumerationQuery
                        {
                            TenantId = tenantId,
                            UserId = firstUserId,
                            MaxResults = 10
                        }, token).ConfigureAwait(false);

                        Assert(result.Objects.Count == 1, "Account user map user filter returned the wrong number of rows.");
                        Assert(result.Objects[0].Id == firstMap.Id, "Account user map user filter returned the wrong row.");
                    }),
                    new TestCaseDescriptor(suiteId, "mapped_account_enumeration_filters", "Mapped account enumeration excludes unmapped accounts", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("mapped_account");
                        string userId = ScopedUserId("mapped_account");
                        string mappedAccountId = await ledger.CreateAccountAsync("mapped", null, null, null, tenantId, token).ConfigureAwait(false);
                        string unmappedAccountId = await ledger.CreateAccountAsync("unmapped", null, null, null, tenantId, token).ConfigureAwait(false);

                        await ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
                        {
                            TenantId = tenantId,
                            AccountId = mappedAccountId,
                            UserId = userId
                        }, token).ConfigureAwait(false);

                        EnumerationResult<Account> result = await ledger.Driver.Accounts.EnumerateAsync(new EnumerationQuery
                        {
                            TenantId = tenantId,
                            MappedUserId = userId
                        }, token).ConfigureAwait(false);

                        Assert(result.Objects.Any(account => account.Id == mappedAccountId), "Mapped account was not returned.");
                        Assert(!result.Objects.Any(account => account.Id == unmappedAccountId), "Unmapped account was returned.");
                    }),
                    new TestCaseDescriptor(suiteId, "audit_round_trip", "Audit record persists", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("audit");
                        AuditRecord record = await ledger.Driver.AuditRecords.CreateAsync(new AuditRecord
                        {
                            TenantId = tenantId,
                            EventType = "Authorization",
                            ResourceType = "Account",
                            OperationType = "Read",
                            Result = "Denied",
                            Reason = "No matching permission"
                        }, token).ConfigureAwait(false);

                        EnumerationResult<AuditRecord> result = await ledger.Driver.AuditRecords.EnumerateAsync(new EnumerationQuery { TenantId = tenantId }, token).ConfigureAwait(false);
                        Assert(result.Objects.Any(item => item.Id == record.Id), "Audit record did not persist.");
                    }),
                    new TestCaseDescriptor(suiteId, "rbac_builtin_assignment_permits", "Built-in RBAC assignment permits matching operation", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("rbac");
                        string userId = ScopedUserId("rbac");
                        UserRoleAssignment assignment = await ledger.Driver.Rbac.CreateUserRoleAssignmentAsync(new UserRoleAssignment
                        {
                            TenantId = tenantId,
                            UserId = userId,
                            RoleName = "Viewer",
                            ResourceScope = "Resource",
                            ResourceId = ScopedAccountId("rbac")
                        }, token).ConfigureAwait(false);

                        List<UserRoleAssignment> assignments = await ledger.Driver.Rbac.EnumerateUserRoleAssignmentsAsync(tenantId, userId, token).ConfigureAwait(false);
                        UserRole? role = await ledger.Driver.Rbac.ReadRoleByNameAsync(tenantId, "Viewer", token).ConfigureAwait(false);
                        List<RolePermissionMap> maps = role != null
                            ? await ledger.Driver.Rbac.EnumerateRolePermissionMapsAsync(tenantId, role.Id, token).ConfigureAwait(false)
                            : new List<RolePermissionMap>();

                        Assert(assignments.Any(item => item.Id == assignment.Id), "RBAC assignment did not persist.");
                        Assert(role != null && role.IsBuiltIn, "Built-in Viewer role was not seeded.");
                        Assert(maps.Count > 0, "Built-in Viewer role has no permission maps.");
                    }),
                    new TestCaseDescriptor(suiteId, "account_units_round_trip", "Account units persist, update, and clear", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("account_units");

                        string accountId = await ledger.CreateAccountAsync(
                            new Account("units-account") { TenantId = tenantId, Units = "USD" }, 100m, token).ConfigureAwait(false);

                        Account? created = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                        Assert(created != null && created.Units == "USD", "Account units did not persist on create.");

                        Account? update = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                        Assert(update != null, "Account not found for units update.");
                        update!.Units = "tokens";
                        await ledger.UpdateAccountAsync(update, token).ConfigureAwait(false);

                        Account? updated = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                        Assert(updated != null && updated.Units == "tokens", "Account units did not update.");

                        Account? clear = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                        clear!.Units = null;
                        await ledger.UpdateAccountAsync(clear, token).ConfigureAwait(false);

                        Account? cleared = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                        Assert(cleared != null && cleared.Units == null, "Account units were not cleared.");
                    }),
                    new TestCaseDescriptor(suiteId, "account_update_round_trip", "Account update persists mutable fields and preserves tenant", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        string tenantId = ScopedTenantId("account_update");

                        string accountId = await ledger.CreateAccountAsync(
                            new Account("update-account") { TenantId = tenantId, Notes = "before", Units = "USD" }, null, token).ConfigureAwait(false);

                        Account? existing = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                        Assert(existing != null, "Account not found for update.");
                        existing!.Name = "renamed-account";
                        existing.Notes = "after";
                        existing.Units = "EUR";
                        existing.Labels = new List<string> { "blue" };
                        existing.Tags = new Dictionary<string, string> { { "team", "finance" } };
                        await ledger.UpdateAccountAsync(existing, token).ConfigureAwait(false);

                        Account? read = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
                        Assert(read != null, "Updated account not found.");
                        Assert(read!.Name == "renamed-account", "Account name did not update.");
                        Assert(read.Notes == "after", "Account notes did not update.");
                        Assert(read.Units == "EUR", "Account units did not update.");
                        Assert(read.TenantId == tenantId, "Account tenant must be preserved on update.");
                        Assert(read.Labels.Contains("blue"), "Account labels did not update.");
                        Assert(read.Tags.ContainsKey("team"), "Account tags did not update.");
                    })
                });
        }

        private static TestSuiteDescriptor RequestHistorySuite()
        {
            string suiteId = "request_history";
            return new TestSuiteDescriptor(
                suiteId,
                "Request history storage boundaries",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "request_history_filters_and_management_are_tenant_scoped", "Request history enumeration, reads, summaries, and deletes obey tenant and principal boundaries", async token =>
                    {
                        await using Ledger ledger = CreateLedger();
                        DateTime now = DateTime.UtcNow;
                        string tenantAId = ScopedTenantId("request_a");
                        string tenantBId = ScopedTenantId("request_b");
                        string userAId = ScopedUserId("request_a");
                        string userBId = ScopedUserId("request_b");
                        string userCId = ScopedUserId("request_c");

                        RequestHistoryEntry tenantAUserA = await ledger.Driver.RequestHistory.CreateAsync(new RequestHistoryEntry
                        {
                            TenantId = tenantAId,
                            PrincipalId = userAId,
                            PrincipalType = "User",
                            Method = "GET",
                            Path = "/v1/accounts",
                            Url = "/v1/accounts?maxResults=25",
                            StatusCode = 200,
                            DurationMs = 12.5,
                            RequestHeaders = new Dictionary<string, string> { { "x-test", "tenant-a" } },
                            ResponseHeaders = new Dictionary<string, string> { { "content-type", "application/json" } },
                            ResponseBody = "{\"ok\":true}",
                            CreatedUtc = now.AddMinutes(-10),
                            CompletedUtc = now.AddMinutes(-10).AddMilliseconds(12)
                        }, token).ConfigureAwait(false);

                        RequestHistoryEntry tenantAUserB = await ledger.Driver.RequestHistory.CreateAsync(new RequestHistoryEntry
                        {
                            TenantId = tenantAId,
                            PrincipalId = userBId,
                            PrincipalType = "User",
                            Method = "GET",
                            Path = "/v1/accounts/blocked",
                            Url = "/v1/accounts/blocked",
                            StatusCode = 403,
                            DurationMs = 8.75,
                            CreatedUtc = now.AddMinutes(-5),
                            CompletedUtc = now.AddMinutes(-5).AddMilliseconds(9)
                        }, token).ConfigureAwait(false);

                        RequestHistoryEntry tenantBUser = await ledger.Driver.RequestHistory.CreateAsync(new RequestHistoryEntry
                        {
                            TenantId = tenantBId,
                            PrincipalId = userCId,
                            PrincipalType = "User",
                            Method = "POST",
                            Path = "/v1/entries",
                            Url = "/v1/entries",
                            StatusCode = 201,
                            DurationMs = 21.25,
                            CreatedUtc = now.AddMinutes(-2),
                            CompletedUtc = now.AddMinutes(-2).AddMilliseconds(21)
                        }, token).ConfigureAwait(false);

                        RequestHistoryResult tenantAResult = await ledger.Driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter
                        {
                            TenantId = tenantAId,
                            MaxResults = 100
                        }, token).ConfigureAwait(false);
                        Assert(tenantAResult.Objects.Any(item => item.Id == tenantAUserA.Id), "Tenant A request history entry was not returned.");
                        Assert(tenantAResult.Objects.Any(item => item.Id == tenantAUserB.Id), "Tenant A second request history entry was not returned.");
                        Assert(!tenantAResult.Objects.Any(item => item.Id == tenantBUser.Id), "Cross-tenant request history leaked into tenant-scoped enumeration.");

                        RequestHistoryResult tenantAUserAResult = await ledger.Driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter
                        {
                            TenantId = tenantAId,
                            PrincipalId = userAId,
                            MaxResults = 100
                        }, token).ConfigureAwait(false);
                        Assert(tenantAUserAResult.Objects.Count == 1 && tenantAUserAResult.Objects[0].Id == tenantAUserA.Id, "Principal-scoped request history returned the wrong records.");

                        RequestHistoryEntry? tenantScopedCrossRead = await ledger.Driver.RequestHistory.ReadAsync(tenantAId, tenantBUser.Id, token).ConfigureAwait(false);
                        Assert(tenantScopedCrossRead == null, "Tenant-scoped read returned a cross-tenant request history entry.");

                        RequestHistoryEntry? systemRead = await ledger.Driver.RequestHistory.ReadAsync(null, tenantBUser.Id, token).ConfigureAwait(false);
                        Assert(systemRead != null && systemRead.Id == tenantBUser.Id, "Unscoped system read did not return the cross-tenant request history entry.");

                        bool crossTenantDeleted = await ledger.Driver.RequestHistory.DeleteAsync(tenantAId, tenantBUser.Id, token).ConfigureAwait(false);
                        Assert(!crossTenantDeleted, "Tenant-scoped delete removed a cross-tenant request history entry.");
                        RequestHistoryEntry? tenantBStillExists = await ledger.Driver.RequestHistory.ReadAsync(tenantBId, tenantBUser.Id, token).ConfigureAwait(false);
                        Assert(tenantBStillExists != null, "Cross-tenant request history entry was removed by a scoped delete.");

                        RequestHistorySummary tenantASummary = await ledger.Driver.RequestHistory.SummarizeAsync(new RequestHistoryFilter
                        {
                            TenantId = tenantAId,
                            FromUtc = now.AddMinutes(-30),
                            ToUtc = now.AddMinutes(1),
                            BucketMinutes = 15
                        }, token).ConfigureAwait(false);
                        Assert(tenantASummary.TotalCount == 2, "Tenant A summary count mismatch.");
                        Assert(tenantASummary.TotalSuccess == 1, "Tenant A success summary mismatch.");
                        Assert(tenantASummary.TotalFailure == 1, "Tenant A failure summary mismatch.");
                        Assert(Math.Abs(tenantASummary.AverageDurationMs - 10.625) < 0.001, "Tenant A average duration summary mismatch.");
                        Assert(tenantASummary.Buckets.Count > 0, "Tenant A summary buckets were not returned.");
                        Assert(tenantASummary.Buckets.Sum(bucket => bucket.SuccessCount) == 1, "Tenant A bucket success count mismatch.");
                        Assert(tenantASummary.Buckets.Sum(bucket => bucket.FailureCount) == 1, "Tenant A bucket failure count mismatch.");

                        long deletedTenantA = await ledger.Driver.RequestHistory.DeleteManyAsync(new RequestHistoryFilter
                        {
                            TenantId = tenantAId,
                            PathContains = "/v1/accounts",
                            MaxResults = 100
                        }, token).ConfigureAwait(false);
                        Assert(deletedTenantA == 2, "Tenant-scoped bulk delete removed the wrong number of entries.");

                        RequestHistoryResult allRemaining = await ledger.Driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter
                        {
                            MaxResults = 100
                        }, token).ConfigureAwait(false);
                        Assert(!allRemaining.Objects.Any(item => item.TenantId == tenantAId), "Tenant A request history entries remained after scoped delete.");
                        Assert(allRemaining.Objects.Any(item => item.Id == tenantBUser.Id), "Scoped bulk delete removed a cross-tenant request history entry.");
                    })
                });
        }

        private static string CreateDatabaseFilename()
        {
            return Path.Combine(Path.GetTempPath(), "netledger-test-" + UniqueSuffix(24) + ".db");
        }

        private static Ledger CreateLedger()
        {
            return new Ledger(CreateDatabaseSettings());
        }

        private static DatabaseSettings CreateDatabaseSettings()
        {
            DatabaseSettings configured = GetConfiguredDatabaseSettings();
            DatabaseSettings settings = CloneDatabaseSettings(configured);

            if (settings.Type == DatabaseTypeEnum.Sqlite && String.IsNullOrWhiteSpace(settings.Filename))
            {
                settings.Filename = CreateDatabaseFilename();
            }

            return settings;
        }

        private static DatabaseSettings GetConfiguredDatabaseSettings()
        {
            if (_ConfiguredSettings != null)
            {
                return CloneDatabaseSettings(_ConfiguredSettings);
            }

            return NetLedgerTestConfiguration.FromEnvironment();
        }

        private static string UniqueSuffix(int length)
        {
            string value = NetLedgerId.Generate("tst");
            return value.Length <= length ? value : value.Substring(value.Length - length);
        }

        private static string ScopedTenantId(string name)
        {
            return "ten_" + NormalizeIdentifierPart(name) + "_" + _RunScope;
        }

        private static string ScopedUserId(string name)
        {
            return "usr_" + NormalizeIdentifierPart(name) + "_" + _RunScope;
        }

        private static string ScopedAccountId(string name)
        {
            return "acct_" + NormalizeIdentifierPart(name) + "_" + _RunScope;
        }

        private static string NormalizeIdentifierPart(string value)
        {
            if (String.IsNullOrEmpty(value)) return "test";

            StringBuilder builder = new StringBuilder();
            foreach (char c in value)
            {
                if (Char.IsLetterOrDigit(c))
                {
                    builder.Append(Char.ToLowerInvariant(c));
                }
                else if (c == '_' || c == '-')
                {
                    builder.Append('_');
                }
            }

            return builder.Length == 0 ? "test" : builder.ToString();
        }

        private static TestSuiteDescriptor ProviderMatrixSuite()
        {
            string suiteId = "provider_matrix";
            List<TestCaseDescriptor> tests = new List<TestCaseDescriptor>();

            if (IsProviderMatrixEnabled())
            {
                tests.Add(new TestCaseDescriptor(suiteId, "sqlite_legacy_prettyid_pk_startup_migration", "SQLite migrates legacy integer primary keys to PrettyId primary keys", token =>
                    RunSqlitePrettyIdPrimaryKeyMigrationAsync(token)));
                tests.Add(new TestCaseDescriptor(suiteId, "sqlite_full_v3_workflows", "SQLite supports all v3 workflows", token =>
                    RunProviderFullWorkflowAsync(DatabaseTypeEnum.Sqlite, token)));
                tests.Add(new TestCaseDescriptor(suiteId, "mysql_full_v3_workflows", "MySQL supports all v3 workflows", token =>
                    RunProviderFullWorkflowAsync(DatabaseTypeEnum.Mysql, token)));
                tests.Add(new TestCaseDescriptor(suiteId, "postgresql_full_v3_workflows", "PostgreSQL supports all v3 workflows", token =>
                    RunProviderFullWorkflowAsync(DatabaseTypeEnum.Postgresql, token)));
                tests.Add(new TestCaseDescriptor(suiteId, "sqlserver_full_v3_workflows", "SQL Server supports all v3 workflows", token =>
                    RunProviderFullWorkflowAsync(DatabaseTypeEnum.SqlServer, token)));
            }
            else
            {
                DatabaseTypeEnum configuredType = GetConfiguredDatabaseSettings().Type;
                if (configuredType == DatabaseTypeEnum.Sqlite)
                {
                    tests.Add(new TestCaseDescriptor(suiteId, "sqlite_legacy_prettyid_pk_startup_migration", "SQLite migrates legacy integer primary keys to PrettyId primary keys", token =>
                        RunSqlitePrettyIdPrimaryKeyMigrationAsync(token)));
                }

                tests.Add(new TestCaseDescriptor(
                    suiteId,
                    configuredType.ToString().ToLowerInvariant() + "_full_v3_workflows",
                    configuredType + " supports all v3 workflows",
                    token => RunProviderFullWorkflowAsync(configuredType, token)));
            }

            return new TestSuiteDescriptor(
                suiteId,
                "Live SQL provider certification",
                tests);
        }

        private static async Task RunSqlitePrettyIdPrimaryKeyMigrationAsync(CancellationToken token)
        {
            string filename = CreateDatabaseFilename();
            string accountId = NetLedgerId.Generate(IdentifierPrefixes.Account);
            string entryId = NetLedgerId.Generate(IdentifierPrefixes.Entry);
            string credentialId = NetLedgerId.Generate(IdentifierPrefixes.Credential);

            await CreateLegacyPrettyIdPrimaryKeyDatabaseAsync(filename, accountId, entryId, credentialId, token).ConfigureAwait(false);

            await using Ledger ledger = new Ledger(filename);

            Account? account = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
            Assert(account != null && account.Id == accountId && account.Name == "legacy account", "Migrated account was not readable by PrettyId.");

            Entry? entry = await ledger.GetEntryAsync(entryId, token).ConfigureAwait(false);
            Assert(entry != null && entry.Id == entryId && entry.AccountId == accountId && entry.Amount == 42.25m, "Migrated entry was not readable by PrettyId.");

            ApiKey? credential = await ledger.Driver.ApiKeys.ReadByIdAsync(credentialId, token).ConfigureAwait(false);
            Assert(credential != null && credential.Id == credentialId && credential.Name == "legacy credential", "Migrated credential was not readable by PrettyId.");

            DataTable accountColumns = await ledger.Driver.ExecuteQueryAsync("PRAGMA table_info(accounts);", false, token).ConfigureAwait(false);
            DataTable entryColumns = await ledger.Driver.ExecuteQueryAsync("PRAGMA table_info(entries);", false, token).ConfigureAwait(false);
            DataTable credentialColumns = await ledger.Driver.ExecuteQueryAsync("PRAGMA table_info(apikeys);", false, token).ConfigureAwait(false);

            AssertPrimaryTextColumn(accountColumns, "id", "accounts");
            AssertPrimaryTextColumn(entryColumns, "id", "entries");
            AssertPrimaryTextColumn(credentialColumns, "id", "apikeys");
            Assert(!ColumnExists(accountColumns, "guid"), "accounts.guid remained after migration.");
            Assert(!ColumnExists(entryColumns, "guid"), "entries.guid remained after migration.");
            Assert(!ColumnExists(credentialColumns, "guid"), "apikeys.guid remained after migration.");

            DataTable migrationRows = await ledger.Driver.ExecuteQueryAsync("SELECT COUNT(*) FROM schemamigrations WHERE name = 'prettyid-primary-keys-v1' AND success = 1;", false, token).ConfigureAwait(false);
            Assert(ReadCount(migrationRows) == 1L, "PrettyId primary-key migration was not recorded as successful.");
        }

        private static async Task CreateLegacyPrettyIdPrimaryKeyDatabaseAsync(
            string filename,
            string accountId,
            string entryId,
            string credentialId,
            CancellationToken token)
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + filename + ";"))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                List<string> statements = new List<string>
                {
                    @"CREATE TABLE accounts (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        guid TEXT NOT NULL,
                        tenantid TEXT NOT NULL DEFAULT '',
                        owneruserid TEXT,
                        name TEXT NOT NULL,
                        notes TEXT,
                        labels TEXT NOT NULL DEFAULT '[]',
                        tags TEXT NOT NULL DEFAULT '{}',
                        active INTEGER NOT NULL DEFAULT 1,
                        createdutc TEXT NOT NULL,
                        lastupdateutc TEXT NOT NULL DEFAULT ''
                    );",
                    @"CREATE TABLE entries (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        guid TEXT NOT NULL,
                        tenantid TEXT NOT NULL DEFAULT '',
                        accountguid TEXT NOT NULL,
                        type TEXT NOT NULL,
                        amount REAL NOT NULL,
                        description TEXT,
                        replaces TEXT,
                        committed INTEGER NOT NULL DEFAULT 0,
                        committedbyguid TEXT,
                        committedutc TEXT,
                        labels TEXT NOT NULL DEFAULT '[]',
                        tags TEXT NOT NULL DEFAULT '{}',
                        createdutc TEXT NOT NULL,
                        lastupdateutc TEXT NOT NULL DEFAULT ''
                    );",
                    @"CREATE TABLE apikeys (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        guid TEXT NOT NULL,
                        tenantid TEXT NOT NULL DEFAULT '',
                        userid TEXT NOT NULL DEFAULT '',
                        name TEXT NOT NULL,
                        apikey TEXT NOT NULL,
                        secretkeysha256 TEXT,
                        secretkeylast4 TEXT,
                        active INTEGER NOT NULL DEFAULT 1,
                        isadmin INTEGER NOT NULL DEFAULT 0,
                        createdutc TEXT NOT NULL
                    );",
                    "INSERT INTO accounts (id, guid, tenantid, owneruserid, name, notes, labels, tags, active, createdutc, lastupdateutc) VALUES (1, '" + accountId + "', 'ten_legacy', 'usr_legacy', 'legacy account', 'legacy notes', '[\"legacy\"]', '{\"source\":\"migration\"}', 1, '2026-01-01 00:00:00.000000', '2026-01-01 00:00:01.000000');",
                    "INSERT INTO entries (id, guid, tenantid, accountguid, type, amount, description, replaces, committed, committedbyguid, committedutc, labels, tags, createdutc, lastupdateutc) VALUES (2, '" + entryId + "', 'ten_legacy', '" + accountId + "', 'Credit', 42.25, 'legacy credit', NULL, 0, NULL, NULL, '[\"legacy\"]', '{\"source\":\"migration\"}', '2026-01-01 00:00:02.000000', '2026-01-01 00:00:03.000000');",
                    "INSERT INTO apikeys (id, guid, tenantid, userid, name, apikey, secretkeysha256, secretkeylast4, active, isadmin, createdutc) VALUES (3, '" + credentialId + "', 'ten_legacy', 'usr_legacy', 'legacy credential', 'key_legacy', 'hash_legacy', 'last', 1, 0, '2026-01-01 00:00:04.000000');"
                };

                foreach (string statement in statements)
                {
                    using (SqliteCommand command = new SqliteCommand(statement, connection))
                    {
                        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }
            }
        }

        private static TestSuiteDescriptor SecurityBoundarySuite()
        {
            string suiteId = "security_boundaries";
            return new TestSuiteDescriptor(
                suiteId,
                "Multi-tenant authorization boundaries",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "system_admin_accesses_any_tenant_resource", "System admins can access and manage resources in any tenant", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        await AssertPermitAsync(scenario, scenario.SystemAdmin, scenario.TenantA.Id, "Tenant", "Read", null, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.SystemAdmin, null, "Account", "Read", null, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.SystemAdmin, null, "Balance", "Read", null, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.SystemAdmin, scenario.TenantB.Id, "Tenant", "Read", scenario.TenantB.Id, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.SystemAdmin, scenario.TenantB.Id, "User", "Create", null, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.SystemAdmin, scenario.TenantB.Id, "Account", "Delete", scenario.TenantBAccountId, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.SystemAdmin, scenario.TenantB.Id, "Entry", "Create", scenario.TenantBAccountId, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.SystemAdmin, scenario.TenantB.Id, "Balance", "Execute", scenario.TenantBAccountId, token).ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "tenant_admin_limited_to_own_tenant", "Tenant admins can manage only resources in their tenant", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        await AssertPermitAsync(scenario, scenario.TenantAAdmin, scenario.TenantA.Id, "Tenant", "Read", scenario.TenantA.Id, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAAdmin, scenario.TenantA.Id, "User", "Create", null, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAAdmin, scenario.TenantA.Id, "Account", "Delete", scenario.TenantAUserAccountId, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAAdmin, scenario.TenantA.Id, "Entry", "Create", scenario.TenantAUserAccountId, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAAdmin, scenario.TenantA.Id, "Balance", "Execute", scenario.TenantAUserAccountId, token).ConfigureAwait(false);

                        await AssertDenyAsync(scenario, scenario.TenantAAdmin, scenario.TenantA.Id, "Tenant", "Read", null, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAAdmin, scenario.TenantB.Id, "Tenant", "Read", scenario.TenantB.Id, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAAdmin, scenario.TenantB.Id, "User", "Create", null, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAAdmin, scenario.TenantB.Id, "Account", "Read", scenario.TenantBAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAAdmin, scenario.TenantB.Id, "Entry", "Create", scenario.TenantBAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAAdmin, scenario.TenantB.Id, "Balance", "Execute", scenario.TenantBAccountId, token).ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "regular_user_read_self_and_mapped_resources_only", "Regular users read their tenant and can access only mapped ledger resources", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        await AssertPermitAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Tenant", "Read", scenario.TenantA.Id, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "User", "Read", scenario.TenantAUser.Id, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Account", "Read", null, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Account", "Read", scenario.TenantAUserAccountId, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Entry", "Create", scenario.TenantAUserAccountId, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Balance", "Execute", scenario.TenantAUserAccountId, token).ConfigureAwait(false);

                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Tenant", "Read", null, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantB.Id, "Tenant", "Read", scenario.TenantB.Id, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "User", "Read", scenario.TenantAOtherUser.Id, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "User", "Create", null, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Account", "Read", scenario.TenantAUnmappedAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantB.Id, "Account", "Read", scenario.TenantBAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Entry", "Create", scenario.TenantAUnmappedAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Balance", "Execute", scenario.TenantAUnmappedAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Account", "Delete", scenario.TenantAUserAccountId, token).ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "mapped_enumeration_never_leaks_unmapped_or_cross_tenant_accounts", "Mapped account enumeration returns only accounts mapped to the principal within the tenant", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        EnumerationResult<Account> tenantAResult = await scenario.Ledger.Driver.Accounts.EnumerateAsync(new EnumerationQuery
                        {
                            TenantId = scenario.TenantA.Id,
                            MappedUserId = scenario.TenantAUser.Id
                        }, token).ConfigureAwait(false);

                        Assert(tenantAResult.Objects.Any(account => account.Id == scenario.TenantAUserAccountId), "Mapped account was not returned.");
                        Assert(!tenantAResult.Objects.Any(account => account.Id == scenario.TenantAUnmappedAccountId), "Unmapped same-tenant account leaked.");
                        Assert(!tenantAResult.Objects.Any(account => account.Id == scenario.TenantBAccountId), "Cross-tenant account leaked.");

                        EnumerationResult<Account> tenantBResult = await scenario.Ledger.Driver.Accounts.EnumerateAsync(new EnumerationQuery
                        {
                            TenantId = scenario.TenantB.Id,
                            MappedUserId = scenario.TenantAUser.Id
                        }, token).ConfigureAwait(false);

                        Assert(tenantBResult.Objects.Count == 0, "Cross-tenant mapped enumeration returned data.");
                    }),
                    new TestCaseDescriptor(suiteId, "unauthenticated_api_handlers_reject_protected_operations", "Protected API handlers reject unauthenticated requests before executing resource operations", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        await AssertApiErrorAsync(scenario.AccountHandler.EnumerateAsync(CreateUnauthenticatedRequest(scenario.TenantA.Id), token), ApiErrorEnum.Unauthorized, "Unauthenticated account enumeration was not rejected.").ConfigureAwait(false);

                        RequestContext entryReq = CreateUnauthenticatedRequest(scenario.TenantA.Id);
                        entryReq.AccountId = scenario.TenantAUserAccountId;
                        await AssertApiErrorAsync(scenario.EntryHandler.GetEntriesAsync(entryReq, token), ApiErrorEnum.Unauthorized, "Unauthenticated entry enumeration was not rejected.").ConfigureAwait(false);

                        RequestContext balanceReq = CreateUnauthenticatedRequest(scenario.TenantA.Id);
                        balanceReq.AccountId = scenario.TenantAUserAccountId;
                        await AssertApiErrorAsync(scenario.BalanceHandler.GetBalanceAsync(balanceReq, token), ApiErrorEnum.Unauthorized, "Unauthenticated balance read was not rejected.").ConfigureAwait(false);

                        await AssertApiErrorAsync(scenario.IdentityHandler.EnumerateUsersAsync(CreateUnauthenticatedRequest(scenario.TenantA.Id), token), ApiErrorEnum.Unauthorized, "Unauthenticated user enumeration was not rejected.").ConfigureAwait(false);
                        await AssertApiErrorAsync(scenario.CredentialHandler.EnumerateAsync(CreateUnauthenticatedRequest(scenario.TenantA.Id), token), ApiErrorEnum.Unauthorized, "Unauthenticated credential enumeration was not rejected.").ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "tenant_admin_unqualified_enumerations_are_scoped_to_authenticated_tenant", "Tenant-admin API enumerations without an explicit tenant do not leak other tenants", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        ResponseContext accountsResponse = await AssertApiSuccessAsync(scenario.AccountHandler.EnumerateAsync(CreateRequest(scenario.TenantAAdmin, null), token), "Tenant admin account enumeration failed.").ConfigureAwait(false);
                        EnumerationResult<Account> accounts = AssertEnumerationResult<Account>(accountsResponse, "Account enumeration did not return an enumeration result.");
                        Assert(accounts.Objects.Any(account => account.Id == scenario.TenantAUserAccountId), "Tenant admin did not see own-tenant account.");
                        Assert(!accounts.Objects.Any(account => account.Id == scenario.TenantBAccountId), "Tenant admin account enumeration leaked a cross-tenant account.");
                        Assert(accounts.Objects.All(account => account.TenantId == scenario.TenantA.Id), "Tenant admin account enumeration returned a resource outside the authenticated tenant.");

                        ResponseContext balancesResponse = await AssertApiSuccessAsync(scenario.BalanceHandler.GetAllBalancesAsync(CreateRequest(scenario.TenantAAdmin, null), token), "Tenant admin balance enumeration failed.").ConfigureAwait(false);
                        Dictionary<string, Balance>? balances = balancesResponse.Data as Dictionary<string, Balance>;
                        Assert(balances != null, "Balance enumeration did not return a dictionary.");
                        Assert(balances!.ContainsKey(scenario.TenantAUserAccountId), "Tenant admin did not see own-tenant balance.");
                        Assert(!balances.ContainsKey(scenario.TenantBAccountId), "Tenant admin balance enumeration leaked a cross-tenant balance.");

                        ResponseContext usersResponse = await AssertApiSuccessAsync(scenario.IdentityHandler.EnumerateUsersAsync(CreateRequest(scenario.TenantAAdmin, null), token), "Tenant admin user enumeration failed.").ConfigureAwait(false);
                        EnumerationResult<User> users = AssertEnumerationResult<User>(usersResponse, "User enumeration did not return an enumeration result.");
                        Assert(users.Objects.Any(user => user.Id == scenario.TenantAUser.Id), "Tenant admin did not see own-tenant user.");
                        Assert(!users.Objects.Any(user => user.Id == scenario.TenantBUser.Id), "Tenant admin user enumeration leaked a cross-tenant user.");
                        Assert(users.Objects.All(user => user.TenantId == scenario.TenantA.Id), "Tenant admin user enumeration returned a resource outside the authenticated tenant.");
                    }),
                    new TestCaseDescriptor(suiteId, "tenant_admin_cannot_omit_tenant_to_access_cross_tenant_account", "Tenant admins cannot use a missing tenant selector to operate on another tenant's account", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        RequestContext systemRead = CreateRequest(scenario.SystemAdmin, null);
                        systemRead.AccountId = scenario.TenantBAccountId;
                        await AssertApiSuccessAsync(scenario.AccountHandler.ReadAsync(systemRead, token), "System admin could not read a cross-tenant account without a tenant selector.").ConfigureAwait(false);

                        RequestContext accountRead = CreateRequest(scenario.TenantAAdmin, null);
                        accountRead.AccountId = scenario.TenantBAccountId;
                        await AssertApiErrorAsync(scenario.AccountHandler.ReadAsync(accountRead, token), ApiErrorEnum.Forbidden, "Tenant admin read a cross-tenant account without a tenant selector.").ConfigureAwait(false);

                        RequestContext entryRead = CreateRequest(scenario.TenantAAdmin, null);
                        entryRead.AccountId = scenario.TenantBAccountId;
                        await AssertApiErrorAsync(scenario.EntryHandler.GetEntriesAsync(entryRead, token), ApiErrorEnum.Forbidden, "Tenant admin enumerated cross-tenant entries without a tenant selector.").ConfigureAwait(false);

                        RequestContext balanceRead = CreateRequest(scenario.TenantAAdmin, null);
                        balanceRead.AccountId = scenario.TenantBAccountId;
                        await AssertApiErrorAsync(scenario.BalanceHandler.GetBalanceAsync(balanceRead, token), ApiErrorEnum.Forbidden, "Tenant admin read a cross-tenant balance without a tenant selector.").ConfigureAwait(false);

                        RequestContext debitReq = CreateRequest(scenario.TenantAAdmin, null);
                        debitReq.AccountId = scenario.TenantBAccountId;
                        SetJsonBody(debitReq, new AddEntriesRequest { Amount = 10m, Notes = "cross-tenant attempt" });
                        await AssertApiErrorAsync(scenario.EntryHandler.AddDebitsAsync(debitReq, token), ApiErrorEnum.Forbidden, "Tenant admin created a cross-tenant debit without a tenant selector.").ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "account_update_enforces_tenant_boundaries", "Account update is permitted only for authorized principals and never across tenants", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        // Authorization matrix for the Update operation.
                        await AssertPermitAsync(scenario, scenario.SystemAdmin, scenario.TenantB.Id, "Account", "Update", scenario.TenantBAccountId, token).ConfigureAwait(false);
                        await AssertPermitAsync(scenario, scenario.TenantAAdmin, scenario.TenantA.Id, "Account", "Update", scenario.TenantAUserAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAAdmin, scenario.TenantB.Id, "Account", "Update", scenario.TenantBAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Account", "Update", scenario.TenantAUserAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantA.Id, "Account", "Update", scenario.TenantAUnmappedAccountId, token).ConfigureAwait(false);
                        await AssertDenyAsync(scenario, scenario.TenantAUser, scenario.TenantB.Id, "Account", "Update", scenario.TenantBAccountId, token).ConfigureAwait(false);

                        // System admin can update a cross-tenant account and the change persists.
                        RequestContext systemUpdate = CreateRequest(scenario.SystemAdmin, scenario.TenantB.Id);
                        systemUpdate.AccountId = scenario.TenantBAccountId;
                        SetJsonBody(systemUpdate, new UpdateAccountRequest { Name = "tenant-b-renamed", Units = "USD" });
                        await AssertApiSuccessAsync(scenario.AccountHandler.UpdateAsync(systemUpdate, token), "System admin could not update a cross-tenant account.").ConfigureAwait(false);
                        Account? afterSystemUpdate = await scenario.Ledger.GetAccountByIdAsync(scenario.TenantBAccountId, token).ConfigureAwait(false);
                        Assert(afterSystemUpdate != null && afterSystemUpdate.Units == "USD" && afterSystemUpdate.Name == "tenant-b-renamed", "System admin account update did not persist.");
                        Assert(afterSystemUpdate!.TenantId == scenario.TenantB.Id, "Account update must not move the account to another tenant.");

                        // Tenant admin can update an account in their own tenant.
                        RequestContext tenantAdminUpdate = CreateRequest(scenario.TenantAAdmin, scenario.TenantA.Id);
                        tenantAdminUpdate.AccountId = scenario.TenantAUserAccountId;
                        SetJsonBody(tenantAdminUpdate, new UpdateAccountRequest { Name = "tenant-a-renamed", Units = "tokens" });
                        await AssertApiSuccessAsync(scenario.AccountHandler.UpdateAsync(tenantAdminUpdate, token), "Tenant admin could not update an own-tenant account.").ConfigureAwait(false);

                        // Tenant admin cannot update a cross-tenant account.
                        RequestContext crossTenantUpdate = CreateRequest(scenario.TenantAAdmin, scenario.TenantB.Id);
                        crossTenantUpdate.AccountId = scenario.TenantBAccountId;
                        SetJsonBody(crossTenantUpdate, new UpdateAccountRequest { Name = "should-fail", Units = "USD" });
                        await AssertApiErrorAsync(scenario.AccountHandler.UpdateAsync(crossTenantUpdate, token), ApiErrorEnum.Forbidden, "Tenant admin updated a cross-tenant account.").ConfigureAwait(false);

                        // Regular mapped user cannot update; Update is not an allowed regular-user operation.
                        RequestContext mappedUserUpdate = CreateRequest(scenario.TenantAUser, scenario.TenantA.Id);
                        mappedUserUpdate.AccountId = scenario.TenantAUserAccountId;
                        SetJsonBody(mappedUserUpdate, new UpdateAccountRequest { Name = "should-fail", Units = "USD" });
                        await AssertApiErrorAsync(scenario.AccountHandler.UpdateAsync(mappedUserUpdate, token), ApiErrorEnum.Forbidden, "Regular mapped user updated an account.").ConfigureAwait(false);

                        // Unauthenticated update is rejected before any resource mutation.
                        RequestContext anonUpdate = CreateUnauthenticatedRequest(scenario.TenantA.Id);
                        anonUpdate.AccountId = scenario.TenantAUserAccountId;
                        SetJsonBody(anonUpdate, new UpdateAccountRequest { Name = "should-fail" });
                        await AssertApiErrorAsync(scenario.AccountHandler.UpdateAsync(anonUpdate, token), ApiErrorEnum.Unauthorized, "Unauthenticated account update was not rejected.").ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "regular_user_api_handlers_expose_only_mapped_account_surface", "Regular-user API calls are limited to self and mapped account resources", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        ResponseContext accountsResponse = await AssertApiSuccessAsync(scenario.AccountHandler.EnumerateAsync(CreateRequest(scenario.TenantAUser, null), token), "Regular user account enumeration failed.").ConfigureAwait(false);
                        EnumerationResult<Account> accounts = AssertEnumerationResult<Account>(accountsResponse, "Regular user account enumeration did not return an enumeration result.");
                        Assert(accounts.Objects.Count == 1 && accounts.Objects[0].Id == scenario.TenantAUserAccountId, "Regular user account enumeration returned unmapped or cross-tenant accounts.");

                        RequestContext mappedRead = CreateRequest(scenario.TenantAUser, null);
                        mappedRead.AccountId = scenario.TenantAUserAccountId;
                        await AssertApiSuccessAsync(scenario.AccountHandler.ReadAsync(mappedRead, token), "Regular user could not read a mapped account.").ConfigureAwait(false);

                        RequestContext unmappedRead = CreateRequest(scenario.TenantAUser, null);
                        unmappedRead.AccountId = scenario.TenantAUnmappedAccountId;
                        await AssertApiErrorAsync(scenario.AccountHandler.ReadAsync(unmappedRead, token), ApiErrorEnum.Forbidden, "Regular user read an unmapped same-tenant account.").ConfigureAwait(false);

                        RequestContext crossTenantRead = CreateRequest(scenario.TenantAUser, null);
                        crossTenantRead.AccountId = scenario.TenantBAccountId;
                        await AssertApiErrorAsync(scenario.AccountHandler.ReadAsync(crossTenantRead, token), ApiErrorEnum.Forbidden, "Regular user read a cross-tenant account.").ConfigureAwait(false);

                        RequestContext debitReq = CreateRequest(scenario.TenantAUser, null);
                        debitReq.AccountId = scenario.TenantAUserAccountId;
                        SetJsonBody(debitReq, new AddEntriesRequest { Amount = 12m, Notes = "mapped debit", Labels = new List<string> { "blue" }, Tags = new Dictionary<string, string> { { "color", "blue" } } });
                        await AssertApiSuccessAsync(scenario.EntryHandler.AddDebitsAsync(debitReq, token), "Regular user could not create a debit on a mapped account.").ConfigureAwait(false);

                        RequestContext unmappedDebitReq = CreateRequest(scenario.TenantAUser, null);
                        unmappedDebitReq.AccountId = scenario.TenantAUnmappedAccountId;
                        SetJsonBody(unmappedDebitReq, new AddEntriesRequest { Amount = 12m, Notes = "unmapped debit" });
                        await AssertApiErrorAsync(scenario.EntryHandler.AddDebitsAsync(unmappedDebitReq, token), ApiErrorEnum.Forbidden, "Regular user created a debit on an unmapped account.").ConfigureAwait(false);

                        RequestContext balanceReq = CreateRequest(scenario.TenantAUser, null);
                        balanceReq.AccountId = scenario.TenantAUserAccountId;
                        await AssertApiSuccessAsync(scenario.BalanceHandler.GetBalanceAsync(balanceReq, token), "Regular user could not read a mapped account balance.").ConfigureAwait(false);

                        RequestContext unmappedBalanceReq = CreateRequest(scenario.TenantAUser, null);
                        unmappedBalanceReq.AccountId = scenario.TenantAUnmappedAccountId;
                        await AssertApiErrorAsync(scenario.BalanceHandler.GetBalanceAsync(unmappedBalanceReq, token), ApiErrorEnum.Forbidden, "Regular user read an unmapped account balance.").ConfigureAwait(false);
                        await AssertApiErrorAsync(scenario.BalanceHandler.GetAllBalancesAsync(CreateRequest(scenario.TenantAUser, null), token), ApiErrorEnum.Forbidden, "Regular user enumerated all balances.").ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "identity_api_enforces_role_boundaries_and_redacts_secrets", "Identity API calls respect role boundaries and never return password hashes", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        RequestContext selfRead = CreateRequest(scenario.TenantAUser, scenario.TenantA.Id);
                        selfRead.UserId = scenario.TenantAUser.Id;
                        ResponseContext selfResponse = await AssertApiSuccessAsync(scenario.IdentityHandler.ReadUserAsync(selfRead, token), "Regular user could not read self.").ConfigureAwait(false);
                        User? self = selfResponse.Data as User;
                        Assert(self != null && String.IsNullOrEmpty(self.PasswordSha256), "Self-read user response exposed a password hash.");

                        RequestContext otherRead = CreateRequest(scenario.TenantAUser, scenario.TenantA.Id);
                        otherRead.UserId = scenario.TenantAOtherUser.Id;
                        await AssertApiErrorAsync(scenario.IdentityHandler.ReadUserAsync(otherRead, token), ApiErrorEnum.Forbidden, "Regular user read another same-tenant user.").ConfigureAwait(false);

                        await AssertApiErrorAsync(scenario.IdentityHandler.EnumerateUsersAsync(CreateRequest(scenario.TenantAUser, scenario.TenantA.Id), token), ApiErrorEnum.Forbidden, "Regular user enumerated users.").ConfigureAwait(false);

                        RequestContext regularCreate = CreateRequest(scenario.TenantAUser, scenario.TenantA.Id);
                        SetJsonBody(regularCreate, new CreateUserRequest { Email = "regular-created-" + UniqueSuffix(24) + "@example.com", Password = "password", IsAdmin = true, IsTenantAdmin = true });
                        await AssertApiErrorAsync(scenario.IdentityHandler.CreateUserAsync(regularCreate, token), ApiErrorEnum.Forbidden, "Regular user created or escalated a user.").ConfigureAwait(false);

                        RequestContext tenantAdminCreate = CreateRequest(scenario.TenantAAdmin, scenario.TenantA.Id);
                        SetJsonBody(tenantAdminCreate, new CreateUserRequest { Email = "tenant-admin-created-" + UniqueSuffix(24) + "@example.com", Password = "password", IsAdmin = false, IsTenantAdmin = false });
                        ResponseContext tenantAdminCreateResponse = await AssertApiSuccessAsync(scenario.IdentityHandler.CreateUserAsync(tenantAdminCreate, token), "Tenant admin could not create an own-tenant user.").ConfigureAwait(false);
                        User? created = tenantAdminCreateResponse.Data as User;
                        Assert(created != null && created.TenantId == scenario.TenantA.Id, "Tenant-admin-created user had the wrong tenant.");
                        Assert(created != null && String.IsNullOrEmpty(created.PasswordSha256), "Created user response exposed a password hash.");

                        RequestContext crossTenantCreate = CreateRequest(scenario.TenantAAdmin, scenario.TenantB.Id);
                        SetJsonBody(crossTenantCreate, new CreateUserRequest { Email = "cross-created-" + UniqueSuffix(24) + "@example.com", Password = "password" });
                        await AssertApiErrorAsync(scenario.IdentityHandler.CreateUserAsync(crossTenantCreate, token), ApiErrorEnum.Forbidden, "Tenant admin created a user in another tenant.").ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "entry_handler_preserves_complex_enumeration_filters_under_security_scope", "Entry enumeration applies amount, label, tag, and ordering filters within the authorized account", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        await scenario.Ledger.AddDebitAsync(scenario.TenantAUserAccountId, 10m, "matching low debit", null, false, new List<string> { "blue" }, new Dictionary<string, string> { { "color", "blue" } }, scenario.TenantA.Id, token).ConfigureAwait(false);
                        await scenario.Ledger.AddDebitAsync(scenario.TenantAUserAccountId, 25m, "matching high debit", null, false, new List<string> { "blue" }, new Dictionary<string, string> { { "color", "blue" } }, scenario.TenantA.Id, token).ConfigureAwait(false);
                        await scenario.Ledger.AddDebitAsync(scenario.TenantAUserAccountId, 60m, "too large debit", null, false, new List<string> { "blue" }, new Dictionary<string, string> { { "color", "blue" } }, scenario.TenantA.Id, token).ConfigureAwait(false);
                        await scenario.Ledger.AddDebitAsync(scenario.TenantAUserAccountId, 30m, "wrong metadata debit", null, false, new List<string> { "red" }, new Dictionary<string, string> { { "color", "red" } }, scenario.TenantA.Id, token).ConfigureAwait(false);
                        await scenario.Ledger.AddCreditAsync(scenario.TenantAUserAccountId, 40m, "wrong metadata credit", null, false, new List<string> { "blue" }, new Dictionary<string, string> { { "color", "green" } }, scenario.TenantA.Id, token).ConfigureAwait(false);

                        RequestContext enumerateReq = CreateRequest(scenario.TenantAUser, null);
                        enumerateReq.AccountId = scenario.TenantAUserAccountId;
                        SetJsonBody(enumerateReq, new EnumerationQuery
                        {
                            DebitMinimum = 5m,
                            DebitMaximum = 50m,
                            Labels = new List<string> { "blue" },
                            Tags = new Dictionary<string, string> { { "color", "blue" } },
                            Ordering = EnumerationOrderEnum.AmountDescending
                        });

                        ResponseContext response = await AssertApiSuccessAsync(scenario.EntryHandler.EnumerateAsync(enumerateReq, token), "Complex entry enumeration failed.").ConfigureAwait(false);
                        EnumerationResult<Entry> entries = AssertEnumerationResult<Entry>(response, "Entry enumeration did not return an enumeration result.");
                        List<Entry> matchingEntries = entries.Objects.Where(entry => entry.Type == EntryType.Debit).ToList();
                        Assert(matchingEntries.Count == 2, "Entry enumeration did not return exactly the matching debit entries.");
                        Assert(matchingEntries[0].Amount == 25m && matchingEntries[1].Amount == 10m, "Entry enumeration did not preserve amount-descending order.");
                        Assert(matchingEntries.All(entry => entry.TenantId == scenario.TenantA.Id && entry.AccountId == scenario.TenantAUserAccountId), "Entry enumeration returned data outside the authorized account.");
                    }),
                    new TestCaseDescriptor(suiteId, "effective_permissions_report_role_boundaries", "Effective permissions expose admin flags and scoped regular-user permissions", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        EffectivePermissionsResponse system = scenario.Authorization.GetEffectivePermissions(CreateRequest(scenario.SystemAdmin, scenario.TenantA.Id));
                        EffectivePermissionsResponse tenantAdmin = scenario.Authorization.GetEffectivePermissions(CreateRequest(scenario.TenantAAdmin, scenario.TenantA.Id));
                        EffectivePermissionsResponse regular = scenario.Authorization.GetEffectivePermissions(CreateRequest(scenario.TenantAUser, scenario.TenantA.Id));

                        Assert(system.IsAdmin, "System admin flag missing.");
                        Assert(!system.IsTenantAdmin, "System admin should not be marked as tenant admin unless explicitly set.");
                        Assert(tenantAdmin.IsTenantAdmin, "Tenant admin flag missing.");
                        Assert(!tenantAdmin.IsAdmin, "Tenant admin was incorrectly marked as system admin.");
                        Assert(!regular.IsAdmin && !regular.IsTenantAdmin, "Regular user was incorrectly marked admin.");
                        Assert(regular.Permissions.Any(permission =>
                            permission.ResourceType == "User" &&
                            permission.OperationType == "Read" &&
                            permission.ResourceId == scenario.TenantAUser.Id), "Regular user self-read permission missing.");
                    }),
                    new TestCaseDescriptor(suiteId, "admin_credential_auth_context_has_system_admin_privileges", "Admin credentials are treated as system-admin principals", _ =>
                    {
                        AuthContext adminContext = AuthContext.Success(new ApiKey("admin-test", true));
                        AuthContext regularContext = AuthContext.Success(new ApiKey("regular-test", false));

                        Assert(adminContext.IsAdmin, "Admin credential did not produce system-admin auth context.");
                        Assert(!regularContext.IsAdmin, "Regular credential produced system-admin auth context.");
                        return Task.CompletedTask;
                    }),
                    new TestCaseDescriptor(suiteId, "credential_enumeration_is_scoped_by_role_and_tenant", "Credential enumeration never leaks cross-tenant or other-user credentials", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        EnumerationResult<ApiKey> systemResult = await EnumerateCredentialsAsync(scenario, scenario.SystemAdmin, null, token).ConfigureAwait(false);
                        AssertCredentialVisible(systemResult, scenario.TenantAUserCredentialId, "System admin did not see tenant A credential.");
                        AssertCredentialVisible(systemResult, scenario.TenantBUserCredentialId, "System admin did not see tenant B credential.");

                        EnumerationResult<ApiKey> tenantAdminResult = await EnumerateCredentialsAsync(scenario, scenario.TenantAAdmin, null, token).ConfigureAwait(false);
                        AssertCredentialVisible(tenantAdminResult, scenario.TenantAUserCredentialId, "Tenant admin did not see own-tenant credential.");
                        AssertCredentialVisible(tenantAdminResult, scenario.TenantAOtherUserCredentialId, "Tenant admin did not see another user credential in their tenant.");
                        AssertCredentialHidden(tenantAdminResult, scenario.TenantBUserCredentialId, "Tenant admin saw a cross-tenant credential.");
                        await AssertCredentialForbiddenAsync(scenario.CredentialHandler.EnumerateAsync(CreateRequest(scenario.TenantAAdmin, scenario.TenantB.Id), token), "Tenant admin enumerated a different tenant's credentials.").ConfigureAwait(false);

                        EnumerationResult<ApiKey> regularResult = await EnumerateCredentialsAsync(scenario, scenario.TenantAUser, null, token).ConfigureAwait(false);
                        AssertCredentialVisible(regularResult, scenario.TenantAUserCredentialId, "Regular user did not see their own credential.");
                        AssertCredentialHidden(regularResult, scenario.TenantAOtherUserCredentialId, "Regular user saw another same-tenant user's credential.");
                        AssertCredentialHidden(regularResult, scenario.TenantBUserCredentialId, "Regular user saw a cross-tenant credential.");
                        await AssertCredentialForbiddenAsync(scenario.CredentialHandler.EnumerateAsync(CreateRequest(scenario.TenantAUser, scenario.TenantB.Id), token), "Regular user enumerated a different tenant's credentials.").ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "credential_management_enforces_tenant_and_self_boundaries", "Credential creation and revocation obey system, tenant-admin, and regular-user boundaries", async token =>
                    {
                        await using SecurityScenario scenario = await CreateSecurityScenarioAsync(token).ConfigureAwait(false);

                        string systemCreatedName = "system-created-" + UniqueSuffix(24);
                        await AssertCredentialSuccessAsync(CreateCredentialAsync(
                            scenario,
                            scenario.SystemAdmin,
                            null,
                            systemCreatedName,
                            scenario.TenantB.Id,
                            scenario.TenantBUser.Id,
                            false,
                            token), "System admin could not create a cross-tenant credential.").ConfigureAwait(false);
                        ApiKey systemCreated = await FindCredentialByNameAsync(scenario, systemCreatedName, token).ConfigureAwait(false);
                        Assert(systemCreated.TenantId == scenario.TenantB.Id, "System-created credential tenant mismatch.");
                        Assert(systemCreated.UserId == scenario.TenantBUser.Id, "System-created credential user mismatch.");
                        Assert(!systemCreated.IsAdmin, "Credential carried admin privileges instead of inheriting them from its user.");

                        await AssertCredentialForbiddenAsync(CreateCredentialAsync(
                            scenario,
                            scenario.TenantAAdmin,
                            null,
                            "tenant-admin-cross-" + UniqueSuffix(24),
                            scenario.TenantB.Id,
                            scenario.TenantBUser.Id,
                            false,
                            token), "Tenant admin created a cross-tenant credential.").ConfigureAwait(false);

                        string tenantAdminCreatedName = "tenant-admin-created-" + UniqueSuffix(24);
                        await AssertCredentialSuccessAsync(CreateCredentialAsync(
                            scenario,
                            scenario.TenantAAdmin,
                            null,
                            tenantAdminCreatedName,
                            null,
                            scenario.TenantAOtherUser.Id,
                            true,
                            token), "Tenant admin could not create an own-tenant credential.").ConfigureAwait(false);
                        ApiKey tenantAdminCreated = await FindCredentialByNameAsync(scenario, tenantAdminCreatedName, token).ConfigureAwait(false);
                        Assert(tenantAdminCreated.TenantId == scenario.TenantA.Id, "Tenant admin-created credential tenant mismatch.");
                        Assert(tenantAdminCreated.UserId == scenario.TenantAOtherUser.Id, "Tenant admin-created credential user mismatch.");
                        Assert(!tenantAdminCreated.IsAdmin, "Tenant admin created a system-admin credential.");

                        await AssertCredentialForbiddenAsync(CreateCredentialAsync(
                            scenario,
                            scenario.TenantAUser,
                            null,
                            "regular-cross-" + UniqueSuffix(24),
                            scenario.TenantB.Id,
                            scenario.TenantBUser.Id,
                            true,
                            token), "Regular user created a cross-tenant credential.").ConfigureAwait(false);

                        string regularCreatedName = "regular-created-" + UniqueSuffix(24);
                        await AssertCredentialSuccessAsync(CreateCredentialAsync(
                            scenario,
                            scenario.TenantAUser,
                            null,
                            regularCreatedName,
                            null,
                            scenario.TenantBUser.Id,
                            true,
                            token), "Regular user could not create their own credential.").ConfigureAwait(false);
                        ApiKey regularCreated = await FindCredentialByNameAsync(scenario, regularCreatedName, token).ConfigureAwait(false);
                        Assert(regularCreated.TenantId == scenario.TenantA.Id, "Regular user-created credential tenant mismatch.");
                        Assert(regularCreated.UserId == scenario.TenantAUser.Id, "Regular user-created credential was not mapped to self.");
                        Assert(!regularCreated.IsAdmin, "Regular user created a system-admin credential.");

                        await AssertCredentialForbiddenAsync(RevokeCredentialAsync(scenario, scenario.TenantAAdmin, null, scenario.TenantBUserCredentialId, token), "Tenant admin revoked a cross-tenant credential.").ConfigureAwait(false);
                        await AssertCredentialForbiddenAsync(RevokeCredentialAsync(scenario, scenario.TenantAUser, null, scenario.TenantAOtherUserCredentialId, token), "Regular user revoked another user's credential.").ConfigureAwait(false);
                        await AssertCredentialSuccessAsync(RevokeCredentialAsync(scenario, scenario.TenantAUser, null, scenario.TenantAUserCredentialId, token), "Regular user could not revoke their own credential.").ConfigureAwait(false);
                    }),
                    new TestCaseDescriptor(suiteId, "default_admin_bootstrap_repairs_admin_flags", "Default admin bootstrap repairs existing admin@netledger privileges", async token =>
                    {
                        await using Ledger ledger = CreateLedger();

                        Tenant? existingTenant = await ledger.Driver.Tenants.ReadAsync("default", token).ConfigureAwait(false);
                        if (existingTenant == null)
                        {
                            await ledger.Driver.Tenants.CreateAsync(new Tenant
                            {
                                Id = "default",
                                Name = "Default",
                                Active = true,
                                IsProtected = true
                            }, token).ConfigureAwait(false);
                        }

                        User? existing = await ledger.Driver.Users.ReadByEmailAsync("default", "admin@netledger", token).ConfigureAwait(false);
                        if (existing == null)
                        {
                            existing = await ledger.Driver.Users.CreateAsync(new User
                            {
                                Id = "usr_default_admin",
                                TenantId = "default",
                                FirstName = "Default",
                                LastName = "Admin",
                                Email = "admin@netledger",
                                PasswordSha256 = AuthService.HashPasswordSha256("password"),
                                IsAdmin = false,
                                IsTenantAdmin = false,
                                Active = false,
                                IsProtected = false
                            }, token).ConfigureAwait(false);
                        }
                        else
                        {
                            existing.IsAdmin = false;
                            existing.IsTenantAdmin = false;
                            existing.Active = false;
                            existing.IsProtected = false;
                            existing = await ledger.Driver.Users.UpdateAsync(existing, token).ConfigureAwait(false);
                        }

                        LoggingModule logging = new LoggingModule();
                        logging.Settings.EnableConsole = false;
                        using (AuthService authService = new AuthService(new ServerSettings(), logging, ledger.Driver))
                        {
                        }

                        User? repaired = await ledger.Driver.Users.ReadAsync(existing.TenantId, existing.Id, token).ConfigureAwait(false);
                        Assert(repaired != null, "Default admin was not found after bootstrap.");
                        Assert(repaired!.IsAdmin, "Default admin system-admin flag was not repaired.");
                        Assert(repaired.IsTenantAdmin, "Default admin tenant-admin flag was not repaired.");
                        Assert(repaired.Active, "Default admin active flag was not repaired.");
                        Assert(repaired.IsProtected, "Default admin protected flag was not repaired.");
                    })
                });
        }

        private static async Task<SecurityScenario> CreateSecurityScenarioAsync(CancellationToken token)
        {
            Ledger ledger = CreateLedger();

            try
            {
                Tenant tenantA = await ledger.Driver.Tenants.CreateAsync(new Tenant
                {
                    Name = "Security Tenant A",
                    Region = "test-a"
                }, token).ConfigureAwait(false);

                Tenant tenantB = await ledger.Driver.Tenants.CreateAsync(new Tenant
                {
                    Name = "Security Tenant B",
                    Region = "test-b"
                }, token).ConfigureAwait(false);

                User systemAdmin = await CreateScenarioUserAsync(ledger, tenantA.Id, "system-admin", true, false, token).ConfigureAwait(false);
                User tenantAAdmin = await CreateScenarioUserAsync(ledger, tenantA.Id, "tenant-a-admin", false, true, token).ConfigureAwait(false);
                User tenantBAdmin = await CreateScenarioUserAsync(ledger, tenantB.Id, "tenant-b-admin", false, true, token).ConfigureAwait(false);
                User tenantAUser = await CreateScenarioUserAsync(ledger, tenantA.Id, "tenant-a-user", false, false, token).ConfigureAwait(false);
                User tenantAOtherUser = await CreateScenarioUserAsync(ledger, tenantA.Id, "tenant-a-other-user", false, false, token).ConfigureAwait(false);
                User tenantBUser = await CreateScenarioUserAsync(ledger, tenantB.Id, "tenant-b-user", false, false, token).ConfigureAwait(false);

                string tenantAUserAccountId = await ledger.CreateAccountAsync(
                    "tenant-a-mapped",
                    null,
                    new List<string> { "mapped", "blue" },
                    new Dictionary<string, string> { { "color", "blue" }, { "scope", "tenant-a" } },
                    tenantA.Id,
                    token).ConfigureAwait(false);
                string tenantAUnmappedAccountId = await ledger.CreateAccountAsync(
                    "tenant-a-unmapped",
                    null,
                    new List<string> { "unmapped", "red" },
                    new Dictionary<string, string> { { "color", "red" }, { "scope", "tenant-a" } },
                    tenantA.Id,
                    token).ConfigureAwait(false);
                string tenantBAccountId = await ledger.CreateAccountAsync(
                    "tenant-b-mapped",
                    null,
                    new List<string> { "mapped", "green" },
                    new Dictionary<string, string> { { "color", "green" }, { "scope", "tenant-b" } },
                    tenantB.Id,
                    token).ConfigureAwait(false);

                await ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
                {
                    TenantId = tenantA.Id,
                    AccountId = tenantAUserAccountId,
                    UserId = tenantAUser.Id
                }, token).ConfigureAwait(false);

                await ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
                {
                    TenantId = tenantB.Id,
                    AccountId = tenantBAccountId,
                    UserId = tenantBUser.Id
                }, token).ConfigureAwait(false);

                LoggingModule logging = new LoggingModule();
                logging.Settings.EnableConsole = false;

                ServerSettings settings = new ServerSettings();
                AuthService authService = new AuthService(settings, logging, ledger.Driver);
                AuthorizationService authorizationService = new AuthorizationService(ledger.Driver, logging);
                ApiKeyHandler credentialHandler = new ApiKeyHandler(settings, logging, authService);
                AccountHandler accountHandler = new AccountHandler(settings, logging, ledger, authorizationService);
                EntryHandler entryHandler = new EntryHandler(settings, logging, ledger, authorizationService);
                BalanceHandler balanceHandler = new BalanceHandler(settings, logging, ledger, authorizationService);
                IdentityHandler identityHandler = new IdentityHandler(settings, logging, ledger.Driver, authService, authorizationService);

                ApiKey tenantAUserCredential = await authService.CreateApiKeyAsync("tenant-a-user-credential", false, tenantA.Id, tenantAUser.Id, token).ConfigureAwait(false);
                ApiKey tenantAOtherUserCredential = await authService.CreateApiKeyAsync("tenant-a-other-user-credential", false, tenantA.Id, tenantAOtherUser.Id, token).ConfigureAwait(false);
                ApiKey tenantBUserCredential = await authService.CreateApiKeyAsync("tenant-b-user-credential", false, tenantB.Id, tenantBUser.Id, token).ConfigureAwait(false);

                return new SecurityScenario(
                    ledger,
                    authorizationService,
                    authService,
                    credentialHandler,
                    accountHandler,
                    entryHandler,
                    balanceHandler,
                    identityHandler,
                    tenantA,
                    tenantB,
                    systemAdmin,
                    tenantAAdmin,
                    tenantBAdmin,
                    tenantAUser,
                    tenantAOtherUser,
                    tenantBUser,
                    tenantAUserAccountId,
                    tenantAUnmappedAccountId,
                    tenantBAccountId,
                    tenantAUserCredential.Id,
                    tenantAOtherUserCredential.Id,
                    tenantBUserCredential.Id);
            }
            catch
            {
                await ledger.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private static async Task<User> CreateScenarioUserAsync(
            Ledger ledger,
            string tenantId,
            string emailPrefix,
            bool isAdmin,
            bool isTenantAdmin,
            CancellationToken token)
        {
            return await ledger.Driver.Users.CreateAsync(new User
            {
                TenantId = tenantId,
                Email = emailPrefix + "-" + UniqueSuffix(24) + "@example.com",
                PasswordSha256 = Credential.HashSecret("password"),
                IsAdmin = isAdmin,
                IsTenantAdmin = isTenantAdmin
            }, token).ConfigureAwait(false);
        }

        private static RequestContext CreateRequest(User user, string? tenantId)
        {
            return new RequestContext
            {
                TenantId = tenantId,
                Auth = AuthContext.Success(user, new AuthSession
                {
                    TenantId = user.TenantId,
                    UserId = user.Id
                })
            };
        }

        private static async Task AssertBalanceAsync(
            Ledger ledger,
            string accountId,
            decimal expectedCommitted,
            decimal expectedPending,
            decimal expectedNetPending,
            int expectedPendingCredits,
            int expectedPendingDebits,
            CancellationToken token,
            string step)
        {
            Balance balance = await ledger.GetBalanceAsync(accountId, true, token).ConfigureAwait(false);
            Assert(balance.CommittedBalance == expectedCommitted, step + ": committed balance expected " + expectedCommitted + " but was " + balance.CommittedBalance + ".");
            Assert(balance.PendingBalance == expectedPending, step + ": pending balance expected " + expectedPending + " but was " + balance.PendingBalance + ".");
            Assert(balance.PendingCredits.Count == expectedPendingCredits, step + ": pending credit count expected " + expectedPendingCredits + " but was " + balance.PendingCredits.Count + ".");
            Assert(balance.PendingDebits.Count == expectedPendingDebits, step + ": pending debit count expected " + expectedPendingDebits + " but was " + balance.PendingDebits.Count + ".");
            Assert(balance.PendingCredits.Total - balance.PendingDebits.Total == expectedNetPending, step + ": net pending expected " + expectedNetPending + " but was " + (balance.PendingCredits.Total - balance.PendingDebits.Total) + ".");
            Assert(balance.PendingBalance == balance.CommittedBalance + balance.PendingCredits.Total - balance.PendingDebits.Total, step + ": pending balance formula is inconsistent.");
        }

        private static RequestContext CreateUnauthenticatedRequest(string? tenantId)
        {
            return new RequestContext
            {
                TenantId = tenantId,
                Auth = AuthContext.Failed(AuthResult.NoCredentials, "No credentials provided")
            };
        }

        private static void SetJsonBody(RequestContext req, object body)
        {
            string json = JsonSerializer.Serialize(body);
            req.Data = Encoding.UTF8.GetBytes(json);
            req.ContentLength = req.Data.Length;
        }

        private static async Task<ResponseContext> AssertApiSuccessAsync(Task<ResponseContext> responseTask, string message)
        {
            ResponseContext response = await responseTask.ConfigureAwait(false);
            Assert(response.Success, message + " Status=" + response.StatusCode + " Error=" + response.Error?.Description);
            return response;
        }

        private static async Task<ResponseContext> AssertApiErrorAsync(Task<ResponseContext> responseTask, ApiErrorEnum expectedError, string message)
        {
            ResponseContext response = await responseTask.ConfigureAwait(false);
            Assert(!response.Success && response.StatusCode == (int)expectedError, message + " Status=" + response.StatusCode + " Error=" + response.Error?.Description);
            return response;
        }

        private static EnumerationResult<T> AssertEnumerationResult<T>(ResponseContext response, string message)
        {
            EnumerationResult<T>? result = response.Data as EnumerationResult<T>;
            Assert(result != null, message);
            return result!;
        }

        private static async Task AssertPermitAsync(
            SecurityScenario scenario,
            User user,
            string? requestTenantId,
            string resourceType,
            string operationType,
            string? resourceId,
            CancellationToken token)
        {
            RequestContext req = CreateRequest(user, requestTenantId);
            ApplyAccountScopedResource(req, resourceType, resourceId);

            AuthorizationDecision decision = await scenario.Authorization.AuthorizeAsync(
                req,
                resourceType,
                operationType,
                resourceId,
                token).ConfigureAwait(false);

            Assert(decision.Permitted, "Expected permit for " + DescribeAuthorization(user, requestTenantId, resourceType, operationType, resourceId) + " but was denied: " + decision.Reason);
        }

        private static async Task AssertDenyAsync(
            SecurityScenario scenario,
            User user,
            string? requestTenantId,
            string resourceType,
            string operationType,
            string? resourceId,
            CancellationToken token)
        {
            RequestContext req = CreateRequest(user, requestTenantId);
            ApplyAccountScopedResource(req, resourceType, resourceId);

            AuthorizationDecision decision = await scenario.Authorization.AuthorizeAsync(
                req,
                resourceType,
                operationType,
                resourceId,
                token).ConfigureAwait(false);

            Assert(!decision.Permitted, "Expected deny for " + DescribeAuthorization(user, requestTenantId, resourceType, operationType, resourceId) + " but was permitted.");
        }

        private static void ApplyAccountScopedResource(RequestContext req, string resourceType, string? resourceId)
        {
            if ((String.Equals(resourceType, "Entry", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(resourceType, "Balance", StringComparison.OrdinalIgnoreCase)) &&
                !String.IsNullOrEmpty(resourceId))
            {
                req.AccountId = resourceId;
            }
        }

        private static async Task<EnumerationResult<ApiKey>> EnumerateCredentialsAsync(SecurityScenario scenario, User user, string? requestTenantId, CancellationToken token)
        {
            ResponseContext response = await scenario.CredentialHandler.EnumerateAsync(CreateRequest(user, requestTenantId), token).ConfigureAwait(false);
            Assert(response.Success, "Credential enumeration failed for " + user.Email + ": " + response.Error?.Description);
            EnumerationResult<ApiKey>? result = response.Data as EnumerationResult<ApiKey>;
            Assert(result != null, "Credential enumeration response did not contain an enumeration result.");
            return result!;
        }

        private static Task<ResponseContext> CreateCredentialAsync(
            SecurityScenario scenario,
            User user,
            string? requestTenantId,
            string name,
            string? tenantId,
            string? userId,
            bool? isAdmin,
            CancellationToken token)
        {
            RequestContext req = CreateRequest(user, requestTenantId);
            string body = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["name"] = name,
                ["tenantId"] = tenantId,
                ["userId"] = userId,
                ["isAdmin"] = isAdmin
            });
            req.Data = Encoding.UTF8.GetBytes(body);
            req.ContentLength = req.Data.Length;
            return scenario.CredentialHandler.CreateAsync(req, token);
        }

        private static Task<ResponseContext> RevokeCredentialAsync(SecurityScenario scenario, User user, string? requestTenantId, string credentialId, CancellationToken token)
        {
            RequestContext req = CreateRequest(user, requestTenantId);
            req.CredentialId = credentialId;
            return scenario.CredentialHandler.RevokeAsync(req, token);
        }

        private static async Task AssertCredentialSuccessAsync(Task<ResponseContext> responseTask, string message)
        {
            ResponseContext response = await responseTask.ConfigureAwait(false);
            Assert(response.Success, message + " Status=" + response.StatusCode + " Error=" + response.Error?.Description);
        }

        private static async Task AssertCredentialForbiddenAsync(Task<ResponseContext> responseTask, string message)
        {
            ResponseContext response = await responseTask.ConfigureAwait(false);
            Assert(!response.Success && response.StatusCode == (int)ApiErrorEnum.Forbidden, message + " Status=" + response.StatusCode);
        }

        private static void AssertCredentialVisible(EnumerationResult<ApiKey> result, string credentialId, string message)
        {
            Assert(result.Objects.Any(credential => credential.Id == credentialId), message);
        }

        private static void AssertCredentialHidden(EnumerationResult<ApiKey> result, string credentialId, string message)
        {
            Assert(!result.Objects.Any(credential => credential.Id == credentialId), message);
        }

        private static async Task<ApiKey> FindCredentialByNameAsync(SecurityScenario scenario, string name, CancellationToken token)
        {
            EnumerationResult<ApiKey> result = await scenario.AuthService.EnumerateApiKeysAsync(new ApiKeyEnumerationQuery
            {
                SearchTerm = name
            }, token).ConfigureAwait(false);
            ApiKey? credential = result.Objects.FirstOrDefault(item => item.Name == name);
            Assert(credential != null, "Credential " + name + " was not created.");
            return credential!;
        }

        private static string DescribeAuthorization(User user, string? requestTenantId, string resourceType, string operationType, string? resourceId)
        {
            return "user=" + user.Email +
                ", authTenant=" + user.TenantId +
                ", requestTenant=" + (requestTenantId ?? "<none>") +
                ", resource=" + resourceType +
                ", operation=" + operationType +
                ", resourceId=" + (resourceId ?? "<none>");
        }

        private static async Task RunProviderFullWorkflowAsync(DatabaseTypeEnum type, CancellationToken token)
        {
            DatabaseSettings settings = CreateProviderWorkflowSettings(type);
            string tenantId = "ten_test_" + UniqueSuffix(16);
            string userId = "usr_test_" + UniqueSuffix(16);

            await using Ledger ledger = new Ledger(settings);

            Tenant tenant = await ledger.Driver.Tenants.CreateAsync(new Tenant
            {
                Id = tenantId,
                Name = type + " Tenant",
                Region = "test"
            }, token).ConfigureAwait(false);
            Tenant? readTenant = await ledger.Driver.Tenants.ReadAsync(tenant.Id, token).ConfigureAwait(false);
            Assert(readTenant != null && readTenant.Name == tenant.Name, type + " tenant workflow failed.");

            User user = await ledger.Driver.Users.CreateAsync(new User
            {
                TenantId = tenantId,
                Email = "provider-" + UniqueSuffix(24) + "@example.com",
                PasswordSha256 = Credential.HashSecret("provider-password"),
                IsTenantAdmin = true
            }, token).ConfigureAwait(false);
            User? readUser = await ledger.Driver.Users.ReadByEmailAsync(tenantId, user.Email, token).ConfigureAwait(false);
            Assert(readUser != null && readUser.Id == user.Id && readUser.IsTenantAdmin, type + " user workflow failed.");

            AuthSession session = await ledger.Driver.AuthSessions.CreateAsync(new AuthSession
            {
                TenantId = tenantId,
                UserId = user.Id
            }, token).ConfigureAwait(false);
            AuthSession? readSession = await ledger.Driver.AuthSessions.ReadByTokenAsync(session.Token, token).ConfigureAwait(false);
            Assert(readSession != null && readSession.UserId == user.Id, type + " session workflow failed.");

            string accountId = await ledger.CreateAccountAsync(
                type.ToString().ToLowerInvariant() + "-matrix-account",
                100m,
                new List<string> { "provider", type.ToString().ToLowerInvariant() },
                new Dictionary<string, string> { { "engine", type.ToString() } },
                tenantId,
                token).ConfigureAwait(false);

            Account account = await ledger.GetAccountByIdAsync(accountId, token).ConfigureAwait(false);
            Assert(account.TenantId == tenantId, type + " account tenant did not persist.");
            Assert(account.Labels.Contains("provider"), type + " account label did not persist.");
            Assert(account.Tags["engine"] == type.ToString(), type + " account tag did not persist.");

            AccountUserMap map = await ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
            {
                TenantId = tenantId,
                AccountId = accountId,
                UserId = user.Id
            }, token).ConfigureAwait(false);
            bool mapExists = await ledger.Driver.AccountUserMaps.ExistsAsync(tenantId, accountId, user.Id, token).ConfigureAwait(false);
            Assert(mapExists && map.Id.Length == NetLedgerId.Length, type + " account-user map workflow failed.");

            EnumerationResult<Account> mappedAccounts = await ledger.Driver.Accounts.EnumerateAsync(new EnumerationQuery
            {
                TenantId = tenantId,
                MappedUserId = user.Id
            }, token).ConfigureAwait(false);
            Assert(mappedAccounts.Objects.Any(item => item.Id == accountId), type + " mapped account enumeration failed.");

            string entryId = await ledger.AddCreditAsync(
                accountId,
                25m,
                "provider certification credit",
                null,
                false,
                new List<string> { "credit", "certified" },
                new Dictionary<string, string> { { "engine", type.ToString() } },
                tenantId,
                token).ConfigureAwait(false);

            Entry entry = await ledger.GetEntryAsync(entryId, token).ConfigureAwait(false);
            Assert(entry.TenantId == tenantId, type + " entry tenant did not persist.");
            Assert(entry.Labels.Contains("certified"), type + " entry label did not persist.");
            Assert(entry.Tags["engine"] == type.ToString(), type + " entry tag did not persist.");

            EnumerationResult<Entry> entries = await ledger.Driver.Entries.EnumerateAsync(accountId, new EnumerationQuery
            {
                TenantId = tenantId,
                Labels = new List<string> { "credit", "certified" },
                Tags = new Dictionary<string, string> { { "engine", type.ToString() } },
                CreditMinimum = 20m,
                CreditMaximum = 30m
            }, token).ConfigureAwait(false);
            Assert(entries.Objects.Any(item => item.Id == entryId), type + " metadata/amount entry enumeration did not return the expected credit.");

            ApiKey credential = new ApiKey(type + "-credential", false)
            {
                TenantId = tenantId,
                UserId = userId,
                SecretKeySha256 = Credential.HashSecret("provider-secret"),
                SecretKeyLast4 = "cret"
            };

            ApiKey created = await ledger.Driver.ApiKeys.CreateAsync(credential, token).ConfigureAwait(false);
            ApiKey read = await ledger.Driver.ApiKeys.ReadByIdAsync(created.Id, token).ConfigureAwait(false);
            Assert(read.TenantId == tenantId, type + " credential tenant did not persist.");
            Assert(read.UserId == userId, type + " credential user did not persist.");
            Assert(read.SecretKeySha256 == credential.SecretKeySha256, type + " credential secret verifier did not persist.");

            AuditRecord audit = await ledger.Driver.AuditRecords.CreateAsync(new AuditRecord
            {
                TenantId = tenantId,
                PrincipalId = user.Id,
                PrincipalType = "User",
                EventType = "ProviderCertification",
                ResourceType = "Account",
                OperationType = "Read",
                ResourceId = accountId,
                Result = "Permit"
            }, token).ConfigureAwait(false);
            EnumerationResult<AuditRecord> auditRecords = await ledger.Driver.AuditRecords.EnumerateAsync(new EnumerationQuery { TenantId = tenantId }, token).ConfigureAwait(false);
            Assert(auditRecords.Objects.Any(item => item.Id == audit.Id), type + " audit workflow failed.");

            RequestHistoryEntry requestHistory = await ledger.Driver.RequestHistory.CreateAsync(new RequestHistoryEntry
            {
                TenantId = tenantId,
                PrincipalId = user.Id,
                PrincipalType = "User",
                Method = "GET",
                Path = "/v1/accounts",
                Url = "/v1/accounts?maxResults=25",
                StatusCode = 200,
                DurationMs = 6.5,
                CreatedUtc = DateTime.UtcNow.AddMinutes(-1),
                CompletedUtc = DateTime.UtcNow
            }, token).ConfigureAwait(false);
            RequestHistoryResult requestHistoryResult = await ledger.Driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter
            {
                TenantId = tenantId,
                PrincipalId = user.Id,
                MaxResults = 25
            }, token).ConfigureAwait(false);
            Assert(requestHistoryResult.Objects.Any(item => item.Id == requestHistory.Id), type + " request history workflow failed.");

            UserRole? viewer = await ledger.Driver.Rbac.ReadRoleByNameAsync(tenantId, "Viewer", token).ConfigureAwait(false);
            Assert(viewer != null && viewer.IsBuiltIn, type + " built-in RBAC role was not seeded.");
            List<RolePermissionMap> roleMaps = await ledger.Driver.Rbac.EnumerateRolePermissionMapsAsync(tenantId, viewer!.Id, token).ConfigureAwait(false);
            Assert(roleMaps.Count > 0, type + " built-in RBAC permissions were not seeded.");

            UserRoleAssignment roleAssignment = await ledger.Driver.Rbac.CreateUserRoleAssignmentAsync(new UserRoleAssignment
            {
                TenantId = tenantId,
                UserId = user.Id,
                RoleName = "Viewer",
                ResourceScope = "Resource",
                ResourceId = accountId
            }, token).ConfigureAwait(false);
            List<UserRoleAssignment> assignments = await ledger.Driver.Rbac.EnumerateUserRoleAssignmentsAsync(tenantId, user.Id, token).ConfigureAwait(false);
            Assert(assignments.Any(item => item.Id == roleAssignment.Id), type + " user role assignment workflow failed.");

            CredentialScopeAssignment credentialAssignment = await ledger.Driver.Rbac.CreateCredentialScopeAssignmentAsync(new CredentialScopeAssignment
            {
                TenantId = tenantId,
                CredentialId = created.Id,
                RoleName = "Viewer",
                ResourceScope = "Resource",
                ResourceId = accountId,
                OperationTypes = new List<string> { "Read" },
                ResourceTypes = new List<string> { "Account" }
            }, token).ConfigureAwait(false);
            List<CredentialScopeAssignment> credentialAssignments = await ledger.Driver.Rbac.EnumerateCredentialScopeAssignmentsAsync(tenantId, created.Id, token).ConfigureAwait(false);
            Assert(credentialAssignments.Any(item => item.Id == credentialAssignment.Id), type + " credential scope assignment workflow failed.");

            bool revoked = await ledger.Driver.AuthSessions.RevokeAsync(tenantId, session.Id, "provider certification", token).ConfigureAwait(false);
            AuthSession? revokedSession = await ledger.Driver.AuthSessions.ReadByTokenAsync(session.Token, token).ConfigureAwait(false);
            Assert(revoked && revokedSession != null && !revokedSession.Active, type + " session revoke workflow failed.");
        }

        private static bool IsProviderMatrixEnabled()
        {
            return String.Equals(Environment.GetEnvironmentVariable("NETLEDGER_PROVIDER_MATRIX"), "1", StringComparison.Ordinal);
        }

        private static DatabaseSettings CreateProviderWorkflowSettings(DatabaseTypeEnum type)
        {
            DatabaseSettings configured = GetConfiguredDatabaseSettings();
            if (!IsProviderMatrixEnabled() && configured.Type == type)
            {
                return CreateDatabaseSettings();
            }

            return CreateProviderSettings(type);
        }

        private static DatabaseSettings CreateProviderSettings(DatabaseTypeEnum type)
        {
            DatabaseSettings settings = NetLedgerTestConfiguration.CreateProviderSettings(type);
            if (settings.Type == DatabaseTypeEnum.Sqlite && String.IsNullOrWhiteSpace(settings.Filename))
            {
                settings.Filename = CreateDatabaseFilename();
            }

            return settings;
        }

        private static DatabaseSettings CloneDatabaseSettings(DatabaseSettings settings)
        {
            return NetLedgerTestConfiguration.CloneDatabaseSettings(settings);
        }

        private static ArchiveCatalogSettings CreateArchiveCatalogSettings()
        {
            return NetLedgerTestConfiguration.ToArchiveCatalogSettings(CreateDatabaseSettings());
        }

        private static ArchiveStoragePoolSettings? CreateS3ArchiveStorageSettings()
        {
            string? endpoint = Environment.GetEnvironmentVariable("NETLEDGER_ARCHIVE_TEST_S3_ENDPOINT");
            string? bucket = Environment.GetEnvironmentVariable("NETLEDGER_ARCHIVE_TEST_S3_BUCKET");
            if (String.IsNullOrWhiteSpace(endpoint) || String.IsNullOrWhiteSpace(bucket))
            {
                return null;
            }

            string? prefix = Environment.GetEnvironmentVariable("NETLEDGER_ARCHIVE_TEST_S3_PREFIX");
            string? region = Environment.GetEnvironmentVariable("NETLEDGER_ARCHIVE_TEST_S3_REGION");
            string? accessKey = Environment.GetEnvironmentVariable("NETLEDGER_ARCHIVE_TEST_S3_ACCESS_KEY");
            string? secretKey = Environment.GetEnvironmentVariable("NETLEDGER_ARCHIVE_TEST_S3_SECRET_KEY");

            return new ArchiveStoragePoolSettings
            {
                Id = "asp_touchstone_s3",
                Name = "Touchstone S3 archive test",
                Type = ArchiveStoragePoolType.S3,
                Bucket = bucket,
                Prefix = String.IsNullOrWhiteSpace(prefix) ? "netledger-archive-tests" : prefix,
                Region = String.IsNullOrWhiteSpace(region) ? "us-west-1" : region,
                Endpoint = endpoint,
                AccessKey = accessKey,
                SecretKey = secretKey,
                Format = ArchiveFormat.JsonlGzip,
                Compression = ArchiveCompression.Gzip
            };
        }

        private static ServerSettings CreateAutomaticArchiveServerSettings(string archiveEndpoint, bool globalAutomaticEnabled)
        {
            ServerSettings settings = new ServerSettings();
            settings.Archive.Enabled = true;
            settings.Archive.ArchiveServerEndpoint = archiveEndpoint;
            settings.Archive.DefaultActiveDataRetentionDays = 365;
            settings.Archive.Automatic.Enabled = globalAutomaticEnabled;
            settings.Archive.Automatic.MaxRetentionDays = 1;
            settings.Archive.Automatic.IntervalSeconds = 3600;
            settings.Archive.Automatic.InitialDelaySeconds = 0;
            settings.Archive.Automatic.MaxAccountsPerRun = 10000;
            settings.Archive.Automatic.MaxBatchRows = 10;
            settings.Archive.Automatic.DeleteAfterCommit = false;
            settings.Archive.Automatic.Retry.MaxAttempts = 1;
            settings.Archive.Automatic.Retry.InitialDelaySeconds = 0;
            settings.Archive.Automatic.Retry.MaxDelaySeconds = 0;
            return settings;
        }

        private static async Task<AccountArchivalSettings> UpsertAutomaticAccountOverrideAsync(
            Ledger ledger,
            string tenantId,
            string accountId,
            bool enabled,
            CancellationToken token)
        {
            return await ledger.Driver.AccountArchivalSettings.UpsertAsync(new AccountArchivalSettings
            {
                TenantId = tenantId,
                AccountId = accountId,
                Enabled = enabled,
                MaxRetentionDays = 1,
                IntervalSeconds = 3600,
                MaxBatchRows = 10,
                DeleteAfterCommit = false,
                RetryMaxAttempts = 1,
                RetryInitialDelaySeconds = 0,
                RetryMaxDelaySeconds = 0,
                NextAttemptUtc = DateTime.UtcNow.AddSeconds(-1)
            }, token).ConfigureAwait(false);
        }

        private static async Task CreateOldCommittedCreditAsync(
            Ledger ledger,
            string tenantId,
            string accountId,
            DateTime createdUtc,
            CancellationToken token)
        {
            DateTime timestamp = createdUtc.ToUniversalTime();
            await ledger.Driver.Entries.CreateAsync(new Entry
            {
                TenantId = tenantId,
                AccountId = accountId,
                Type = EntryType.Credit,
                Amount = 25m,
                Description = "old committed credit",
                IsCommitted = true,
                CommittedById = "test-archive-commit",
                CommittedUtc = timestamp,
                CreatedUtc = timestamp,
                LastUpdateUtc = timestamp
            }, token).ConfigureAwait(false);
        }

        private static List<string> ActiveLedgerTableNames()
        {
            return new List<string>
            {
                "accounts",
                "entries",
                "apikeys",
                "accountarchivalsettings",
                "accountlocks",
                "accountusermaps",
                "tenants",
                "users",
                "authsessions",
                "auditrecords",
                "requesthistory",
                "schemamigrations"
            };
        }

        private static async Task<HashSet<string>> ReadSqlTableNamesAsync(DatabaseSettings settings, CancellationToken token)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            string query;
            switch (settings.Type)
            {
                case DatabaseTypeEnum.Mysql:
                    query = "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE();";
                    break;
                case DatabaseTypeEnum.Postgresql:
                    query = "SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema() AND table_type = 'BASE TABLE';";
                    break;
                case DatabaseTypeEnum.SqlServer:
                    query = "SELECT name FROM sys.tables;";
                    break;
                case DatabaseTypeEnum.Sqlite:
                default:
                    query = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
                    break;
            }

            HashSet<string> tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (DbConnection connection = CreateDbConnection(settings))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    command.CommandTimeout = settings.ConnectionTimeoutSeconds;
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            if (!await reader.IsDBNullAsync(0, token).ConfigureAwait(false))
                            {
                                tableNames.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }

            return tableNames;
        }

        private static async Task<long> CountRowsByIdAsync(DatabaseSettings settings, string tableName, string id, CancellationToken token)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            using (DbConnection connection = CreateDbConnection(settings))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM " + QuoteIdentifier(settings.Type, tableName) + " WHERE " + QuoteIdentifier(settings.Type, "id") + " = " + QuoteValue(id) + ";";
                    command.CommandTimeout = settings.ConnectionTimeoutSeconds;
                    object? result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return Convert.ToInt64(result);
                }
            }
        }

        private static DbConnection CreateDbConnection(DatabaseSettings settings)
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
                    return new MySqlConnection(mysql.ConnectionString);

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
                    return new NpgsqlConnection(postgres.ConnectionString);

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
                    return new SqlConnection(sqlServer.ConnectionString);

                case DatabaseTypeEnum.Sqlite:
                default:
                    SQLitePCL.Batteries_V2.Init();
                    return new SqliteConnection("Data Source=" + settings.Filename + ";");
            }
        }

        private static string QuoteIdentifier(DatabaseTypeEnum type, string value)
        {
            if (type == DatabaseTypeEnum.Mysql) return "`" + value + "`";
            if (type == DatabaseTypeEnum.SqlServer) return "[" + value + "]";
            return "\"" + value + "\"";
        }

        private static string QuoteValue(string value)
        {
            return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        }

        private static void AssertPrimaryTextColumn(DataTable columns, string columnName, string tableName)
        {
            DataRow? column = FindColumn(columns, columnName);
            if (column == null)
            {
                throw new InvalidOperationException(tableName + "." + columnName + " column was missing.");
            }

            string type = column["type"]?.ToString() ?? String.Empty;
            string primaryKey = column["pk"]?.ToString() ?? String.Empty;
            Assert(String.Equals(type, "TEXT", StringComparison.OrdinalIgnoreCase), tableName + "." + columnName + " was not a TEXT column.");
            Assert(primaryKey == "1", tableName + "." + columnName + " was not the primary key.");
        }

        private static bool ColumnExists(DataTable columns, string columnName)
        {
            return FindColumn(columns, columnName) != null;
        }

        private static DataRow? FindColumn(DataTable columns, string columnName)
        {
            if (columns == null) return null;

            foreach (DataRow row in columns.Rows)
            {
                string name = row["name"]?.ToString() ?? String.Empty;
                if (String.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }

            return null;
        }

        private static long ReadCount(DataTable result)
        {
            if (result == null || result.Rows.Count == 0) return 0L;
            return Convert.ToInt64(result.Rows[0][0]);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertThrows<T>(Action action, string message)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }
    }
}
