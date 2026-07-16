import { Account, AccountEnumerationQuery, EnumerationResult } from '../models';
import { HttpClient } from '../http-client';
/**
 * Account management operations.
 */
export declare class AccountMethods {
    private readonly client;
    constructor(client: HttpClient);
    /**
     * Create a new account.
     * @param name Account name.
     * @param notes Optional notes.
     * @returns The created account.
     */
    create(name: string, notes?: string, labels?: string[], tags?: Record<string, string>): Promise<Account>;
    /**
     * Get an account by identifier.
     * @param accountId The account identifier.
     * @returns The account.
     */
    get(accountId: string): Promise<Account>;
    /**
     * Get an account by name.
     * @param name The account name.
     * @returns The account.
     */
    getByName(name: string): Promise<Account>;
    /**
     * Check if an account exists.
     * @param accountId The account identifier.
     * @returns True if the account exists.
     */
    exists(accountId: string): Promise<boolean>;
    /**
     * Delete an account.
     * @param accountId The account identifier.
     */
    delete(accountId: string): Promise<void>;
    /**
     * Enumerate accounts with optional filtering and pagination.
     * @param query Query parameters.
     * @returns Enumeration result containing accounts.
     */
    enumerate(query?: AccountEnumerationQuery): Promise<EnumerationResult<Account>>;
}
//# sourceMappingURL=account.d.ts.map