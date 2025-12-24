import { NetLedgerClient, EntryType, EnumerationOrder, Entry } from '../index';

interface TestResult {
    name: string;
    success: boolean;
    elapsedMs: number;
    errorMessage?: string;
}

class TestHarness {
    private client: NetLedgerClient;
    private results: TestResult[] = [];
    private totalStartTime: number = 0;
    private testAccountGuid: string = '';
    private testEntryGuids: string[] = [];
    private enumerationTestAccountGuid: string = '';

    constructor(endpoint: string, apiKey: string) {
        this.client = new NetLedgerClient(endpoint, apiKey);
    }

    async run(): Promise<number> {
        console.log('NetLedger SDK Test Harness (JavaScript/TypeScript)');
        console.log('==================================================');
        console.log();
        console.log(`Endpoint: ${this.client.baseUrl}`);
        console.log();

        this.totalStartTime = Date.now();

        try {
            await this.runServiceTests();
            await this.runAccountTests();
            await this.runEntryTests();
            await this.runBalanceTests();
            await this.runEnumerationTests();
            await this.runApiKeyTests();
            await this.runCleanupTests();
        } catch (err) {
            console.error('Fatal error:', err);
        }

        this.printSummary();

        const failedCount = this.results.filter(r => !r.success).length;
        return failedCount > 0 ? 1 : 0;
    }

    private async runServiceTests(): Promise<void> {
        this.printSectionHeader('SERVICE TESTS');

        await this.runTest('Health Check', async () => {
            const healthy = await this.client.service.healthCheck();
            if (!healthy) throw new Error('Health check returned false');
        });

        await this.runTest('Get Service Info', async () => {
            const info = await this.client.service.getInfo();
            if (!info.Name) throw new Error('Service name is empty');
        });

        console.log();
    }

    private async runAccountTests(): Promise<void> {
        this.printSectionHeader('ACCOUNT TESTS');

        const testAccountName = `TestAccount_${Date.now()}`;

        await this.runTest('Create Account', async () => {
            const account = await this.client.account.create(testAccountName, 'Test notes');
            if (!account.GUID) throw new Error('Account GUID is empty');
            if (account.Name !== testAccountName) throw new Error('Account name mismatch');
            this.testAccountGuid = account.GUID;
        });

        await this.runTest('Check Account Exists', async () => {
            if (!this.testAccountGuid) throw new Error('No account to check');
            const exists = await this.client.account.exists(this.testAccountGuid);
            if (!exists) throw new Error('Account should exist');
        });

        await this.runTest('Get Account by GUID', async () => {
            if (!this.testAccountGuid) throw new Error('No account to get');
            const account = await this.client.account.get(this.testAccountGuid);
            if (account.GUID !== this.testAccountGuid) throw new Error('Account GUID mismatch');
        });

        await this.runTest('Get Account by Name', async () => {
            if (!this.testAccountGuid) throw new Error('No account to get');
            const account = await this.client.account.getByName(testAccountName);
            if (account.GUID !== this.testAccountGuid) throw new Error('Account GUID mismatch');
        });

        await this.runTest('Enumerate Accounts', async () => {
            const result = await this.client.account.enumerate({ MaxResults: 10 });
            if (!result.Objects || result.Objects.length === 0) throw new Error('No accounts returned');
        });

        await this.runTest('Enumerate Accounts with Search', async () => {
            const result = await this.client.account.enumerate({
                MaxResults: 10,
                SearchTerm: testAccountName
            });
            if (!result.Objects || result.Objects.length === 0) throw new Error('Search should find the test account');
        });

        console.log();
    }

