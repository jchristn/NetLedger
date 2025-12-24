namespace NetLedger.Sdk.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;

    /// <summary>
    /// Test harness for the NetLedger SDK.
    /// </summary>
    internal class Program
    {
        #region Private-Members

        private static string _Endpoint = string.Empty;
        private static string _ApiKey = string.Empty;
        private static NetLedgerClient _Client = null!;
        private static List<TestResult> _Results = new List<TestResult>();
        private static Stopwatch _TotalStopwatch = new Stopwatch();

        #endregion

        #region Entry-Point

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("NetLedger SDK Test Harness");
            Console.WriteLine("==========================");
            Console.WriteLine();

            if (args.Length < 2)
            {
                Console.WriteLine("Usage: NetLedger.Sdk.Test <endpoint> <api-key>");
                Console.WriteLine();
                Console.WriteLine("Example: NetLedger.Sdk.Test http://localhost:8080 your-api-key-here");
                return 1;
            }

            _Endpoint = args[0];
            _ApiKey = args[1];

            Console.WriteLine($"Endpoint: {_Endpoint}");
            Console.WriteLine($"API Key: {_ApiKey.Substring(0, Math.Min(8, _ApiKey.Length))}...");
            Console.WriteLine();

            try
            {
                _Client = new NetLedgerClient(_Endpoint, _ApiKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create client: {ex.Message}");
                return 1;
            }

            _TotalStopwatch.Start();

            try
            {
                await RunServiceTests().ConfigureAwait(false);
                await RunAccountTests().ConfigureAwait(false);
                await RunEntryTests().ConfigureAwait(false);
                await RunEnumerationTests().ConfigureAwait(false);
                await RunBalanceTests().ConfigureAwait(false);
                await RunApiKeyTests().ConfigureAwait(false);
                await CleanupTests().ConfigureAwait(false);
            }
            finally
            {
                _TotalStopwatch.Stop();
                _Client.Dispose();
            }

            PrintSummary();

            int failedCount = _Results.Count(r => !r.Success);
            return failedCount > 0 ? 1 : 0;
        }

        #endregion

        #region Test-Sections

        private static async Task RunServiceTests()
        {
            PrintSectionHeader("SERVICE TESTS");

            await RunTest("Health Check", async () =>
            {
                bool healthy = await _Client.Service.HealthCheckAsync().ConfigureAwait(false);
                if (!healthy) throw new Exception("Health check returned false");
            }).ConfigureAwait(false);

            await RunTest("Get Service Info", async () =>
            {
                ServiceInfo info = await _Client.Service.GetInfoAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(info.Name)) throw new Exception("Service name is empty");
            }).ConfigureAwait(false);

            Console.WriteLine();
        }

        private static async Task RunAccountTests()
        {
            PrintSectionHeader("ACCOUNT TESTS");

            Account? createdAccount = null;
            string testAccountName = $"TestAccount_{Guid.NewGuid():N}";

            await RunTest("Create Account", async () =>
            {
                createdAccount = await _Client.Account.CreateAsync(testAccountName, "Test notes").ConfigureAwait(false);
                if (createdAccount.GUID == Guid.Empty) throw new Exception("Account GUID is empty");
                if (createdAccount.Name != testAccountName) throw new Exception("Account name mismatch");
            }).ConfigureAwait(false);

            await RunTest("Check Account Exists", async () =>
            {
                if (createdAccount == null) throw new Exception("No account to check");
                bool exists = await _Client.Account.ExistsAsync(createdAccount.GUID).ConfigureAwait(false);
                if (!exists) throw new Exception("Account should exist");
            }).ConfigureAwait(false);

            await RunTest("Get Account by GUID", async () =>
            {
                if (createdAccount == null) throw new Exception("No account to get");
                Account account = await _Client.Account.GetAsync(createdAccount.GUID).ConfigureAwait(false);
                if (account.GUID != createdAccount.GUID) throw new Exception("Account GUID mismatch");
            }).ConfigureAwait(false);

            await RunTest("Get Account by Name", async () =>
            {
                if (createdAccount == null) throw new Exception("No account to get");
                Account account = await _Client.Account.GetByNameAsync(testAccountName).ConfigureAwait(false);
                if (account.GUID != createdAccount.GUID) throw new Exception("Account GUID mismatch");
            }).ConfigureAwait(false);

            await RunTest("Enumerate Accounts", async () =>
            {
                EnumerationResult<Account> result = await _Client.Account.EnumerateAsync(new AccountEnumerationQuery
                {
                    MaxResults = 10
                }).ConfigureAwait(false);
                if (result.Objects == null || result.Objects.Count == 0) throw new Exception("No accounts returned");
            }).ConfigureAwait(false);

            await RunTest("Enumerate Accounts with Search", async () =>
            {
                EnumerationResult<Account> result = await _Client.Account.EnumerateAsync(new AccountEnumerationQuery
                {
                    MaxResults = 10,
                    SearchTerm = testAccountName
                }).ConfigureAwait(false);
                if (result.Objects == null || result.Objects.Count == 0) throw new Exception("Search should find the test account");
            }).ConfigureAwait(false);

            // Store for later tests
            if (createdAccount != null)
            {
                _TestAccountGuid = createdAccount.GUID;
            }

            Console.WriteLine();
        }

        private static Guid _TestAccountGuid = Guid.Empty;
        private static List<Guid> _TestEntryGuids = new List<Guid>();

        private static async Task RunEntryTests()
        {
            PrintSectionHeader("ENTRY TESTS");

            if (_TestAccountGuid == Guid.Empty)
            {
                Console.WriteLine("  SKIPPED: No test account available");
                Console.WriteLine();
                return;
            }

            await RunTest("Add Single Credit", async () =>
            {
                Entry entry = await _Client.Entry.AddCreditAsync(_TestAccountGuid, 100.00m, "Test credit").ConfigureAwait(false);
                if (entry.GUID == Guid.Empty) throw new Exception("Entry GUID is empty");
                if (entry.Amount != 100.00m) throw new Exception("Amount mismatch");
                if (entry.Type != EntryType.Credit) throw new Exception("Type should be Credit");
                _TestEntryGuids.Add(entry.GUID);
            }).ConfigureAwait(false);

            await RunTest("Add Single Debit", async () =>
            {
                Entry entry = await _Client.Entry.AddDebitAsync(_TestAccountGuid, 25.50m, "Test debit").ConfigureAwait(false);
                if (entry.GUID == Guid.Empty) throw new Exception("Entry GUID is empty");
                if (entry.Amount != 25.50m) throw new Exception("Amount mismatch");
                if (entry.Type != EntryType.Debit) throw new Exception("Type should be Debit");
                _TestEntryGuids.Add(entry.GUID);
            }).ConfigureAwait(false);

            await RunTest("Add Multiple Credits (Batch)", async () =>
            {
                List<EntryInput> inputs = new List<EntryInput>
                {
                    new EntryInput(10.00m, "Batch credit 1"),
                    new EntryInput(20.00m, "Batch credit 2"),
                    new EntryInput(30.00m, "Batch credit 3")
                };
                List<Entry> entries = await _Client.Entry.AddCreditsAsync(_TestAccountGuid, inputs).ConfigureAwait(false);
                if (entries.Count != 3) throw new Exception($"Expected 3 entries, got {entries.Count}");
                foreach (Entry entry in entries)
                {
                    _TestEntryGuids.Add(entry.GUID);
                }
            }).ConfigureAwait(false);

            await RunTest("Add Multiple Debits (Batch)", async () =>
            {
                List<EntryInput> inputs = new List<EntryInput>
                {
                    new EntryInput(5.00m, "Batch debit 1"),
                    new EntryInput(7.50m, "Batch debit 2")
                };
                List<Entry> entries = await _Client.Entry.AddDebitsAsync(_TestAccountGuid, inputs).ConfigureAwait(false);
                if (entries.Count != 2) throw new Exception($"Expected 2 entries, got {entries.Count}");
                foreach (Entry entry in entries)
                {
                    _TestEntryGuids.Add(entry.GUID);
                }
            }).ConfigureAwait(false);

            await RunTest("Get All Entries", async () =>
            {
                List<Entry> entries = await _Client.Entry.GetAllAsync(_TestAccountGuid).ConfigureAwait(false);
                if (entries.Count < _TestEntryGuids.Count) throw new Exception($"Expected at least {_TestEntryGuids.Count} entries");
            }).ConfigureAwait(false);

            await RunTest("Get Pending Entries", async () =>
            {
                List<Entry> entries = await _Client.Entry.GetPendingAsync(_TestAccountGuid).ConfigureAwait(false);
                if (entries.Count == 0) throw new Exception("Should have pending entries");
            }).ConfigureAwait(false);

            await RunTest("Get Pending Credits", async () =>
            {
                List<Entry> entries = await _Client.Entry.GetPendingCreditsAsync(_TestAccountGuid).ConfigureAwait(false);
                if (entries.Count == 0) throw new Exception("Should have pending credits");
            }).ConfigureAwait(false);

            await RunTest("Get Pending Debits", async () =>
            {
                List<Entry> entries = await _Client.Entry.GetPendingDebitsAsync(_TestAccountGuid).ConfigureAwait(false);
                if (entries.Count == 0) throw new Exception("Should have pending debits");
            }).ConfigureAwait(false);

            await RunTest("Enumerate Entries", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_TestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 50,
                    Ordering = EnumerationOrder.CreatedDescending
                }).ConfigureAwait(false);
                if (result.Objects == null || result.Objects.Count == 0) throw new Exception("No entries returned");
            }).ConfigureAwait(false);

            await RunTest("Enumerate Entries with Amount Filter", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_TestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 50,
                    AmountMinimum = 10.00m,
                    AmountMaximum = 50.00m
                }).ConfigureAwait(false);
                // Just verify no error
            }).ConfigureAwait(false);

            // Add an entry to cancel
            Entry? entryToCancel = null;
            await RunTest("Add Entry for Cancellation", async () =>
            {
                entryToCancel = await _Client.Entry.AddCreditAsync(_TestAccountGuid, 1.00m, "Entry to cancel").ConfigureAwait(false);
            }).ConfigureAwait(false);

            await RunTest("Cancel Entry", async () =>
            {
                if (entryToCancel == null) throw new Exception("No entry to cancel");
                await _Client.Entry.CancelAsync(_TestAccountGuid, entryToCancel.GUID).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Console.WriteLine();
        }

        private static Guid _EnumTestAccountGuid = Guid.Empty;
        private static List<Entry> _EnumTestEntries = new List<Entry>();

        private static async Task RunEnumerationTests()
        {
            PrintSectionHeader("ENUMERATION TESTS");

            // Create a dedicated account for enumeration tests
            string enumAccountName = $"EnumTestAccount_{Guid.NewGuid():N}";

            await RunTest("Create Enumeration Test Account", async () =>
            {
                Account account = await _Client.Account.CreateAsync(enumAccountName, "Account for enumeration tests").ConfigureAwait(false);
                _EnumTestAccountGuid = account.GUID;
            }).ConfigureAwait(false);

            if (_EnumTestAccountGuid == Guid.Empty)
            {
                Console.WriteLine("  SKIPPED: Could not create test account");
                Console.WriteLine();
                return;
            }

            // Create 10 entries with known amounts for testing ordering and pagination
            // Amounts: 10, 20, 30, 40, 50, 60, 70, 80, 90, 100
            await RunTest("Create 10 Entries for Pagination Tests", async () =>
            {
                _EnumTestEntries.Clear();
                for (int i = 1; i <= 10; i++)
                {
                    decimal amount = i * 10.0m;
                    Entry entry = await _Client.Entry.AddCreditAsync(_EnumTestAccountGuid, amount, $"Entry {i}").ConfigureAwait(false);
                    _EnumTestEntries.Add(entry);
                    await Task.Delay(50).ConfigureAwait(false); // Ensure distinct timestamps
                }
                if (_EnumTestEntries.Count != 10)
                    throw new Exception($"Expected 10 entries, got {_EnumTestEntries.Count}");
            }).ConfigureAwait(false);

            // Test: Basic pagination - page 1 of 3 (3 entries per page)
            await RunTest("Pagination: First Page (3 of 10)", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 3,
                    Skip = 0,
                    Ordering = EnumerationOrder.CreatedAscending
                }).ConfigureAwait(false);

                if (result.Objects == null || result.Objects.Count != 3)
                    throw new Exception($"Expected 3 entries, got {result.Objects?.Count ?? 0}");
                if (result.TotalRecords != 10)
                    throw new Exception($"TotalRecords should be 10, got {result.TotalRecords}");
                if (result.RecordsRemaining != 7)
                    throw new Exception($"RecordsRemaining should be 7, got {result.RecordsRemaining}");
                if (result.EndOfResults)
                    throw new Exception("EndOfResults should be false for first page");
            }).ConfigureAwait(false);

            // Test: Pagination - middle page
            await RunTest("Pagination: Middle Page (Skip 3, Take 3)", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 3,
                    Skip = 3,
                    Ordering = EnumerationOrder.CreatedAscending
                }).ConfigureAwait(false);

                if (result.Objects == null || result.Objects.Count != 3)
                    throw new Exception($"Expected 3 entries, got {result.Objects?.Count ?? 0}");
                if (result.TotalRecords != 10)
                    throw new Exception($"TotalRecords should be 10, got {result.TotalRecords}");
                if (result.RecordsRemaining != 4)
                    throw new Exception($"RecordsRemaining should be 4, got {result.RecordsRemaining}");
                if (result.EndOfResults)
                    throw new Exception("EndOfResults should be false for middle page");
            }).ConfigureAwait(false);

            // Test: Pagination - last page (partial)
            await RunTest("Pagination: Last Page (Skip 9, Take 3)", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 3,
                    Skip = 9,
                    Ordering = EnumerationOrder.CreatedAscending
                }).ConfigureAwait(false);

                if (result.Objects == null || result.Objects.Count != 1)
                    throw new Exception($"Expected 1 entry on last page, got {result.Objects?.Count ?? 0}");
                if (result.TotalRecords != 10)
                    throw new Exception($"TotalRecords should be 10, got {result.TotalRecords}");
                if (result.RecordsRemaining != 0)
                    throw new Exception($"RecordsRemaining should be 0, got {result.RecordsRemaining}");
                if (!result.EndOfResults)
                    throw new Exception("EndOfResults should be true for last page");
            }).ConfigureAwait(false);

            // Test: Ordering - CreatedAscending (oldest first)
            await RunTest("Ordering: CreatedAscending", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    Ordering = EnumerationOrder.CreatedAscending
                }).ConfigureAwait(false);

                if (result.Objects == null || result.Objects.Count != 10)
                    throw new Exception($"Expected 10 entries, got {result.Objects?.Count ?? 0}");

                // First entry should have smallest amount (10) since created first
                if (result.Objects[0].Amount != 10.0m)
                    throw new Exception($"First entry should have amount 10, got {result.Objects[0].Amount}");
                // Last entry should have largest amount (100) since created last
                if (result.Objects[9].Amount != 100.0m)
                    throw new Exception($"Last entry should have amount 100, got {result.Objects[9].Amount}");

                // Verify ascending order by creation time
                for (int i = 1; i < result.Objects.Count; i++)
                {
                    if (result.Objects[i].CreatedUtc < result.Objects[i - 1].CreatedUtc)
                        throw new Exception($"Entries not in CreatedAscending order at index {i}");
                }
            }).ConfigureAwait(false);

            // Test: Ordering - CreatedDescending (newest first)
            await RunTest("Ordering: CreatedDescending", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    Ordering = EnumerationOrder.CreatedDescending
                }).ConfigureAwait(false);

                if (result.Objects == null || result.Objects.Count != 10)
                    throw new Exception($"Expected 10 entries, got {result.Objects?.Count ?? 0}");

                // First entry should have largest amount (100) since created last
                if (result.Objects[0].Amount != 100.0m)
                    throw new Exception($"First entry should have amount 100, got {result.Objects[0].Amount}");
                // Last entry should have smallest amount (10) since created first
                if (result.Objects[9].Amount != 10.0m)
                    throw new Exception($"Last entry should have amount 10, got {result.Objects[9].Amount}");

                // Verify descending order by creation time
                for (int i = 1; i < result.Objects.Count; i++)
                {
                    if (result.Objects[i].CreatedUtc > result.Objects[i - 1].CreatedUtc)
                        throw new Exception($"Entries not in CreatedDescending order at index {i}");
                }
            }).ConfigureAwait(false);

            // Test: Ordering - AmountAscending (smallest first)
            await RunTest("Ordering: AmountAscending", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    Ordering = EnumerationOrder.AmountAscending
                }).ConfigureAwait(false);

                if (result.Objects == null || result.Objects.Count != 10)
                    throw new Exception($"Expected 10 entries, got {result.Objects?.Count ?? 0}");

                // Verify ascending order by amount
                for (int i = 1; i < result.Objects.Count; i++)
                {
                    if (result.Objects[i].Amount < result.Objects[i - 1].Amount)
                        throw new Exception($"Entries not in AmountAscending order at index {i}: {result.Objects[i].Amount} < {result.Objects[i - 1].Amount}");
                }

                // First should be 10, last should be 100
                if (result.Objects[0].Amount != 10.0m)
                    throw new Exception($"First entry should have amount 10, got {result.Objects[0].Amount}");
                if (result.Objects[9].Amount != 100.0m)
                    throw new Exception($"Last entry should have amount 100, got {result.Objects[9].Amount}");
            }).ConfigureAwait(false);

            // Test: Ordering - AmountDescending (largest first)
            await RunTest("Ordering: AmountDescending", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    Ordering = EnumerationOrder.AmountDescending
                }).ConfigureAwait(false);

                if (result.Objects == null || result.Objects.Count != 10)
                    throw new Exception($"Expected 10 entries, got {result.Objects?.Count ?? 0}");

                // Verify descending order by amount
                for (int i = 1; i < result.Objects.Count; i++)
                {
                    if (result.Objects[i].Amount > result.Objects[i - 1].Amount)
                        throw new Exception($"Entries not in AmountDescending order at index {i}: {result.Objects[i].Amount} > {result.Objects[i - 1].Amount}");
                }

                // First should be 100, last should be 10
                if (result.Objects[0].Amount != 100.0m)
                    throw new Exception($"First entry should have amount 100, got {result.Objects[0].Amount}");
                if (result.Objects[9].Amount != 10.0m)
                    throw new Exception($"Last entry should have amount 10, got {result.Objects[9].Amount}");
            }).ConfigureAwait(false);

            // Test: Pagination with ordering - verify ordering is preserved across pages
            await RunTest("Pagination with AmountDescending: Verify Order Across Pages", async () =>
            {
                EnumerationResult<Entry> page1 = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 5,
                    Skip = 0,
                    Ordering = EnumerationOrder.AmountDescending
                }).ConfigureAwait(false);

                EnumerationResult<Entry> page2 = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 5,
                    Skip = 5,
                    Ordering = EnumerationOrder.AmountDescending
                }).ConfigureAwait(false);

                if (page1.Objects == null || page1.Objects.Count != 5)
                    throw new Exception($"Page 1 should have 5 entries, got {page1.Objects?.Count ?? 0}");
                if (page2.Objects == null || page2.Objects.Count != 5)
                    throw new Exception($"Page 2 should have 5 entries, got {page2.Objects?.Count ?? 0}");

                // Page 1 should have amounts: 100, 90, 80, 70, 60
                // Page 2 should have amounts: 50, 40, 30, 20, 10
                if (page1.Objects[0].Amount != 100.0m)
                    throw new Exception($"Page 1 first entry should be 100, got {page1.Objects[0].Amount}");
                if (page1.Objects[4].Amount != 60.0m)
                    throw new Exception($"Page 1 last entry should be 60, got {page1.Objects[4].Amount}");
                if (page2.Objects[0].Amount != 50.0m)
                    throw new Exception($"Page 2 first entry should be 50, got {page2.Objects[0].Amount}");
                if (page2.Objects[4].Amount != 10.0m)
                    throw new Exception($"Page 2 last entry should be 10, got {page2.Objects[4].Amount}");

                // Last entry of page 1 should be > first entry of page 2
                if (page1.Objects[4].Amount <= page2.Objects[0].Amount)
                    throw new Exception("Ordering not preserved across pages");
            }).ConfigureAwait(false);

            // Test: Amount filtering with pagination
            await RunTest("Amount Filter with Pagination", async () =>
            {
                // Filter for amounts between 30 and 70 (inclusive)
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 10,
                    AmountMinimum = 30.0m,
                    AmountMaximum = 70.0m,
                    Ordering = EnumerationOrder.AmountAscending
                }).ConfigureAwait(false);

                if (result.Objects == null)
                    throw new Exception("Objects should not be null");

                // Should return entries with amounts: 30, 40, 50, 60, 70 = 5 entries
                if (result.Objects.Count != 5)
                    throw new Exception($"Expected 5 filtered entries, got {result.Objects.Count}");

                // Verify all entries are within the filter range
                foreach (Entry entry in result.Objects)
                {
                    if (entry.Amount < 30.0m || entry.Amount > 70.0m)
                        throw new Exception($"Entry amount {entry.Amount} is outside filter range 30-70");
                }

                if (result.TotalRecords != 5)
                    throw new Exception($"TotalRecords should be 5 for filtered query, got {result.TotalRecords}");
            }).ConfigureAwait(false);

            // Test: Exact page boundary - request exactly the remaining records
            await RunTest("Pagination: Exact Page Boundary (Skip 7, Take 3)", async () =>
            {
                EnumerationResult<Entry> result = await _Client.Entry.EnumerateAsync(_EnumTestAccountGuid, new EntryEnumerationQuery
                {
                    MaxResults = 3,
                    Skip = 7,
                    Ordering = EnumerationOrder.CreatedAscending
                }).ConfigureAwait(false);

                // Should return exactly 3 entries (8th, 9th, 10th - amounts 80, 90, 100)
                if (result.Objects == null || result.Objects.Count != 3)
                    throw new Exception($"Expected 3 entries, got {result.Objects?.Count ?? 0}");
                if (result.TotalRecords != 10)
                    throw new Exception($"TotalRecords should be 10, got {result.TotalRecords}");
                if (result.RecordsRemaining != 0)
                    throw new Exception($"RecordsRemaining should be 0, got {result.RecordsRemaining}");
                if (!result.EndOfResults)
                    throw new Exception("EndOfResults should be true when exactly at end");
            }).ConfigureAwait(false);

            // Test: Account enumeration pagination
            await RunTest("Account Enumeration: Pagination Fields", async () =>
            {
                EnumerationResult<Account> result = await _Client.Account.EnumerateAsync(new AccountEnumerationQuery
                {
                    MaxResults = 2,
                    Skip = 0
                }).ConfigureAwait(false);

                // Just verify the pagination fields are populated
                if (result.Objects == null)
                    throw new Exception("Objects should not be null");

                // TotalRecords should be >= 2 (we have at least the test accounts)
                if (result.TotalRecords < 1)
                    throw new Exception($"TotalRecords should be at least 1, got {result.TotalRecords}");

                // If we have more than 2 accounts, EndOfResults should be false
                if (result.TotalRecords > 2 && result.EndOfResults)
                    throw new Exception("EndOfResults should be false when more records exist");

                // RecordsRemaining should match TotalRecords - returned count
                int expectedRemaining = Math.Max(0, result.TotalRecords - result.Objects.Count);
                if (result.RecordsRemaining != expectedRemaining)
                    throw new Exception($"RecordsRemaining should be {expectedRemaining}, got {result.RecordsRemaining}");
            }).ConfigureAwait(false);

            Console.WriteLine();
        }

        private static async Task RunBalanceTests()
        {
            PrintSectionHeader("BALANCE TESTS");

            if (_TestAccountGuid == Guid.Empty)
            {
                Console.WriteLine("  SKIPPED: No test account available");
                Console.WriteLine();
                return;
            }

            await RunTest("Get Balance", async () =>
            {
                Balance balance = await _Client.Balance.GetAsync(_TestAccountGuid).ConfigureAwait(false);
                // Committed should be 0 since nothing committed yet
                // Pending should reflect the entries we added
            }).ConfigureAwait(false);

            await RunTest("Get All Balances", async () =>
            {
                List<Balance> balances = await _Client.Balance.GetAllAsync().ConfigureAwait(false);
                if (balances.Count == 0) throw new Exception("Should have at least one balance");
            }).ConfigureAwait(false);

            await RunTest("Commit All Pending Entries", async () =>
            {
                CommitResult result = await _Client.Balance.CommitAsync(_TestAccountGuid).ConfigureAwait(false);
                // Verify commit happened
            }).ConfigureAwait(false);

            await RunTest("Get Balance After Commit", async () =>
            {
                Balance balance = await _Client.Balance.GetAsync(_TestAccountGuid).ConfigureAwait(false);
                // After commit, committed balance should reflect the entries
            }).ConfigureAwait(false);

            await RunTest("Verify Balance Chain", async () =>
            {
                bool valid = await _Client.Balance.VerifyAsync(_TestAccountGuid).ConfigureAwait(false);
                if (!valid) throw new Exception("Balance chain should be valid");
            }).ConfigureAwait(false);

            // Test committing specific entries
            await RunTest("Add Entries for Selective Commit", async () =>
            {
                await _Client.Entry.AddCreditAsync(_TestAccountGuid, 50.00m, "Selective commit test").ConfigureAwait(false);
                await _Client.Entry.AddCreditAsync(_TestAccountGuid, 75.00m, "Selective commit test 2").ConfigureAwait(false);
            }).ConfigureAwait(false);

            await RunTest("Commit Specific Entries", async () =>
            {
                List<Entry> pending = await _Client.Entry.GetPendingAsync(_TestAccountGuid).ConfigureAwait(false);
                if (pending.Count > 0)
                {
                    List<Guid> guids = new List<Guid> { pending[0].GUID };
                    CommitResult result = await _Client.Balance.CommitAsync(_TestAccountGuid, guids).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            await RunTest("Get Historical Balance", async () =>
            {
                Balance balance = await _Client.Balance.GetAsOfAsync(_TestAccountGuid, DateTime.UtcNow).ConfigureAwait(false);
                // Just verify no error
            }).ConfigureAwait(false);

            Console.WriteLine();
        }

        private static async Task RunApiKeyTests()
        {
            PrintSectionHeader("API KEY TESTS");

            ApiKeyInfo? createdKey = null;

            await RunTest("Create API Key", async () =>
            {
                createdKey = await _Client.ApiKey.CreateAsync("Test SDK Key", isAdmin: false).ConfigureAwait(false);
                if (createdKey.GUID == Guid.Empty) throw new Exception("API key GUID is empty");
                if (string.IsNullOrEmpty(createdKey.Key)) throw new Exception("API key value should be returned on creation");
            }).ConfigureAwait(false);

            await RunTest("Enumerate API Keys", async () =>
            {
                EnumerationResult<ApiKeyInfo> result = await _Client.ApiKey.EnumerateAsync(new ApiKeyEnumerationQuery
                {
                    MaxResults = 10
                }).ConfigureAwait(false);
                if (result.Objects == null || result.Objects.Count == 0) throw new Exception("Should have at least one API key");
            }).ConfigureAwait(false);

            await RunTest("Revoke API Key", async () =>
            {
                if (createdKey == null) throw new Exception("No API key to revoke");
                await _Client.ApiKey.RevokeAsync(createdKey.GUID).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Console.WriteLine();
        }

        private static async Task CleanupTests()
        {
            PrintSectionHeader("CLEANUP");

            if (_TestAccountGuid != Guid.Empty)
            {
                await RunTest("Delete Test Account", async () =>
                {
                    await _Client.Account.DeleteAsync(_TestAccountGuid).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            if (_EnumTestAccountGuid != Guid.Empty)
            {
                await RunTest("Delete Enumeration Test Account", async () =>
                {
                    await _Client.Account.DeleteAsync(_EnumTestAccountGuid).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            Console.WriteLine();
        }

        #endregion

        #region Helper-Methods

        private static void PrintSectionHeader(string title)
        {
            Console.WriteLine($"[{title}]");
        }

        private static async Task RunTest(string name, Func<Task> test)
        {
            Stopwatch sw = Stopwatch.StartNew();
            bool success = false;
            string? errorMessage = null;

            try
            {
                await test().ConfigureAwait(false);
                success = true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
            finally
            {
                sw.Stop();
            }

            TestResult result = new TestResult
            {
                Name = name,
                Success = success,
                ElapsedMs = sw.ElapsedMilliseconds,
                ErrorMessage = errorMessage
            };
            _Results.Add(result);

            string status = success ? "PASS" : "FAIL";
            string color = success ? "\u001b[32m" : "\u001b[31m";
            string reset = "\u001b[0m";

            Console.WriteLine($"  [{color}{status}{reset}] {name} ({sw.ElapsedMilliseconds}ms)");
            if (!success && !string.IsNullOrEmpty(errorMessage))
            {
                Console.WriteLine($"         Error: {errorMessage}");
            }
        }

        private static void PrintSummary()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("TEST SUMMARY");
            Console.WriteLine("========================================");
            Console.WriteLine();

            int passCount = _Results.Count(r => r.Success);
            int failCount = _Results.Count(r => !r.Success);

            string overallStatus = failCount == 0 ? "PASS" : "FAIL";
            string color = failCount == 0 ? "\u001b[32m" : "\u001b[31m";
            string reset = "\u001b[0m";

            Console.WriteLine($"Total Tests: {_Results.Count}");
            Console.WriteLine($"Passed:      {passCount}");
            Console.WriteLine($"Failed:      {failCount}");
            Console.WriteLine();
            Console.WriteLine($"Overall:     [{color}{overallStatus}{reset}]");
            Console.WriteLine($"Total Time:  {_TotalStopwatch.ElapsedMilliseconds}ms ({_TotalStopwatch.Elapsed.TotalSeconds:F2}s)");
            Console.WriteLine();

            if (failCount > 0)
            {
                Console.WriteLine("Failed Tests:");
                foreach (TestResult result in _Results.Where(r => !r.Success))
                {
                    Console.WriteLine($"  - {result.Name}: {result.ErrorMessage}");
                }
                Console.WriteLine();
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents the result of a single test.
    /// </summary>
    internal class TestResult
    {
        public string Name { get; set; } = string.Empty;
        public bool Success { get; set; }
        public long ElapsedMs { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
