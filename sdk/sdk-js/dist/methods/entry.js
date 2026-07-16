"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.EntryMethods = void 0;
const errors_1 = require("../errors");
/**
 * Entry operations.
 */
class EntryMethods {
    constructor(client) {
        this.client = client;
    }
    /**
     * Add a credit entry.
     * @param accountId The account identifier.
     * @param amount The credit amount (must be positive).
     * @param notes Optional notes.
     * @returns The identifier of the created entry.
     */
    async addCredit(accountId, amount, notes) {
        if (amount <= 0) {
            throw new errors_1.NetLedgerValidationError('Amount must be greater than zero', 'amount');
        }
        const response = await this.client.put(`/v1/accounts/${accountId}/credits`, { Amount: amount, Notes: notes });
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
    async addCredits(accountId, entries) {
        if (!entries || entries.length === 0) {
            throw new errors_1.NetLedgerValidationError('Entries array cannot be empty', 'entries');
        }
        for (const entry of entries) {
            if (entry.Amount <= 0) {
                throw new errors_1.NetLedgerValidationError('All amounts must be greater than zero', 'entries');
            }
        }
        const response = await this.client.put(`/v1/accounts/${accountId}/credits`, { Entries: entries });
        return response.Data?.EntryIds || [];
    }
    /**
     * Add a debit entry.
     * @param accountId The account identifier.
     * @param amount The debit amount (must be positive).
     * @param notes Optional notes.
     * @returns The identifier of the created entry.
     */
    async addDebit(accountId, amount, notes) {
        if (amount <= 0) {
            throw new errors_1.NetLedgerValidationError('Amount must be greater than zero', 'amount');
        }
        const response = await this.client.put(`/v1/accounts/${accountId}/debits`, { Amount: amount, Notes: notes });
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
    async addDebits(accountId, entries) {
        if (!entries || entries.length === 0) {
            throw new errors_1.NetLedgerValidationError('Entries array cannot be empty', 'entries');
        }
        for (const entry of entries) {
            if (entry.Amount <= 0) {
                throw new errors_1.NetLedgerValidationError('All amounts must be greater than zero', 'entries');
            }
        }
        const response = await this.client.put(`/v1/accounts/${accountId}/debits`, { Entries: entries });
        return response.Data?.EntryIds || [];
    }
    /**
     * Get all entries for an account.
     * @param accountId The account identifier.
     * @returns All entries.
     */
    async getAll(accountId) {
        const response = await this.client.get(`/v1/accounts/${accountId}/entries`);
        return response.Data || [];
    }
    /**
     * Enumerate entries with filtering and pagination.
     * @param accountId The account identifier.
     * @param query Query parameters.
     * @returns Enumeration result.
     */
    async enumerate(accountId, query) {
        const response = await this.client.post(`/v1/accounts/${accountId}/entries/enumerate`, query || {});
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    /**
     * Get all pending (uncommitted) entries.
     * @param accountId The account identifier.
     * @returns Pending entries.
     */
    async getPending(accountId) {
        const response = await this.client.get(`/v1/accounts/${accountId}/entries/pending`);
        return response.Data || [];
    }
    /**
     * Get pending credit entries.
     * @param accountId The account identifier.
     * @returns Pending credits.
     */
    async getPendingCredits(accountId) {
        const response = await this.client.get(`/v1/accounts/${accountId}/entries/pending/credits`);
        return response.Data || [];
    }
    /**
     * Get pending debit entries.
     * @param accountId The account identifier.
     * @returns Pending debits.
     */
    async getPendingDebits(accountId) {
        const response = await this.client.get(`/v1/accounts/${accountId}/entries/pending/debits`);
        return response.Data || [];
    }
    /**
     * Cancel (delete) a pending entry.
     * @param accountId The account identifier.
     * @param entryId The entry identifier.
     */
    async cancel(accountId, entryId) {
        await this.client.delete(`/v1/accounts/${accountId}/entries/${entryId}`);
    }
}
exports.EntryMethods = EntryMethods;
//# sourceMappingURL=entry.js.map