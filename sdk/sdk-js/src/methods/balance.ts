import { Balance, CommitResult, CommitRequest, HistoricalBalance } from '../models';
import { HttpClient } from '../http-client';
import { NetLedgerApiError } from '../errors';

/**
 * Balance operations.
 */
export class BalanceMethods {
    private readonly client: HttpClient;

    constructor(client: HttpClient) {
        this.client = client;
    }

    /**
     * Get the current balance for an account.
     * @param accountId The account identifier.
     * @returns The account balance.
     */
    async get(accountId: string): Promise<Balance> {
        const response = await this.client.get<Balance>(
            `/v1/accounts/${accountId}/balance`
        );
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }

    /**
     * Get the historical balance as of a specific time.
     * @param accountId The account identifier.
     * @param asOfUtc The UTC timestamp.
     * @returns The balance as of that time.
     */
    async getAsOf(accountId: string, asOfUtc: Date): Promise<HistoricalBalance> {
        const asOf = asOfUtc.toISOString();
        const response = await this.client.get<HistoricalBalance>(
            `/v1/accounts/${accountId}/balance/asof?asOf=${asOf}`
        );
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }

    /**
     * Get balances for all accounts.
     * @returns All account balances.
     */
    async getAll(): Promise<Balance[]> {
        const response = await this.client.get<Balance[]>('/v1/balances');
        return response.Data || [];
    }

    /**
     * Commit all pending entries for an account.
     * @param accountId The account identifier.
     * @returns The commit result.
     */
    async commit(accountId: string): Promise<CommitResult>;
    /**
     * Commit specific entries for an account.
     * @param accountId The account identifier.
     * @param entryIds The identifiers of entries to commit.
     * @returns The commit result.
     */
    async commit(accountId: string, entryIds: string[]): Promise<CommitResult>;
    async commit(accountId: string, entryIds?: string[]): Promise<CommitResult> {
        const body: CommitRequest | null = entryIds ? { EntryIds: entryIds } : null;
        const response = await this.client.post<CommitResult>(
            `/v1/accounts/${accountId}/commit`,
            body
        );
        return response.Data || { EntriesCommitted: 0 };
    }

    /**
     * Verify the balance chain integrity.
     * @param accountId The account identifier.
     * @returns True if the balance chain is valid.
     */
    async verify(accountId: string): Promise<boolean> {
        try {
            const response = await this.client.get<unknown>(
                `/v1/accounts/${accountId}/verify`
            );
            return response.StatusCode === 200;
        } catch (err) {
            if (err instanceof NetLedgerApiError && err.statusCode === 409) {
                return false;
            }
            throw err;
        }
    }
}
