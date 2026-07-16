namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;
    using NetLedger.Database;
    using NetLedger.Server.API.Agnostic;
    using NetLedger.Server.Authentication;
    using NetLedger.Server.Models;
    using NetLedger.Server.Models.Identity;
    using NetLedger.Server.Settings;
    using SyslogLogging;
    using Touchstone.Core;

    /// <summary>
    /// Shared Touchstone suites for NetLedger.
    /// </summary>
    public static class NetLedgerSuites
    {
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledgerA = new Ledger(filename);
                        await using Ledger ledgerB = new Ledger(filename);
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
                        ApiKey credential = new ApiKey("worker", false)
                        {
                            TenantId = "ten_test",
                            UserId = "usr_test",
                            SecretKeySha256 = Credential.HashSecret("sk_test_secret"),
                            SecretKeyLast4 = "cret"
                        };

                        ApiKey created = await ledger.Driver.ApiKeys.CreateAsync(credential, token).ConfigureAwait(false);
                        ApiKey read = await ledger.Driver.ApiKeys.ReadByIdAsync(created.Id, token).ConfigureAwait(false);
                        Assert(read.TenantId == "ten_test", "Credential tenant ID did not persist.");
                        Assert(read.UserId == "usr_test", "Credential user ID did not persist.");
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);

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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
                        string tenantId = "ten_test";
                        string accountId = await ledger.CreateAccountAsync("mapped", null, null, null, tenantId, token).ConfigureAwait(false);
                        AccountUserMap map = await ledger.Driver.AccountUserMaps.CreateAsync(new AccountUserMap
                        {
                            TenantId = tenantId,
                            AccountId = accountId,
                            UserId = "usr_test"
                        }, token).ConfigureAwait(false);

                        bool exists = await ledger.Driver.AccountUserMaps.ExistsAsync(tenantId, accountId, map.UserId, token).ConfigureAwait(false);
                        Assert(exists, "Account user map did not persist.");
                    }),
                    new TestCaseDescriptor(suiteId, "mapped_account_enumeration_filters", "Mapped account enumeration excludes unmapped accounts", async token =>
                    {
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
                        string tenantId = "ten_test";
                        string userId = "usr_test";
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
                        AuditRecord record = await ledger.Driver.AuditRecords.CreateAsync(new AuditRecord
                        {
                            TenantId = "ten_test",
                            EventType = "Authorization",
                            ResourceType = "Account",
                            OperationType = "Read",
                            Result = "Denied",
                            Reason = "No matching permission"
                        }, token).ConfigureAwait(false);

                        EnumerationResult<AuditRecord> result = await ledger.Driver.AuditRecords.EnumerateAsync(new EnumerationQuery { TenantId = "ten_test" }, token).ConfigureAwait(false);
                        Assert(result.Objects.Any(item => item.Id == record.Id), "Audit record did not persist.");
                    }),
                    new TestCaseDescriptor(suiteId, "rbac_builtin_assignment_permits", "Built-in RBAC assignment permits matching operation", async token =>
                    {
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
                        string tenantId = "ten_test";
                        UserRoleAssignment assignment = await ledger.Driver.Rbac.CreateUserRoleAssignmentAsync(new UserRoleAssignment
                        {
                            TenantId = tenantId,
                            UserId = "usr_test",
                            RoleName = "Viewer",
                            ResourceScope = "Resource",
                            ResourceId = "acct_test"
                        }, token).ConfigureAwait(false);

                        List<UserRoleAssignment> assignments = await ledger.Driver.Rbac.EnumerateUserRoleAssignmentsAsync(tenantId, "usr_test", token).ConfigureAwait(false);
                        UserRole? role = await ledger.Driver.Rbac.ReadRoleByNameAsync(tenantId, "Viewer", token).ConfigureAwait(false);
                        List<RolePermissionMap> maps = role != null
                            ? await ledger.Driver.Rbac.EnumerateRolePermissionMapsAsync(tenantId, role.Id, token).ConfigureAwait(false)
                            : new List<RolePermissionMap>();

                        Assert(assignments.Any(item => item.Id == assignment.Id), "RBAC assignment did not persist.");
                        Assert(role != null && role.IsBuiltIn, "Built-in Viewer role was not seeded.");
                        Assert(maps.Count > 0, "Built-in Viewer role has no permission maps.");
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);
                        DateTime now = DateTime.UtcNow;

                        RequestHistoryEntry tenantAUserA = await ledger.Driver.RequestHistory.CreateAsync(new RequestHistoryEntry
                        {
                            TenantId = "ten_a",
                            PrincipalId = "usr_a",
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
                            TenantId = "ten_a",
                            PrincipalId = "usr_b",
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
                            TenantId = "ten_b",
                            PrincipalId = "usr_c",
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
                            TenantId = "ten_a",
                            MaxResults = 100
                        }, token).ConfigureAwait(false);
                        Assert(tenantAResult.Objects.Any(item => item.Id == tenantAUserA.Id), "Tenant A request history entry was not returned.");
                        Assert(tenantAResult.Objects.Any(item => item.Id == tenantAUserB.Id), "Tenant A second request history entry was not returned.");
                        Assert(!tenantAResult.Objects.Any(item => item.Id == tenantBUser.Id), "Cross-tenant request history leaked into tenant-scoped enumeration.");

                        RequestHistoryResult tenantAUserAResult = await ledger.Driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter
                        {
                            TenantId = "ten_a",
                            PrincipalId = "usr_a",
                            MaxResults = 100
                        }, token).ConfigureAwait(false);
                        Assert(tenantAUserAResult.Objects.Count == 1 && tenantAUserAResult.Objects[0].Id == tenantAUserA.Id, "Principal-scoped request history returned the wrong records.");

                        RequestHistoryEntry? tenantScopedCrossRead = await ledger.Driver.RequestHistory.ReadAsync("ten_a", tenantBUser.Id, token).ConfigureAwait(false);
                        Assert(tenantScopedCrossRead == null, "Tenant-scoped read returned a cross-tenant request history entry.");

                        RequestHistoryEntry? systemRead = await ledger.Driver.RequestHistory.ReadAsync(null, tenantBUser.Id, token).ConfigureAwait(false);
                        Assert(systemRead != null && systemRead.Id == tenantBUser.Id, "Unscoped system read did not return the cross-tenant request history entry.");

                        bool crossTenantDeleted = await ledger.Driver.RequestHistory.DeleteAsync("ten_a", tenantBUser.Id, token).ConfigureAwait(false);
                        Assert(!crossTenantDeleted, "Tenant-scoped delete removed a cross-tenant request history entry.");
                        RequestHistoryEntry? tenantBStillExists = await ledger.Driver.RequestHistory.ReadAsync("ten_b", tenantBUser.Id, token).ConfigureAwait(false);
                        Assert(tenantBStillExists != null, "Cross-tenant request history entry was removed by a scoped delete.");

                        RequestHistorySummary tenantASummary = await ledger.Driver.RequestHistory.SummarizeAsync(new RequestHistoryFilter
                        {
                            TenantId = "ten_a",
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
                            TenantId = "ten_a",
                            PathContains = "/v1/accounts",
                            MaxResults = 100
                        }, token).ConfigureAwait(false);
                        Assert(deletedTenantA == 2, "Tenant-scoped bulk delete removed the wrong number of entries.");

                        RequestHistoryResult allRemaining = await ledger.Driver.RequestHistory.EnumerateAsync(new RequestHistoryFilter
                        {
                            MaxResults = 100
                        }, token).ConfigureAwait(false);
                        Assert(!allRemaining.Objects.Any(item => item.TenantId == "ten_a"), "Tenant A request history entries remained after scoped delete.");
                        Assert(allRemaining.Objects.Any(item => item.Id == tenantBUser.Id), "Scoped bulk delete removed a cross-tenant request history entry.");
                    })
                });
        }

        private static string CreateDatabaseFilename()
        {
            return Path.Combine(Path.GetTempPath(), "netledger-test-" + UniqueSuffix(24) + ".db");
        }

        private static string UniqueSuffix(int length)
        {
            string value = NetLedgerId.Generate("tst");
            return value.Length <= length ? value : value.Substring(value.Length - length);
        }

        private static TestSuiteDescriptor ProviderMatrixSuite()
        {
            string suiteId = "provider_matrix";
            return new TestSuiteDescriptor(
                suiteId,
                "Live SQL provider certification",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(suiteId, "sqlite_full_v3_workflows", "SQLite supports all v3 workflows", token =>
                        RunProviderFullWorkflowAsync(DatabaseTypeEnum.Sqlite, token)),
                    new TestCaseDescriptor(suiteId, "mysql_full_v3_workflows", "MySQL supports all v3 workflows", token =>
                        RunProviderFullWorkflowAsync(DatabaseTypeEnum.Mysql, token)),
                    new TestCaseDescriptor(suiteId, "postgresql_full_v3_workflows", "PostgreSQL supports all v3 workflows", token =>
                        RunProviderFullWorkflowAsync(DatabaseTypeEnum.Postgresql, token)),
                    new TestCaseDescriptor(suiteId, "sqlserver_full_v3_workflows", "SQL Server supports all v3 workflows", token =>
                        RunProviderFullWorkflowAsync(DatabaseTypeEnum.SqlServer, token))
                });
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
                        string filename = CreateDatabaseFilename();
                        await using Ledger ledger = new Ledger(filename);

                        await ledger.Driver.Tenants.CreateAsync(new Tenant
                        {
                            Id = "default",
                            Name = "Default",
                            Active = true,
                            IsProtected = true
                        }, token).ConfigureAwait(false);

                        User existing = await ledger.Driver.Users.CreateAsync(new User
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
            Ledger ledger = new Ledger(CreateDatabaseFilename());

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
            if (type != DatabaseTypeEnum.Sqlite && !IsProviderMatrixEnabled())
            {
                return;
            }

            DatabaseSettings settings = CreateProviderSettings(type);
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

        private static DatabaseSettings CreateProviderSettings(DatabaseTypeEnum type)
        {
            string prefix = "NETLEDGER_" + type.ToString().ToUpperInvariant() + "_";
            if (type == DatabaseTypeEnum.Sqlite)
            {
                return new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = CreateDatabaseFilename()
                };
            }

            DatabaseSettings settings = new DatabaseSettings
            {
                Type = type,
                Hostname = Environment.GetEnvironmentVariable(prefix + "HOST") ?? "localhost",
                DatabaseName = Environment.GetEnvironmentVariable(prefix + "DATABASE") ?? "netledger",
                Username = Environment.GetEnvironmentVariable(prefix + "USER") ?? (type == DatabaseTypeEnum.SqlServer ? "sa" : "netledger"),
                Password = Environment.GetEnvironmentVariable(prefix + "PASSWORD") ?? (type == DatabaseTypeEnum.SqlServer ? "NetLedger!Passw0rd" : "netledger"),
                RequireEncryption = false,
                ConnectionTimeoutSeconds = 60
            };

            string? port = Environment.GetEnvironmentVariable(prefix + "PORT");
            if (!String.IsNullOrEmpty(port) && Int32.TryParse(port, out int parsedPort))
            {
                settings.Port = parsedPort;
            }

            return settings;
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
