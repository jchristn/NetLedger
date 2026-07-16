"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.BalanceMethods = void 0;
const errors_1 = require("../errors");
/**
 * Balance operations.
 */
class BalanceMethods {
    constructor(client) {
        this.client = client;
    }
    /**
     * Get the current balance for an account.
     * @param accountId The account identifier.
     * @returns The account balance.
     */
    async get(accountId) {
        const response = await this.client.get(`/v1/accounts/${accountId}/balance`);
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
    async getAsOf(accountId, asOfUtc) {
        const asOf = asOfUtc.toISOString();
        const response = await this.client.get(`/v1/accounts/${accountId}/balance/asof?asOf=${asOf}`);
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }
    /**
     * Get balances for all accounts.
     * @returns All account balances.
     */
    async getAll() {
        const response = await this.client.get('/v1/balances');
        return response.Data || [];
    }
    async commit(accountId, entryIds) {
        const body = entryIds ? { EntryIds: entryIds } : null;
        const response = await this.client.post(`/v1/accounts/${accountId}/commit`, body);
        return response.Data || { EntriesCommitted: 0 };
    }
    /**
     * Verify the balance chain integrity.
     * @param accountId The account identifier.
     * @returns True if the balance chain is valid.
     */
    async verify(accountId) {
        try {
            const response = await this.client.get(`/v1/accounts/${accountId}/verify`);
            return response.StatusCode === 200;
        }
        catch (err) {
            if (err instanceof errors_1.NetLedgerApiError && err.statusCode === 409) {
                return false;
            }
            throw err;
        }
    }
}
exports.BalanceMethods = BalanceMethods;
//# sourceMappingURL=balance.js.map