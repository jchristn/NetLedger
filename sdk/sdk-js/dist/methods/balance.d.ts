import { Balance, CommitResult, HistoricalBalance } from '../models';
import { HttpClient } from '../http-client';
/**
 * Balance operations.
 */
export declare class BalanceMethods {
    private readonly client;
    constructor(client: HttpClient);
    /**
     * Get the current balance for an account.
     * @param accountGuid The account GUID.
     * @returns The account balance.
     */
    get(accountGuid: string): Promise<Balance>;
    /**
     * Get the historical balance as of a specific time.
     * @param accountGuid The account GUID.
     * @param asOfUtc The UTC timestamp.
     * @returns The balance as of that time.
     */
    getAsOf(accountGuid: string, asOfUtc: Date): Promise<HistoricalBalance>;
    /**
     * Get balances for all accounts.
     * @returns All account balances.
     */
    getAll(): Promise<Balance[]>;
    /**
     * Commit all pending entries for an account.
     * @param accountGuid The account GUID.
     * @returns The commit result.
     */
    commit(accountGuid: string): Promise<CommitResult>;
    /**
     * Commit specific entries for an account.
     * @param accountGuid The account GUID.
     * @param entryGuids The GUIDs of entries to commit.
     * @returns The commit result.
     */
    commit(accountGuid: string, entryGuids: string[]): Promise<CommitResult>;
    /**
     * Verify the balance chain integrity.
     * @param accountGuid The account GUID.
     * @returns True if the balance chain is valid.
     */
    verify(accountGuid: string): Promise<boolean>;
}
//# sourceMappingURL=balance.d.ts.map