import { Entry, EntryInput, EntryEnumerationQuery, EnumerationResult } from '../models';
import { HttpClient } from '../http-client';
/**
 * Entry operations.
 */
export declare class EntryMethods {
    private readonly client;
    constructor(client: HttpClient);
    /**
     * Add a credit entry.
     * @param accountId The account identifier.
     * @param amount The credit amount (must be positive).
     * @param notes Optional notes.
     * @returns The identifier of the created entry.
     */
    addCredit(accountId: string, amount: number, notes?: string): Promise<string>;
    /**
     * Add multiple credit entries.
     * @param accountId The account identifier.
     * @param entries The credit entries to add.
     * @returns The identifiers of the created entries.
     */
    addCredits(accountId: string, entries: EntryInput[]): Promise<string[]>;
    /**
     * Add a debit entry.
     * @param accountId The account identifier.
     * @param amount The debit amount (must be positive).
     * @param notes Optional notes.
     * @returns The identifier of the created entry.
     */
    addDebit(accountId: string, amount: number, notes?: string): Promise<string>;
    /**
     * Add multiple debit entries.
     * @param accountId The account identifier.
     * @param entries The debit entries to add.
     * @returns The identifiers of the created entries.
     */
    addDebits(accountId: string, entries: EntryInput[]): Promise<string[]>;
    /**
     * Get all entries for an account.
     * @param accountId The account identifier.
     * @returns All entries.
     */
    getAll(accountId: string): Promise<Entry[]>;
    /**
     * Enumerate entries with filtering and pagination.
     * @param accountId The account identifier.
     * @param query Query parameters.
     * @returns Enumeration result.
     */
    enumerate(accountId: string, query?: EntryEnumerationQuery): Promise<EnumerationResult<Entry>>;
    /**
     * Get all pending (uncommitted) entries.
     * @param accountId The account identifier.
     * @returns Pending entries.
     */
    getPending(accountId: string): Promise<Entry[]>;
    /**
     * Get pending credit entries.
     * @param accountId The account identifier.
     * @returns Pending credits.
     */
    getPendingCredits(accountId: string): Promise<Entry[]>;
    /**
     * Get pending debit entries.
     * @param accountId The account identifier.
     * @returns Pending debits.
     */
    getPendingDebits(accountId: string): Promise<Entry[]>;
    /**
     * Cancel (delete) a pending entry.
     * @param accountId The account identifier.
     * @param entryId The entry identifier.
     */
    cancel(accountId: string, entryId: string): Promise<void>;
}
//# sourceMappingURL=entry.d.ts.map