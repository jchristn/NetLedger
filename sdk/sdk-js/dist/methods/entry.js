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
     * @param accountGuid The account GUID.
     * @param amount The credit amount (must be positive).
     * @param notes Optional notes.
     * @returns The GUID of the created entry.
     */
    async addCredit(accountGuid, amount, notes) {
        if (amount <= 0) {
            throw new errors_1.NetLedgerValidationError('Amount must be greater than zero', 'amount');
        }
        const response = await this.client.put(`/v1/accounts/${accountGuid}/credits`, { Amount: amount, Notes: notes });
        if (!response.Data || !response.Data.EntryGuids || response.Data.EntryGuids.length === 0) {
            throw new Error('No data returned from server');
        }
        return response.Data.EntryGuids[0];
    }
    /**
     * Add multiple credit entries.
     * @param accountGuid The account GUID.
     * @param entries The credit entries to add.
     * @returns The GUIDs of the created entries.
     */
    async addCredits(accountGuid, entries) {
        if (!entries || entries.length === 0) {
            throw new errors_1.NetLedgerValidationError('Entries array cannot be empty', 'entries');
        }
        for (const entry of entries) {
            if (entry.Amount <= 0) {
                throw new errors_1.NetLedgerValidationError('All amounts must be greater than zero', 'entries');
            }
        }
        const response = await this.client.put(`/v1/accounts/${accountGuid}/credits`, { Entries: entries });
        return response.Data?.EntryGuids || [];
    }
    /**
     * Add a debit entry.
     * @param accountGuid The account GUID.
     * @param amount The debit amount (must be positive).
     * @param notes Optional notes.
     * @returns The GUID of the created entry.
     */
    async addDebit(accountGuid, amount, notes) {
        if (amount <= 0) {
            throw new errors_1.NetLedgerValidationError('Amount must be greater than zero', 'amount');
        }
        const response = await this.client.put(`/v1/accounts/${accountGuid}/debits`, { Amount: amount, Notes: notes });
        if (!response.Data || !response.Data.EntryGuids || response.Data.EntryGuids.length === 0) {
            throw new Error('No data returned from server');
        }
        return response.Data.EntryGuids[0];
    }
    /**
     * Add multiple debit entries.
     * @param accountGuid The account GUID.
     * @param entries The debit entries to add.
     * @returns The GUIDs of the created entries.
     */
    async addDebits(accountGuid, entries) {
        if (!entries || entries.length === 0) {
            throw new errors_1.NetLedgerValidationError('Entries array cannot be empty', 'entries');
        }
        for (const entry of entries) {
            if (entry.Amount <= 0) {
                throw new errors_1.NetLedgerValidationError('All amounts must be greater than zero', 'entries');
            }
        }
        const response = await this.client.put(`/v1/accounts/${accountGuid}/debits`, { Entries: entries });
        return response.Data?.EntryGuids || [];
    }
    /**
     * Get all entries for an account.
     * @param accountGuid The account GUID.
     * @returns All entries.
     */
    async getAll(accountGuid) {
        const response = await this.client.get(`/v1/accounts/${accountGuid}/entries`);
        return response.Data || [];
    }
    /**
     * Enumerate entries with filtering and pagination.
     * @param accountGuid The account GUID.
     * @param query Query parameters.
     * @returns Enumeration result.
     */
    async enumerate(accountGuid, query) {
        const response = await this.client.post(`/v1/accounts/${accountGuid}/entries/enumerate`, query || {});
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    /**
     * Get all pending (uncommitted) entries.
     * @param accountGuid The account GUID.
     * @returns Pending entries.
     */
    async getPending(accountGuid) {
        const response = await this.client.get(`/v1/accounts/${accountGuid}/entries/pending`);
        return response.Data || [];
    }
    /**
     * Get pending credit entries.
     * @param accountGuid The account GUID.
     * @returns Pending credits.
     */
    async getPendingCredits(accountGuid) {
        const response = await this.client.get(`/v1/accounts/${accountGuid}/entries/pending/credits`);
        return response.Data || [];
    }
    /**
     * Get pending debit entries.
     * @param accountGuid The account GUID.
     * @returns Pending debits.
     */
    async getPendingDebits(accountGuid) {
        const response = await this.client.get(`/v1/accounts/${accountGuid}/entries/pending/debits`);
        return response.Data || [];
    }
    /**
     * Cancel (delete) a pending entry.
     * @param accountGuid The account GUID.
     * @param entryGuid The entry GUID.
     */
    async cancel(accountGuid, entryGuid) {
        await this.client.delete(`/v1/accounts/${accountGuid}/entries/${entryGuid}`);
    }
}
exports.EntryMethods = EntryMethods;
//# sourceMappingURL=entry.js.map