    private async runEntryTests(): Promise<void> {
        this.printSectionHeader('ENTRY TESTS');

        if (!this.testAccountGuid) {
            console.log('  SKIPPED: No test account available');
            console.log();
            return;
        }

        await this.runTest('Add Single Credit', async () => {
            const entryGuid = await this.client.entry.addCredit(this.testAccountGuid, 100.00, 'Test credit');
            if (!entryGuid) throw new Error('Entry GUID is empty');
            this.testEntryGuids.push(entryGuid);
        });

        await this.runTest('Add Single Debit', async () => {
            const entryGuid = await this.client.entry.addDebit(this.testAccountGuid, 25.50, 'Test debit');
            if (!entryGuid) throw new Error('Entry GUID is empty');
            this.testEntryGuids.push(entryGuid);
        });

        await this.runTest('Add Multiple Credits (Batch)', async () => {
            const entryGuids = await this.client.entry.addCredits(this.testAccountGuid, [
                { Amount: 10.00, Notes: 'Batch credit 1' },
                { Amount: 20.00, Notes: 'Batch credit 2' },
                { Amount: 30.00, Notes: 'Batch credit 3' }
            ]);
            if (entryGuids.length !== 3) throw new Error(`Expected 3 entries, got ${entryGuids.length}`);
            entryGuids.forEach(guid => this.testEntryGuids.push(guid));
        });

        await this.runTest('Add Multiple Debits (Batch)', async () => {
            const entryGuids = await this.client.entry.addDebits(this.testAccountGuid, [
                { Amount: 5.00, Notes: 'Batch debit 1' },
                { Amount: 7.50, Notes: 'Batch debit 2' }
            ]);
            if (entryGuids.length !== 2) throw new Error(`Expected 2 entries, got ${entryGuids.length}`);
            entryGuids.forEach(guid => this.testEntryGuids.push(guid));
        });

        await this.runTest('Get All Entries', async () => {
            const entries = await this.client.entry.getAll(this.testAccountGuid);
            if (entries.length < this.testEntryGuids.length) {
                throw new Error(`Expected at least ${this.testEntryGuids.length} entries`);
            }
        });

        await this.runTest('Get Pending Entries', async () => {
            const entries = await this.client.entry.getPending(this.testAccountGuid);
            if (entries.length === 0) throw new Error('Should have pending entries');
        });

        await this.runTest('Get Pending Credits', async () => {
            const entries = await this.client.entry.getPendingCredits(this.testAccountGuid);
            if (entries.length === 0) throw new Error('Should have pending credits');
        });

        await this.runTest('Get Pending Debits', async () => {
            const entries = await this.client.entry.getPendingDebits(this.testAccountGuid);
            if (entries.length === 0) throw new Error('Should have pending debits');
        });

        await this.runTest('Enumerate Entries', async () => {
            const result = await this.client.entry.enumerate(this.testAccountGuid, {
                MaxResults: 50,
                Ordering: EnumerationOrder.CreatedDescending
            });
            if (!result.Objects || result.Objects.length === 0) throw new Error('No entries returned');
        });

        await this.runTest('Enumerate Entries with Amount Filter', async () => {
            await this.client.entry.enumerate(this.testAccountGuid, {
                MaxResults: 50,
                AmountMinimum: 10.00,
                AmountMaximum: 50.00
            });
            // Just verify no error
        });

        let entryToCancel: string | undefined;
        await this.runTest('Add Entry for Cancellation', async () => {
            entryToCancel = await this.client.entry.addCredit(this.testAccountGuid, 1.00, 'Entry to cancel');
        });

        await this.runTest('Cancel Entry', async () => {
            if (!entryToCancel) throw new Error('No entry to cancel');
            await this.client.entry.cancel(this.testAccountGuid, entryToCancel);
        });

        console.log();
    }

