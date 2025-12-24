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
    create(name: string, notes?: string): Promise<Account>;
    /**
     * Get an account by GUID.
     * @param accountGuid The account GUID.
     * @returns The account.
     */
    get(accountGuid: string): Promise<Account>;
    /**
     * Get an account by name.
     * @param name The account name.
     * @returns The account.
     */
    getByName(name: string): Promise<Account>;
    /**
     * Check if an account exists.
     * @param accountGuid The account GUID.
     * @returns True if the account exists.
     */
    exists(accountGuid: string): Promise<boolean>;
    /**
     * Delete an account.
     * @param accountGuid The account GUID.
     */
    delete(accountGuid: string): Promise<void>;
    /**
     * Enumerate accounts with optional filtering and pagination.
     * @param query Query parameters.
     * @returns Enumeration result containing accounts.
     */
    enumerate(query?: AccountEnumerationQuery): Promise<EnumerationResult<Account>>;
}
//# sourceMappingURL=account.d.ts.map