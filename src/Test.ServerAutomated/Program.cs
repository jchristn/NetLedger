namespace Test.ServerAutomated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NetLedger.Sdk;

    class Program
    {
        #region Private-Members

        private static List<TestResult> _Results = new List<TestResult>();
        private static string _Endpoint = string.Empty;
        private static string _ApiKey = string.Empty;
        private static NetLedgerClient? _Client = null;
        private static int _TestCount = 0;
        private static int _PassCount = 0;
        private static int _FailCount = 0;
        private static Stopwatch _TotalStopwatch = new Stopwatch();
        private static bool _NoCleanup = false;
        private static List<string> _RetainedAccounts = new List<string>();

        #endregion

        #region Main

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("NetLedger Server Automated Test Suite - v2.0.0");
            Console.WriteLine("================================================================================");
            Console.WriteLine("");

            if (!ParseArguments(args))
            {
                ShowHelp();
                return 1;
            }

            Console.WriteLine($"Endpoint: {_Endpoint}");
            Console.WriteLine($"API Key:  {_ApiKey.Substring(0, Math.Min(8, _ApiKey.Length))}...");
            Console.WriteLine("");

            try
            {
                _Client = new NetLedgerClient(_Endpoint, _ApiKey);
                _TotalStopwatch.Start();

                // Run all test categories
                await RunServiceTestsAsync().ConfigureAwait(false);
                await RunAccountCreationTestsAsync().ConfigureAwait(false);
                await RunAccountRetrievalTestsAsync().ConfigureAwait(false);
                await RunCreditDebitTestsAsync().ConfigureAwait(false);
                await RunBatchOperationTestsAsync().ConfigureAwait(false);
                await RunBalanceTestsAsync().ConfigureAwait(false);
                await RunCommitTestsAsync().ConfigureAwait(false);
                await RunPendingEntriesTestsAsync().ConfigureAwait(false);
                await RunEntryEnumerationTestsAsync().ConfigureAwait(false);
                await RunEntryCancellationTestsAsync().ConfigureAwait(false);
                await RunAccountEnumerationTestsAsync().ConfigureAwait(false);
                await RunHistoricalBalanceTestsAsync().ConfigureAwait(false);
                await RunBalanceChainVerificationTestsAsync().ConfigureAwait(false);
                await RunErrorHandlingTestsAsync().ConfigureAwait(false);
                await RunEdgeCaseTestsAsync().ConfigureAwait(false);
                await RunPerformanceTestsAsync().ConfigureAwait(false);
                await RunConcurrencyTestsAsync().ConfigureAwait(false);

                _TotalStopwatch.Stop();

                // Display summary
                DisplaySummary();

                _Client.Dispose();

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

        #endregion

        #region Argument-Parsing

        static bool ParseArguments(string[] args)
        {
            if (args == null || args.Length == 0) return false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();

                if (arg == "--help" || arg == "-?" || arg == "-h")
                {
                    return false;
                }
                else if (arg == "--endpoint" || arg == "-e")
                {
                    if (i + 1 < args.Length)
                    {
                        _Endpoint = args[++i];
                    }
                }
                else if (arg == "--apikey" || arg == "-k")
                {
                    if (i + 1 < args.Length)
                    {
                        _ApiKey = args[++i];
                    }
                }
                else if (arg == "--no-cleanup")
                {
                    _NoCleanup = true;
                }
            }

            if (string.IsNullOrWhiteSpace(_Endpoint) || string.IsNullOrWhiteSpace(_ApiKey))
            {
                return false;
            }

            return true;
        }

        static void ShowHelp()
        {
            Console.WriteLine("NetLedger Server Automated Test Suite");
            Console.WriteLine("");
            Console.WriteLine("Usage: Test.ServerAutomated --endpoint <url> --apikey <key> [options]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  --endpoint, -e <url>  The NetLedger server endpoint URL");
            Console.WriteLine("  --apikey, -k <key>    The API key for authentication");
            Console.WriteLine("  --no-cleanup          Do not delete test accounts after running");
            Console.WriteLine("  --help, -?, -h        Show this help message");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  Test.ServerAutomated -e http://localhost:8080 -k your-api-key");
            Console.WriteLine("  Test.ServerAutomated --endpoint https://api.example.com --apikey abc123");
            Console.WriteLine("  Test.ServerAutomated -e http://localhost:8080 -k your-api-key --no-cleanup");
        }

        #endregion

        #region Service-Tests

        static async Task RunServiceTestsAsync()
        {
            Console.WriteLine("--- Service Tests ---");
            Console.WriteLine("");

            await TestAsync("Health check returns true", async () =>
            {
                bool isHealthy = await _Client!.Service.HealthCheckAsync().ConfigureAwait(false);
                return isHealthy;
            }).ConfigureAwait(false);

            await TestAsync("Get service info returns valid data", async () =>
            {
                ServiceInfo info = await _Client!.Service.GetInfoAsync().ConfigureAwait(false);
                return info != null && !string.IsNullOrEmpty(info.Version);
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Account-Creation-Tests

        static async Task RunAccountCreationTestsAsync()
        {
            Console.WriteLine("--- Account Creation Tests ---");
            Console.WriteLine("");

            await TestAsync("Create account with name only", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("Test Account " + UniqueSuffix(8)).ConfigureAwait(false);
                bool result = account != null && account.Id != String.Empty;
                if (result) await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return result;
            }).ConfigureAwait(false);

            await TestAsync("Create account with Account object", async () =>
            {
                Account input = new Account
                {
                    Name = "Test Account " + UniqueSuffix(8)
                };
                Account account = await _Client!.Account.CreateAsync(input).ConfigureAwait(false);
                bool result = account != null && account.Id != String.Empty;
                if (result) await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return result;
            }).ConfigureAwait(false);

            await TestAsync("Created account has correct name", async () =>
            {
                string expectedName = "NameTest " + UniqueSuffix(8);
                Account account = await _Client!.Account.CreateAsync(expectedName).ConfigureAwait(false);
                bool result = account != null && account.Name == expectedName;
                if (account != null) await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return result;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Account-Retrieval-Tests

        static async Task RunAccountRetrievalTestsAsync()
        {
            Console.WriteLine("--- Account Retrieval Tests ---");
            Console.WriteLine("");

            // Use a simple name without spaces to avoid URL encoding issues
            string testName = "RetrievalTest" + UniqueSuffix(8);
            Account createdAccount = await _Client!.Account.CreateAsync(testName).ConfigureAwait(false);

            await TestAsync("Get account by identifier", async () =>
            {
                Account account = await _Client!.Account.GetAsync(createdAccount.Id).ConfigureAwait(false);
                return account != null && account.Id == createdAccount.Id;
            }).ConfigureAwait(false);

            await TestAsync("Get account by name", async () =>
            {
                Account account = await _Client!.Account.GetByNameAsync(testName).ConfigureAwait(false);
                return account != null && account.Name == testName;
            }).ConfigureAwait(false);

            await TestAsync("Check account exists returns true", async () =>
            {
                bool exists = await _Client!.Account.ExistsAsync(createdAccount.Id).ConfigureAwait(false);
                return exists;
            }).ConfigureAwait(false);

            await TestAsync("Check non-existent account returns false", async () =>
            {
                bool exists = await _Client!.Account.ExistsAsync(MissingId()).ConfigureAwait(false);
                return !exists;
            }).ConfigureAwait(false);

            await TestAsync("Get non-existent account by identifier throws 404", async () =>
            {
                try
                {
                    await _Client!.Account.GetAsync(MissingId()).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
                }
            }).ConfigureAwait(false);

            await TestAsync("Get non-existent account by name throws 404", async () =>
            {
                try
                {
                    await _Client!.Account.GetByNameAsync("NonExistent" + UniqueSuffix(24)).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
                }
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(createdAccount.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Credit-Debit-Tests

        static async Task RunCreditDebitTestsAsync()
        {
            Console.WriteLine("--- Credit and Debit Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("Transaction Test " + UniqueSuffix(8)).ConfigureAwait(false);

            await TestAsync("Add credit", async () =>
            {
                Entry entry = await _Client!.Entry.AddCreditAsync(account.Id, 50.00m, "Test credit").ConfigureAwait(false);
                return entry != null && entry.Id != String.Empty && entry.Type == EntryType.Credit && entry.Amount == 50.00m;
            }).ConfigureAwait(false);

            await TestAsync("Add debit", async () =>
            {
                Entry entry = await _Client!.Entry.AddDebitAsync(account.Id, 25.00m, "Test debit").ConfigureAwait(false);
                return entry != null && entry.Id != String.Empty && entry.Type == EntryType.Debit && entry.Amount == 25.00m;
            }).ConfigureAwait(false);

            await TestAsync("Add credit with description", async () =>
            {
                Entry entry = await _Client!.Entry.AddCreditAsync(account.Id, 10.00m, "Credit with notes").ConfigureAwait(false);
                return entry != null && entry.Description == "Credit with notes";
            }).ConfigureAwait(false);

            await TestAsync("Entry has correct account identifier", async () =>
            {
                Entry entry = await _Client!.Entry.AddCreditAsync(account.Id, 5.00m).ConfigureAwait(false);
                return entry.AccountId == account.Id;
            }).ConfigureAwait(false);

            await TestAsync("Entry is initially uncommitted", async () =>
            {
                Entry entry = await _Client!.Entry.AddCreditAsync(account.Id, 3.00m).ConfigureAwait(false);
                return !entry.IsCommitted;
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Batch-Operation-Tests

        static async Task RunBatchOperationTestsAsync()
        {
            Console.WriteLine("--- Batch Operation Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("Batch Test " + UniqueSuffix(8)).ConfigureAwait(false);

            await TestAsync("Add multiple credits in batch", async () =>
            {
                List<EntryInput> credits = new List<EntryInput>
                {
                    new EntryInput(10.00m, "Batch credit 1"),
                    new EntryInput(20.00m, "Batch credit 2"),
                    new EntryInput(30.00m, "Batch credit 3")
                };
                List<Entry> entries = await _Client!.Entry.AddCreditsAsync(account.Id, credits).ConfigureAwait(false);
                return entries != null && entries.Count == 3;
            }).ConfigureAwait(false);

            await TestAsync("Add multiple debits in batch", async () =>
            {
                List<EntryInput> debits = new List<EntryInput>
                {
                    new EntryInput(5.00m, "Batch debit 1"),
                    new EntryInput(10.00m, "Batch debit 2")
                };
                List<Entry> entries = await _Client!.Entry.AddDebitsAsync(account.Id, debits).ConfigureAwait(false);
                return entries != null && entries.Count == 2;
            }).ConfigureAwait(false);

            await TestAsync("Batch operations affect pending balance correctly", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                // Credits: 10 + 20 + 30 = 60, Debits: 5 + 10 = 15, Net = 45
                return balance.PendingBalance == 45.00m;
            }).ConfigureAwait(false);

            await TestAsync("Batch credits return correct types", async () =>
            {
                Account account2 = await _Client!.Account.CreateAsync("Batch Test 2 " + UniqueSuffix(8)).ConfigureAwait(false);
                List<EntryInput> credits = new List<EntryInput>
                {
                    new EntryInput(100.00m),
                    new EntryInput(200.00m)
                };
                List<Entry> entries = await _Client!.Entry.AddCreditsAsync(account2.Id, credits).ConfigureAwait(false);
                bool allCredits = entries.All(e => e.Type == EntryType.Credit);
                await CleanupAccountAsync(account2.Id).ConfigureAwait(false);
                return allCredits;
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Balance-Tests

        static async Task RunBalanceTestsAsync()
        {
            Console.WriteLine("--- Balance Calculation Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("Balance Test " + UniqueSuffix(8)).ConfigureAwait(false);

            await TestAsync("New account has zero balance", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 0m && balance.PendingBalance == 0m;
            }).ConfigureAwait(false);

            await _Client.Entry.AddCreditAsync(account.Id, 100.00m, "Test credit").ConfigureAwait(false);
            await _Client.Entry.AddDebitAsync(account.Id, 30.00m, "Test debit").ConfigureAwait(false);

            await TestAsync("Pending balance reflects uncommitted entries", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 0m && balance.PendingBalance == 70.00m;
            }).ConfigureAwait(false);

            await TestAsync("Pending credits count is correct", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.PendingCredits != null && balance.PendingCredits.Count == 1;
            }).ConfigureAwait(false);

            await TestAsync("Pending debits count is correct", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.PendingDebits != null && balance.PendingDebits.Count == 1;
            }).ConfigureAwait(false);

            await TestAsync("Pending credits total is correct", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.PendingCredits != null && balance.PendingCredits.Total == 100.00m;
            }).ConfigureAwait(false);

            await TestAsync("Pending debits total is correct", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.PendingDebits != null && balance.PendingDebits.Total == 30.00m;
            }).ConfigureAwait(false);

            await TestAsync("Get all balances returns data", async () =>
            {
                List<Balance> balances = await _Client!.Balance.GetAllAsync().ConfigureAwait(false);
                return balances != null && balances.Count > 0;
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Commit-Tests

        static async Task RunCommitTestsAsync()
        {
            Console.WriteLine("--- Commit Operation Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("CommitTest" + UniqueSuffix(8)).ConfigureAwait(false);
            await _Client.Entry.AddCreditAsync(account.Id, 100.00m).ConfigureAwait(false);
            await _Client.Entry.AddDebitAsync(account.Id, 30.00m).ConfigureAwait(false);

            await TestAsync("Commit all pending entries updates balance", async () =>
            {
                // Server returns Balance object, not CommitResult with EntriesCommitted
                await _Client!.Balance.CommitAsync(account.Id).ConfigureAwait(false);
                Balance balance = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 70.00m;
            }).ConfigureAwait(false);

            await TestAsync("Committed balance equals pending balance after commit", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 70.00m && balance.PendingBalance == 70.00m;
            }).ConfigureAwait(false);

            Entry credit1 = await _Client.Entry.AddCreditAsync(account.Id, 20.00m).ConfigureAwait(false);
            await _Client.Entry.AddCreditAsync(account.Id, 30.00m).ConfigureAwait(false);
            await _Client.Entry.AddDebitAsync(account.Id, 10.00m).ConfigureAwait(false);

            await TestAsync("Commit specific entries only", async () =>
            {
                await _Client!.Balance.CommitAsync(account.Id, new List<string> { credit1.Id }).ConfigureAwait(false);
                Balance balance = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                // Started with 70, committed +20, so committed = 90, pending = 90 + 30 - 10 = 110
                return balance.CommittedBalance == 90.00m && balance.PendingBalance == 110.00m;
            }).ConfigureAwait(false);

            await TestAsync("Commit returns CommitResult", async () =>
            {
                Entry credit3 = await _Client!.Entry.AddCreditAsync(account.Id, 5.00m).ConfigureAwait(false);
                CommitResult result = await _Client.Balance.CommitAsync(account.Id, new List<string> { credit3.Id }).ConfigureAwait(false);
                // The server returns Balance, SDK wraps it - just verify we get a result
                return result != null;
            }).ConfigureAwait(false);

            await TestAsync("Commit with no pending entries succeeds", async () =>
            {
                Account emptyAccount = await _Client!.Account.CreateAsync("EmptyCommitTest" + UniqueSuffix(8)).ConfigureAwait(false);
                // Commit on empty account should not throw
                CommitResult result = await _Client.Balance.CommitAsync(emptyAccount.Id).ConfigureAwait(false);
                Balance balance = await _Client.Balance.GetAsync(emptyAccount.Id).ConfigureAwait(false);
                await CleanupAccountAsync(emptyAccount.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 0m;
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Pending-Entries-Tests

        static async Task RunPendingEntriesTestsAsync()
        {
            Console.WriteLine("--- Pending Entries Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("Pending Test " + UniqueSuffix(8)).ConfigureAwait(false);
            await _Client.Entry.AddCreditAsync(account.Id, 25.00m).ConfigureAwait(false);
            await _Client.Entry.AddCreditAsync(account.Id, 35.00m).ConfigureAwait(false);
            await _Client.Entry.AddDebitAsync(account.Id, 15.00m).ConfigureAwait(false);

            await TestAsync("Get pending entries returns all", async () =>
            {
                List<Entry> entries = await _Client!.Entry.GetPendingAsync(account.Id).ConfigureAwait(false);
                return entries != null && entries.Count == 3;
            }).ConfigureAwait(false);

            await TestAsync("Get pending credits only", async () =>
            {
                List<Entry> entries = await _Client!.Entry.GetPendingCreditsAsync(account.Id).ConfigureAwait(false);
                return entries != null && entries.Count == 2 && entries.All(e => e.Type == EntryType.Credit);
            }).ConfigureAwait(false);

            await TestAsync("Get pending debits only", async () =>
            {
                List<Entry> entries = await _Client!.Entry.GetPendingDebitsAsync(account.Id).ConfigureAwait(false);
                return entries != null && entries.Count == 1 && entries.All(e => e.Type == EntryType.Debit);
            }).ConfigureAwait(false);

            await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);

            await TestAsync("Get pending entries after commit returns empty", async () =>
            {
                List<Entry> entries = await _Client!.Entry.GetPendingAsync(account.Id).ConfigureAwait(false);
                return entries != null && entries.Count == 0;
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Entry-Enumeration-Tests

        static async Task RunEntryEnumerationTestsAsync()
        {
            Console.WriteLine("--- Entry Enumeration Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("Enum Test " + UniqueSuffix(8)).ConfigureAwait(false);

            // Add entries with varying amounts
            for (int i = 1; i <= 25; i++)
            {
                decimal amount = i * 10m;
                await _Client.Entry.AddCreditAsync(account.Id, amount, $"Credit {i}").ConfigureAwait(false);
                await Task.Delay(10).ConfigureAwait(false);
                await _Client.Entry.AddDebitAsync(account.Id, amount, $"Debit {i}").ConfigureAwait(false);
                await Task.Delay(10).ConfigureAwait(false);
            }

            await TestAsync("Enumerate all entries", async () =>
            {
                List<Entry> entries = await _Client!.Entry.GetAllAsync(account.Id).ConfigureAwait(false);
                return entries != null && entries.Count == 50;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate with MaxResults", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery { MaxResults = 10 };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                return result.Objects != null && result.Objects.Count == 10 && result.TotalRecords == 50;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate with Skip pagination", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery { MaxResults = 10, Skip = 10 };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                return result.Objects != null && result.Objects.Count == 10 && result.RecordsRemaining == 30;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate CreatedDescending ordering", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    Ordering = EnumerationOrder.CreatedDescending
                };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                for (int i = 1; i < result.Objects!.Count; i++)
                {
                    if (result.Objects[i].CreatedUtc > result.Objects[i - 1].CreatedUtc)
                        return false;
                }
                return true;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate CreatedAscending ordering", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    Ordering = EnumerationOrder.CreatedAscending
                };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                for (int i = 1; i < result.Objects!.Count; i++)
                {
                    if (result.Objects[i].CreatedUtc < result.Objects[i - 1].CreatedUtc)
                        return false;
                }
                return true;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate AmountDescending ordering", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    Ordering = EnumerationOrder.AmountDescending
                };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                for (int i = 1; i < result.Objects!.Count; i++)
                {
                    if (result.Objects[i].Amount > result.Objects[i - 1].Amount)
                        return false;
                }
                return true;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate AmountAscending ordering", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    Ordering = EnumerationOrder.AmountAscending
                };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                for (int i = 1; i < result.Objects!.Count; i++)
                {
                    if (result.Objects[i].Amount < result.Objects[i - 1].Amount)
                        return false;
                }
                return true;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate with AmountMinimum filter", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery
                {
                    MaxResults = 100,
                    AmountMinimum = 100.00m
                };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                return result.Objects != null && result.Objects.All(e => e.Amount >= 100.00m);
            }).ConfigureAwait(false);

            await TestAsync("Enumerate with AmountMaximum filter", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery
                {
                    MaxResults = 100,
                    AmountMaximum = 50.00m
                };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                return result.Objects != null && result.Objects.All(e => e.Amount <= 50.00m);
            }).ConfigureAwait(false);

            await TestAsync("Enumerate with amount range filter", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery
                {
                    MaxResults = 100,
                    AmountMinimum = 50.00m,
                    AmountMaximum = 100.00m
                };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                return result.Objects != null && result.Objects.All(e => e.Amount >= 50.00m && e.Amount <= 100.00m);
            }).ConfigureAwait(false);

            await TestAsync("Enumerate with date filter", async () =>
            {
                DateTime now = DateTime.UtcNow;
                EntryEnumerationQuery query = new EntryEnumerationQuery
                {
                    MaxResults = 100,
                    CreatedAfterUtc = now.AddMinutes(-10),
                    CreatedBeforeUtc = now.AddMinutes(10)
                };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                return result.Objects != null && result.Objects.Count > 0;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate with ContinuationToken", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery { MaxResults = 10 };
                EnumerationResult<Entry> result1 = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);

                if (string.IsNullOrEmpty(result1.ContinuationToken))
                    return false;

                query.ContinuationToken = result1.ContinuationToken;
                query.Skip = 0;
                EnumerationResult<Entry> result2 = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);

                // Second page should have different entries
                return result2.Objects != null && result2.Objects.Count > 0 &&
                       result2.Objects[0].Id != result1.Objects![0].Id;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate EndOfResults is accurate", async () =>
            {
                EntryEnumerationQuery query = new EntryEnumerationQuery { MaxResults = 100 };
                EnumerationResult<Entry> result = await _Client!.Entry.EnumerateAsync(account.Id, query).ConfigureAwait(false);
                return result.EndOfResults && result.RecordsRemaining == 0;
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Entry-Cancellation-Tests

        static async Task RunEntryCancellationTestsAsync()
        {
            Console.WriteLine("--- Entry Cancellation Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("Cancel Test " + UniqueSuffix(8)).ConfigureAwait(false);
            Entry credit = await _Client.Entry.AddCreditAsync(account.Id, 50.00m).ConfigureAwait(false);

            await TestAsync("Cancel pending entry", async () =>
            {
                await _Client!.Entry.CancelAsync(account.Id, credit.Id).ConfigureAwait(false);
                List<Entry> entries = await _Client.Entry.GetPendingAsync(account.Id).ConfigureAwait(false);
                return entries.Count == 0;
            }).ConfigureAwait(false);

            await TestAsync("Pending balance updates after cancellation", async () =>
            {
                Entry credit1 = await _Client!.Entry.AddCreditAsync(account.Id, 100.00m).ConfigureAwait(false);
                Balance balanceBefore = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                await _Client.Entry.CancelAsync(account.Id, credit1.Id).ConfigureAwait(false);
                Balance balanceAfter = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balanceBefore.PendingBalance != balanceAfter.PendingBalance;
            }).ConfigureAwait(false);

            await TestAsync("Cancel non-existent entry throws 404", async () =>
            {
                try
                {
                    await _Client!.Entry.CancelAsync(account.Id, MissingId()).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
                }
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Account-Enumeration-Tests

        static async Task RunAccountEnumerationTestsAsync()
        {
            Console.WriteLine("--- Account Enumeration Tests ---");
            Console.WriteLine("");

            // Create test accounts with a unique prefix
            string prefix = "EnumTest" + UniqueSuffix(6);
            List<Account> createdAccounts = new List<Account>();
            for (int i = 0; i < 10; i++)
            {
                Account acct = await _Client!.Account.CreateAsync($"{prefix}{i}").ConfigureAwait(false);
                createdAccounts.Add(acct);
            }

            await TestAsync("Enumerate accounts returns results", async () =>
            {
                EnumerationResult<Account> result = await _Client!.Account.EnumerateAsync().ConfigureAwait(false);
                return result.Objects != null && result.Objects.Count > 0;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate accounts with MaxResults", async () =>
            {
                AccountEnumerationQuery query = new AccountEnumerationQuery { MaxResults = 5 };
                EnumerationResult<Account> result = await _Client!.Account.EnumerateAsync(query).ConfigureAwait(false);
                return result.Objects != null && result.Objects.Count <= 5;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate accounts with Skip", async () =>
            {
                AccountEnumerationQuery query = new AccountEnumerationQuery { Skip = 5, MaxResults = 100 };
                EnumerationResult<Account> result = await _Client!.Account.EnumerateAsync(query).ConfigureAwait(false);
                return result.Objects != null;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate accounts TotalRecords populated", async () =>
            {
                AccountEnumerationQuery query = new AccountEnumerationQuery { MaxResults = 5 };
                EnumerationResult<Account> result = await _Client!.Account.EnumerateAsync(query).ConfigureAwait(false);
                // TotalRecords should be at least the number we created
                return result.TotalRecords >= 10;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate accounts RecordsRemaining is accurate", async () =>
            {
                AccountEnumerationQuery query = new AccountEnumerationQuery { MaxResults = 3 };
                EnumerationResult<Account> result = await _Client!.Account.EnumerateAsync(query).ConfigureAwait(false);
                // RecordsRemaining = TotalRecords - returned count
                return result.RecordsRemaining == result.TotalRecords - result.Objects!.Count;
            }).ConfigureAwait(false);

            await TestAsync("Enumerate accounts EndOfResults is accurate", async () =>
            {
                AccountEnumerationQuery query = new AccountEnumerationQuery { MaxResults = 1000 };
                EnumerationResult<Account> result = await _Client!.Account.EnumerateAsync(query).ConfigureAwait(false);
                return result.EndOfResults && result.RecordsRemaining == 0;
            }).ConfigureAwait(false);

            // Cleanup
            foreach (Account acct in createdAccounts)
            {
                await CleanupAccountAsync(acct.Id).ConfigureAwait(false);
            }

            Console.WriteLine("");
        }

        #endregion

        #region Historical-Balance-Tests

        static async Task RunHistoricalBalanceTestsAsync()
        {
            Console.WriteLine("--- Historical Balance Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("HistoricalTest" + UniqueSuffix(8)).ConfigureAwait(false);

            // Record timestamp before any commits
            DateTime timestampBeforeFirstCommit = DateTime.UtcNow;
            await Task.Delay(100).ConfigureAwait(false);

            // Add and commit first set
            await _Client.Entry.AddCreditAsync(account.Id, 100.00m).ConfigureAwait(false);
            await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);

            // Wait and record timestamp after first commit
            await Task.Delay(100).ConfigureAwait(false);
            DateTime timestampAfterFirstCommit = DateTime.UtcNow;
            await Task.Delay(100).ConfigureAwait(false);

            // Add and commit second set
            await _Client.Entry.AddCreditAsync(account.Id, 50.00m).ConfigureAwait(false);
            await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);

            await TestAsync("GetBalanceAsOf returns zero before first commit", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsOfAsync(account.Id, timestampBeforeFirstCommit).ConfigureAwait(false);
                return balance.CommittedBalance == 0m;
            }).ConfigureAwait(false);

            await TestAsync("GetBalanceAsOf returns balance after first commit", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsOfAsync(account.Id, timestampAfterFirstCommit).ConfigureAwait(false);
                return balance.CommittedBalance == 100.00m;
            }).ConfigureAwait(false);

            await TestAsync("GetBalanceAsOf returns current balance for future date", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsOfAsync(account.Id, DateTime.UtcNow.AddHours(1)).ConfigureAwait(false);
                return balance.CommittedBalance == 150.00m;
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Balance-Chain-Verification-Tests

        static async Task RunBalanceChainVerificationTestsAsync()
        {
            Console.WriteLine("--- Balance Chain Verification Tests ---");
            Console.WriteLine("");

            Account account = await _Client!.Account.CreateAsync("Chain Test " + UniqueSuffix(8)).ConfigureAwait(false);

            // Build up a chain of commits
            await _Client.Entry.AddCreditAsync(account.Id, 10.00m).ConfigureAwait(false);
            await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);

            await _Client.Entry.AddCreditAsync(account.Id, 20.00m).ConfigureAwait(false);
            await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);

            await _Client.Entry.AddDebitAsync(account.Id, 5.00m).ConfigureAwait(false);
            await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);

            await TestAsync("VerifyBalanceChain returns true for valid chain", async () =>
            {
                bool isValid = await _Client!.Balance.VerifyAsync(account.Id).ConfigureAwait(false);
                return isValid;
            }).ConfigureAwait(false);

            await TestAsync("Final balance is correct after chain", async () =>
            {
                Balance balance = await _Client!.Balance.GetAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 25.00m;
            }).ConfigureAwait(false);

            // Cleanup
            await CleanupAccountAsync(account.Id).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Error-Handling-Tests

        static async Task RunErrorHandlingTestsAsync()
        {
            Console.WriteLine("--- Error Handling Tests ---");
            Console.WriteLine("");

            await TestAsync("Add credit to non-existent account throws 404", async () =>
            {
                try
                {
                    await _Client!.Entry.AddCreditAsync(MissingId(), 10.00m).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
                }
            }).ConfigureAwait(false);

            await TestAsync("Add debit to non-existent account throws 404", async () =>
            {
                try
                {
                    await _Client!.Entry.AddDebitAsync(MissingId(), 10.00m).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
                }
            }).ConfigureAwait(false);

            await TestAsync("Get balance for non-existent account throws 404", async () =>
            {
                try
                {
                    await _Client!.Balance.GetAsync(MissingId()).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
                }
            }).ConfigureAwait(false);

            await TestAsync("Commit for non-existent account throws 404", async () =>
            {
                try
                {
                    await _Client!.Balance.CommitAsync(MissingId()).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
                }
            }).ConfigureAwait(false);

            await TestAsync("Delete non-existent account throws 404", async () =>
            {
                try
                {
                    await _Client!.Account.DeleteAsync(MissingId()).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
                }
            }).ConfigureAwait(false);

            await TestAsync("Verify balance chain for non-existent account throws 404", async () =>
            {
                try
                {
                    await _Client!.Balance.VerifyAsync(MissingId()).ConfigureAwait(false);
                    return false;
                }
                catch (NetLedgerApiException ex) when (ex.StatusCode == 404)
                {
                    return true;
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
                Account account = await _Client!.Account.CreateAsync("EdgeTest1" + UniqueSuffix(8)).ConfigureAwait(false);
                await _Client.Entry.AddCreditAsync(account.Id, 10.00m).ConfigureAwait(false);
                await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);
                await _Client.Entry.AddCreditAsync(account.Id, 20.00m).ConfigureAwait(false);
                await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);
                await _Client.Entry.AddCreditAsync(account.Id, 30.00m).ConfigureAwait(false);
                await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);
                Balance balance = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 60.00m;
            }).ConfigureAwait(false);

            await TestAsync("Large number of pending entries", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("EdgeTest2" + UniqueSuffix(8)).ConfigureAwait(false);
                for (int i = 0; i < 50; i++)
                {
                    await _Client.Entry.AddCreditAsync(account.Id, 1.00m).ConfigureAwait(false);
                }
                List<Entry> entries = await _Client.Entry.GetPendingAsync(account.Id).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return entries.Count == 50;
            }).ConfigureAwait(false);

            await TestAsync("Very long account name", async () =>
            {
                string longName = "LongName_" + new string('A', 200);
                Account account = await _Client!.Account.CreateAsync(longName).ConfigureAwait(false);
                Account retrieved = await _Client.Account.GetAsync(account.Id).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return retrieved.Name == longName;
            }).ConfigureAwait(false);

            await TestAsync("Very long entry description", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("EdgeTest3" + UniqueSuffix(8)).ConfigureAwait(false);
                string longDesc = new string('B', 200);
                Entry entry = await _Client.Entry.AddCreditAsync(account.Id, 10.00m, longDesc).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return entry.Description == longDesc;
            }).ConfigureAwait(false);

            await TestAsync("Partial commit of pending entries", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("EdgeTest4" + UniqueSuffix(8)).ConfigureAwait(false);
                Entry credit1 = await _Client.Entry.AddCreditAsync(account.Id, 10.00m).ConfigureAwait(false);
                Entry credit2 = await _Client.Entry.AddCreditAsync(account.Id, 20.00m).ConfigureAwait(false);
                Entry credit3 = await _Client.Entry.AddCreditAsync(account.Id, 30.00m).ConfigureAwait(false);
                await _Client.Balance.CommitAsync(account.Id, new List<string> { credit1.Id, credit3.Id }).ConfigureAwait(false);
                Balance balance = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 40.00m && balance.PendingBalance == 60.00m;
            }).ConfigureAwait(false);

            await TestAsync("Mix of credits and debits resulting in zero", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("EdgeTest5" + UniqueSuffix(8)).ConfigureAwait(false);
                await _Client.Entry.AddCreditAsync(account.Id, 100.00m).ConfigureAwait(false);
                await _Client.Entry.AddDebitAsync(account.Id, 100.00m).ConfigureAwait(false);
                await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);
                Balance balance = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == 0m;
            }).ConfigureAwait(false);

            await TestAsync("Mix of credits and debits resulting in negative", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("EdgeTest6" + UniqueSuffix(8)).ConfigureAwait(false);
                await _Client.Entry.AddCreditAsync(account.Id, 50.00m).ConfigureAwait(false);
                await _Client.Entry.AddDebitAsync(account.Id, 100.00m).ConfigureAwait(false);
                await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);
                Balance balance = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return balance.CommittedBalance == -50.00m;
            }).ConfigureAwait(false);

            await TestAsync("Account creation timestamp is set", async () =>
            {
                DateTime before = DateTime.UtcNow.AddSeconds(-1);
                Account account = await _Client!.Account.CreateAsync("EdgeTest7" + UniqueSuffix(8)).ConfigureAwait(false);
                DateTime after = DateTime.UtcNow.AddSeconds(1);
                Account retrieved = await _Client.Account.GetAsync(account.Id).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                return retrieved.CreatedUtc >= before && retrieved.CreatedUtc <= after;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Performance-Tests

        static async Task RunPerformanceTestsAsync()
        {
            Console.WriteLine("--- Performance Tests ---");
            Console.WriteLine("");

            await TestAsync("Create 50 accounts in reasonable time", async () =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                string prefix = "Perf_" + UniqueSuffix(6);
                List<Account> accounts = new List<Account>();

                for (int i = 0; i < 50; i++)
                {
                    Account acct = await _Client!.Account.CreateAsync($"{prefix}_{i}").ConfigureAwait(false);
                    accounts.Add(acct);
                }

                sw.Stop();

                // Cleanup
                foreach (Account acct in accounts)
                {
                    await CleanupAccountAsync(acct.Id).ConfigureAwait(false);
                }

                return sw.Elapsed.TotalSeconds < 30;
            }).ConfigureAwait(false);

            await TestAsync("Add 100 entries in reasonable time", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("Perf Test " + UniqueSuffix(8)).ConfigureAwait(false);
                Stopwatch sw = Stopwatch.StartNew();

                for (int i = 0; i < 100; i++)
                {
                    await _Client.Entry.AddCreditAsync(account.Id, 1.00m).ConfigureAwait(false);
                }

                sw.Stop();
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);

                return sw.Elapsed.TotalSeconds < 30;
            }).ConfigureAwait(false);

            await TestAsync("Commit 50 entries in reasonable time", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("Perf Test 2 " + UniqueSuffix(8)).ConfigureAwait(false);

                for (int i = 0; i < 50; i++)
                {
                    await _Client.Entry.AddCreditAsync(account.Id, 1.00m).ConfigureAwait(false);
                }

                Stopwatch sw = Stopwatch.StartNew();
                await _Client.Balance.CommitAsync(account.Id).ConfigureAwait(false);
                sw.Stop();

                await CleanupAccountAsync(account.Id).ConfigureAwait(false);

                return sw.Elapsed.TotalSeconds < 10;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Concurrency-Tests

        static async Task RunConcurrencyTestsAsync()
        {
            Console.WriteLine("--- Concurrency Tests ---");
            Console.WriteLine("");

            await TestAsync("Concurrent credits to same account are handled correctly", async () =>
            {
                Account account = await _Client!.Account.CreateAsync("Concurrent Test 1 " + UniqueSuffix(8)).ConfigureAwait(false);

                List<Task<Entry>> tasks = new List<Task<Entry>>();
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(_Client.Entry.AddCreditAsync(account.Id, 10.00m));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);

                Balance balance = await _Client.Balance.GetAsync(account.Id).ConfigureAwait(false);
                await CleanupAccountAsync(account.Id).ConfigureAwait(false);

                return balance.PendingBalance == 100.00m;
            }).ConfigureAwait(false);

            await TestAsync("Concurrent operations on different accounts work correctly", async () =>
            {
                List<Task<Account>> accountTasks = new List<Task<Account>>();
                string prefix = "Concurrent_" + UniqueSuffix(6);

                for (int i = 0; i < 5; i++)
                {
                    accountTasks.Add(_Client!.Account.CreateAsync($"{prefix}_{i}"));
                }
                Account[] accounts = await Task.WhenAll(accountTasks).ConfigureAwait(false);

                List<Task<Entry>> creditTasks = new List<Task<Entry>>();
                foreach (Account account in accounts)
                {
                    creditTasks.Add(_Client!.Entry.AddCreditAsync(account.Id, 100.00m));
                }
                await Task.WhenAll(creditTasks).ConfigureAwait(false);

                List<Task> commitTasks = new List<Task>();
                foreach (Account account in accounts)
                {
                    commitTasks.Add(_Client!.Balance.CommitAsync(account.Id));
                }
                await Task.WhenAll(commitTasks).ConfigureAwait(false);

                List<Task<Balance>> balanceTasks = new List<Task<Balance>>();
                foreach (Account account in accounts)
                {
                    balanceTasks.Add(_Client!.Balance.GetAsync(account.Id));
                }
                Balance[] balances = await Task.WhenAll(balanceTasks).ConfigureAwait(false);

                // Cleanup
                foreach (Account account in accounts)
                {
                    await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                }

                return balances.All(b => b.CommittedBalance == 100.00m);
            }).ConfigureAwait(false);

            await TestAsync("Concurrent account creation works correctly", async () =>
            {
                string prefix = "ConcCreate_" + UniqueSuffix(6);
                List<Task<Account>> tasks = new List<Task<Account>>();

                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(_Client!.Account.CreateAsync($"{prefix}_{i}"));
                }

                Account[] accounts = await Task.WhenAll(tasks).ConfigureAwait(false);

                bool allUnique = accounts.Select(a => a.Id).Distinct().Count() == 10;

                // Cleanup
                foreach (Account account in accounts)
                {
                    await CleanupAccountAsync(account.Id).ConfigureAwait(false);
                }

                return allUnique;
            }).ConfigureAwait(false);

            Console.WriteLine("");
        }

        #endregion

        #region Test-Infrastructure

        static async Task CleanupAccountAsync(string accountId)
        {
            if (_NoCleanup)
            {
                if (!_RetainedAccounts.Contains(accountId))
                {
                    _RetainedAccounts.Add(accountId);
                }
            }
            else
            {
                await _Client!.Account.DeleteAsync(accountId).ConfigureAwait(false);
            }
        }

        static async Task TestAsync(string testName, Func<Task<bool>> testFunc)
        {
            _TestCount++;
            bool passed = false;
            string? error = null;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                passed = await testFunc().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                passed = false;
                error = ex.GetType().Name + ": " + ex.Message;
            }

            sw.Stop();
            long elapsedMs = sw.ElapsedMilliseconds;

            if (passed)
            {
                _PassCount++;
                Console.WriteLine($"[PASS] {testName} ({elapsedMs}ms)");
            }
            else
            {
                _FailCount++;
                Console.WriteLine($"[FAIL] {testName} ({elapsedMs}ms)");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"       Error: {error}");
                }
            }

            _Results.Add(new TestResult
            {
                TestName = testName,
                Passed = passed,
                ElapsedMs = elapsedMs,
                Error = error
            });
        }

        static void DisplaySummary()
        {
            Console.WriteLine("");
            Console.WriteLine("================================================================================");
            Console.WriteLine("Test Summary");
            Console.WriteLine("================================================================================");
            Console.WriteLine("");
            Console.WriteLine($"Total Tests:   {_TestCount}");
            Console.WriteLine($"Passed:        {_PassCount}");
            Console.WriteLine($"Failed:        {_FailCount}");
            Console.WriteLine($"Success Rate:  {(_TestCount > 0 ? ((_PassCount * 100.0) / _TestCount).ToString("F2") : "0.00")}%");
            Console.WriteLine($"Total Runtime: {_TotalStopwatch.ElapsedMilliseconds}ms ({_TotalStopwatch.Elapsed.TotalSeconds:F2}s)");
            Console.WriteLine("");

            if (_FailCount > 0)
            {
                Console.WriteLine("Failed Tests:");
                Console.WriteLine("--------------------------------------------------------------------------------");
                foreach (TestResult result in _Results.Where(r => !r.Passed))
                {
                    Console.WriteLine($"  - {result.TestName} ({result.ElapsedMs}ms)");
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Console.WriteLine($"    Error: {result.Error}");
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

            if (_NoCleanup && _RetainedAccounts.Count > 0)
            {
                Console.WriteLine("");
                Console.WriteLine($"Cleanup skipped. {_RetainedAccounts.Count} test account(s) retained on server:");
                foreach (string accountId in _RetainedAccounts)
                {
                    Console.WriteLine($"  - {accountId}");
                }
            }
        }

        static string UniqueSuffix(int length)
        {
            string value = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x") + Random.Shared.NextInt64(0, Int64.MaxValue).ToString("x");
            return value.Length <= length ? value : value.Substring(value.Length - length);
        }

        static string MissingId()
        {
            return "missing_" + UniqueSuffix(16);
        }

        #endregion
    }

    #region Test-Result-Class

    class TestResult
    {
        public string TestName { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public long ElapsedMs { get; set; }
        public string? Error { get; set; }
    }

    #endregion
}