    private async runBalanceTests(): Promise<void> {
        this.printSectionHeader('BALANCE TESTS');

        if (!this.testAccountGuid) {
            console.log('  SKIPPED: No test account available');
            console.log();
            return;
        }

        await this.runTest('Get Balance', async () => {
            await this.client.balance.get(this.testAccountGuid);
        });

        await this.runTest('Get All Balances', async () => {
            const balances = await this.client.balance.getAll();
            if (balances.length === 0) throw new Error('Should have at least one balance');
        });

        await this.runTest('Commit All Pending Entries', async () => {
            await this.client.balance.commit(this.testAccountGuid);
        });

        await this.runTest('Get Balance After Commit', async () => {
            await this.client.balance.get(this.testAccountGuid);
        });

        await this.runTest('Verify Balance Chain', async () => {
            const valid = await this.client.balance.verify(this.testAccountGuid);
            if (!valid) throw new Error('Balance chain should be valid');
        });

        await this.runTest('Add Entries for Selective Commit', async () => {
            await this.client.entry.addCredit(this.testAccountGuid, 50.00, 'Selective commit test');
            await this.client.entry.addCredit(this.testAccountGuid, 75.00, 'Selective commit test 2');
        });

        await this.runTest('Commit Specific Entries', async () => {
            const pending = await this.client.entry.getPending(this.testAccountGuid);
            if (pending.length > 0) {
                await this.client.balance.commit(this.testAccountGuid, [pending[0].GUID]);
            }
        });

        await this.runTest('Get Historical Balance', async () => {
            await this.client.balance.getAsOf(this.testAccountGuid, new Date());
        });

        console.log();
    }

    private async runEnumerationTests(): Promise<void> {
        this.printSectionHeader('ENUMERATION TESTS');

        // Create a dedicated account for enumeration tests with many entries
        const enumerationAccountName = `EnumerationTest_${Date.now()}`;

        await this.runTest('Create Enumeration Test Account', async () => {
            const account = await this.client.account.create(enumerationAccountName, 'Account for enumeration tests');
            if (!account.GUID) throw new Error('Account GUID is empty');
            this.enumerationTestAccountGuid = account.GUID;
        });

        if (!this.enumerationTestAccountGuid) {
            console.log('  SKIPPED: No enumeration test account available');
            console.log();
            return;
        }

        // Create 15 entries with varying amounts for comprehensive pagination and ordering tests
        // Using distinct amounts: 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150
        await this.runTest('Create Multiple Entries for Pagination', async () => {
            const amounts = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150];
            for (let i = 0; i < amounts.length; i++) {
                const amount = amounts[i];
                // Alternate between credits and debits
                if (i % 2 === 0) {
                    await this.client.entry.addCredit(this.enumerationTestAccountGuid, amount, `Enum test credit ${amount}`);
                } else {
                    await this.client.entry.addDebit(this.enumerationTestAccountGuid, amount, `Enum test debit ${amount}`);
                }
                // Small delay to ensure distinct creation timestamps
                await new Promise(resolve => setTimeout(resolve, 50));
            }
        });

