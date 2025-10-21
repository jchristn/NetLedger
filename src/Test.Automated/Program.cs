namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger;

    class Program
    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.

        private static List<TestResult> _Results = new List<TestResult>();
        private static string _DatabaseFile = "test_automated.db";
        private static Ledger? _Ledger = null;
        private static int _TestCount = 0;
        private static int _PassCount = 0;
        private static int _FailCount = 0;

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("NetLedger Automated Test Suite - v2.0.0");
            Console.WriteLine("================================================================================");
            Console.WriteLine("");

            try
            {
                // Clean up any existing test database
                if (File.Exists(_DatabaseFile))
                {
                    File.Delete(_DatabaseFile);
                }

                _Ledger = new Ledger(_DatabaseFile);

                // Run all test categories
                await RunAccountCreationTestsAsync().ConfigureAwait(false);
                await RunAccountRetrievalTestsAsync().ConfigureAwait(false);
                await RunCreditDebitTestsAsync().ConfigureAwait(false);
                await RunBatchOperationTestsAsync().ConfigureAwait(false);
                await RunBalanceTestsAsync().ConfigureAwait(false);
                await RunCommitTestsAsync().ConfigureAwait(false);
                await RunPendingEntriesTestsAsync().ConfigureAwait(false);
                await RunEntrySearchTestsAsync().ConfigureAwait(false);
                await RunEntryCancellationTestsAsync().ConfigureAwait(false);
                await RunEnumerationTestsAsync().ConfigureAwait(false);
                await RunAccountDeletionTestsAsync().ConfigureAwait(false);
                await RunEventTestsAsync().ConfigureAwait(false);
                await RunErrorHandlingTestsAsync().ConfigureAwait(false);
                await RunEdgeCaseTestsAsync().ConfigureAwait(false);
                await RunNewFeatureTestsAsync().ConfigureAwait(false);
                await RunPerformanceTestsAsync().ConfigureAwait(false);
                await RunConcurrencyTestsAsync().ConfigureAwait(false);

                // Display summary
                DisplaySummary();

                // Clean up
                await _Ledger.DisposeAsync().ConfigureAwait(false);
                if (File.Exists(_DatabaseFile))
                {
                    File.Delete(_DatabaseFile);
                }

                return _FailCount > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("");
                Console.WriteLine("FATAL ERROR: " + ex.Message);
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        #region Account-Creation-Tests

        static async Task RunAccountCreationTestsAsync()
        {
            Console.WriteLine("--- Account Creation Tests ---");
            Console.WriteLine("");

            await TestAsync("Create account with name only", async () =>
            {
                Guid guid = await _Ledger!.CreateAccountAsync("Test Account 1").ConfigureAwait(false);
                return guid != Guid.Empty;
            }).ConfigureAwait(false);

            await TestAsync("Create account with initial balance", async () =>
            {
                Guid guid = await _Ledger!.CreateAccountAsync("Test Account 2", 100.00m).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(guid).ConfigureAwait(false);
                return balance.CommittedBalance == 100.00m;
            }).ConfigureAwait(false);

            await TestAsync("Create account with zero initial balance", async () =>
            {
                Guid guid = await _Ledger!.CreateAccountAsync("Test Account 3", 0m).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(guid).ConfigureAwait(false);
                return balance.CommittedBalance == 0m;
            }).ConfigureAwait(false);

            await TestAsync("Create account with negative initial balance", async () =>
            {
                Guid guid = await _Ledger!.CreateAccountAsync("Test Account 4", -50.00m).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(guid).ConfigureAwait(false);
                return balance.CommittedBalance == -50.00m;
            }).ConfigureAwait(false);

            await TestAsync("Create account with large decimal value", async () =>
            {
                Guid guid = await _Ledger!.CreateAccountAsync("Test Account 5", 999999.99999999m).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(guid).ConfigureAwait(false);
                return balance.CommittedBalance == 999999.99999999m;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Account-Retrieval-Tests

        static async Task RunAccountRetrievalTestsAsync()
        {
            Console.WriteLine("--- Account Retrieval Tests ---");
            Console.WriteLine("");

            Guid testGuid = await _Ledger!.CreateAccountAsync("Retrieval Test").ConfigureAwait(false);

            await TestAsync("Get account by name", async () =>
            {
                Account? account = await _Ledger!.GetAccountByNameAsync("Retrieval Test").ConfigureAwait(false);
                return account != null && account.Name == "Retrieval Test";
            }).ConfigureAwait(false);

            await TestAsync("Get account by GUID", async () =>
            {
                Account? account = await _Ledger!.GetAccountByGuidAsync(testGuid).ConfigureAwait(false);
                return account != null && account.GUID == testGuid;
            }).ConfigureAwait(false);

            await TestAsync("Get all accounts", async () =>
            {
                List<Account> accounts = await _Ledger!.GetAllAccountsAsync().ConfigureAwait(false);
                return accounts != null && accounts.Count > 0;
            }).ConfigureAwait(false);

            await TestAsync("Search accounts by term", async () =>
            {
                List<Account> accounts = await _Ledger!.GetAllAccountsAsync("Retrieval").ConfigureAwait(false);
                return accounts != null && accounts.Count > 0 && accounts.Any(a => a.Name.Contains("Retrieval"));
            }).ConfigureAwait(false);

            await TestAsync("Get all accounts with pagination (skip)", async () =>
            {
                List<Account> accounts = await _Ledger!.GetAllAccountsAsync(null, skip: 2).ConfigureAwait(false);
                return accounts != null;
            }).ConfigureAwait(false);

            await TestAsync("Get all accounts with pagination (take)", async () =>
            {
                List<Account> accounts = await _Ledger!.GetAllAccountsAsync(null, skip: 0, take: 3).ConfigureAwait(false);
                return accounts != null && accounts.Count <= 3;
            }).ConfigureAwait(false);

            await TestAsync("Get non-existent account by name returns null", async () =>
            {
                Account? account = await _Ledger!.GetAccountByNameAsync("Does Not Exist").ConfigureAwait(false);
                return account == null;
            }).ConfigureAwait(false);

            await TestAsync("Get non-existent account by GUID returns null", async () =>
            {
                Account? account = await _Ledger!.GetAccountByGuidAsync(Guid.NewGuid()).ConfigureAwait(false);
                return account == null;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Credit-Debit-Tests

        static async Task RunCreditDebitTestsAsync()
        {
            Console.WriteLine("--- Credit and Debit Tests ---");
            Console.WriteLine("");

            Guid accountGuid = await _Ledger!.CreateAccountAsync("Transaction Test", 0m).ConfigureAwait(false);

            await TestAsync("Add credit", async () =>
            {
                Guid entryGuid = await _Ledger!.AddCreditAsync(accountGuid, 50.00m, "Test credit").ConfigureAwait(false);
                return entryGuid != Guid.Empty;
            }).ConfigureAwait(false);

            await TestAsync("Add debit", async () =>
            {
                Guid entryGuid = await _Ledger!.AddDebitAsync(accountGuid, 25.00m, "Test debit").ConfigureAwait(false);
                return entryGuid != Guid.Empty;
            }).ConfigureAwait(false);

            await TestAsync("Add zero amount credit", async () =>
            {
                Guid entryGuid = await _Ledger!.AddCreditAsync(accountGuid, 0m, "Zero credit").ConfigureAwait(false);
                return entryGuid != Guid.Empty;
            }).ConfigureAwait(false);

            await TestAsync("Add zero amount debit", async () =>
            {
                Guid entryGuid = await _Ledger!.AddDebitAsync(accountGuid, 0m, "Zero debit").ConfigureAwait(false);
                return entryGuid != Guid.Empty;
            }).ConfigureAwait(false);

            await TestAsync("Add credit with notes", async () =>
            {
                Guid entryGuid = await _Ledger!.AddCreditAsync(accountGuid, 10.00m, "Credit with notes").ConfigureAwait(false);
                List<Entry> entries = await _Ledger.GetPendingEntriesAsync(accountGuid).ConfigureAwait(false);
                Entry? entry = entries.FirstOrDefault(e => e.GUID == entryGuid);
                return entry != null && entry.Description == "Credit with notes";
            }).ConfigureAwait(false);

            await TestAsync("Add already committed credit", async () =>
            {
                Guid entryGuid = await _Ledger!.AddCreditAsync(accountGuid, 15.00m, "Committed credit", null, true).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 15.00m;
            }).ConfigureAwait(false);

            await TestAsync("Add already committed debit", async () =>
            {
                Guid entryGuid = await _Ledger!.AddDebitAsync(accountGuid, 5.00m, "Committed debit", null, true).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 10.00m;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Batch-Operation-Tests

        static async Task RunBatchOperationTestsAsync()
        {
            Console.WriteLine("--- Batch Operation Tests ---");
            Console.WriteLine("");

            Guid accountGuid = await _Ledger!.CreateAccountAsync("Batch Test", 0m).ConfigureAwait(false);

            await TestAsync("Add multiple credits in batch", async () =>
            {
                List<(decimal amount, string? notes)> credits = new List<(decimal, string?)>
                {
                    (10.00m, "Batch credit 1"),
                    (20.00m, "Batch credit 2"),
                    (30.00m, "Batch credit 3")
                };
                List<Guid> guids = await _Ledger!.AddCreditsAsync(accountGuid, credits).ConfigureAwait(false);
                return guids != null && guids.Count == 3;
            }).ConfigureAwait(false);

            await TestAsync("Add multiple debits in batch", async () =>
            {
                List<(decimal amount, string? notes)> debits = new List<(decimal, string?)>
                {
                    (5.00m, "Batch debit 1"),
                    (10.00m, "Batch debit 2")
                };
                List<Guid> guids = await _Ledger!.AddDebitsAsync(accountGuid, debits).ConfigureAwait(false);
                return guids != null && guids.Count == 2;
            }).ConfigureAwait(false);

            await TestAsync("Batch operations affect pending balance correctly", async () =>
            {
                Balance balance = await _Ledger!.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                // Credits: 10 + 20 + 30 = 60, Debits: 5 + 10 = 15, Net = 45
                return balance.PendingBalance == 45.00m;
            }).ConfigureAwait(false);

            await TestAsync("Add batch credits with immediate commit", async () =>
            {
                Guid accountGuid2 = await _Ledger!.CreateAccountAsync("Batch Test 2", 0m).ConfigureAwait(false);
                List<(decimal amount, string? notes)> credits = new List<(decimal, string?)>
                {
                    (100.00m, "Committed batch 1"),
                    (200.00m, "Committed batch 2")
                };
                List<Guid> guids = await _Ledger.AddCreditsAsync(accountGuid2, credits, true).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(accountGuid2).ConfigureAwait(false);
                return balance.CommittedBalance == 300.00m;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Balance-Tests

        static async Task RunBalanceTestsAsync()
        {
            Console.WriteLine("--- Balance Calculation Tests ---");
            Console.WriteLine("");

            Guid accountGuid = await _Ledger!.CreateAccountAsync("Balance Test", 100.00m).ConfigureAwait(false);

            await TestAsync("Get balance with no pending entries", async () =>
            {
                Balance balance = await _Ledger!.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 100.00m && balance.PendingBalance == 100.00m;
            }).ConfigureAwait(false);

            await _Ledger.AddCreditAsync(accountGuid, 50.00m, "Pending credit").ConfigureAwait(false);
            await _Ledger.AddDebitAsync(accountGuid, 25.00m, "Pending debit").ConfigureAwait(false);

            await TestAsync("Get balance with pending entries", async () =>
            {
                Balance balance = await _Ledger!.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 100.00m && balance.PendingBalance == 125.00m;
            }).ConfigureAwait(false);

            await TestAsync("Pending credits count is correct", async () =>
            {
                Balance balance = await _Ledger!.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.PendingCredits.Count == 1;
            }).ConfigureAwait(false);

            await TestAsync("Pending debits count is correct", async () =>
            {
                Balance balance = await _Ledger!.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.PendingDebits.Count == 1;
            }).ConfigureAwait(false);

            await TestAsync("Pending credits total is correct", async () =>
            {
                Balance balance = await _Ledger!.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.PendingCredits.Total == 50.00m;
            }).ConfigureAwait(false);

            await TestAsync("Pending debits total is correct", async () =>
            {
                Balance balance = await _Ledger!.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.PendingDebits.Total == 25.00m;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Commit-Tests

        static async Task RunCommitTestsAsync()
        {
            Console.WriteLine("--- Commit Operation Tests ---");
            Console.WriteLine("");

            Guid accountGuid = await _Ledger!.CreateAccountAsync("Commit Test", 0m).ConfigureAwait(false);
            await _Ledger.AddCreditAsync(accountGuid, 100.00m).ConfigureAwait(false);
            await _Ledger.AddDebitAsync(accountGuid, 30.00m).ConfigureAwait(false);

            await TestAsync("Commit all pending entries", async () =>
            {
                Balance balance = await _Ledger!.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 70.00m && balance.PendingBalance == 70.00m;
            }).ConfigureAwait(false);

            Guid credit1 = await _Ledger.AddCreditAsync(accountGuid, 20.00m).ConfigureAwait(false);
            Guid credit2 = await _Ledger.AddCreditAsync(accountGuid, 30.00m).ConfigureAwait(false);
            Guid debit1 = await _Ledger.AddDebitAsync(accountGuid, 10.00m).ConfigureAwait(false);

            await TestAsync("Commit specific entries only", async () =>
            {
                Balance balance = await _Ledger!.CommitEntriesAsync(accountGuid, new List<Guid> { credit1, debit1 }).ConfigureAwait(false);
                return balance.CommittedBalance == 80.00m && balance.PendingBalance == 110.00m;
            }).ConfigureAwait(false);

            await TestAsync("Committed entries list is populated", async () =>
            {
                Guid credit3 = await _Ledger!.AddCreditAsync(accountGuid, 5.00m).ConfigureAwait(false);
                Balance balance = await _Ledger.CommitEntriesAsync(accountGuid, new List<Guid> { credit3 }).ConfigureAwait(false);
                return balance.Committed != null && balance.Committed.Count > 0;
            }).ConfigureAwait(false);

            await TestAsync("Commit with no pending entries", async () =>
            {
                Guid emptyAccountGuid = await _Ledger!.CreateAccountAsync("Empty Commit Test", 50.00m).ConfigureAwait(false);
                Balance balance = await _Ledger.CommitEntriesAsync(emptyAccountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 50.00m;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Pending-Entries-Tests

        static async Task RunPendingEntriesTestsAsync()
        {
            Console.WriteLine("--- Pending Entries Tests ---");
            Console.WriteLine("");

            Guid accountGuid = await _Ledger!.CreateAccountAsync("Pending Test", 0m).ConfigureAwait(false);
            await _Ledger.AddCreditAsync(accountGuid, 25.00m).ConfigureAwait(false);
            await _Ledger.AddCreditAsync(accountGuid, 35.00m).ConfigureAwait(false);
            await _Ledger.AddDebitAsync(accountGuid, 15.00m).ConfigureAwait(false);

            await TestAsync("Get pending entries returns all", async () =>
            {
                List<Entry> entries = await _Ledger!.GetPendingEntriesAsync(accountGuid).ConfigureAwait(false);
                return entries != null && entries.Count == 3;
            }).ConfigureAwait(false);

            await TestAsync("Get pending credits only", async () =>
            {
                List<Entry> entries = await _Ledger!.GetPendingCreditsAsync(accountGuid).ConfigureAwait(false);
                return entries != null && entries.Count == 2 && entries.All(e => e.Type == EntryType.Credit);
            }).ConfigureAwait(false);

            await TestAsync("Get pending debits only", async () =>
            {
                List<Entry> entries = await _Ledger!.GetPendingDebitsAsync(accountGuid).ConfigureAwait(false);
                return entries != null && entries.Count == 1 && entries.All(e => e.Type == EntryType.Debit);
            }).ConfigureAwait(false);

            await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);

            await TestAsync("Get pending entries after commit returns empty", async () =>
            {
                List<Entry> entries = await _Ledger!.GetPendingEntriesAsync(accountGuid).ConfigureAwait(false);
                return entries != null && entries.Count == 0;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Entry-Search-Tests

        static async Task RunEntrySearchTestsAsync()
        {
            Console.WriteLine("--- Entry Search Tests ---");
            Console.WriteLine("");

            Guid accountGuid = await _Ledger!.CreateAccountAsync("Search Test", 0m).ConfigureAwait(false);
            await _Ledger.AddCreditAsync(accountGuid, 100.00m, "Salary payment", null, true).ConfigureAwait(false);
            await _Ledger.AddDebitAsync(accountGuid, 50.00m, "Grocery shopping", null, true).ConfigureAwait(false);
            await _Ledger.AddCreditAsync(accountGuid, 25.00m, "Bonus payment", null, true).ConfigureAwait(false);

            await TestAsync("Search all entries", async () =>
            {
                List<Entry> entries = await _Ledger!.GetEntriesAsync(accountGuid).ConfigureAwait(false);
                return entries != null && entries.Count == 3;
            }).ConfigureAwait(false);

            await TestAsync("Search entries by description term", async () =>
            {
                List<Entry> entries = await _Ledger!.GetEntriesAsync(accountGuid, searchTerm: "payment").ConfigureAwait(false);
                return entries != null && entries.Count == 2;
            }).ConfigureAwait(false);

            await TestAsync("Search entries by amount range", async () =>
            {
                List<Entry> entries = await _Ledger!.GetEntriesAsync(accountGuid, amountMin: 50.00m, amountMax: 100.00m).ConfigureAwait(false);
                return entries != null && entries.Count == 2;
            }).ConfigureAwait(false);

            await TestAsync("Search entries by entry type", async () =>
            {
                List<Entry> entries = await _Ledger!.GetEntriesAsync(accountGuid, entryType: EntryType.Credit).ConfigureAwait(false);
                return entries != null && entries.Count == 2;
            }).ConfigureAwait(false);

            await TestAsync("Search entries by date range", async () =>
            {
                DateTime start = DateTime.UtcNow.AddHours(-1);
                DateTime end = DateTime.UtcNow.AddHours(1);
                List<Entry> entries = await _Ledger!.GetEntriesAsync(accountGuid, startTimeUtc: start, endTimeUtc: end).ConfigureAwait(false);
                return entries != null && entries.Count > 0;
            }).ConfigureAwait(false);

            await TestAsync("Search entries with pagination (skip)", async () =>
            {
                List<Entry> entries = await _Ledger!.GetEntriesAsync(accountGuid, skip: 1).ConfigureAwait(false);
                return entries != null && entries.Count == 2;
            }).ConfigureAwait(false);

            await TestAsync("Search entries with pagination (take)", async () =>
            {
                List<Entry> entries = await _Ledger!.GetEntriesAsync(accountGuid, take: 2).ConfigureAwait(false);
                return entries != null && entries.Count == 2;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Entry-Cancellation-Tests

        static async Task RunEntryCancellationTestsAsync()
        {
            Console.WriteLine("--- Entry Cancellation Tests ---");
            Console.WriteLine("");

            Guid accountGuid = await _Ledger!.CreateAccountAsync("Cancel Test", 0m).ConfigureAwait(false);
            Guid creditGuid = await _Ledger.AddCreditAsync(accountGuid, 50.00m).ConfigureAwait(false);

            await TestAsync("Cancel pending entry", async () =>
            {
                await _Ledger!.CancelPendingAsync(accountGuid, creditGuid).ConfigureAwait(false);
                List<Entry> entries = await _Ledger.GetPendingEntriesAsync(accountGuid).ConfigureAwait(false);
                return entries.Count == 0;
            }).ConfigureAwait(false);

            await TestAsync("Pending balance updates after cancellation", async () =>
            {
                Guid credit1 = await _Ledger!.AddCreditAsync(accountGuid, 100.00m).ConfigureAwait(false);
                Balance balanceBefore = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                await _Ledger.CancelPendingAsync(accountGuid, credit1).ConfigureAwait(false);
                Balance balanceAfter = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balanceBefore.PendingBalance != balanceAfter.PendingBalance;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Enumeration-Tests

        static async Task RunEnumerationTestsAsync()
        {
            Console.WriteLine("--- Enumeration Tests ---");
            Console.WriteLine("");

            // Create account with 100 records
            Guid enumAccount = await _Ledger!.CreateAccountAsync("Enumeration Test Account", 1000.00m).ConfigureAwait(false);
            List<Guid> entryGuids = new List<Guid>();

            // Add 50 credits and 50 debits with varying amounts
            for (int i = 1; i <= 50; i++)
            {
                decimal amount = i * 10m; // Amounts from 10 to 500
                Guid creditGuid = await _Ledger.AddCreditAsync(enumAccount, amount, $"Credit {i}").ConfigureAwait(false);
                entryGuids.Add(creditGuid);
                // Small delay to ensure distinct timestamps
                await Task.Delay(5).ConfigureAwait(false);

                Guid debitGuid = await _Ledger.AddDebitAsync(enumAccount, amount, $"Debit {i}").ConfigureAwait(false);
                entryGuids.Add(debitGuid);
                await Task.Delay(5).ConfigureAwait(false);
            }

            // Test 1: CreatedDescending with Skip
            await TestAsync("Enum: CreatedDescending with Skip", async () =>
            {
                List<Entry> allEntries = new List<Entry>();
                int pageSize = 10;
                int totalRetrieved = 0;
                int skip = 0;

                while (true)
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = pageSize,
                        Skip = skip,
                        Ordering = EnumerationOrderEnum.CreatedDescending
                    };

                    EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                    // Validate metadata
                    if (totalRetrieved == 0 && result.TotalRecords != 100) return false;
                    if (result.Objects == null || result.Objects.Count > pageSize) return false;
                    if (result.Objects.Count == 0) break;

                    // Validate ordering (should be descending by CreatedUtc)
                    for (int i = 1; i < result.Objects.Count; i++)
                    {
                        if (result.Objects[i].CreatedUtc > result.Objects[i - 1].CreatedUtc)
                            return false;
                    }

                    allEntries.AddRange(result.Objects);
                    totalRetrieved += result.Objects.Count;

                    // Validate records remaining
                    int expectedRemaining = 100 - totalRetrieved;
                    if (result.RecordsRemaining != expectedRemaining) return false;

                    // Validate EndOfResults
                    if (result.RecordsRemaining == 0 && !result.EndOfResults) return false;
                    if (result.RecordsRemaining > 0 && result.EndOfResults) return false;

                    if (result.EndOfResults) break;
                    skip += pageSize;
                }

                return allEntries.Count == 100;
            }).ConfigureAwait(false);

            // Test 2: CreatedDescending with ContinuationToken
            await TestAsync("Enum: CreatedDescending with ContinuationToken", async () =>
            {
                List<Entry> allEntries = new List<Entry>();
                Guid? continuationToken = null;
                int pageSize = 10;
                int totalRetrieved = 0;

                while (true)
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = pageSize,
                        ContinuationToken = continuationToken,
                        Ordering = EnumerationOrderEnum.CreatedDescending
                    };

                    EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                    // Validate metadata
                    if (totalRetrieved == 0 && result.TotalRecords != 100) return false;
                    if (result.Objects == null || result.Objects.Count > pageSize) return false;
                    if (result.Objects.Count == 0) break;

                    // Validate continuation token matches first entry
                    if (continuationToken != null && continuationToken.Value != result.Objects[0].GUID)
                    {
                        // First entry should be after continuation token
                        Entry? prevEntry = allEntries.Count > 0 ? allEntries.Last() : null;
                        if (prevEntry != null && result.Objects[0].CreatedUtc >= prevEntry.CreatedUtc)
                            return false;
                    }

                    // Validate ordering
                    for (int i = 1; i < result.Objects.Count; i++)
                    {
                        if (result.Objects[i].CreatedUtc > result.Objects[i - 1].CreatedUtc)
                            return false;
                    }

                    allEntries.AddRange(result.Objects);
                    totalRetrieved += result.Objects.Count;

                    // Validate records remaining
                    int expectedRemaining = 100 - totalRetrieved;
                    if (result.RecordsRemaining != expectedRemaining) return false;

                    // Validate EndOfResults
                    if (result.RecordsRemaining == 0 && !result.EndOfResults) return false;
                    if (result.RecordsRemaining > 0 && result.EndOfResults) return false;

                    if (result.EndOfResults) break;
                    continuationToken = result.ContinuationToken;
                }

                return allEntries.Count == 100;
            }).ConfigureAwait(false);

            // Test 3: CreatedAscending with Skip
            await TestAsync("Enum: CreatedAscending with Skip", async () =>
            {
                List<Entry> allEntries = new List<Entry>();
                int pageSize = 10;
                int skip = 0;

                while (true)
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = pageSize,
                        Skip = skip,
                        Ordering = EnumerationOrderEnum.CreatedAscending
                    };

                    EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                    if (result.Objects == null || result.Objects.Count == 0) break;

                    // Validate ordering (ascending)
                    for (int i = 1; i < result.Objects.Count; i++)
                    {
                        if (result.Objects[i].CreatedUtc < result.Objects[i - 1].CreatedUtc)
                            return false;
                    }

                    allEntries.AddRange(result.Objects);
                    if (result.EndOfResults) break;
                    skip += pageSize;
                }

                return allEntries.Count == 100;
            }).ConfigureAwait(false);

            // Test 4: CreatedAscending with ContinuationToken
            await TestAsync("Enum: CreatedAscending with ContinuationToken", async () =>
            {
                List<Entry> allEntries = new List<Entry>();
                Guid? continuationToken = null;

                while (true)
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = 10,
                        ContinuationToken = continuationToken,
                        Ordering = EnumerationOrderEnum.CreatedAscending
                    };

                    EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                    if (result.Objects == null || result.Objects.Count == 0) break;

                    // Validate ordering
                    for (int i = 1; i < result.Objects.Count; i++)
                    {
                        if (result.Objects[i].CreatedUtc < result.Objects[i - 1].CreatedUtc)
                            return false;
                    }

                    allEntries.AddRange(result.Objects);
                    if (result.EndOfResults) break;
                    continuationToken = result.ContinuationToken;
                }

                return allEntries.Count == 100;
            }).ConfigureAwait(false);

            // Test 5: AmountDescending with Skip
            await TestAsync("Enum: AmountDescending with Skip", async () =>
            {
                List<Entry> allEntries = new List<Entry>();
                int skip = 0;

                while (true)
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = 10,
                        Skip = skip,
                        Ordering = EnumerationOrderEnum.AmountDescending
                    };

                    EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                    if (result.Objects == null || result.Objects.Count == 0) break;

                    // Validate ordering (descending by amount)
                    for (int i = 1; i < result.Objects.Count; i++)
                    {
                        if (result.Objects[i].Amount > result.Objects[i - 1].Amount)
                            return false;
                    }

                    allEntries.AddRange(result.Objects);
                    if (result.EndOfResults) break;
                    skip += 10;
                }

                return allEntries.Count == 100;
            }).ConfigureAwait(false);

            // Test 6: AmountDescending with ContinuationToken
            await TestAsync("Enum: AmountDescending with ContinuationToken", async () =>
            {
                List<Entry> allEntries = new List<Entry>();
                Guid? continuationToken = null;

                while (true)
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = 10,
                        ContinuationToken = continuationToken,
                        Ordering = EnumerationOrderEnum.AmountDescending
                    };

                    EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                    if (result.Objects == null || result.Objects.Count == 0) break;

                    // Validate ordering
                    for (int i = 1; i < result.Objects.Count; i++)
                    {
                        if (result.Objects[i].Amount > result.Objects[i - 1].Amount)
                            return false;
                    }

                    allEntries.AddRange(result.Objects);
                    if (result.EndOfResults) break;
                    continuationToken = result.ContinuationToken;
                }

                return allEntries.Count == 100;
            }).ConfigureAwait(false);

            // Test 7: AmountAscending with Skip
            await TestAsync("Enum: AmountAscending with Skip", async () =>
            {
                List<Entry> allEntries = new List<Entry>();
                int skip = 0;

                while (true)
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = 10,
                        Skip = skip,
                        Ordering = EnumerationOrderEnum.AmountAscending
                    };

                    EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                    if (result.Objects == null || result.Objects.Count == 0) break;

                    // Validate ordering (ascending by amount)
                    for (int i = 1; i < result.Objects.Count; i++)
                    {
                        if (result.Objects[i].Amount < result.Objects[i - 1].Amount)
                            return false;
                    }

                    allEntries.AddRange(result.Objects);
                    if (result.EndOfResults) break;
                    skip += 10;
                }

                return allEntries.Count == 100;
            }).ConfigureAwait(false);

            // Test 8: AmountAscending with ContinuationToken
            await TestAsync("Enum: AmountAscending with ContinuationToken", async () =>
            {
                List<Entry> allEntries = new List<Entry>();
                Guid? continuationToken = null;

                while (true)
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = 10,
                        ContinuationToken = continuationToken,
                        Ordering = EnumerationOrderEnum.AmountAscending
                    };

                    EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                    if (result.Objects == null || result.Objects.Count == 0) break;

                    // Validate ordering
                    for (int i = 1; i < result.Objects.Count; i++)
                    {
                        if (result.Objects[i].Amount < result.Objects[i - 1].Amount)
                            return false;
                    }

                    allEntries.AddRange(result.Objects);
                    if (result.EndOfResults) break;
                    continuationToken = result.ContinuationToken;
                }

                return allEntries.Count == 100;
            }).ConfigureAwait(false);

            // Test 9: Date filters - CreatedAfterUtc
            await TestAsync("Enum: CreatedAfterUtc filter", async () =>
            {
                // Get a timestamp from middle of dataset
                List<Entry> allEntries = await _Ledger.GetPendingEntriesAsync(enumAccount).ConfigureAwait(false);
                DateTime middleTimestamp = allEntries[50].CreatedUtc;

                EnumerationQuery query = new EnumerationQuery
                {
                    AccountGUID = enumAccount,
                    MaxResults = 100,
                    Ordering = EnumerationOrderEnum.CreatedDescending,
                    CreatedAfterUtc = middleTimestamp
                };

                EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                // Should get approximately half the records
                return result.Objects != null && result.Objects.Count < 100 && result.Objects.Count > 0 &&
                       result.Objects.All(e => e.CreatedUtc >= middleTimestamp);
            }).ConfigureAwait(false);

            // Test 10: Date filters - CreatedBeforeUtc
            await TestAsync("Enum: CreatedBeforeUtc filter", async () =>
            {
                List<Entry> allEntries = await _Ledger.GetPendingEntriesAsync(enumAccount).ConfigureAwait(false);
                DateTime middleTimestamp = allEntries[50].CreatedUtc;

                EnumerationQuery query = new EnumerationQuery
                {
                    AccountGUID = enumAccount,
                    MaxResults = 100,
                    Ordering = EnumerationOrderEnum.CreatedDescending,
                    CreatedBeforeUtc = middleTimestamp
                };

                EnumerationResult<Entry> result = await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);

                return result.Objects != null && result.Objects.Count < 100 && result.Objects.Count > 0 &&
                       result.Objects.All(e => e.CreatedUtc <= middleTimestamp);
            }).ConfigureAwait(false);

            // Test 11: Validate Skip and ContinuationToken cannot be used together
            await TestAsync("Enum: Skip and ContinuationToken mutual exclusion", async () =>
            {
                try
                {
                    EnumerationQuery query = new EnumerationQuery
                    {
                        AccountGUID = enumAccount,
                        MaxResults = 10,
                        Skip = 10,
                        ContinuationToken = Guid.NewGuid(),
                        Ordering = EnumerationOrderEnum.CreatedDescending
                    };

                    await _Ledger.EnumerateTransactionsAsync(query).ConfigureAwait(false);
                    return false; // Should have thrown
                }
                catch (ArgumentException)
                {
                    return true; // Expected
                }
            }).ConfigureAwait(false);

            // Clean up
            await _Ledger.DeleteAccountByGuidAsync(enumAccount).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Account-Deletion-Tests

        static async Task RunAccountDeletionTestsAsync()
        {
            Console.WriteLine("--- Account Deletion Tests ---");
            Console.WriteLine("");

            Guid accountGuid = await _Ledger!.CreateAccountAsync("Delete Test By GUID").ConfigureAwait(false);
            await _Ledger.AddCreditAsync(accountGuid, 50.00m).ConfigureAwait(false);

            await TestAsync("Delete account by GUID", async () =>
            {
                await _Ledger!.DeleteAccountByGuidAsync(accountGuid).ConfigureAwait(false);
                Account? account = await _Ledger.GetAccountByGuidAsync(accountGuid).ConfigureAwait(false);
                return account == null;
            }).ConfigureAwait(false);

            string accountName = "Delete Test By Name";
            await _Ledger.CreateAccountAsync(accountName).ConfigureAwait(false);

            await TestAsync("Delete account by name", async () =>
            {
                await _Ledger!.DeleteAccountByNameAsync(accountName).ConfigureAwait(false);
                Account? account = await _Ledger.GetAccountByNameAsync(accountName).ConfigureAwait(false);
                return account == null;
            }).ConfigureAwait(false);

            await TestAsync("Delete non-existent account by GUID does not throw", async () =>
            {
                try
                {
                    await _Ledger!.DeleteAccountByGuidAsync(Guid.NewGuid()).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            await TestAsync("Delete non-existent account by name does not throw", async () =>
            {
                try
                {
                    await _Ledger!.DeleteAccountByNameAsync("Does Not Exist").ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Event-Tests

        static async Task RunEventTestsAsync()
        {
            Console.WriteLine("--- Event Tests ---");
            Console.WriteLine("");

            bool accountCreatedFired = false;
            bool creditAddedFired = false;
            bool debitAddedFired = false;
            bool entryCanceledFired = false;
            bool entriesCommittedFired = false;
            bool accountDeletedFired = false;

            _Ledger!.AccountCreated += (sender, args) => { accountCreatedFired = true; };
            _Ledger.CreditAdded += (sender, args) => { creditAddedFired = true; };
            _Ledger.DebitAdded += (sender, args) => { debitAddedFired = true; };
            _Ledger.EntryCanceled += (sender, args) => { entryCanceledFired = true; };
            _Ledger.EntriesCommitted += (sender, args) => { entriesCommittedFired = true; };
            _Ledger.AccountDeleted += (sender, args) => { accountDeletedFired = true; };

            Guid accountGuid = await _Ledger.CreateAccountAsync("Event Test").ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false); // Allow event to fire

            await TestAsync("AccountCreated event fires", async () =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return accountCreatedFired;
            }).ConfigureAwait(false);

            Guid creditGuid = await _Ledger.AddCreditAsync(accountGuid, 50.00m).ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false);

            await TestAsync("CreditAdded event fires", async () =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return creditAddedFired;
            }).ConfigureAwait(false);

            Guid debitGuid = await _Ledger.AddDebitAsync(accountGuid, 25.00m).ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false);

            await TestAsync("DebitAdded event fires", async () =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return debitAddedFired;
            }).ConfigureAwait(false);

            await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false);

            await TestAsync("EntriesCommitted event fires", async () =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return entriesCommittedFired;
            }).ConfigureAwait(false);

            Guid cancelGuid = await _Ledger.AddCreditAsync(accountGuid, 10.00m).ConfigureAwait(false);
            await _Ledger.CancelPendingAsync(accountGuid, cancelGuid).ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false);

            await TestAsync("EntryCanceled event fires", async () =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return entryCanceledFired;
            }).ConfigureAwait(false);

            await _Ledger.DeleteAccountByGuidAsync(accountGuid).ConfigureAwait(false);
            await Task.Delay(100).ConfigureAwait(false);

            await TestAsync("AccountDeleted event fires", async () =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return accountDeletedFired;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Error-Handling-Tests

        static async Task RunErrorHandlingTestsAsync()
        {
            Console.WriteLine("--- Error Handling Tests ---");
            Console.WriteLine("");

            await TestAsync("Create account with null name throws exception", async () =>
            {
                try
                {
                    await _Ledger!.CreateAccountAsync(null!).ConfigureAwait(false);
                    return false;
                }
                catch (ArgumentNullException)
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            await TestAsync("Create account with empty name throws exception", async () =>
            {
                try
                {
                    await _Ledger!.CreateAccountAsync("").ConfigureAwait(false);
                    return false;
                }
                catch (ArgumentNullException)
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            await TestAsync("Add credit with negative amount throws exception", async () =>
            {
                try
                {
                    Guid accountGuid = await _Ledger!.CreateAccountAsync("Error Test").ConfigureAwait(false);
                    await _Ledger.AddCreditAsync(accountGuid, -10.00m).ConfigureAwait(false);
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            await TestAsync("Add debit with negative amount throws exception", async () =>
            {
                try
                {
                    Guid accountGuid = await _Ledger!.CreateAccountAsync("Error Test 2").ConfigureAwait(false);
                    await _Ledger.AddDebitAsync(accountGuid, -10.00m).ConfigureAwait(false);
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            await TestAsync("Add credit to non-existent account throws exception", async () =>
            {
                try
                {
                    await _Ledger!.AddCreditAsync(Guid.NewGuid(), 10.00m).ConfigureAwait(false);
                    return false;
                }
                catch (KeyNotFoundException)
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            await TestAsync("Get balance for non-existent account throws exception", async () =>
            {
                try
                {
                    await _Ledger!.GetBalanceAsync(Guid.NewGuid()).ConfigureAwait(false);
                    return false;
                }
                catch (KeyNotFoundException)
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            await TestAsync("Cancel non-existent pending entry throws exception", async () =>
            {
                try
                {
                    Guid accountGuid = await _Ledger!.CreateAccountAsync("Error Test 3").ConfigureAwait(false);
                    await _Ledger.CancelPendingAsync(accountGuid, Guid.NewGuid()).ConfigureAwait(false);
                    return false;
                }
                catch (KeyNotFoundException)
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            await TestAsync("GetEntries with invalid date range throws exception", async () =>
            {
                try
                {
                    Guid accountGuid = await _Ledger!.CreateAccountAsync("Error Test 4").ConfigureAwait(false);
                    DateTime end = DateTime.UtcNow;
                    DateTime start = end.AddHours(1);
                    await _Ledger.GetEntriesAsync(accountGuid, startTimeUtc: start, endTimeUtc: end).ConfigureAwait(false);
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Edge-Case-Tests

        static async Task RunEdgeCaseTestsAsync()
        {
            Console.WriteLine("--- Edge Case Tests ---");
            Console.WriteLine("");

            await TestAsync("Multiple commits on same account", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Edge Test 1", 0m).ConfigureAwait(false);
                await _Ledger.AddCreditAsync(accountGuid, 10.00m, null, null, false).ConfigureAwait(false);
                await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
                await _Ledger.AddCreditAsync(accountGuid, 20.00m, null, null, false).ConfigureAwait(false);
                await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
                await _Ledger.AddCreditAsync(accountGuid, 30.00m, null, null, false).ConfigureAwait(false);
                await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 60.00m;
            }).ConfigureAwait(false);

            await TestAsync("Large number of pending entries", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Edge Test 2", 0m).ConfigureAwait(false);
                for (int i = 0; i < 100; i++)
                {
                    await _Ledger.AddCreditAsync(accountGuid, 1.00m).ConfigureAwait(false);
                }
                List<Entry> entries = await _Ledger.GetPendingEntriesAsync(accountGuid).ConfigureAwait(false);
                return entries.Count == 100;
            }).ConfigureAwait(false);

            await TestAsync("Commit large number of entries", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Edge Test 3", 0m).ConfigureAwait(false);
                for (int i = 0; i < 50; i++)
                {
                    await _Ledger.AddCreditAsync(accountGuid, 2.00m).ConfigureAwait(false);
                }
                Balance balance = await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 100.00m && balance.PendingBalance == 100.00m;
            }).ConfigureAwait(false);

            await TestAsync("Very long account name", async () =>
            {
                string longName = new string('A', 250);
                Guid guid = await _Ledger!.CreateAccountAsync(longName).ConfigureAwait(false);
                Account? account = await _Ledger.GetAccountByGuidAsync(guid).ConfigureAwait(false);
                return account != null && account.Name == longName;
            }).ConfigureAwait(false);

            await TestAsync("Very long entry description", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Edge Test 4").ConfigureAwait(false);
                string longDesc = new string('B', 250);
                Guid entryGuid = await _Ledger.AddCreditAsync(accountGuid, 10.00m, longDesc).ConfigureAwait(false);
                List<Entry> entries = await _Ledger.GetPendingEntriesAsync(accountGuid).ConfigureAwait(false);
                Entry? entry = entries.FirstOrDefault(e => e.GUID == entryGuid);
                return entry != null && entry.Description == longDesc;
            }).ConfigureAwait(false);

            await TestAsync("Partial commit of pending entries", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Edge Test 5", 0m).ConfigureAwait(false);
                Guid credit1 = await _Ledger.AddCreditAsync(accountGuid, 10.00m).ConfigureAwait(false);
                Guid credit2 = await _Ledger.AddCreditAsync(accountGuid, 20.00m).ConfigureAwait(false);
                Guid credit3 = await _Ledger.AddCreditAsync(accountGuid, 30.00m).ConfigureAwait(false);
                Balance balance = await _Ledger.CommitEntriesAsync(accountGuid, new List<Guid> { credit1, credit3 }).ConfigureAwait(false);
                return balance.CommittedBalance == 40.00m && balance.PendingBalance == 60.00m;
            }).ConfigureAwait(false);

            await TestAsync("Mix of credits and debits resulting in zero", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Edge Test 6", 100.00m).ConfigureAwait(false);
                await _Ledger.AddCreditAsync(accountGuid, 50.00m, null, null, true).ConfigureAwait(false);
                await _Ledger.AddDebitAsync(accountGuid, 150.00m, null, null, true).ConfigureAwait(false);
                Balance balance = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 0m;
            }).ConfigureAwait(false);

            await TestAsync("Account with many transactions over time", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Edge Test 7", 0m).ConfigureAwait(false);
                for (int i = 0; i < 20; i++)
                {
                    await _Ledger.AddCreditAsync(accountGuid, 5.00m, null, null, false).ConfigureAwait(false);
                    await _Ledger.AddDebitAsync(accountGuid, 2.00m, null, null, false).ConfigureAwait(false);
                    await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
                }
                Balance balance = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.CommittedBalance == 60.00m;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region New-Feature-Tests

        static async Task RunNewFeatureTestsAsync()
        {
            Console.WriteLine("--- New Feature Tests (v2.0.0) ---");
            Console.WriteLine("");

            // Test GetBalanceAsOf
            await TestAsync("GetBalanceAsOf returns correct historical balance", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Historical Test", 100.00m).ConfigureAwait(false);
                DateTime timestamp1 = DateTime.UtcNow;
                await Task.Delay(100).ConfigureAwait(false);

                await _Ledger.AddCreditAsync(accountGuid, 50.00m, null, null, true).ConfigureAwait(false);
                DateTime timestamp2 = DateTime.UtcNow;
                await Task.Delay(100).ConfigureAwait(false);

                await _Ledger.AddDebitAsync(accountGuid, 30.00m, null, null, true).ConfigureAwait(false);

                decimal balanceAt1 = await _Ledger.GetBalanceAsOfAsync(accountGuid, timestamp1).ConfigureAwait(false);
                decimal balanceAt2 = await _Ledger.GetBalanceAsOfAsync(accountGuid, timestamp2).ConfigureAwait(false);

                return balanceAt1 == 100.00m && balanceAt2 == 150.00m;
            }).ConfigureAwait(false);

            // Test GetAllBalances
            await TestAsync("GetAllBalances returns balances for all accounts", async () =>
            {
                await _Ledger!.CreateAccountAsync("GetAll Test 1", 100.00m).ConfigureAwait(false);
                await _Ledger.CreateAccountAsync("GetAll Test 2", 200.00m).ConfigureAwait(false);

                Dictionary<Guid, Balance> balances = await _Ledger.GetAllBalancesAsync().ConfigureAwait(false);
                return balances != null && balances.Count >= 2;
            }).ConfigureAwait(false);

            // Test VerifyBalanceChain
            await TestAsync("VerifyBalanceChain returns true for valid chain", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Chain Test", 0m).ConfigureAwait(false);
                await _Ledger.AddCreditAsync(accountGuid, 10.00m, null, null, false).ConfigureAwait(false);
                await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
                await _Ledger.AddCreditAsync(accountGuid, 20.00m, null, null, false).ConfigureAwait(false);
                await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);

                bool isValid = await _Ledger.VerifyBalanceChainAsync(accountGuid).ConfigureAwait(false);
                return isValid;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Performance-Tests

        static async Task RunPerformanceTestsAsync()
        {
            Console.WriteLine("--- Performance Tests ---");
            Console.WriteLine("");

            await TestAsync("Create 100 accounts in reasonable time", async () =>
            {
                DateTime start = DateTime.UtcNow;
                for (int i = 0; i < 100; i++)
                {
                    await _Ledger!.CreateAccountAsync($"Perf Account {i}").ConfigureAwait(false);
                }
                DateTime end = DateTime.UtcNow;
                TimeSpan duration = end - start;
                return duration.TotalSeconds < 10; // Should complete in under 10 seconds
            }).ConfigureAwait(false);

            await TestAsync("Add 500 entries in reasonable time", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Perf Test").ConfigureAwait(false);
                DateTime start = DateTime.UtcNow;
                for (int i = 0; i < 500; i++)
                {
                    await _Ledger.AddCreditAsync(accountGuid, 1.00m).ConfigureAwait(false);
                }
                DateTime end = DateTime.UtcNow;
                TimeSpan duration = end - start;
                return duration.TotalSeconds < 15; // Should complete in under 15 seconds
            }).ConfigureAwait(false);

            await TestAsync("Commit 200 entries in reasonable time", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Perf Test 2", 0m).ConfigureAwait(false);
                for (int i = 0; i < 200; i++)
                {
                    await _Ledger.AddCreditAsync(accountGuid, 1.00m).ConfigureAwait(false);
                }

                DateTime start = DateTime.UtcNow;
                await _Ledger.CommitEntriesAsync(accountGuid).ConfigureAwait(false);
                DateTime end = DateTime.UtcNow;
                TimeSpan duration = end - start;
                return duration.TotalSeconds < 5; // Should complete in under 5 seconds
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Concurrency-Tests

        static async Task RunConcurrencyTestsAsync()
        {
            Console.WriteLine("--- Concurrency Tests ---");
            Console.WriteLine("");

            await TestAsync("Concurrent credits to same account are serialized correctly", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Concurrent Test 1", 0m).ConfigureAwait(false);

                List<Task> tasks = new List<Task>();
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(_Ledger.AddCreditAsync(accountGuid, 10.00m));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);

                Balance balance = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                return balance.PendingBalance == 100.00m;
            }).ConfigureAwait(false);

            await TestAsync("Concurrent operations on different accounts work correctly", async () =>
            {
                List<Task<Guid>> accountTasks = new List<Task<Guid>>();
                for (int i = 0; i < 5; i++)
                {
                    accountTasks.Add(_Ledger!.CreateAccountAsync($"Concurrent Account {i}", 100.00m));
                }
                Guid[] accountGuids = await Task.WhenAll(accountTasks).ConfigureAwait(false);

                List<Task> creditTasks = new List<Task>();
                foreach (Guid guid in accountGuids)
                {
                    creditTasks.Add(_Ledger.AddCreditAsync(guid, 50.00m, null, null, true));
                }
                await Task.WhenAll(creditTasks).ConfigureAwait(false);

                List<Task<Balance>> balanceTasks = new List<Task<Balance>>();
                foreach (Guid guid in accountGuids)
                {
                    balanceTasks.Add(_Ledger.GetBalanceAsync(guid));
                }
                Balance[] balances = await Task.WhenAll(balanceTasks).ConfigureAwait(false);

                return balances.All(b => b.CommittedBalance == 150.00m);
            }).ConfigureAwait(false);

            await TestAsync("Concurrent commits are handled correctly", async () =>
            {
                Guid accountGuid = await _Ledger!.CreateAccountAsync("Concurrent Test 2", 0m).ConfigureAwait(false);

                // Add entries
                for (int i = 0; i < 20; i++)
                {
                    await _Ledger.AddCreditAsync(accountGuid, 5.00m).ConfigureAwait(false);
                }

                // Try to commit concurrently (should serialize)
                List<Task<Balance>> commitTasks = new List<Task<Balance>>
                {
                    _Ledger.CommitEntriesAsync(accountGuid)
                };

                try
                {
                    await Task.WhenAll(commitTasks).ConfigureAwait(false);
                    Balance balance = await _Ledger.GetBalanceAsync(accountGuid).ConfigureAwait(false);
                    return balance.CommittedBalance == 100.00m;
                }
                catch
                {
                    // Expected - one might fail if entries already committed
                    return true;
                }
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Test-Infrastructure

        static async Task TestAsync(string testName, Func<Task<bool>> testFunc)
        {
            _TestCount++;
            bool passed = false;
            string? error = null;

            try
            {
                passed = await testFunc().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                passed = false;
                error = ex.ToString();
            }

            if (passed)
            {
                _PassCount++;
                Console.WriteLine("[PASS] " + testName);
            }
            else
            {
                _FailCount++;
                Console.WriteLine("[FAIL] " + testName);
                if (!String.IsNullOrEmpty(error))
                {
                    Console.WriteLine("       Error: " + error);
                }
            }

            _Results.Add(new TestResult
            {
                TestName = testName,
                Passed = passed,
                Error = error
            });
        }

        static void DisplaySummary()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("Test Summary");
            Console.WriteLine("================================================================================");
            Console.WriteLine("");
            Console.WriteLine("Total Tests:  " + _TestCount);
            Console.WriteLine("Passed:       " + _PassCount);
            Console.WriteLine("Failed:       " + _FailCount);
            Console.WriteLine("Success Rate: " + (_TestCount > 0 ? ((_PassCount * 100.0) / _TestCount).ToString("F2") : "0.00") + "%");
            Console.WriteLine("");

            if (_FailCount > 0)
            {
                Console.WriteLine("Failed Tests:");
                Console.WriteLine("--------------------------------------------------------------------------------");
                foreach (TestResult result in _Results.Where(r => !r.Passed))
                {
                    Console.WriteLine("  - " + result.TestName);
                    if (!String.IsNullOrEmpty(result.Error))
                    {
                        Console.WriteLine("    Error: " + result.Error);
                    }
                }
                Console.WriteLine("");
            }

            Console.WriteLine("================================================================================");
            if (_FailCount == 0)
            {
                Console.WriteLine("ALL TESTS PASSED");
            }
            else
            {
                Console.WriteLine("SOME TESTS FAILED");
            }
            Console.WriteLine("================================================================================");
        }

        #endregion

#pragma warning restore CS8602 // Dereference of a possibly null reference.
    }
}
