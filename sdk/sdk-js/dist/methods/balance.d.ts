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
     * @param accountId The account identifier.
     * @returns The account balance.
     */
    get(accountId: string): Promise<Balance>;
    /**
     * Get the historical balance as of a specific time.
     * @param accountId The account identifier.
     * @param asOfUtc The UTC timestamp.
     * @returns The balance as of that time.
     */
    getAsOf(accountId: string, asOfUtc: Date): Promise<HistoricalBalance>;
    /**
     * Get balances for all accounts.
     * @returns All account balances.
     */
    getAll(): Promise<Balance[]>;
    /**
     * Commit all pending entries for an account.
     * @param accountId The account identifier.
     * @returns The commit result.
     */
    commit(accountId: string): Promise<CommitResult>;
    /**
     * Commit specific entries for an account.
     * @param accountId The account identifier.
     * @param entryIds The identifiers of entries to commit.
     * @returns The commit result.
     */
    commit(accountId: string, entryIds: string[]): Promise<CommitResult>;
    /**
     * Verify the balance chain integrity.
     * @param accountId The account identifier.
     * @returns True if the balance chain is valid.
     */
    verify(accountId: string): Promise<boolean>;
}
//# sourceMappingURL=balance.d.ts.map