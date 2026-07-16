import { Entry, EntryInput, EntryEnumerationQuery, EnumerationResult, AddEntriesResponse } from '../models';
import { HttpClient } from '../http-client';
import { NetLedgerValidationError } from '../errors';

/**
 * Entry operations.
 */
export class EntryMethods {
    private readonly client: HttpClient;

    constructor(client: HttpClient) {
        this.client = client;
    }

    /**
     * Add a credit entry.
     * @param accountId The account identifier.
     * @param amount The credit amount (must be positive).
     * @param notes Optional notes.
     * @returns The identifier of the created entry.
     */
    async addCredit(accountId: string, amount: number, notes?: string): Promise<string> {
        if (amount <= 0) {
            throw new NetLedgerValidationError('Amount must be greater than zero', 'amount');
        }
        const response = await this.client.put<AddEntriesResponse>(
            `/v1/accounts/${accountId}/credits`,
            { Amount: amount, Notes: notes }
        );
        if (!response.Data || !response.Data.EntryIds || response.Data.EntryIds.length === 0) {
            throw new Error('No data returned from server');
        }
        return response.Data.EntryIds[0];
    }

    /**
     * Add multiple credit entries.
     * @param accountId The account identifier.
     * @param entries The credit entries to add.
     * @returns The identifiers of the created entries.
     */
    async addCredits(accountId: string, entries: EntryInput[]): Promise<string[]> {
        if (!entries || entries.length === 0) {
            throw new NetLedgerValidationError('Entries array cannot be empty', 'entries');
        }
        for (const entry of entries) {
            if (entry.Amount <= 0) {
                throw new NetLedgerValidationError('All amounts must be greater than zero', 'entries');
            }
        }
        const response = await this.client.put<AddEntriesResponse>(
            `/v1/accounts/${accountId}/credits`,
            { Entries: entries }
        );
        return response.Data?.EntryIds || [];
    }

    /**
     * Add a debit entry.
     * @param accountId The account identifier.
     * @param amount The debit amount (must be positive).
     * @param notes Optional notes.
     * @returns The identifier of the created entry.
     */
    async addDebit(accountId: string, amount: number, notes?: string): Promise<string> {
        if (amount <= 0) {
            throw new NetLedgerValidationError('Amount must be greater than zero', 'amount');
        }
        const response = await this.client.put<AddEntriesResponse>(
            `/v1/accounts/${accountId}/debits`,
            { Amount: amount, Notes: notes }
        );
        if (!response.Data || !response.Data.EntryIds || response.Data.EntryIds.length === 0) {
            throw new Error('No data returned from server');
        }
        return response.Data.EntryIds[0];
    }

    /**
     * Add multiple debit entries.
     * @param accountId The account identifier.
     * @param entries The debit entries to add.
     * @returns The identifiers of the created entries.
     */
    async addDebits(accountId: string, entries: EntryInput[]): Promise<string[]> {
        if (!entries || entries.length === 0) {
            throw new NetLedgerValidationError('Entries array cannot be empty', 'entries');
        }
        for (const entry of entries) {
            if (entry.Amount <= 0) {
                throw new NetLedgerValidationError('All amounts must be greater than zero', 'entries');
            }
        }
        const response = await this.client.put<AddEntriesResponse>(
            `/v1/accounts/${accountId}/debits`,
            { Entries: entries }
        );
        return response.Data?.EntryIds || [];
    }

    /**
     * Get all entries for an account.
     * @param accountId The account identifier.
     * @returns All entries.
     */
    async getAll(accountId: string): Promise<Entry[]> {
        const response = await this.client.get<Entry[]>(
            `/v1/accounts/${accountId}/entries`
        );
        return response.Data || [];
    }

    /**
     * Enumerate entries with filtering and pagination.
     * @param accountId The account identifier.
     * @param query Query parameters.
     * @returns Enumeration result.
     */
    async enumerate(accountId: string, query?: EntryEnumerationQuery): Promise<EnumerationResult<Entry>> {
        const response = await this.client.post<EnumerationResult<Entry>>(
            `/v1/accounts/${accountId}/entries/enumerate`,
            query || {}
        );
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }

    /**
     * Get all pending (uncommitted) entries.
     * @param accountId The account identifier.
     * @returns Pending entries.
     */
    async getPending(accountId: string): Promise<Entry[]> {
        const response = await this.client.get<Entry[]>(
            `/v1/accounts/${accountId}/entries/pending`
        );
        return response.Data || [];
    }

    /**
     * Get pending credit entries.
     * @param accountId The account identifier.
     * @returns Pending credits.
     */
    async getPendingCredits(accountId: string): Promise<Entry[]> {
        const response = await this.client.get<Entry[]>(
            `/v1/accounts/${accountId}/entries/pending/credits`
        );
        return response.Data || [];
    }

    /**
     * Get pending debit entries.
     * @param accountId The account identifier.
     * @returns Pending debits.
     */
    async getPendingDebits(accountId: string): Promise<Entry[]> {
        const response = await this.client.get<Entry[]>(
            `/v1/accounts/${accountId}/entries/pending/debits`
        );
        return response.Data || [];
    }

    /**
     * Cancel (delete) a pending entry.
     * @param accountId The account identifier.
     * @param entryId The entry identifier.
     */
    async cancel(accountId: string, entryId: string): Promise<void> {
        await this.client.delete(`/v1/accounts/${accountId}/entries/${entryId}`);
    }
}