        // Test 1: Basic enumeration with result field validation
        await this.runTest('Enumerate Entries - Validate TotalRecords', async () => {
            const result = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 100
            });
            if (result.TotalRecords !== 15) {
                throw new Error(`Expected TotalRecords=15, got ${result.TotalRecords}`);
            }
            if (!result.Objects || result.Objects.length !== 15) {
                throw new Error(`Expected 15 entries, got ${result.Objects?.length ?? 0}`);
            }
        });

        // Test 2: Pagination - First page with small MaxResults
        await this.runTest('Enumerate Entries - First Page (MaxResults=5)', async () => {
            const result = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 5
            });
            if (result.TotalRecords !== 15) {
                throw new Error(`Expected TotalRecords=15, got ${result.TotalRecords}`);
            }
            if (!result.Objects || result.Objects.length !== 5) {
                throw new Error(`Expected 5 entries on first page, got ${result.Objects?.length ?? 0}`);
            }
            if (result.EndOfResults) {
                throw new Error('EndOfResults should be false on first page');
            }
            if (result.RecordsRemaining !== 10) {
                throw new Error(`Expected RecordsRemaining=10, got ${result.RecordsRemaining}`);
            }
            if (!result.ContinuationToken) {
                throw new Error('ContinuationToken should be present for pagination');
            }
        });

        // Test 3: Pagination - Second page using continuation token
        let firstPageToken: string | undefined;
        await this.runTest('Enumerate Entries - Second Page with ContinuationToken', async () => {
            // Get first page
            const firstPage = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 5
            });
            firstPageToken = firstPage.ContinuationToken;
            if (!firstPageToken) throw new Error('No continuation token from first page');

            // Get second page
            const secondPage = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 5,
                ContinuationToken: firstPageToken
            });
            if (!secondPage.Objects || secondPage.Objects.length !== 5) {
                throw new Error(`Expected 5 entries on second page, got ${secondPage.Objects?.length ?? 0}`);
            }
            if (secondPage.EndOfResults) {
                throw new Error('EndOfResults should be false on second page');
            }
            if (secondPage.RecordsRemaining !== 5) {
                throw new Error(`Expected RecordsRemaining=5, got ${secondPage.RecordsRemaining}`);
            }
            if (!secondPage.ContinuationToken) {
                throw new Error('ContinuationToken should be present for third page');
            }
        });

        // Test 4: Pagination - Last page validation
        await this.runTest('Enumerate Entries - Last Page EndOfResults=true', async () => {
            // Get all pages until end
            let continuationToken: string | undefined;
            let pageCount = 0;
            let lastPage;

            do {
                const page = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                    MaxResults: 5,
                    ContinuationToken: continuationToken
                });
                continuationToken = page.ContinuationToken;
                lastPage = page;
                pageCount++;

                if (pageCount > 10) throw new Error('Too many pages - possible infinite loop');
            } while (!lastPage.EndOfResults);

            if (pageCount !== 3) {
                throw new Error(`Expected 3 pages (15 entries / 5 per page), got ${pageCount}`);
            }
            if (!lastPage.EndOfResults) {
                throw new Error('EndOfResults should be true on last page');
            }
            if (lastPage.RecordsRemaining !== 0) {
                throw new Error(`Expected RecordsRemaining=0 on last page, got ${lastPage.RecordsRemaining}`);
            }
            if (!lastPage.Objects || lastPage.Objects.length !== 5) {
                throw new Error(`Expected 5 entries on last page, got ${lastPage.Objects?.length ?? 0}`);
            }
        });

        // Test 5: Complete pagination - Verify no duplicates across pages
        await this.runTest('Enumerate Entries - No Duplicates Across Pages', async () => {
            const allGuids: Set<string> = new Set();
            let continuationToken: string | undefined;
            let totalEntriesCollected = 0;

            do {
                const page = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                    MaxResults: 4,
                    ContinuationToken: continuationToken
                });

                if (page.Objects) {
                    for (const entry of page.Objects) {
                        if (allGuids.has(entry.GUID)) {
                            throw new Error(`Duplicate entry GUID found: ${entry.GUID}`);
                        }
                        allGuids.add(entry.GUID);
                        totalEntriesCollected++;
                    }
                }

                continuationToken = page.ContinuationToken;
            } while (continuationToken);

            if (totalEntriesCollected !== 15) {
                throw new Error(`Expected 15 total entries across all pages, got ${totalEntriesCollected}`);
            }
        });

        // Test 6: Ordering - CreatedAscending
        await this.runTest('Enumerate Entries - Order CreatedAscending', async () => {
            const result = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 15,
                Ordering: EnumerationOrder.CreatedAscending
            });
            if (!result.Objects || result.Objects.length === 0) {
                throw new Error('No entries returned');
            }
            // Verify ascending order by creation date
            for (let i = 1; i < result.Objects.length; i++) {
                const prevDate = new Date(result.Objects[i - 1].CreatedUtc).getTime();
                const currDate = new Date(result.Objects[i].CreatedUtc).getTime();
                if (currDate < prevDate) {
                    throw new Error(`Entries not in CreatedAscending order at index ${i}`);
                }
            }
        });

        // Test 7: Ordering - CreatedDescending
        await this.runTest('Enumerate Entries - Order CreatedDescending', async () => {
            const result = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 15,
                Ordering: EnumerationOrder.CreatedDescending
            });
            if (!result.Objects || result.Objects.length === 0) {
                throw new Error('No entries returned');
            }
            // Verify descending order by creation date
            for (let i = 1; i < result.Objects.length; i++) {
                const prevDate = new Date(result.Objects[i - 1].CreatedUtc).getTime();
                const currDate = new Date(result.Objects[i].CreatedUtc).getTime();
                if (currDate > prevDate) {
                    throw new Error(`Entries not in CreatedDescending order at index ${i}`);
                }
            }
        });

        // Test 8: Ordering - AmountAscending
        await this.runTest('Enumerate Entries - Order AmountAscending', async () => {
            const result = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 15,
                Ordering: EnumerationOrder.AmountAscending
            });
            if (!result.Objects || result.Objects.length === 0) {
                throw new Error('No entries returned');
            }
            // Verify ascending order by amount
            for (let i = 1; i < result.Objects.length; i++) {
                const prevAmount = result.Objects[i - 1].Amount;
                const currAmount = result.Objects[i].Amount;
                if (currAmount < prevAmount) {
                    throw new Error(`Entries not in AmountAscending order at index ${i}: ${prevAmount} > ${currAmount}`);
                }
            }
            // Verify smallest amount is first (should be 10)
            if (result.Objects[0].Amount !== 10) {
                throw new Error(`Expected first entry amount=10, got ${result.Objects[0].Amount}`);
            }
        });

        // Test 9: Ordering - AmountDescending
        await this.runTest('Enumerate Entries - Order AmountDescending', async () => {
            const result = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 15,
                Ordering: EnumerationOrder.AmountDescending
            });
            if (!result.Objects || result.Objects.length === 0) {
                throw new Error('No entries returned');
            }
            // Verify descending order by amount
            for (let i = 1; i < result.Objects.length; i++) {
                const prevAmount = result.Objects[i - 1].Amount;
                const currAmount = result.Objects[i].Amount;
                if (currAmount > prevAmount) {
                    throw new Error(`Entries not in AmountDescending order at index ${i}: ${prevAmount} < ${currAmount}`);
                }
            }
            // Verify largest amount is first (should be 150)
            if (result.Objects[0].Amount !== 150) {
                throw new Error(`Expected first entry amount=150, got ${result.Objects[0].Amount}`);
            }
        });

        // Test 10: Ordering persists across pagination
        await this.runTest('Enumerate Entries - Ordering Persists Across Pages', async () => {
            const allAmounts: number[] = [];
            let continuationToken: string | undefined;

            do {
                const page = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                    MaxResults: 5,
                    Ordering: EnumerationOrder.AmountAscending,
                    ContinuationToken: continuationToken
                });

                if (page.Objects) {
                    for (const entry of page.Objects) {
                        allAmounts.push(entry.Amount);
                    }
                }

                continuationToken = page.ContinuationToken;
            } while (continuationToken);

            // Verify all amounts are in ascending order across all pages
            for (let i = 1; i < allAmounts.length; i++) {
                if (allAmounts[i] < allAmounts[i - 1]) {
                    throw new Error(`Ordering not maintained across pages at index ${i}: ${allAmounts[i - 1]} > ${allAmounts[i]}`);
                }
            }
        });

        // Test 11: Account enumeration with Skip parameter
        await this.runTest('Enumerate Accounts - Skip Parameter', async () => {
            // First get total count
            const allAccounts = await this.client.account.enumerate({ MaxResults: 100 });
            const totalAccounts = allAccounts.TotalRecords;

            if (totalAccounts < 2) {
                throw new Error('Need at least 2 accounts to test Skip parameter');
            }

            // Get first account
            const firstPage = await this.client.account.enumerate({ MaxResults: 1 });
            const firstAccountGuid = firstPage.Objects?.[0]?.GUID;

            // Skip first account
            const skippedPage = await this.client.account.enumerate({ MaxResults: 1, Skip: 1 });
            const skippedAccountGuid = skippedPage.Objects?.[0]?.GUID;

            if (firstAccountGuid === skippedAccountGuid) {
                throw new Error('Skip parameter did not skip the first account');
            }

            // Verify TotalRecords remains same with Skip
            if (skippedPage.TotalRecords !== totalAccounts) {
                throw new Error(`TotalRecords should remain ${totalAccounts} with Skip, got ${skippedPage.TotalRecords}`);
            }
        });

        // Test 12: Account enumeration pagination validation
        await this.runTest('Enumerate Accounts - Pagination Fields', async () => {
            const result = await this.client.account.enumerate({ MaxResults: 2 });

            // Validate required fields exist
            if (typeof result.TotalRecords !== 'number') {
                throw new Error('TotalRecords field missing or not a number');
            }
            if (typeof result.RecordsRemaining !== 'number') {
                throw new Error('RecordsRemaining field missing or not a number');
            }
            if (typeof result.EndOfResults !== 'boolean') {
                throw new Error('EndOfResults field missing or not a boolean');
            }

            // Validate logical consistency
            if (result.Objects) {
                const recordsReturned = result.Objects.length;
                const expectedRemaining = Math.max(0, result.TotalRecords - recordsReturned);

                if (result.EndOfResults && result.RecordsRemaining !== 0) {
                    throw new Error('EndOfResults=true but RecordsRemaining is not 0');
                }
                if (!result.EndOfResults && result.RecordsRemaining === 0) {
                    throw new Error('EndOfResults=false but RecordsRemaining is 0');
                }
            }
        });

        // Test 13: Entry enumeration with amount filter combined with ordering
        await this.runTest('Enumerate Entries - Amount Filter with Ordering', async () => {
            // Filter for amounts between 50 and 100 inclusive, ordered by amount descending
            const result = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 15,
                AmountMinimum: 50,
                AmountMaximum: 100,
                Ordering: EnumerationOrder.AmountDescending
            });

            if (!result.Objects || result.Objects.length === 0) {
                throw new Error('No entries returned for amount filter');
            }

            // Verify all entries are within range
            for (const entry of result.Objects) {
                if (entry.Amount < 50 || entry.Amount > 100) {
                    throw new Error(`Entry amount ${entry.Amount} outside filter range [50, 100]`);
                }
            }

            // Verify descending order
            for (let i = 1; i < result.Objects.length; i++) {
                if (result.Objects[i].Amount > result.Objects[i - 1].Amount) {
                    throw new Error('Entries not in AmountDescending order within filter');
                }
            }

            // Should have entries: 100, 90, 80, 70, 60, 50 (6 entries)
            if (result.Objects.length !== 6) {
                throw new Error(`Expected 6 entries in range [50, 100], got ${result.Objects.length}`);
            }
        });

        // Test 14: MaxResults boundary - requesting more than available
        await this.runTest('Enumerate Entries - MaxResults Exceeds Available', async () => {
            const result = await this.client.entry.enumerate(this.enumerationTestAccountGuid, {
                MaxResults: 1000  // Much more than the 15 we have
            });

            if (result.Objects?.length !== 15) {
                throw new Error(`Expected 15 entries (all available), got ${result.Objects?.length ?? 0}`);
            }
            if (!result.EndOfResults) {
                throw new Error('EndOfResults should be true when all entries returned');
            }
            if (result.RecordsRemaining !== 0) {
                throw new Error(`RecordsRemaining should be 0, got ${result.RecordsRemaining}`);
            }
        });

        // Cleanup: Delete the enumeration test account
        await this.runTest('Delete Enumeration Test Account', async () => {
            await this.client.account.delete(this.enumerationTestAccountGuid);
            const exists = await this.client.account.exists(this.enumerationTestAccountGuid);
            if (exists) throw new Error('Account should have been deleted');
        });

        console.log();
    }

    private async runApiKeyTests(): Promise<void> {
        this.printSectionHeader('API KEY TESTS');

        let createdKeyGuid: string | undefined;

        await this.runTest('Create API Key', async () => {
            const key = await this.client.apiKey.create('Test SDK Key', false);
            if (!key.GUID) throw new Error('API key GUID is empty');
            if (!key.Key) throw new Error('API key value should be returned on creation');
            createdKeyGuid = key.GUID;
        });

        await this.runTest('Enumerate API Keys', async () => {
            const result = await this.client.apiKey.enumerate({ MaxResults: 10 });
            if (!result.Objects || result.Objects.length === 0) throw new Error('Should have at least one API key');
        });

        await this.runTest('Revoke API Key', async () => {
            if (!createdKeyGuid) throw new Error('No API key to revoke');
            await this.client.apiKey.revoke(createdKeyGuid);
        });

        console.log();
    }

    private async runCleanupTests(): Promise<void> {
        this.printSectionHeader('CLEANUP');

        if (this.testAccountGuid) {
            await this.runTest('Delete Test Account', async () => {
                await this.client.account.delete(this.testAccountGuid);
                const exists = await this.client.account.exists(this.testAccountGuid);
                if (exists) throw new Error('Account should have been deleted');
            });
        }

        console.log();
    }

    private printSectionHeader(title: string): void {
        console.log(`[${title}]`);
    }

    private async runTest(name: string, test: () => Promise<void>): Promise<void> {
        const startTime = Date.now();
        let success = false;
        let errorMessage: string | undefined;

        try {
            await test();
            success = true;
        } catch (err) {
            errorMessage = err instanceof Error ? err.message : String(err);
        }

        const elapsedMs = Date.now() - startTime;

        this.results.push({
            name,
            success,
            elapsedMs,
            errorMessage
        });

        const status = success ? 'PASS' : 'FAIL';
        const color = success ? '\x1b[32m' : '\x1b[31m';
        const reset = '\x1b[0m';

        console.log(`  [${color}${status}${reset}] ${name} (${elapsedMs}ms)`);
        if (!success && errorMessage) {
            console.log(`         Error: ${errorMessage}`);
        }
    }

    private printSummary(): void {
        const totalTime = Date.now() - this.totalStartTime;

        console.log('========================================');
        console.log('TEST SUMMARY');
        console.log('========================================');
        console.log();

        const passCount = this.results.filter(r => r.success).length;
        const failCount = this.results.filter(r => !r.success).length;

        const overallStatus = failCount === 0 ? 'PASS' : 'FAIL';
        const color = failCount === 0 ? '\x1b[32m' : '\x1b[31m';
        const reset = '\x1b[0m';

        console.log(`Total Tests: ${this.results.length}`);
        console.log(`Passed:      ${passCount}`);
        console.log(`Failed:      ${failCount}`);
        console.log();
        console.log(`Overall:     [${color}${overallStatus}${reset}]`);
        console.log(`Total Time:  ${totalTime}ms (${(totalTime / 1000).toFixed(2)}s)`);
        console.log();

        if (failCount > 0) {
            console.log('Failed Tests:');
            for (const result of this.results.filter(r => !r.success)) {
                console.log(`  - ${result.name}: ${result.errorMessage}`);
            }
            console.log();
        }
    }
}

// Main entry point
async function main(): Promise<void> {
    const args = process.argv.slice(2);

    if (args.length < 2) {
        console.log('Usage: node test-harness.js <endpoint> <api-key>');
        console.log();
        console.log('Example: node test-harness.js http://localhost:8080 your-api-key-here');
        process.exit(1);
    }

    const endpoint = args[0];
    const apiKey = args[1];

    console.log(`API Key: ${apiKey.substring(0, Math.min(8, apiKey.length))}...`);

    const harness = new TestHarness(endpoint, apiKey);
    const exitCode = await harness.run();
    process.exit(exitCode);
}

main().catch(err => {
    console.error('Fatal error:', err);
    process.exit(1);
});
