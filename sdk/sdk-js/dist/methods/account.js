"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.AccountMethods = void 0;
const errors_1 = require("../errors");
/**
 * Account management operations.
 */
class AccountMethods {
    constructor(client) {
        this.client = client;
    }
    /**
     * Create a new account.
     * @param name Account name.
     * @param notes Optional notes.
     * @returns The created account.
     */
    async create(name, notes) {
        if (!name || name.trim() === '') {
            throw new errors_1.NetLedgerValidationError('Account name cannot be empty', 'name');
        }
        const response = await this.client.put('/v1/accounts', { Name: name, Notes: notes });
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }
    /**
     * Get an account by GUID.
     * @param accountGuid The account GUID.
     * @returns The account.
     */
    async get(accountGuid) {
        const response = await this.client.get(`/v1/accounts/${accountGuid}`);
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }
    /**
     * Get an account by name.
     * @param name The account name.
     * @returns The account.
     */
    async getByName(name) {
        if (!name || name.trim() === '') {
            throw new errors_1.NetLedgerValidationError('Account name cannot be empty', 'name');
        }
        const encodedName = encodeURIComponent(name);
        const response = await this.client.get(`/v1/accounts/byname/${encodedName}`);
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }
    /**
     * Check if an account exists.
     * @param accountGuid The account GUID.
     * @returns True if the account exists.
     */
    async exists(accountGuid) {
        return await this.client.head(`/v1/accounts/${accountGuid}`);
    }
    /**
     * Delete an account.
     * @param accountGuid The account GUID.
     */
    async delete(accountGuid) {
        await this.client.delete(`/v1/accounts/${accountGuid}`);
    }
    /**
     * Enumerate accounts with optional filtering and pagination.
     * @param query Query parameters.
     * @returns Enumeration result containing accounts.
     */
    async enumerate(query) {
        const params = new URLSearchParams();
        if (query) {
            if (query.MaxResults !== undefined)
                params.append('maxResults', query.MaxResults.toString());
            if (query.Skip !== undefined)
                params.append('skip', query.Skip.toString());
            if (query.SearchTerm)
                params.append('searchTerm', query.SearchTerm);
        }
        const queryString = params.toString();
        const path = queryString ? `/v1/accounts?${queryString}` : '/v1/accounts';
        const response = await this.client.get(path);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
}
exports.AccountMethods = AccountMethods;
//# sourceMappingURL=account.js.map