"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ApiKeyMethods = void 0;
const errors_1 = require("../errors");
/**
 * API key management operations.
 */
class ApiKeyMethods {
    constructor(client) {
        this.client = client;
    }
    /**
     * Create a new API key.
     * @param name Display name for the key.
     * @param isAdmin Whether the key has admin privileges.
     * @returns The created API key info (includes the key value).
     */
    async create(name, isAdmin = false) {
        if (!name || name.trim() === '') {
            throw new errors_1.NetLedgerValidationError('API key name cannot be empty', 'name');
        }
        const response = await this.client.put('/v1/apikeys', { Name: name, IsAdmin: isAdmin });
        if (!response.Data) {
            throw new Error('No data returned from server');
        }
        return response.Data;
    }
    /**
     * Enumerate API keys.
     * @param query Query parameters.
     * @returns Enumeration result (key values not included).
     */
    async enumerate(query) {
        const params = new URLSearchParams();
        if (query) {
            if (query.MaxResults !== undefined)
                params.append('maxResults', query.MaxResults.toString());
            if (query.Skip !== undefined)
                params.append('skip', query.Skip.toString());
        }
        const queryString = params.toString();
        const path = queryString ? `/v1/apikeys?${queryString}` : '/v1/apikeys';
        const response = await this.client.get(path);
        return response.Data || { TotalRecords: 0, RecordsRemaining: 0, EndOfResults: true };
    }
    /**
     * Revoke (delete) an API key.
     * @param apiKeyGuid The API key GUID.
     */
    async revoke(apiKeyGuid) {
        await this.client.delete(`/v1/apikeys/${apiKeyGuid}`);
    }
}
exports.ApiKeyMethods = ApiKeyMethods;
//# sourceMappingURL=apikey.js.map