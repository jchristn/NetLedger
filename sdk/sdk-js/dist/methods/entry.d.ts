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
     * @param accountGuid The account GUID.
     * @param amount The credit amount (must be positive).
     * @param notes Optional notes.
     * @returns The GUID of the created entry.
     */
    addCredit(accountGuid: string, amount: number, notes?: string): Promise<string>;
    /**
     * Add multiple credit entries.
     * @param accountGuid The account GUID.
     * @param entries The credit entries to add.
     * @returns The GUIDs of the created entries.
     */
    addCredits(accountGuid: string, entries: EntryInput[]): Promise<string[]>;
    /**
     * Add a debit entry.
     * @param accountGuid The account GUID.
     * @param amount The debit amount (must be positive).
     * @param notes Optional notes.
     * @returns The GUID of the created entry.
     */
    addDebit(accountGuid: string, amount: number, notes?: string): Promise<string>;
    /**
     * Add multiple debit entries.
     * @param accountGuid The account GUID.
     * @param entries The debit entries to add.
     * @returns The GUIDs of the created entries.
     */
    addDebits(accountGuid: string, entries: EntryInput[]): Promise<string[]>;
    /**
     * Get all entries for an account.
     * @param accountGuid The account GUID.
     * @returns All entries.
     */
    getAll(accountGuid: string): Promise<Entry[]>;
    /**
     * Enumerate entries with filtering and pagination.
     * @param accountGuid The account GUID.
     * @param query Query parameters.
     * @returns Enumeration result.
     */
    enumerate(accountGuid: string, query?: EntryEnumerationQuery): Promise<EnumerationResult<Entry>>;
    /**
     * Get all pending (uncommitted) entries.
     * @param accountGuid The account GUID.
     * @returns Pending entries.
     */
    getPending(accountGuid: string): Promise<Entry[]>;
    /**
     * Get pending credit entries.
     * @param accountGuid The account GUID.
     * @returns Pending credits.
     */
    getPendingCredits(accountGuid: string): Promise<Entry[]>;
    /**
     * Get pending debit entries.
     * @param accountGuid The account GUID.
     * @returns Pending debits.
     */
    getPendingDebits(accountGuid: string): Promise<Entry[]>;
    /**
     * Cancel (delete) a pending entry.
     * @param accountGuid The account GUID.
     * @param entryGuid The entry GUID.
     */
    cancel(accountGuid: string, entryGuid: string): Promise<void>;
}
//# sourceMappingURL=entry.d.ts.map