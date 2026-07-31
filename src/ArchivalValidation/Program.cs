namespace ArchivalValidation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using NetLedger.Sdk;

    internal static class Program
    {
        private const string TenantId = "default";
        private const string StoragePoolId = "asp_validation";
        private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffffZ";

        private static readonly JsonSerializerOptions ConfigJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions JsonlOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public static async Task<int> Main()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "netledger-archival-validation-" + Guid.NewGuid().ToString("N"));
            bool succeeded = false;

            try
            {
                Directory.CreateDirectory(tempRoot);
                await RunAsync(tempRoot).ConfigureAwait(false);
                succeeded = true;
                Console.WriteLine("ArchivalValidation completed successfully.");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ArchivalValidation failed: " + e.Message);
                Console.Error.WriteLine(e.ToString());
                Console.Error.WriteLine("Temporary artifacts retained at: " + tempRoot);
                return 1;
            }
            finally
            {
                if (succeeded)
                {
                    TryDeleteDirectory(tempRoot);
                }
            }
        }

        private static async Task RunAsync(string tempRoot)
        {
            string repoRoot = FindRepoRoot();
            string framework = GetCurrentTargetFramework();
            int activePort = GetFreePort();
            int archivePort = GetFreePort();
            string activeUrl = "http://127.0.0.1:" + activePort.ToString(CultureInfo.InvariantCulture);
            string archiveUrl = "http://127.0.0.1:" + archivePort.ToString(CultureInfo.InvariantCulture);
            string activeDb = Path.Combine(tempRoot, "active.sqlite");
            string archiveDb = Path.Combine(tempRoot, "archive.sqlite");
            string objectRoot = Path.Combine(tempRoot, "objects");
            string activeConfig = Path.Combine(tempRoot, "active.json");
            string archiveConfig = Path.Combine(tempRoot, "archive.json");

            Directory.CreateDirectory(objectRoot);
            await File.WriteAllTextAsync(activeConfig, BuildActiveConfig(activePort, archiveUrl, activeDb)).ConfigureAwait(false);
            await File.WriteAllTextAsync(archiveConfig, BuildArchiveConfig(archivePort, activeUrl, archiveDb, objectRoot)).ConfigureAwait(false);

            using ServerProcess archiveServer = ServerProcess.Start("Archive Server", repoRoot, Path.Combine(repoRoot, "src", "NetLedger.Archive.Server", "NetLedger.Archive.Server.csproj"), framework, archiveConfig);
            await WaitForHealthAsync("Archive Server", archiveUrl, archiveServer).ConfigureAwait(false);

            using ServerProcess activeServer = ServerProcess.Start("NetLedger Server", repoRoot, Path.Combine(repoRoot, "src", "NetLedger.Server", "NetLedger.Server.csproj"), framework, activeConfig);
            await WaitForHealthAsync("NetLedger Server", activeUrl, activeServer).ConfigureAwait(false);

            using NetLedgerClient active = new NetLedgerClient(activeUrl, "validation-token", TenantId);
            using NetLedgerClient archive = new NetLedgerClient(archiveUrl, "validation-token", TenantId);
            active.TimeoutMs = 120000;
            archive.TimeoutMs = 120000;

            await ValidateArchiveHealthAndStorageAsync(archive).ConfigureAwait(false);
            await ValidateActiveExportAndColdRetrievalAsync(active, archive, activeDb).ConfigureAwait(false);
            await ValidateSdkMigrationLifecycleAsync(archive).ConfigureAwait(false);
        }

        private static async Task ValidateArchiveHealthAndStorageAsync(NetLedgerClient archive)
        {
            ArchiveHealth health = await archive.Archive.HealthAsync().ConfigureAwait(false);
            Expect(health.Healthy, "Archive Server health endpoint returned healthy.");

            List<ArchiveStoragePoolInfo> pools = await archive.Archive.StoragePoolsAsync().ConfigureAwait(false);
            ArchiveStoragePoolInfo? pool = pools.FirstOrDefault(p => String.Equals(p.Id, StoragePoolId, StringComparison.Ordinal));
            Expect(pool != null, "Archive Server registered the validation storage pool.");

            ArchiveStoragePoolHealthInfo poolHealth = await archive.Archive.StoragePoolHealthAsync(StoragePoolId).ConfigureAwait(false);
            Expect(poolHealth.Healthy, "Archive storage pool health endpoint returned healthy.");
        }

        private static async Task ValidateActiveExportAndColdRetrievalAsync(NetLedgerClient active, NetLedgerClient archive, string activeDb)
        {
            DateTime now = DateTime.UtcNow;
            DateTime oldStart = now.AddDays(-14).AddMinutes(-5);
            DateTime initialBalanceUtc = oldStart.AddMinutes(1);
            DateTime oldCreditUtc = oldStart.AddMinutes(10);
            DateTime oldDebitUtc = oldStart.AddMinutes(20);
            DateTime oldBalanceUtc = oldStart.AddMinutes(30);
            DateTime oldEnd = oldStart.AddHours(2);
            DateTime hotStart = now.AddHours(-1);
            DateTime hotEnd = now.AddHours(2);

            Account account = await active.Account.CreateAsync("Archival validation " + Guid.NewGuid().ToString("N")).ConfigureAwait(false);
            Expect(!String.IsNullOrWhiteSpace(account.Id), "Created active validation account.");

            List<Entry> coldCredits = await active.Entry.AddCreditsAsync(account.Id, new List<EntryInput>
            {
                new EntryInput(100.00m, "cold validation credit")
                {
                    Labels = new List<string> { "archived", "validation" },
                    Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["temperature"] = "cold",
                        ["batch"] = "primary"
                    }
                }
            }).ConfigureAwait(false);

            List<Entry> coldDebits = await active.Entry.AddDebitsAsync(account.Id, new List<EntryInput>
            {
                new EntryInput(25.00m, "cold validation debit")
                {
                    Labels = new List<string> { "archived", "validation" },
                    Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["temperature"] = "cold",
                        ["batch"] = "primary"
                    }
                }
            }).ConfigureAwait(false);

            List<string> coldIds = new List<string> { coldCredits[0].Id, coldDebits[0].Id };
            await active.Balance.CommitAsync(account.Id, coldIds).ConfigureAwait(false);
            await BackdateCommittedEntriesAsync(activeDb, account.Id, new Dictionary<string, DateTime>
            {
                [coldIds[0]] = oldCreditUtc,
                [coldIds[1]] = oldDebitUtc
            }, initialBalanceUtc, oldBalanceUtc).ConfigureAwait(false);
            Pass("Backdated committed cold rows in the temporary active SQLite database.");

            List<Entry> hotCredits = await active.Entry.AddCreditsAsync(account.Id, new List<EntryInput>
            {
                new EntryInput(40.00m, "hot validation credit")
                {
                    Labels = new List<string> { "hot", "validation" },
                    Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["temperature"] = "hot",
                        ["batch"] = "primary"
                    }
                }
            }).ConfigureAwait(false);
            string hotId = hotCredits[0].Id;
            await active.Balance.CommitAsync(account.Id, new List<string> { hotId }).ConfigureAwait(false);

            Balance currentBalance = await active.Balance.GetAsync(account.Id).ConfigureAwait(false);
            Expect(currentBalance.CommittedBalance == 115.00m, "Active current balance includes hot and cold committed entries.");

            EnumerationResult<Entry> activeHot = await active.Entry.EnumerateAsync(account.Id, new EntryEnumerationQuery
            {
                CreatedAfterUtc = hotStart,
                CreatedBeforeUtc = hotEnd,
                SearchTerm = "hot validation",
                Labels = new List<string> { "hot" },
                Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["temperature"] = "hot" },
                Ordering = EnumerationOrder.CreatedAscending,
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(activeHot.Objects, new[] { hotId }, "Active hot entry retrieval returns the hot entry only.");

            ArchiveExportResponse export = await active.Archive.ExportTenantAccountEntriesAsync(TenantId, account.Id, new ArchiveExportRequest
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                StoragePoolId = StoragePoolId,
                MaxBatchRows = 1,
                DeleteAfterCommit = false,
                IdempotencyKey = "archival-validation-export-" + Guid.NewGuid().ToString("N")
            }).ConfigureAwait(false);
            Expect(export.RowsExported == 2, "Active export archived two committed transaction entries.");
            Expect(export.Batches.Count == 2, "Active export split rows into two archive batches.");
            Expect(!String.IsNullOrWhiteSpace(export.MigrationId), "Active export returned a migration identifier.");
            Expect(!String.IsNullOrWhiteSpace(export.ManifestId), "Active export returned a manifest identifier.");

            await ValidateArchiveMetadataAsync(archive, account.Id, export, oldStart, oldEnd).ConfigureAwait(false);
            await ValidateColdEntryQueriesAsync(archive, account.Id, coldIds, oldStart, oldEnd).ConfigureAwait(false);
            await ValidateHotColdNegativeCasesAsync(active, archive, account.Id, oldStart, oldEnd, hotStart, hotEnd).ConfigureAwait(false);
        }

        private static async Task ValidateArchiveMetadataAsync(NetLedgerClient archive, string accountId, ArchiveExportResponse export, DateTime oldStart, DateTime oldEnd)
        {
            ArchiveMigrationInfo migration = await archive.Archive.MigrationAsync(export.MigrationId!).ConfigureAwait(false);
            ExpectStatus(migration.Status, "Committed", "Archive migration detail reached Committed status.");

            List<ArchiveMigrationBatchInfo> batches = await archive.Archive.MigrationBatchesAsync(migration.Id!).ConfigureAwait(false);
            Expect(batches.Count == 2, "Archive migration batch enumeration returned both batches.");
            Expect(batches.All(b => String.Equals(b.Status, "Verified", StringComparison.OrdinalIgnoreCase)), "Archive migration batches reached Verified status.");

            ArchiveManifestInfo manifest = await archive.Archive.ManifestAsync(export.ManifestId!).ConfigureAwait(false);
            Expect(manifest.RowCount == 2, "Archive manifest records the two exported entry rows.");
            ExpectStatus(manifest.Status, "Committed", "Archive manifest reached Committed status.");

            List<ArchiveManifestInfo> manifests = await archive.Archive.ManifestsAsync(new ArchiveQuery
            {
                TenantId = TenantId,
                AccountId = accountId,
                FromUtc = oldStart,
                ToUtc = oldEnd,
                MaxResults = 10
            }).ConfigureAwait(false);
            Expect(manifests.Any(m => String.Equals(m.Id, export.ManifestId, StringComparison.Ordinal)), "Archive manifest enumeration includes the export manifest.");

            List<ArchiveObjectInfo> objects = await archive.Archive.ManifestObjectsAsync(export.ManifestId!).ConfigureAwait(false);
            Expect(objects.Count == 2, "Archive manifest object enumeration returned both batch objects.");

            ArchiveObjectMetadataInfo metadata = await archive.Archive.ObjectMetadataAsync(objects[0].Id!).ConfigureAwait(false);
            Expect(metadata.Storage.Exists, "Archive object metadata confirms object exists in storage.");

            List<ArchiveBalanceCheckpointInfo> checkpoints = await archive.Archive.ManifestCheckpointsAsync(export.ManifestId!).ConfigureAwait(false);
            Expect(checkpoints.Count == 0, "Entry-only active export created no archived balance checkpoints.");

            List<ArchiveRangeInfo> ranges = await archive.Archive.RangesAsync(new ArchiveQuery
            {
                TenantId = TenantId,
                AccountId = accountId,
                FromUtc = oldStart,
                ToUtc = oldEnd,
                MaxResults = 10
            }).ConfigureAwait(false);
            Expect(ranges.Any(r => r.RowCount == 2), "Archive range enumeration includes the exported coverage range.");

            ArchiveVerificationResult verification = await archive.Archive.VerifyTenantAccountAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                MaxResults = 10
            }).ConfigureAwait(false);
            Expect(verification.IsValid && verification.CheckedManifests >= 1 && verification.CheckedObjects >= 2, "Archive verification checks committed manifest objects successfully.");
        }

        private static async Task ValidateColdEntryQueriesAsync(NetLedgerClient archive, string accountId, List<string> coldIds, DateTime oldStart, DateTime oldEnd)
        {
            EnumerationResult<Entry> coldAll = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                Ordering = "CreatedAscending",
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(coldAll.Objects, coldIds, "Archive cold retrieval returns both archived entries for the covered range.");

            EnumerationResult<Entry> coldSearch = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                Search = "cold validation",
                Ordering = "CreatedAscending",
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(coldSearch.Objects, coldIds, "Archive cold retrieval applies description search after manifest selection.");

            EnumerationResult<Entry> coldLabels = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                Labels = new List<string> { "archived" },
                Ordering = "CreatedAscending",
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(coldLabels.Objects, coldIds, "Archive cold retrieval applies label filters after manifest selection.");

            EnumerationResult<Entry> coldTags = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["temperature"] = "cold" },
                Ordering = "CreatedAscending",
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(coldTags.Objects, coldIds, "Archive cold retrieval applies tag filters after manifest selection.");

            EnumerationResult<Entry> coldEntries = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                Search = "cold validation",
                Labels = new List<string> { "archived" },
                Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["temperature"] = "cold" },
                Ordering = "CreatedAscending",
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(coldEntries.Objects, coldIds, "Archive cold retrieval returns both archived entries with search, label, and tag filters.");

            EnumerationResult<Entry> coldCredit = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                CreditMinimum = 100.00m,
                CreditMaximum = 100.00m,
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(coldCredit.Objects, new[] { coldIds[0] }, "Archive credit amount filters return the archived credit only.");

            EnumerationResult<Entry> coldDebit = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                DebitMinimum = 25.00m,
                DebitMaximum = 25.00m,
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(coldDebit.Objects, new[] { coldIds[1] }, "Archive debit amount filters return the archived debit only.");

            EnumerationResult<Entry> paged = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                Ordering = "CreatedAscending",
                MaxResults = 1
            }).ConfigureAwait(false);
            Expect((paged.Objects?.Count ?? 0) == 1 && !paged.EndOfResults && paged.RecordsRemaining == 1, "Archive cold retrieval paginates covered results.");

            EnumerationResult<Entry> miss = await archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
            {
                FromUtc = oldStart,
                ToUtc = oldEnd,
                Search = "not-present-in-archive",
                MaxResults = 10
            }).ConfigureAwait(false);
            Expect((miss.Objects?.Count ?? 0) == 0, "Archive covered-range search miss returns an empty result instead of an error.");
        }

        private static async Task ValidateHotColdNegativeCasesAsync(NetLedgerClient active, NetLedgerClient archive, string accountId, DateTime oldStart, DateTime oldEnd, DateTime hotStart, DateTime hotEnd)
        {
            await ExpectApiFailureAsync(
                "Active server rejects fully cold entry retrieval at the hot/cold boundary.",
                () => active.Entry.EnumerateAsync(accountId, new EntryEnumerationQuery
                {
                    CreatedAfterUtc = oldStart,
                    CreatedBeforeUtc = oldEnd,
                    MaxResults = 10
                }),
                409).ConfigureAwait(false);

            await ExpectApiFailureAsync(
                "Archive Server rejects uncovered hot entry retrieval.",
                () => archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
                {
                    FromUtc = hotStart,
                    ToUtc = hotEnd,
                    Search = "hot validation",
                    MaxResults = 10
                }),
                404,
                409).ConfigureAwait(false);

            await ExpectApiFailureAsync(
                "Archive Server rejects partially covered cold range retrieval.",
                () => archive.Archive.TenantEntriesAsync(TenantId, accountId, new ArchiveQuery
                {
                    FromUtc = oldStart.AddHours(-1),
                    ToUtc = oldEnd,
                    MaxResults = 10
                }),
                409).ConfigureAwait(false);

            await ExpectApiFailureAsync(
                "Archived balance-as-of rejects entry-only export without checkpoints.",
                () => archive.Archive.TenantBalanceAsOfAsync(TenantId, accountId, oldEnd),
                404).ConfigureAwait(false);
        }

        private static async Task ValidateSdkMigrationLifecycleAsync(NetLedgerClient archive)
        {
            DateTime start = DateTime.UtcNow.AddDays(-40);
            DateTime entryUtc = start.AddMinutes(5);
            DateTime end = start.AddHours(1);
            string sdkAccountId = "acct_archivalvalidation_sdk_" + Guid.NewGuid().ToString("N");
            string sdkEntryId = "ent_archivalvalidation_sdk_" + Guid.NewGuid().ToString("N");

            Entry sdkEntry = new Entry
            {
                Id = sdkEntryId,
                TenantId = TenantId,
                AccountId = sdkAccountId,
                Type = EntryType.Credit,
                Amount = 12.34m,
                Description = "sdk migration cold validation",
                IsCommitted = true,
                CommittedById = "bal_archivalvalidation_sdk_" + Guid.NewGuid().ToString("N"),
                CommittedUtc = entryUtc,
                CreatedUtc = entryUtc,
                LastUpdateUtc = entryUtc,
                Labels = new List<string> { "sdk", "archived" },
                Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["temperature"] = "cold" }
            };

            byte[] payload = BuildJsonlGzip(new[] { sdkEntry });
            string hash = ToSha256Hex(payload);

            ArchiveMigrationInfo migration = await archive.Archive.CreateMigrationAsync(new ArchiveMigrationRequest
            {
                TenantId = TenantId,
                AccountId = sdkAccountId,
                EntityType = "Entries",
                StoragePoolId = StoragePoolId,
                Format = "JsonlGzip",
                Compression = "Gzip",
                FromUtc = start,
                ToUtc = end,
                IdempotencyKey = "archival-validation-sdk-" + Guid.NewGuid().ToString("N")
            }).ConfigureAwait(false);
            ExpectStatus(migration.Status, "Pending", "SDK create migration returned a pending migration.");

            ArchiveMigrationBatchInfo batch = await archive.Archive.CreateMigrationBatchAsync(migration.Id!, new ArchiveMigrationBatchRequest
            {
                SequenceNumber = 0,
                RowCount = 1,
                ByteCount = payload.Length,
                ContentHashSha256 = hash
            }).ConfigureAwait(false);
            ExpectStatus(batch.Status, "Pending", "SDK create migration batch returned pending batch metadata.");

            using (MemoryStream payloadStream = new MemoryStream(payload))
            {
                batch = await archive.Archive.UploadMigrationBatchContentAsync(migration.Id!, batch.Id!, payloadStream, hash).ConfigureAwait(false);
            }
            ExpectStatus(batch.Status, "Uploaded", "SDK raw batch upload stored gzip JSONL content.");

            migration = await archive.Archive.SealMigrationAsync(migration.Id!).ConfigureAwait(false);
            ExpectStatus(migration.Status, "Sealing", "SDK seal migration moved migration into sealing state.");

            ArchiveManifestInfo manifest = await archive.Archive.CommitMigrationAsync(migration.Id!).ConfigureAwait(false);
            Expect(manifest.RowCount == 1, "SDK commit migration created a one-row manifest.");

            migration = await archive.Archive.MigrationAsync(migration.Id!).ConfigureAwait(false);
            ExpectStatus(migration.Status, "Committed", "SDK migration detail reports Committed after commit.");

            EnumerationResult<Entry> retrieved = await archive.Archive.TenantEntriesAsync(TenantId, sdkAccountId, new ArchiveQuery
            {
                FromUtc = start,
                ToUtc = end,
                Search = "sdk migration",
                Labels = new List<string> { "sdk" },
                Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["temperature"] = "cold" },
                MaxResults = 10
            }).ConfigureAwait(false);
            ExpectContainsOnlyIds(retrieved.Objects, new[] { sdkEntryId }, "SDK-created migration is searchable and retrievable as cold data.");

            ArchiveMigrationInfo abortMigration = await archive.Archive.CreateMigrationAsync(new ArchiveMigrationRequest
            {
                TenantId = TenantId,
                AccountId = "acct_archivalvalidation_abort_" + Guid.NewGuid().ToString("N"),
                EntityType = "Entries",
                StoragePoolId = StoragePoolId,
                Format = "JsonlGzip",
                Compression = "Gzip",
                FromUtc = start.AddDays(-2),
                ToUtc = start.AddDays(-1),
                IdempotencyKey = "archival-validation-abort-" + Guid.NewGuid().ToString("N")
            }).ConfigureAwait(false);

            abortMigration = await archive.Archive.AbortMigrationAsync(abortMigration.Id!).ConfigureAwait(false);
            ExpectStatus(abortMigration.Status, "Aborted", "SDK abort migration marks the migration aborted.");

            await ExpectApiFailureAsync(
                "Archive Server rejects commit for an aborted SDK migration.",
                () => archive.Archive.CommitMigrationAsync(abortMigration.Id!),
                409).ConfigureAwait(false);
        }

        private static async Task BackdateCommittedEntriesAsync(string dbPath, string accountId, Dictionary<string, DateTime> entryTimestamps, DateTime initialBalanceTimestamp, DateTime balanceTimestamp)
        {
            await using SqliteConnection connection = new SqliteConnection("Data Source=" + dbPath);
            await connection.OpenAsync().ConfigureAwait(false);

            List<string> balanceIds = new List<string>();
            foreach (string entryId in entryTimestamps.Keys)
            {
                await using SqliteCommand select = connection.CreateCommand();
                select.CommandText = "SELECT committedbyguid FROM entries WHERE id = $id LIMIT 1;";
                select.Parameters.AddWithValue("$id", entryId);
                object? value = await select.ExecuteScalarAsync().ConfigureAwait(false);
                string? committedById = value as string;
                if (!String.IsNullOrWhiteSpace(committedById))
                {
                    balanceIds.Add(committedById);
                }
            }

            List<string> initialBalanceIds = new List<string>();
            await using (SqliteCommand selectInitialBalances = connection.CreateCommand())
            {
                selectInitialBalances.CommandText = "SELECT id FROM entries WHERE accountguid = $accountid AND type = 'Balance';";
                selectInitialBalances.Parameters.AddWithValue("$accountid", accountId);
                await using SqliteDataReader reader = await selectInitialBalances.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    string id = reader.GetString(0);
                    if (!balanceIds.Contains(id, StringComparer.Ordinal))
                    {
                        initialBalanceIds.Add(id);
                    }
                }
            }

            using SqliteTransaction transaction = connection.BeginTransaction();
            foreach (string initialBalanceId in initialBalanceIds.Distinct(StringComparer.Ordinal))
            {
                await UpdateEntryTimestampsAsync(connection, transaction, initialBalanceId, initialBalanceTimestamp).ConfigureAwait(false);
            }

            foreach (KeyValuePair<string, DateTime> entryTimestamp in entryTimestamps)
            {
                await UpdateEntryTimestampsAsync(connection, transaction, entryTimestamp.Key, entryTimestamp.Value).ConfigureAwait(false);
            }

            foreach (string balanceId in balanceIds.Distinct(StringComparer.Ordinal))
            {
                await UpdateEntryTimestampsAsync(connection, transaction, balanceId, balanceTimestamp).ConfigureAwait(false);
            }

            transaction.Commit();
        }

        private static async Task UpdateEntryTimestampsAsync(SqliteConnection connection, SqliteTransaction transaction, string entryId, DateTime timestamp)
        {
            string formatted = timestamp.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);
            await using SqliteCommand update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                "UPDATE entries SET createdutc = $createdutc, committedutc = $committedutc, lastupdateutc = $lastupdateutc WHERE id = $id;";
            update.Parameters.AddWithValue("$createdutc", formatted);
            update.Parameters.AddWithValue("$committedutc", formatted);
            update.Parameters.AddWithValue("$lastupdateutc", formatted);
            update.Parameters.AddWithValue("$id", entryId);
            int rows = await update.ExecuteNonQueryAsync().ConfigureAwait(false);
            Expect(rows == 1, "Updated timestamp for temporary entry row " + entryId + ".");
        }

        private static byte[] BuildJsonlGzip(IEnumerable<Entry> entries)
        {
            using MemoryStream output = new MemoryStream();
            using (GZipStream gzip = new GZipStream(output, CompressionLevel.Optimal, true))
            {
                using StreamWriter writer = new StreamWriter(gzip, new UTF8Encoding(false));
                foreach (Entry entry in entries)
                {
                    writer.WriteLine(JsonSerializer.Serialize(entry, JsonlOptions));
                }
            }

            return output.ToArray();
        }

        private static string BuildActiveConfig(int port, string archiveUrl, string dbPath)
        {
            object config = new
            {
                Webserver = new
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false,
                    Cors = new
                    {
                        Enabled = true,
                        AllowedOrigins = new[] { "*" },
                        AllowedMethods = new[] { "OPTIONS", "HEAD", "GET", "PUT", "POST", "DELETE" },
                        AllowedHeaders = new[] { "*" },
                        ExposedHeaders = new[] { "Content-Type", "x-netledger-data-scope", "x-hostname", "x-api-version", "x-request-id" },
                        AllowCredentials = false,
                        MaxAgeSeconds = 600
                    }
                },
                Logging = new
                {
                    EnableConsole = false,
                    MinimumLevel = "Info",
                    LogRequests = false
                },
                Authentication = new
                {
                    Enabled = false,
                    DefaultAdminKey = "validation"
                },
                RequestHistory = new
                {
                    Enabled = false,
                    RetentionDays = 1,
                    MaxRequestBodyBytes = 0,
                    MaxResponseBodyBytes = 0
                },
                Archive = new
                {
                    Enabled = true,
                    ArchiveServerEndpoint = archiveUrl,
                    ServiceAccessKey = "validation",
                    ServiceSecretKey = "validation",
                    DefaultActiveDataRetentionDays = 1,
                    Tenants = Array.Empty<object>(),
                    Automatic = new
                    {
                        Enabled = false,
                        MaxRetentionDays = 1,
                        IntervalSeconds = 3600,
                        InitialDelaySeconds = 30,
                        MaxAccountsPerRun = 100,
                        MaxBatchRows = 50000,
                        DeleteAfterCommit = false,
                        StoragePoolId = StoragePoolId,
                        Retry = new
                        {
                            MaxAttempts = 1,
                            InitialDelaySeconds = 1,
                            MaxDelaySeconds = 1
                        }
                    }
                },
                Database = new
                {
                    Type = "Sqlite",
                    Filename = dbPath,
                    LogQueries = false
                }
            };

            return JsonSerializer.Serialize(config, ConfigJsonOptions);
        }

        private static string BuildArchiveConfig(int port, string activeUrl, string dbPath, string objectRoot)
        {
            object config = new
            {
                Webserver = new
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false,
                    Cors = new
                    {
                        Enabled = true,
                        AllowedOrigins = new[] { "*" },
                        AllowedMethods = new[] { "OPTIONS", "HEAD", "GET", "PUT", "POST", "DELETE" },
                        AllowedHeaders = new[] { "*" },
                        ExposedHeaders = new[] { "Content-Type", "x-netledger-data-scope", "x-request-id", "x-hostname", "x-api-version" },
                        AllowCredentials = false,
                        MaxAgeSeconds = 600
                    }
                },
                Logging = new
                {
                    EnableConsole = false,
                    MinimumLevel = "Info",
                    LogRequests = false
                },
                Authentication = new
                {
                    Enabled = false,
                    Mode = "None",
                    NetLedgerServerUrl = activeUrl,
                    RequireTlsForSecrets = false,
                    IntrospectionCacheSeconds = 0
                },
                Catalog = new
                {
                    Type = "Sqlite",
                    Filename = dbPath,
                    LogQueries = false
                },
                Archive = new
                {
                    DefaultStoragePoolId = StoragePoolId,
                    RequireCompleteCoverage = true,
                    MaxEnumerationResults = 1000,
                    MaxMigrationBatchRows = 50000,
                    MaxMigrationBatchBytes = 134217728,
                    AcceptedFormats = new[] { "JsonlGzip" },
                    PreferredFormat = "JsonlGzip"
                },
                StoragePools = new[]
                {
                    new
                    {
                        Id = StoragePoolId,
                        Name = "Archival validation filesystem pool",
                        Type = "FileSystem",
                        BasePath = objectRoot,
                        Prefix = "validation",
                        Format = "JsonlGzip",
                        Compression = "Gzip"
                    }
                },
                RequestHistory = new
                {
                    Enabled = false,
                    RetentionDays = 1,
                    MaxRequestBodyBytes = 0,
                    MaxResponseBodyBytes = 0
                }
            };

            return JsonSerializer.Serialize(config, ConfigJsonOptions);
        }

        private static async Task WaitForHealthAsync(string name, string baseUrl, ServerProcess process)
        {
            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            DateTime deadline = DateTime.UtcNow.AddSeconds(45);
            string healthUrl = baseUrl.TrimEnd('/') + "/v1/health";
            while (DateTime.UtcNow < deadline)
            {
                if (process.Process.HasExited)
                {
                    throw new ValidationFailureException(name + " exited before health check succeeded." + Environment.NewLine + process.Output);
                }

                try
                {
                    using HttpResponseMessage response = await client.GetAsync(healthUrl).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        Pass(name + " health endpoint is reachable.");
                        return;
                    }
                }
                catch
                {
                    // Server is still starting.
                }

                await Task.Delay(250).ConfigureAwait(false);
            }

            throw new ValidationFailureException(name + " health endpoint did not become reachable." + Environment.NewLine + process.Output);
        }

        private static async Task ExpectApiFailureAsync(string description, Func<Task> operation, params int[] allowedStatusCodes)
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (NetLedgerApiException e) when (allowedStatusCodes.Contains(e.StatusCode))
            {
                Pass(description + " HTTP " + e.StatusCode.ToString(CultureInfo.InvariantCulture) + ".");
                return;
            }

            throw new ValidationFailureException(description + " Expected HTTP status " + String.Join("/", allowedStatusCodes) + ".");
        }

        private static void ExpectContainsOnlyIds(IReadOnlyCollection<Entry>? entries, IEnumerable<string> expectedIds, string description)
        {
            List<string> actual = (entries ?? Array.Empty<Entry>()).Select(e => e.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            List<string> expected = expectedIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
            Expect(actual.SequenceEqual(expected, StringComparer.Ordinal), description + " Expected [" + String.Join(", ", expected) + "], actual [" + String.Join(", ", actual) + "].");
        }

        private static void ExpectStatus(string? actual, string expected, string description)
        {
            Expect(String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase), description + " Expected status " + expected + ", actual " + (actual ?? "<null>") + ".");
        }

        private static void Expect(bool condition, string description)
        {
            if (!condition)
            {
                throw new ValidationFailureException(description);
            }

            Pass(description);
        }

        private static void Pass(string description)
        {
            Console.WriteLine("[PASS] " + description);
        }

        private static string ToSha256Hex(byte[] payload)
        {
            return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        }

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static string FindRepoRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                DirectoryInfo? directory = new DirectoryInfo(start);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "src", "NetLedger.sln")))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new ValidationFailureException("Unable to locate repository root containing src/NetLedger.sln.");
        }

        private static string GetCurrentTargetFramework()
        {
            string? frameworkName = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<TargetFrameworkAttribute>()?
                .FrameworkName;

            if (!String.IsNullOrWhiteSpace(frameworkName))
            {
                int versionIndex = frameworkName.IndexOf("Version=v", StringComparison.OrdinalIgnoreCase);
                if (versionIndex >= 0)
                {
                    string version = frameworkName.Substring(versionIndex + "Version=v".Length);
                    int commaIndex = version.IndexOf(',');
                    if (commaIndex >= 0)
                    {
                        version = version.Substring(0, commaIndex);
                    }

                    string major = version.Split('.')[0];
                    if (!String.IsNullOrWhiteSpace(major))
                    {
                        return "net" + major + ".0";
                    }
                }
            }

            return "net8.0";
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        private sealed class ValidationFailureException : Exception
        {
            public ValidationFailureException(string message) : base(message)
            {
            }
        }

        private sealed class ServerProcess : IDisposable
        {
            private readonly StringBuilder _Output = new StringBuilder();
            private readonly object _OutputLock = new object();

            private ServerProcess(string name, Process process)
            {
                Name = name;
                Process = process;
            }

            public string Name { get; }

            public Process Process { get; }

            public string Output
            {
                get
                {
                    lock (_OutputLock)
                    {
                        return _Output.ToString();
                    }
                }
            }

            public static ServerProcess Start(string name, string workingDirectory, string projectPath, string framework, string configPath)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("run");
                startInfo.ArgumentList.Add("--no-build");
                startInfo.ArgumentList.Add("--project");
                startInfo.ArgumentList.Add(projectPath);
                startInfo.ArgumentList.Add("--framework");
                startInfo.ArgumentList.Add(framework);
                startInfo.ArgumentList.Add("--");
                startInfo.ArgumentList.Add("--file");
                startInfo.ArgumentList.Add(configPath);

                Process process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                ServerProcess server = new ServerProcess(name, process);
                process.OutputDataReceived += (_, e) => server.Append("OUT", e.Data);
                process.ErrorDataReceived += (_, e) => server.Append("ERR", e.Data);

                if (!process.Start())
                {
                    throw new ValidationFailureException("Failed to start " + name + ".");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Console.WriteLine("Started " + name + " on PID " + process.Id.ToString(CultureInfo.InvariantCulture) + ".");
                return server;
            }

            public void Dispose()
            {
                try
                {
                    if (!Process.HasExited)
                    {
                        Process.Kill(true);
                    }

                    Process.WaitForExit(5000);
                }
                catch
                {
                    // Best-effort process cleanup.
                }
                finally
                {
                    Process.Dispose();
                }
            }

            private void Append(string stream, string? line)
            {
                if (line == null) return;
                lock (_OutputLock)
                {
                    _Output.Append('[').Append(Name).Append(' ').Append(stream).Append("] ").AppendLine(line);
                }
            }
        }
    }
}
